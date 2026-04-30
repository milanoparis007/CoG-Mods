using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Services;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Sim.Modules;
using SomaSim.Util;

namespace Game.Session.Player.AI;

public sealed class SafehouseAdvisor : AIAdvisor
{
	private SafehouseAdvisorConfig _def;

	private SafehouseAdvisorData _data;

	private int CheckIntervalDays => Game.ctx.clock.TurnsToDays(3);

	public SafehouseAdvisor(PlayerAI manager, NPCDefinition def)
		: base(manager, def)
	{
		_def = Game.serv.globals.settings.npc.FindAdvisorConfig<SafehouseAdvisorConfig>(def.safehouse);
		_data = manager.Data.safehouse;
	}

	public override void OnTurnUpdate()
	{
		CheckForBuildingNeedingModule();
		RefillSafehouseIfNeeded();
		FindCrewToRefill();
		RunSkillCheck();
		RunDonationCheck();
	}

	public override void ProduceRequests(List<AdvisorRequest> results)
	{
		results.AddIfNotNull(ProduceSingleRequest());
	}

	private AdvisorRequest ProduceSingleRequest()
	{
		if (_data.nextBackroomToInstall.IsValid)
		{
			return new AdvisorRequest(this, ScriptNames.INSTALL_BACKROOM)
			{
				priority = AdvisorRequest.Priority.InstallBackroom,
				variables = new Deictics
				{
					targetBuilding = _data.GetAndClear(ref _data.nextBackroomToInstall)
				}
			};
		}
		if (_data.nextCrewToRefill.IsValid)
		{
			return new AdvisorRequest(this, ScriptNames.REFILL_CASH)
			{
				assignedTo = _data.GetAndClear(ref _data.nextCrewToRefill)
			};
		}
		return null;
	}

	private void RunSkillCheck()
	{
		SafehouseAdvisorConfig.Skills skills = _def.skills;
		if (skills != null && skills.growth && _data.nextSkillCheck.days < Game.ctx.clock.Now.days)
		{
			TryGainSkill();
			Fixnum fixnum = skills.checkDayzMin.Evaluate(new ModQuery(_pid));
			Fixnum fixnum2 = skills.checkDayzMax.Evaluate(new ModQuery(_pid));
			int deltaDays = _data.rng.Generate((int)fixnum, (int)fixnum2);
			_data.nextSkillCheck = Game.ctx.clock.Now.IncrementDays(deltaDays);
		}
	}

	internal bool ForceAddNextSkill()
	{
		if (_def.skills != null && _def.skills.growth)
		{
			return TryGainSkill();
		}
		return false;
	}

	private bool TryGainSkill()
	{
		if (!EnsureSkillTrack())
		{
			return false;
		}
		if (_data.skillTrack.Count == 0)
		{
			return false;
		}
		Label label = _data.skillTrack.RemoveAndReturn(0);
		_player.skills.DoLearnFromSkillTrack(label);
		AILog.LogAIDecision(_pid, this, $"Learn skill {label} on track; {_data.skillTrack.Count} left");
		AILog.LogMilestone(_pid, EntityID.INVALID, $"{_pid} learned skill: {label}");
		return true;
	}

	private bool EnsureSkillTrack()
	{
		if (_data.skillTrack != null)
		{
			return true;
		}
		EntityID safehouse = _player.territory.Safehouse;
		bool isSafehouseVanquished = _player.territory.IsSafehouseVanquished;
		if (safehouse.IsNotValid || isSafehouseVanquished)
		{
			return false;
		}
		Entity entity = BuildingUtil.FindBizForBuilding(safehouse);
		_data.skillTrack = entity?.config.biz.GenerateSkillTrack(_data.rng);
		return _data.skillTrack != null;
	}

	private void RunDonationCheck()
	{
		if (_def.donations != null)
		{
			CallWithCooldown(TryDonation, ref _data.nextDonationCheck, _def.donations.firstCheckDayz, _def.donations.cooldownDayz);
		}
	}

