using System.Collections.Generic;
using Game.Core;
using Game.Services;
using Game.Session.Entities;
using SomaSim.Util;

namespace Game.Session.Player.AI;

public sealed class UnitsAdvisor : AIAdvisor
{
	private bool _updateNeeded;

	private UnitsAdvisorConfig _def;

	private UnitsAdvisorData _data;

	private readonly List<EntityID> _eligibileCrew = new List<EntityID>();

	private static readonly List<Entity> _candidates = new List<Entity>();

	private bool NeverGrows => _def.dayzToGrow == null;

	public Label DefaultCar => _def.defaultCar;

	public UnitsAdvisor(PlayerAI manager, NPCDefinition def)
		: base(manager, def)
	{
		_def = Game.serv.globals.settings.npc.FindAdvisorConfig<UnitsAdvisorConfig>(def.units);
		_data = manager.Data.units;
		Game.ctx.simman.peoplegen.OnBeforePersonDeath.Add(OnPersonDeath);
		Game.ctx.events.AddListener(SessionEventType.CrewFedArrest, OnCrewArrest);
	}

	public override void OnTurnUpdate()
	{
		if (_def.dayzToGrow != null)
		{
			CallWithCooldown(MaybeGrowCrew, ref _data.nextExpansion, _def.dayzToGrow, _def.dayzToGrow);
		}
		EnsureEveryoneInVehicle();
		_updateNeeded = true;
	}

	public override void ProduceRequests(List<AdvisorRequest> result)
	{
	}

	public override void Release()
	{
		Game.ctx.events.RemoveListener(SessionEventType.CrewFedArrest, OnCrewArrest);
		Game.ctx.simman.peoplegen.OnBeforePersonDeath.Remove(OnPersonDeath);
		base.Release();
	}

	private void OnPersonDeath(Entity e)
	{
		if (e.data.agent.pid == _pid)
		{
			_updateNeeded = true;
		}
	}

	private void OnCrewArrest(SessionEvent sev)
	{
		if (sev.pid == _pid)
		{
			_updateNeeded = true;
		}
	}

	public EntityID GetEligibleCrewMember()
	{
		if (_updateNeeded)
		{
			RecalculateEligibleCrew();
		}
		return _eligibileCrew.RemoveLastOrDefault();
	}

	private void RecalculateEligibleCrew()
	{
		_ = _updateNeeded;
		_eligibileCrew.Clear();
		foreach (CrewAssignment item in _player.crew.GetLiving())
		{
			if (_player.commands.FindCommand(item.peepId, onlyActive: false) == null && !Game.ctx.simman.cops.IsArrestedOrImprisoned(item.peepId))
			{
				_eligibileCrew.Add(item.peepId);
			}
		}
		_updateNeeded = false;
	}

	private void EnsureEveryoneInVehicle()
	{
		using ListPool<CrewAssignment>.PooledBlockList pooledBlockList = ListPool<CrewAssignment>.Allocate();
		foreach (CrewAssignment item in _player.crew.AllCrew)
		{
			if (item.IsNotDead && item.IsNotAssigned)
			{
				pooledBlockList.Add(item);
			}
		}
		if (pooledBlockList.Count == 0)
		{
			return;
		}
		Node headquartersNode = _player.territory.GetHeadquartersNode();
		if (headquartersNode == null)
		{
			return;
		}
		foreach (CrewAssignment item2 in pooledBlockList)
		{
			if (!Game.ctx.simman.cops.IsArrestedOrImprisoned(item2.peepId))
			{
				_player.crew.CreateVehicleAndAssignCrew(headquartersNode, item2.GetPeep());
			}
		}
	}

	private void MaybeGrowCrew()
	{
		if (ShouldGrow())
		{
			Grow();
		}
	}

	private bool ShouldGrow()
	{
		if (NeverGrows)
		{
			return false;
		}
		int livingCrewCount = _player.crew.LivingCrewCount;
		Fixnum fixnum = _def.cap.Evaluate(_pid);
		if (livingCrewCount < fixnum)
		{
			return _player.crew.CanAddCrew();
		}
		return false;
	}

	private void Grow()
	{
		float probability = (float)_def.growthChance.Evaluate(_pid);
		if (_data.rng.CheckProbability(probability))
		{
			Game.ctx.simman.peoplegen.ProducePeopleWhere((Entity t) => PlayerSocial.IsEligibleCrewMember(Game.ctx.clock.Now, t), _candidates, clearFirst: true);
			if (_candidates.Count != 0)
			{
				Entity entity = _data.rng.PickElement(_candidates);
				Node headquartersNode = _player.territory.GetHeadquartersNode();
				_player.crew.HireNewCrewInVehicle(headquartersNode, entity, null, isBoss: false);
				_candidates.Clear();
				AILog.LogAIDecision(_pid, this, $"Added new crew member, total = {_player.crew.TotalCrewCount}");
				AILog.LogMilestone(_pid, entity.Id, $"{_pid} added new crew member, total = {_player.crew.TotalCrewCount}");
			}
		}
	}
}