	private void TryDonation()
	{
		Fixnum probability = _def.donations.probPerCheck.Evaluate(_pid);
		if (_data.rng.CheckProbability(probability))
		{
			PlayerInfo playerInfo = FindCopToBribe();
			if (playerInfo != null)
			{
				AILog.LogAIDecision(_pid, this, $"Bribing cop {playerInfo.PID} / {playerInfo.ai.precinct.GetPrecinctName(colorized: false)}");
				playerInfo.ai.precinct.StartAIDonation(_player);
			}
		}
		PlayerInfo FindCopToBribe()
		{
			foreach (PlayerID item in _player.meetings.GetPlayersAlreadyMet())
			{
				PlayerInfo playerInfo2 = item.FindPlayer();
				if (playerInfo2.IsJustCop && playerInfo2.ai.precinct.HasDonationFrom(_pid) != DonationState.PaidOff)
				{
					return playerInfo2;
				}
			}
			return null;
		}
	}

	private void RefillSafehouseIfNeeded()
	{
		if (_data.nextSafehouseCheck.days > Game.ctx.clock.Now.days)
		{
			return;
		}
		_data.nextSafehouseCheck = Game.ctx.clock.Now.IncrementDays(CheckIntervalDays);
		Entity entity = _player.territory.Safehouse.FindEntity();
		if (entity == null)
		{
			Logger.Warning($"Missing safehouse for {_pid}, this player should not be ticked anymore");
			return;
		}
		PlayerFinances finances = _player.finances;
		int num = _def.stashCash.from;
		Fixnum cash = finances.GetMoney(entity).cash;
		if (!(cash >= num))
		{
			int num2 = _data.rng.Generate(_def.stashCash);
			Price delta = new Price(num2 - cash);
			if (!finances.CanChangeMoney(entity, delta))
			{
				Logger.Warning($"Can't increase safehouse cash by ${delta.cash} for {_pid}");
			}
			else
			{
				finances.DoChangeMoney(entity, delta, MoneyReason.SafehouseAdvisor);
			}
		}
	}

	private void FindCrewToRefill()
	{
		if (_data.nextCrewToRefill.IsValid)
		{
			return;
		}
		foreach (CrewAssignment item in _player.crew.GetLiving())
		{
			if (item.IsInVehicle && DoesNeedCashRefill(item.peepId))
			{
				_data.nextCrewToRefill = item.peepId;
				break;
			}
		}
	}

	public bool DoesNeedCashRefill(EntityID peepId)
	{
		CrewAssignment crewForPeep = _player.crew.GetCrewForPeep(peepId);
		if (crewForPeep.IsNotValid || !crewForPeep.IsInVehicle)
		{
			return false;
		}
		Fixnum cash = _player.finances.GetMoney(crewForPeep.VehicleID).cash;
		if (!(cash < _def.carryCash.from))
		{
			return cash > _def.carryCash.to;
		}
		return true;
	}

	public void RefillCashForPeep(EntityID peepId, EntityID buildingId)
	{
		EntityID entityID = _player.crew.FindVehicleAssignedToPeep(peepId);
		if (entityID.IsNotValid || peepId.IsNotValid)
		{
			Logger.Warning("Peep or vehicle not valid", peepId, entityID);
			return;
		}
		PlayerFinances finances = _player.finances;
		Fixnum cash = finances.GetMoney(entityID).cash;
		Fixnum cash2 = finances.GetMoney(buildingId).cash;
		Price delta = Price.ZERO;
		Price delta2 = Price.ZERO;
		if (cash > _def.carryCash.to)
		{
			int num = _data.rng.Generate(_def.carryCash);
			Fixnum fixnum = cash - num;
			delta = new Price(fixnum);
			delta2 = new Price(-fixnum);
		}
		if (cash < _def.carryCash.from)
		{
			Fixnum fixnum2 = Fixnum.Min(_data.rng.Generate(_def.carryCash) - cash, cash2);
			delta2 = new Price(fixnum2);
			delta = new Price(-fixnum2);
		}
		if (delta.IsNonZero)
		{
			Entity container = buildingId.FindEntity();
			Entity container2 = entityID.FindEntity();
			finances.DoChangeMoney(container, delta, MoneyReason.SafehouseAdvisor);
			finances.DoChangeMoney(container2, delta2, MoneyReason.SafehouseAdvisor);
		}
	}

	private void CheckForBuildingNeedingModule()
	{
		if (!_player.HasBusinessAdvisor || _data.nextBackroomToInstall.IsValid)
		{
			return;
		}
		foreach (EntityID item in _player.territory.GetAllControlledBuildingsUnsafe())
		{
			if (item.FindEntity().components.residence == null && !item.FindEntity().components.modules.HasBackroomModules())
			{
				_data.nextBackroomToInstall = item;
				AILog.LogAIDecision(_pid, this, $"Install backroom module at {_data.nextBackroomToInstall.FindEntity()}");
				break;
			}
		}
	}

	public void ProcessAddBackroomRequest(EntityID buildingId)
	{
		Entity entity = buildingId.FindEntity();
		var (label, num) = PickRandomBackroomModule(_player, entity);
		if (label.IsSet)
		{
			entity.components.modules.InstallModuleManually(label, Game.ctx.clock.Now);
			AILog.LogMilestone(_pid, buildingId, $"{_pid} installed backroom module {label} in {entity} out of {num} choices");
		}
		else
		{
			Logger.Warning($"Could not find a backroom module for player {_pid}");
		}
	}

	private static (Label id, int choices) PickRandomBackroomModule(PlayerInfo player, Entity building)
	{
		BuildingAndBusinessData bbd = BuildingUtil.FindDataForBuilding(building);
		ModuleSlot moduleSlot = building.components.modules.FindBackroomModuleSlot();
		if (bbd.biz == null || moduleSlot == null)
		{
			Logger.Warning($"Unable to add a backroom module, biz = {bbd.biz}, slot tags = {moduleSlot?.tags}");
			return (id: Label.NULL, choices: 0);
		}
		List<AddModuleDef> item = FindModulesToAddForSlot(player.PID, bbd, moduleSlot).defs;
		if (item.Count == 0)
		{
			Logger.Warning($"Found no backroom modules for biz = {bbd.biz}, bailing");
			return (id: Label.NULL, choices: 0);
		}
		return (id: Game.ctx.scenario.MakeSeededRng(player.PID).PickElement(item).config.Id, choices: item.Count);
	}

	private static (List<AddModuleDef> defs, bool fallback) FindModulesToAddForSlot(PlayerID pid, BuildingAndBusinessData bbd, ModuleSlot slotdef)
	{
		VisitState visit = new VisitState(CrewAssignment.EMPTY, bbd, Game.ctx.clock.Now, pid);
		IEnumerable<AddModuleDef> source = from config in ModulesUtil.FindAllModuleDefsExpensive()
			where bbd.biz.components.biz.CanInstallModuleInSlot(config, slotdef)
			select ModulesUtil.MakeAddModuleDef(config, visit);
		List<AddModuleDef> list = source.Where((AddModuleDef def) => def.passesVisreqs && def.passesReqs).ToList();
		if (list.Count > 0)
		{
			return (defs: list, fallback: false);
		}
		return (defs: source.Where((AddModuleDef def) => def.passesVisreqs).ToList(), fallback: true);
	}

	private static bool IsAcceptableBackroomType(IModuleConfig config)
	{
		if (config.Common == null)
		{
			return false;
		}
		if (!config.Common.tags.Contains(TagConstants.TAG_SAFEHOUSE_BACKROOMS))
		{
			return false;
		}
		if (!(config is ManufactureModuleConfig manufactureModuleConfig))
		{
			return false;
		}
		foreach (Recipe recipe in manufactureModuleConfig.recipes)
		{
			if (recipe.IsConsumer)
			{
				return true;
			}
		}
		return false;
	}
}
