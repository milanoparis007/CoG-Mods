using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Services;
using Game.Services.Store;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Player.AI;
using Game.Session.Setup;
using Game.Session.Sim.Modules;
using Game.UI.Session;
using Game.UI.Session.Crew;
using Game.UI.Session.Tickers;
using SomaSim.Util;

namespace Game.Session.Player;

public sealed class PlayerCrew : PlayerSubmanager, ITurnHandlingPlayerSubmanager
{
	public enum CapacityType
	{
		Crew,
		Car,
		Truck,
		AnyVehicle
	}

	public struct NumAndMax
	{
		public int num;

		public int max;

		public int Underrun => MathUtil.ClampMin(max - num, 0);

		public int Overrun => MathUtil.ClampMin(num - max, 0);

		public NumAndMax(int num, int max)
		{
			this.num = num;
			this.max = max;
		}

		public int GetOverOrUnderrun(bool over)
		{
			if (!over)
			{
				return Underrun;
			}
			return Overrun;
		}
	}

	public static readonly CrewType[] ALL_CREW_TYPES = Enum.GetValues(typeof(CrewType)) as CrewType[];

	private PlayerCrewData _crewdata;

	public readonly Label CAPTAIN = new Label("levelup-captain");

	public static readonly Dictionary<Label, PackID> ETH_TO_PACK_LISTING = new Dictionary<Label, PackID>
	{
		{
			new Label("pl"),
			PackID.EthPackPL
		},
		{
			new Label("ir"),
			PackID.EthPackIR
		},
		{
			new Label("en"),
			PackID.EthPackEN
		},
		{
			new Label("de"),
			PackID.EthPackDE
		},
		{
			new Label("it"),
			PackID.EthPackIT
		}
	};

	public int TotalCrewCount => _crewdata.Count;

	public int LivingCrewCount => _crewdata.countLiving;

	public int DeadCrewCount => _crewdata.countDead;

	public int CountVehicles => _crewdata.allVehicles.Count;

	public PlayerCrewGrowth CrewGrowth => _crewdata.crewcap;

	public IEnumerable<CrewAssignment> AllCrew => _crewdata.rawcrew;

	public IEnumerable<EntityID> AllScavengeableCars => _crewdata.scavengeableCars;

	public IEnumerable<EntityID> AllVehicles => _crewdata.allVehicles;

	public IEnumerable<EntityID> AllUnassignedVehicles => _crewdata.allVehicles.Where((EntityID eid) => _crewdata.FindCrewForTarget(eid).IsNotValid);

	public IEnumerable<CrewAssignment> AllCaptains => _crewdata.rawcrew.Where((CrewAssignment x) => x.GetPeep().components.agent.GetLevel(new Label("levelup-captain")) >= 0);

	private List<OffBoardInfo> CrewOffBoard => _crewdata.crewOffBoard;

	public Dictionary<CrewCardType, List<CrewCardInfoInitData>> OrderingDictionary => _crewdata.orderingByType;

	public bool IsCrewDefeated => _crewdata.countLiving == 0;

	public IEnumerable<CrewAssignment> GetLiving()
	{
		return _crewdata.WhereTypeIsNot(CrewType.Dead);
	}

	public IEnumerable<CrewAssignment> GetDead()
	{
		return _crewdata.WhereTypeIs(CrewType.Dead);
	}

	public IEnumerable<CrewAssignment> GetCrewByType(CrewType type)
	{
		return _crewdata.WhereTypeIs(type);
	}

	public List<OffBoardInfo> GetCrewOffBoardUnsafe()
	{
		return _crewdata.crewOffBoard;
	}

	public override void OnPostInitialize()
	{
		base.OnPostInitialize();
		Game.ctx.simman.peoplegen.OnBeforePersonDeath.Add(ProcessCrewMemberDeath);
		Game.ctx.events.AddListener(SessionEventType.CrewMemberAdded, OnSomeoneAddedCrewMember);
	}

	public override void OnPreRelease()
	{
		Game.ctx.events.RemoveListener(SessionEventType.CrewMemberAdded, OnSomeoneAddedCrewMember);
		Game.ctx.simman.peoplegen.OnBeforePersonDeath.Remove(ProcessCrewMemberDeath);
		base.OnPreRelease();
	}

	public override void OnPostSetDataSource(bool loaded)
	{
		_crewdata = _data.crew;
	}

	public void OnGlobalTurnSetAdvanced()
	{
	}

	public void OnPlayerTurnStarted()
	{
		_crewdata.crewcap.OnPlayerTurnStarted(this);
		HandleLevelups();
		HandleJunkCars();
		HandleBossDeath();
		HandlePromotionPrompting();
		HandleQueuedReturns();
	}

	public void OnPlayerTurnEnded()
	{
	}

	public PlayerTurnStatus GetPlayerTurnStatus()
	{
		return PlayerTurnStatus.TurnFinished;
	}

	public IEnumerable<CrewAssignment> IterateOverMuscleCrew()
	{
		int i = 0;
		int count;
		for (count = _crewdata.rawcrew.Count; i < count; i++)
		{
			CrewAssignment crewAssignment = _crewdata.rawcrew[i];
			if (!crewAssignment.IsDead && crewAssignment.IsInVehicle && !_player.automation.HasAutomation(crewAssignment))
			{
				yield return crewAssignment;
			}
		}
		count = 0;
		for (i = _crewdata.rawcrew.Count; count < i; count++)
		{
			CrewAssignment crewAssignment2 = _crewdata.rawcrew[count];
			if (!crewAssignment2.IsDead && crewAssignment2.IsInVehicle && _player.automation.HasAutomation(crewAssignment2))
			{
				yield return crewAssignment2;
			}
		}
	}

	public void SelectSpecificCrewMember(int index, bool zoom)
	{
		if (!_player.IsHuman)
		{
			return;
		}
		CrewAssignment crew = IterateOverMuscleCrew().Skip(index).FirstOrDefault();
		if (crew.IsValid)
		{
			Game.ctx.selection.SetActive(crew.GetVehicle());
			if (zoom)
			{
				PersonInfoUtil.TweenCameraToCrew(crew, showFx: false);
			}
		}
	}

	public void SelectNextCrewMember(bool zoom)
	{
		if (!_player.IsHuman)
		{
			return;
		}
		Entity currentActive = Game.ctx.selection.CurrentActive;
		if (currentActive != null && zoom)
		{
			PersonInfoUtil.TweenCameraToEntity(currentActive, showFx: false);
			return;
		}
		int current = ((currentActive == null) ? (-1) : _crewdata.FindTargetIndex(currentActive.Id));
		int num = FindNextCrewMemberAfter(current);
		if (num >= 0)
		{
			Entity vehicle = GetCrewForIndex(num).GetVehicle();
			if (vehicle != null)
			{
				Game.ctx.selection.SetActive(vehicle);
			}
		}
	}

	private int FindNextCrewMemberAfter(int current)
	{
		int count = _crewdata.Count;
		int num = current + 1;
		int num2 = num + count;
		for (int i = num; i < num2; i++)
		{
			int num3 = i % count;
			CrewAssignment crewForIndex = GetCrewForIndex(num3);
			if (crewForIndex.IsNotDead && crewForIndex.IsInVehicle)
			{
				return num3;
			}
		}
		return -1;
	}

	private void HandleLevelups()
	{
		if (!_pid.IsHumanPlayer)
		{
			return;
		}
		foreach (CrewAssignment item in _crewdata.rawcrew)
		{
			if (!item.IsDead && !item.IsNotAssigned)
			{
				item.GetPeep().components.agent.HandleLevelups();
			}
		}
	}

	private void HandleJunkCars()
	{
		if (!_pid.IsHumanPlayer)
		{
			return;
		}
		foreach (CrewAssignment item in _crewdata.rawcrew)
		{
			Entity vehicle = item.GetVehicle();
			if (vehicle != null && vehicle.components.mobile.IsJunk())
			{
				AutomationSequence autoOrNull = _player.automation.GetAutoOrNull(vehicle.Id);
				if (autoOrNull != null && !autoOrNull.IsAutoNotActive)
				{
					_player.automation.ToggleExecution(autoOrNull.id, activate: false);
					string fullName = item.GetPeep().data.person.FullName;
					Game.ctx.hud.tickers.AddTextTicker(TickerIcon.DELIVERIES, TickerTitle.DELIVERIES, Loc.Get("ui.tickers.vehicle.deteriorate", "driver", fullName));
				}
			}
		}
	}

	private void OnSomeoneAddedCrewMember(SessionEvent sev)
	{
		_crewdata.crewcap.OnSomeoneAddedCrew(this, sev.pid);
	}

	private void AddToCrew(Entity peep, Entity introducer)
	{
		peep.components.agent.SetPlayer(_pid);
		if (introducer != null)
		{
			peep.components.agent.SetIntroducer(introducer.Id);
		}
		_crewdata.Add(new CrewAssignment(peep.Id));
		TeleportHome(peep.Id);
		PlayerSocial.DebugLogAIHistory(_pid, _pid, peep, null, "ai", "crew-added");
		Game.ctx.events.EnqueueOnce(new SessionEvent(SessionEventType.CrewMemberAdded, peep.Id, _pid));
	}

	private void RemoveFromCrewCompletely(Entity peep)
	{
		PlayerSocial.DebugLogAIHistory(_pid, _pid, peep, null, "ai", "crew-removed");
		_crewdata.Remove(peep.Id);
		peep.components.agent.SetIntroducer(EntityID.INVALID);
		peep.components.agent.SetPlayer(PlayerID.System);
	}

	private (int index, CrewAssignment crew) FindUnassigned(EntityID peepId)
	{
		int num = _crewdata.FindPeepIndex(peepId);
		CrewAssignment item = _crewdata.Get(num);
		return (index: num, crew: item);
	}

	private (int index, CrewAssignment crew) FindAssigned(EntityID peepId, CrewType expected)
	{
		int num = _crewdata.FindPeepIndex(peepId);
		CrewAssignment item = _crewdata.Get(num);
		return (index: num, crew: item);
	}

	private void TeleportHome(EntityID peepId)
	{
		if (Game.ctx.IsInteractive)
		{
			NodeID nodeId = _player.territory?.GetHeadquartersNode()?.id ?? NodeID.INVALID;
			if (nodeId.IsValid)
			{
				Game.ctx.transit.SetAgentAtNode(nodeId, peepId.FindEntity());
			}
		}
	}

	public void AssignCrewToBuilding(EntityID peepId, EntityID buildingId)
	{
		NodeID nodeId = buildingId.FindEntity()?.components.board.GetNodeID() ?? NodeID.INVALID;
		var (index, crewAssignment) = FindUnassigned(peepId);
		_crewdata.Set(index, crewAssignment.SetBuilding(buildingId));
		Game.ctx.transit.SetAgentAtNode(nodeId, peepId);
		Game.ctx.events.EnqueueOnce(new SessionEvent(SessionEventType.CrewBuildingReassigned, buildingId, _pid));
	}

	public EntityID UnassignCrewFromBuilding(EntityID peepId)
	{
		(int index, CrewAssignment crew) tuple = FindAssigned(peepId, CrewType.InBuilding);
		int item = tuple.index;
		CrewAssignment item2 = tuple.crew;
		EntityID buildingID = item2.BuildingID;
		_crewdata.Set(item, item2.SetNotAssigned());
		TeleportHome(peepId);
		Game.ctx.events.EnqueueOnce(new SessionEvent(SessionEventType.CrewBuildingReassigned, buildingID, _pid));
		return buildingID;
	}

	public void AssignCrewToVehicle(EntityID peepId, EntityID vehicleId)
	{
		NodeID nodeId = vehicleId.FindEntity().components.mobile.FindNodeNearThisMobile();
		var (index, crewAssignment) = FindUnassigned(peepId);
		_crewdata.Set(index, crewAssignment.SetVehicle(vehicleId));
		Game.ctx.transit.SetAgentAtNode(nodeId, peepId);
		Game.ctx.events.EnqueueOnce(new SessionEvent(SessionEventType.CrewVehicleReassigned, vehicleId, _pid));
	}

	public EntityID UnassignCrewFromVehicle(EntityID peepId)
	{
		(int index, CrewAssignment crew) tuple = FindAssigned(peepId, CrewType.InVehicle);
		int item = tuple.index;
		CrewAssignment item2 = tuple.crew;
		EntityID vehicleID = item2.VehicleID;
		_player.automation.TryClearAutomationCrew(item2);
		_crewdata.Set(item, item2.SetNotAssigned());
		TeleportHome(peepId);
		Game.ctx.events.EnqueueOnce(new SessionEvent(SessionEventType.CrewVehicleReassigned, vehicleID, _pid));
		return vehicleID;
	}

	public Entity CreateAndTrackVehicleAtSafehouse(Label template)
	{
		return CreateAndTrackVehicle(template, _player.territory.GetHeadquartersNode().pos);
	}

	public Entity CreateAndTrackVehicle(Label template, WorldPos pos)
	{
		Entity entity = Game.ctx.transit.SpawnCarPossiblyHidden(_pid, template, pos);
		_crewdata.AddVehicleTracking(entity.Id);
		if (_player.IsHuman || Game.ctx.players.Human.meetings.IsPlayerMet(_pid))
		{
			PlayerMeetings.RevealAllUnitsOfPlayer(_pid);
		}
		Game.ctx.events.EnqueueOnce(new SessionEvent(SessionEventType.CrewVehicleCreated, entity.Id, _pid));
		return entity;
	}

	public void DestroyAndUntrackVehicle(EntityID vehicleId, bool shutdown)
	{
		Game.ctx.transit.DespawnCar(vehicleId, shutdown);
		_crewdata.RemoveVehicleTracking(vehicleId);
		Game.ctx.events.EnqueueOnce(new SessionEvent(SessionEventType.CrewVehicleRemoved, vehicleId, _pid));
	}

	public int CountVehiclesByType(AvatarType type)
	{
		int num = 0;
		foreach (EntityID allVehicle in _crewdata.allVehicles)
		{
			Entity entity = allVehicle.FindEntity();
			if (entity != null && entity.config.mobile?.type == type)
			{
				num++;
			}
		}
		return num;
	}

	public void RemoveSingletonGoonPeep(Entity peep)
	{
		if (_crewdata.FindPeepIndex(peep.Id) >= 0)
		{
			_player.social.UnmeetCrew(peep.Id);
			UnassignCrewAndDestroyVehicle(peep.Id);
			RemoveFromCrewCompletely(peep);
			_player.territory.RemoveGoonSafehouse();
		}
	}

	private void ProcessCrewMemberDeath(Entity peep)
	{
		int num = _crewdata.FindPeepIndex(peep.Id);
		if (num < 0)
		{
			return;
		}
		EntityID entityID = EntityID.INVALID;
		if (_crewdata.Get(num).IsInVehicle)
		{
			if (!_player.IsHuman)
			{
				MarkPeepsCarAsScavengeable(peep.Id);
			}
			entityID = UnassignCrewFromVehicle(peep.Id);
			if (_player.IsHuman)
			{
				DestroyAndUntrackVehicle(entityID, shutdown: false);
			}
		}
		AutomationExecutor automation = Game.ctx.players.Human.automation;
		AutomationSequence autoOrNull = automation.GetAutoOrNull(peep.components.agent.FindCrewAssignment());
		if (autoOrNull != null)
		{
			autoOrNull.Stop();
			automation.ClearAutomationCrew(autoOrNull.id);
		}
		_player.social.UnmeetCrew(peep.Id);
		_crewdata.MarkAsDead(num);
		Game.ctx.events.EnqueueOnce(new SessionEvent(SessionEventType.CrewMemberKilled, peep.Id, _pid, entityID));
		if (IsCrewDefeated)
		{
			ProcessPlayerDefeat();
		}
	}

	private void ProcessPlayerDefeat()
	{
		Game.ctx.events.EnqueueOnce(new SessionEvent(SessionEventType.CrewVanquished, EntityID.INVALID, _pid));
	}

	private void HandleBossDeath()
	{
		int index;
		if (_player.IsJustGang)
		{
			index = FindFirstLivingCrewIndex();
			if (index != 0 && index >= 0)
			{
				ReassignBoss();
			}
		}
		int FindFirstLivingCrewIndex()
		{
			int i = 0;
			for (int count = _crewdata.rawcrew.Count; i < count; i++)
			{
				if (_crewdata.rawcrew[i].IsNotDead)
				{
					return i;
				}
			}
			return -1;
		}
		void ReassignBoss()
		{
			bool num = _player.meetings.IsPlayerMet(PlayerID.HumanPlayer);
			CrewAssignment value = _crewdata.rawcrew[0];
			CrewAssignment value2 = _crewdata.rawcrew[index];
			string playerGroupName = _player.social.PlayerGroupName;
			_crewdata.rawcrew[0] = value2;
			_crewdata.rawcrew[index] = value;
			_player.social.SetBossInfo(value2.GetPeep());
			Game.ctx.mapdisplay.RefreshTerritoryLabel(_player.PID, updateName: true);
			AILog.LogMilestone(_player.PID, value2.GetPeep().Id, "Shake-up: " + value2.GetPeep().data.person.ShortName + " takes over " + playerGroupName);
			PlayerSocial.DebugLogAIHistory(_pid, _pid, value2.GetPeep(), null, "ai", "boss-replaced");
			if (num)
			{
				string playerFullName = _player.social.PlayerFullName;
				string header = Loc.Get("newspaper-headline.outfitboss", "bossname", playerFullName, "groupname", playerGroupName);
				PhotoConfig value3 = Game.serv.globals.settings.people.combatSettings.eliminateGangPhotos.LastOrDefaultFast();
				Game.serv.ui.AddPopup(new NewspaperPopup(header, value3));
			}
			foreach (Relationship datum in Game.ctx.simman.rels.GetListOrCreate(value.peepId).data)
			{
				Entity entity = datum.to.FindEntity();
				PlayerID pid = entity?.data.agent?.pid ?? PlayerID.INVALID;
				if (pid.IsAnyPlayer)
				{
					EntityID playerPeepId = pid.FindPlayer().social.PlayerPeepId;
					if (!(playerPeepId != entity.Id) && datum.buffs != null && !datum.buffs.IsEmpty)
					{
						Relationship item = Game.ctx.simman.rels.GetOrMakeSymmetrical(value2.peepId, playerPeepId, RelationshipType.Acquaintance, warnOnExisting: false).toTarget;
						foreach (BuffState state in datum.buffs.states)
						{
							item.AddBuff(state.id, state.crewpeep);
						}
					}
				}
			}
		}
	}

	public void HandlePromotionPrompting()
	{
		foreach (CrewAssignment item in GetLiving())
		{
			foreach (RoleDef roleDef in Game.serv.globals.settings.people.social.crew.roleSettings.roleDefs)
			{
				string[] array = new string[4]
				{
					"name",
					item.GetPeep().data.person.FullName,
					"roleName",
					Loc.Get(roleDef.loctitle)
				};
				VisitState visit = new VisitState(item, Game.ctx.clock.Now, PlayerID.HumanPlayer);
				if (roleDef.visReqs.AllPass(visit))
				{
					if (IsQualifiedForRole(item, roleDef))
					{
						TickerBar tickers = Game.ctx.hud.tickers;
						object[] replacements = array;
						tickers.AddPromoteTicker(Loc.Get("ui.tickers.promote-ready", replacements), roleDef);
					}
					else if (IsValidPromotionChoice(item) && roleDef.reqs.CountPass(visit) >= roleDef.reqs.Count - 1 && !HasPrompted(item, roleDef.id))
					{
						TickerBar tickers2 = Game.ctx.hud.tickers;
						object[] replacements = array;
						tickers2.AddPromoteTicker(Loc.Get("ui.tickers.promote-close", replacements), roleDef);
						_crewdata.rolesPrompted.Add(new PlayerCrewData.RolePrompt
						{
							peep = item,
							roleId = roleDef.id
						});
					}
				}
			}
		}
	}

	public void HandleQueuedReturns()
	{
		foreach (QueuedBoardReturnTime item in Game.serv.serializer.instance.Clone(_crewdata.queuedBoardReturns))
		{
			if (item.automaticReturnTime >= Game.ctx.clock.Now)
			{
				ReturnCrewToBoard(item.crew);
			}
		}
	}

	public void AddSupportPayment(EntityID peepId, Price perTurn)
	{
		_data.crew.deadCrewPayments.Add(new PlayerCrewData.FamilyPayment
		{
			peepId = peepId,
			perTurn = perTurn
		});
	}

	public void RemoveSupportPayment(EntityID peepId)
	{
		for (int i = 0; i < _data.crew.deadCrewPayments.Count; i++)
		{
			if (_data.crew.deadCrewPayments[i].peepId == peepId)
			{
				_data.crew.deadCrewPayments.RemoveAt(i);
				break;
			}
		}
	}

	public (bool paying, Price amount) GetSupportPayments(EntityID peepId)
	{
		for (int i = 0; i < _data.crew.deadCrewPayments.Count; i++)
		{
			PlayerCrewData.FamilyPayment familyPayment = _data.crew.deadCrewPayments[i];
			if (familyPayment.peepId == peepId)
			{
				return (paying: true, amount: familyPayment.perTurn);
			}
		}
		return (paying: false, amount: default(Price));
	}

	public IEnumerable<PlayerCrewData.FamilyPayment> GetAllSupportPaymentsUnsafe()
	{
		return _data.crew.deadCrewPayments;
	}

	public bool IsCrew(EntityID peepId)
	{
		return _crewdata.FindPeepIndex(peepId) >= 0;
	}

	public bool IsPlayerPeep(EntityID peepId)
	{
		return _player.social.PlayerPeepId == peepId;
	}

	public void PopulateWithIDs(List<EntityID> list, bool onlyLiving)
	{
		foreach (CrewAssignment item in onlyLiving ? GetLiving() : AllCrew)
		{
			list.Add(item.peepId);
		}
	}

	public EntityID FindVehicleAssignedToPeep(EntityID peepId)
	{
		CrewAssignment crewAssignment = _crewdata.FindCrewForPeep(peepId);
		if (!crewAssignment.IsInVehicle)
		{
			return EntityID.INVALID;
		}
		return crewAssignment.targetId;
	}

	public EntityID FindPeepAssignedToVehicle(EntityID vehicleId)
	{
		CrewAssignment crewAssignment = _crewdata.FindCrewForTarget(vehicleId);
		if (!crewAssignment.IsInVehicle)
		{
			return EntityID.INVALID;
		}
		return crewAssignment.peepId;
	}

	public CrewAssignment GetCrewForIndex(int index)
	{
		return _crewdata.Get(index);
	}

	public CrewAssignment GetCrewForPlayerPeep()
	{
		return GetCrewForIndex(0);
	}

	public CrewAssignment GetCrewForPeep(EntityID peepId)
	{
		return _crewdata.FindCrewForPeep(peepId);
	}

	public CrewAssignment GetCrewForTarget(EntityID targetId)
	{
		return _crewdata.FindCrewForTarget(targetId);
	}

	public string GetCrewPeepName(CrewAssignment crew)
	{
		return GetCrewPeepName(crew, _player.IsHuman);
	}

	public string GetCrewPeepName(CrewAssignment crew, bool human)
	{
		return GetCrewPeepName(crew.peepId.FindEntity(), human && _crewdata.FindPeepIndex(crew.peepId) == 0);
	}

	private string GetCrewPeepName(Entity peep, bool showrank)
	{
		string shortName = peep.data.person.ShortName;
		if (!showrank)
		{
			return shortName;
		}
		return peep.components.agent.WrapWithRankIcon(shortName);
	}

	public string GetCrewPeepAndGroupName(CrewAssignment crew)
	{
		return GetCrewPeepName(crew) + Loc.Get("ui.crewinfo.name-and-group", "group", _player.social.PlayerGroupName);
	}

	public void PopulateWithIDs(ListPool<EntityID>.PooledBlockList blocklist)
	{
		foreach (CrewAssignment item in GetLiving())
		{
			blocklist.Add(item.peepId);
		}
	}

	public bool CanAddCrew(int howmany = 1)
	{
		return _crewdata.countLiving + howmany <= _crewdata.crewcap.currentCap;
	}

	public Fixnum CrewFreeCapacity()
	{
		return _crewdata.crewcap.currentCap - _crewdata.countLiving;
	}

	public List<CrewAssignment> FindAllDriversAtNode(NodeID nodeId)
	{
		return (from crew in FindAllCrewAtNode(nodeId)
			where crew.IsValid && crew.IsInVehicle
			select crew).ToList();
	}

	public List<CrewAssignment> FindAllCrewAtNode(NodeID nodeId)
	{
		List<EntityID> allAgentsAtNodeUnsafe = Game.ctx.transit.GetAllAgentsAtNodeUnsafe(nodeId);
		List<CrewAssignment> list = new List<CrewAssignment>();
		foreach (CrewAssignment item in _crewdata.rawcrew)
		{
			if (allAgentsAtNodeUnsafe.ContainsFast(item.peepId))
			{
				list.Add(item);
			}
		}
		return list;
	}

	public CrewAssignment FindFirstCrewAtLocation(NodeID nodeId, bool onlyInVehicles = true)
	{
		List<EntityID> allAgentsAtNodeUnsafe = Game.ctx.transit.GetAllAgentsAtNodeUnsafe(nodeId);
		foreach (CrewAssignment item in _crewdata.rawcrew)
		{
			if ((!onlyInVehicles || (!item.IsNotValid && item.IsInVehicle)) && allAgentsAtNodeUnsafe.ContainsFast(item.peepId))
			{
				return item;
			}
		}
		return CrewAssignment.EMPTY;
	}

	public CrewAssignment FindFirstCrewIntroducedBy(EntityID introducerId)
	{
		foreach (CrewAssignment item in _crewdata.rawcrew)
		{
			if ((item.GetPeep()?.data.agent?.introducer ?? EntityID.INVALID) == introducerId)
			{
				return item;
			}
		}
		return CrewAssignment.EMPTY;
	}

	public Label FindDefaultCar(bool isBoss)
	{
		if (isBoss && _player.IsHuman && Game.serv.store.IsPreorderInstalled())
		{
			return EntityConstants.VEHICLE_CAR_PREORDER;
		}
		return _player?.ai?.units?.DefaultCar ?? EntityConstants.VEHICLE_CAR;
	}

	public void HireNewCrewMemberUnassigned(Entity peep, Entity introducer)
	{
		AddCrewAtNode(_player.territory.GetHeadquartersNode(), peep, introducer, isBoss: false, inCar: false);
	}

	public void HireNewCrewInVehicle(Node node, Entity peep, Entity introducer, bool isBoss)
	{
		AddCrewAtNode(node, peep, introducer, isBoss, inCar: true);
	}

	public void HireNewCrewInSpecificVehicle(Node node, Entity peep, Entity introducer, Label vehicle)
	{
		AddToCrewUnassigned(peep, introducer, isBoss: false);
		if (vehicle.IsSet)
		{
			CreateVehicleAndAssignCrew(node, peep, vehicle);
		}
	}

	private void AddCrewAtNode(Node node, Entity peep, Entity introducer, bool isBoss, bool inCar)
	{
		AddToCrewUnassigned(peep, introducer, isBoss);
		if (inCar)
		{
			CreateVehicleAndAssignCrew(node, peep, isBoss);
		}
	}

	public void AddToCrewUnassigned(Entity peep, Entity introducer, bool isBoss)
	{
		AddToCrew(peep, introducer);
		_player.social.MeetCrew(peep.Id, isBoss);
		_player.meetings.EnsureMetPlayersKnowCrewSymmetric();
	}

	public Entity CreateVehicleAndAssignCrew(Node node, Entity peep, bool isBoss = false)
	{
		return CreateVehicleAndAssignCrew(node, peep, FindDefaultCar(isBoss));
	}

	private Entity CreateVehicleAndAssignCrew(Node node, Entity peep, Label template)
	{
		Entity entity = CreateAndTrackVehicle(template, node.pos);
		AssignCrewToVehicle(peep.Id, entity.Id);
		return entity;
	}

	public EntityID UnassignCrewAndDestroyVehicle(EntityID peepId)
	{
		EntityID entityID = UnassignCrewFromVehicle(peepId);
		DestroyAndUntrackVehicle(entityID, shutdown: false);
		return entityID;
	}

	public void MarkPeepsCarAsScavengeable(EntityID peep)
	{
		int index = _crewdata.FindPeepIndex(peep);
		EntityID vehicleID = _crewdata.rawcrew[index].VehicleID;
		if (vehicleID.IsValid)
		{
			_crewdata.scavengeableCars.Add(vehicleID);
		}
	}

	public void RemoveScavengeableCar(EntityID car)
	{
		PlayerInfo playerInfo = _crewdata.pid.FindPlayer();
		int num = _crewdata.scavengeableCars.IndexOf(car);
		if (num >= 0)
		{
			Money money = ModulesUtil.GetInventory(car).data.money;
			playerInfo.finances.DoChangeMoney(car.FindEntity(), new Price(-money.cash), MoneyReason.CombatDefeat);
			playerInfo.crew.DestroyAndUntrackVehicle(car, shutdown: false);
			_crewdata.scavengeableCars.RemoveAt(num);
		}
	}

	public NumAndMax GetPeepCount()
	{
		int livingCrewCount = LivingCrewCount;
		int max = (int)CrewGrowth.currentCap;
		return new NumAndMax(livingCrewCount, max);
	}

	public NumAndMax GetVehicleCount(AvatarType type)
	{
		int num = CountVehiclesByType(type);
		int vehicleCap = CrewGrowth.GetVehicleCap(this, type == AvatarType.Car);
		return new NumAndMax(num, vehicleCap);
	}

	public int GetCapacity(CapacityType type)
	{
		switch (type)
		{
		case CapacityType.Crew:
			return GetPeepCount().max;
		case CapacityType.Car:
			return GetVehicleCount(AvatarType.Car).max;
		case CapacityType.Truck:
			return GetVehicleCount(AvatarType.Truck).max;
		case CapacityType.AnyVehicle:
		{
			int max = GetVehicleCount(AvatarType.Car).max;
			int max2 = GetVehicleCount(AvatarType.Truck).max;
			return Math.Max(max, max2);
		}
		default:
			return 0;
		}
	}

	public int GetCapacityOverrun(CapacityType type, bool overrun)
	{
		switch (type)
		{
		case CapacityType.Crew:
			return GetPeepCount().GetOverOrUnderrun(overrun);
		case CapacityType.Car:
			return GetVehicleCount(AvatarType.Car).GetOverOrUnderrun(overrun);
		case CapacityType.Truck:
			return GetVehicleCount(AvatarType.Truck).GetOverOrUnderrun(overrun);
		case CapacityType.AnyVehicle:
		{
			int overOrUnderrun = GetVehicleCount(AvatarType.Car).GetOverOrUnderrun(overrun);
			int overOrUnderrun2 = GetVehicleCount(AvatarType.Truck).GetOverOrUnderrun(overrun);
			return Math.Max(overOrUnderrun, overOrUnderrun2);
		}
		default:
			return 0;
		}
	}

	public List<EntityID> GetQualifiedForRole(Label roleId)
	{
		return GetQualifiedForRole(Game.serv.globals.settings.people.social.crew.roleSettings.GetRoleById(roleId));
	}

	public List<EntityID> GetQualifiedForRole(RoleDef role)
	{
		List<EntityID> list = new List<EntityID>();
		foreach (CrewAssignment item in AllCrew)
		{
			VisitState visit = new VisitState(item, Game.ctx.clock.Now, PlayerID.HumanPlayer);
			if (role.reqs.AllPass(visit))
			{
				Entity peep = item.GetPeep();
				if (peep != null && peep.data.agent?.xp?.GetLevelupLevel(CAPTAIN) == 1 && item.GetPeep()?.data.agent?.xp?.GetCrewRole() == null)
				{
					list.Add(item.peepId);
				}
			}
		}
		return list;
	}

	public bool IsQualifiedForRole(CrewAssignment crew, RoleDef role)
	{
		VisitState visit = new VisitState(crew, Game.ctx.clock.Now, PlayerID.HumanPlayer);
		if (role.reqs.AllPass(visit))
		{
			Entity peep = crew.GetPeep();
			if (peep != null && peep.data.agent?.xp?.GetLevelupLevel(CAPTAIN) == 1)
			{
				return crew.GetPeep()?.data.agent?.xp?.GetCrewRole() == null;
			}
		}
		return false;
	}

	public bool IsValidPromotionChoice(CrewAssignment crew)
	{
		Entity peep = crew.GetPeep();
		if (peep != null && peep.data.agent?.xp?.GetLevelupLevel(CAPTAIN) == 1 && crew.GetPeep()?.data.agent?.xp?.GetCrewRole() == null && crew.GetPeep() != GetCrewForPlayerPeep().GetPeep())
		{
			return crew.IsNotDead;
		}
		return false;
	}

	public bool HasPrompted(CrewAssignment crew, Label roleId)
	{
		return _crewdata.rolesPrompted.Contains(new PlayerCrewData.RolePrompt
		{
			peep = crew,
			roleId = roleId
		});
	}

	public int GetNumCrewInRole(Label roleId)
	{
		return AllCrew.Where(delegate(CrewAssignment x)
		{
			AgentData agent = x.GetPeep().data.agent;
			if (agent == null)
			{
				return false;
			}
			Label? label = agent.xp?.GetCrewRole()?.id;
			Label label2 = roleId;
			if (!label.HasValue)
			{
				return false;
			}
			return !label.HasValue || label.GetValueOrDefault() == label2;
		}).Count();
	}

	public int GetNumSpecialists()
	{
		return AllCrew.Where((CrewAssignment x) => x.GetPeep().data.agent?.xp?.GetCrewRole() != null).Count();
	}

	public void ChangeOffBoardReason(EntityID crew, PlayerCrewData.OffBoardReason reason)
	{
		OffBoardInfo item = CrewOffBoard.Find((OffBoardInfo x) => x.crew == crew);
		CrewOffBoard.Remove(item);
		CrewOffBoard.Add(new OffBoardInfo(crew, item.vehTemplate, reason));
	}

	public bool IsOnBoard(EntityID crew)
	{
		return !CrewOffBoard.Select((OffBoardInfo x) => x.crew).Contains(crew);
	}

	public bool IsOnBoard(Entity crew)
	{
		return IsOnBoard(crew.Id);
	}

	public bool IsOnBoard(CrewAssignment crew)
	{
		return IsOnBoard(crew.peepId);
	}

	public void RemoveCrewFromBoard(Entity crew, PlayerCrewData.OffBoardReason reason, bool removeCar = false, int daysAway = 0)
	{
		RemoveCrewFromBoard(crew.components.agent.FindCrewAssignment(), reason, removeCar, daysAway);
	}

	public void RemoveCrewFromBoard(EntityID crew, PlayerCrewData.OffBoardReason reason, bool removeCar = false, int daysAway = 0)
	{
		RemoveCrewFromBoard(crew.FindEntity(), reason, removeCar, daysAway);
	}

	public void RemoveCrewFromBoard(CrewAssignment crew, PlayerCrewData.OffBoardReason reason, bool removeCar = false, int daysAway = 0)
	{
		_player.commands.FlushQueue(crew.peepId, cancelActive: false);
		if (_player.automation.HasAutomation(crew))
		{
			_player.automation.ClearAutomationCrew(crew);
		}
		Label vehTemplate = Label.NULL;
		if (crew.IsInVehicle)
		{
			Entity entity = crew.VehicleID.FindEntity();
			_player.crew.UnassignCrewFromVehicle(crew.peepId);
			if (_player.IsJustGang || removeCar)
			{
				MoveAllResourcesFromVehicleToSafehouse(entity);
				vehTemplate = entity.config.Template;
				_player.crew.DestroyAndUntrackVehicle(entity.Id, shutdown: false);
			}
		}
		else if (crew.IsInBuilding)
		{
			_player.crew.UnassignCrewFromBuilding(crew.peepId);
		}
		if (!CrewOffBoard.Select((OffBoardInfo x) => x.crew.id).Contains(crew.peepId))
		{
			CrewOffBoard.Add(new OffBoardInfo(crew.peepId, vehTemplate, reason));
			if (daysAway != 0)
			{
				_crewdata.queuedBoardReturns.Add(new QueuedBoardReturnTime(crew.peepId, Game.ctx.clock.Now.IncrementDays(daysAway)));
			}
		}
	}

	public void ReturnCrewToBoard(Entity crew)
	{
		ReturnCrewToBoard(crew.components.agent.FindCrewAssignment());
	}

	public void ReturnCrewToBoard(EntityID crew)
	{
		ReturnCrewToBoard(crew.FindEntity());
	}

	public void ReturnCrewToBoard(CrewAssignment crew)
	{
		OffBoardInfo item = CrewOffBoard.Find((OffBoardInfo x) => x.crew == crew.peepId);
		if (item.vehTemplate.IsSet)
		{
			_player.crew.CreateVehicleAndAssignCrew(_player.territory.GetHeadquartersNode(), item.crew.FindEntity(), item.vehTemplate);
		}
		CrewOffBoard.Remove(item);
		QueuedBoardReturnTime item2 = _crewdata.queuedBoardReturns.Find((QueuedBoardReturnTime x) => x.crew == crew.peepId);
		if (item2.crew.IsValid)
		{
			_crewdata.queuedBoardReturns.Remove(item2);
		}
	}

	private void MoveAllResourcesFromVehicleToSafehouse(Entity vehicle)
	{
		InventoryModule inventory = ModulesUtil.GetInventory(_player.territory.Safehouse.FindEntity());
		InventoryModule inventory2 = ModulesUtil.GetInventory(vehicle);
		ModulesUtil.TransferCashBetweenPlayerInventories(_player.PID, inventory2, inventory);
		foreach (ResourceAndQty item in Game.serv.serializer.instance.Clone(inventory2.data.contents))
		{
			ModulesUtil.TransferResourceBetweenPlayerInventories(_player.PID, inventory2, inventory, item.id);
		}
	}

	public bool CanSeeRole(RoleDef def)
	{
		if (def == null)
		{
			return true;
		}
		bool result = true;
		foreach (IVisitRequirement visReq in def.visReqs)
		{
			if (visReq is CheckPacks)
			{
				result = visReq.DoesPass(new VisitState(CrewAssignment.EMPTY, Game.ctx.clock.Now, _player.PID));
			}
		}
		return result;
	}

	public void UnassignRole(CrewAssignment crew)
	{
		SchemeData schemeForCrew = _player.schemes.GetSchemeForCrew(crew.GetPeep());
		if (schemeForCrew != null)
		{
			_player.schemes.EndCurrentChapter(schemeForCrew.schemeID);
			_player.schemes.EndSchemeForCrew(crew.GetPeep());
		}
		crew.GetPeep().data.agent.xp?.SetCrewRole(Label.NULL);
	}

	public CrewAssignment GetOneCrewForRole(Label roleId)
	{
		return AllCrew.Where(delegate(CrewAssignment x)
		{
			XP xp = x.GetPeep().data.agent.xp;
			return xp != null && xp.crewRole == roleId;
		}).FirstOrDefault();
	}

	protected override void InitializeConsoleEntries()
	{
		base.InitializeConsoleEntries();
		Game.ctx.console.Add(this, new DebugConsoleEntry("crew", "add-crew", CheatAddCrew));
		Game.ctx.console.Add(this, new DebugConsoleEntry("crew", "kill-crew", CheatKillCrew));
		Game.ctx.console.Add(this, new DebugConsoleEntry("crew", "increase-cap", CheatIncreaseCap));
		Game.ctx.console.Add(this, new DebugConsoleEntry("crew", "set-points", CheatSetPoints));
		Game.ctx.console.Add(this, new DebugConsoleEntry("crew", "add-xp", CheatAddXP));
		Game.ctx.console.Add(this, new DebugConsoleEntry("crew", "set-xp", CheatSetXP));
		Game.ctx.console.Add(this, new DebugConsoleEntry("crew", "set-crew-health", CheatSetCrewHealth));
		Game.ctx.console.Add(this, new DebugConsoleEntry("crew", "set-vehicle-health", CheatSetVehicleHealth));
		foreach (EntityConfig item in Game.ctx.entityman.FindTemplatesByTest(IsVehicle))
		{
			Game.ctx.console.Add(this, new DebugConsoleEntry("crew", "grant-vehicle", item.Template.String, CheatGrantVehicle));
		}
		foreach (RoleDef roleDef in Game.serv.globals.settings.people.social.crew.roleSettings.roleDefs)
		{
			if (_player.crew.CanSeeRole(roleDef))
			{
				Game.ctx.console.Add(this, new DebugConsoleEntry("crew", "give-role", roleDef.id.String, CheatSetCrewRole));
			}
		}
		foreach (CrewSettings.StatCategory statCategory in Game.serv.globals.settings.people.social.crew.statCategories)
		{
			foreach (CrewStats entry in statCategory.entries)
			{
				Game.ctx.console.Add(this, new DebugConsoleEntry("crew", "increment-stat", Enum.GetName(typeof(CrewStats), entry).ToLowerInvariant(), CheatIncrementStat));
			}
		}
	}

	protected override void ReleaseConsoleEntries()
	{
		Game.ctx.console.Remove(this);
		base.ReleaseConsoleEntries();
	}

	private static bool IsVehicle(EntityConfig config)
	{
		if (config.mobile != null && config.mobile.IsPeepVehicleType)
		{
			return config.model?.prefabs != null;
		}
		return false;
	}

	private string CheatGrantVehicle(string[] args)
	{
		if (args.Length != 3)
		{
			return DebugConsoleEntry.InvalidParam("Invalid parameters", args, 2, "<vehicle-name>");
		}
		Label label = (Label)args[2];
		Entity entity = CreateAndTrackVehicleAtSafehouse(label);
		Game.ctx.models.RevealModelIfHidden(entity);
		return $"Added {label} at safehouse corner";
	}

	private string CheatAddCrew(string[] args)
	{
		if (args.Length != 2)
		{
			return DebugConsoleEntry.InvalidParam("Invalid parameters", args, 1);
		}
		Entity peep = CreatePlayers.CheatGenerateCrewForPlayer(_player);
		HireNewCrewMemberUnassigned(peep, null);
		return "Added crew peep for human player";
	}

	private string CheatKillCrew(string[] args)
	{
		if (args.Length != 3)
		{
			return DebugConsoleEntry.InvalidParam("Invalid parameters", args, 2, "<crew-index>");
		}
		if (int.TryParse(args[2], out var result))
		{
			CrewAssignment crewForIndex = _player.crew.GetCrewForIndex(result);
			if (crewForIndex.IsValid && crewForIndex.IsNotDead)
			{
				_ = crewForIndex.IsInBuilding;
				Game.ctx.simman.peoplegen.MarkAsDead(crewForIndex.GetPeep(), Game.ctx.clock.Now);
				return $"Killed crew member {crewForIndex.peepId} from human crew.";
			}
		}
		return DebugConsoleEntry.InvalidParam("Invalid crew index", args, 2, "<crew-index>");
	}

	private string CheatSetCrewRole(string[] args)
	{
		if (args.Length != 4)
		{
			return DebugConsoleEntry.InvalidParam("Invalid parameters", args, 2, "<role-id> <crew-index>");
		}
		if (int.TryParse(args[3], out var result))
		{
			CrewAssignment crewForIndex = _player.crew.GetCrewForIndex(result);
			Label label = new Label(args[2]);
			if (Game.serv.globals.settings.people.social.crew.roleSettings.GetRoleById(label) == null)
			{
				DebugConsoleEntry.InvalidParam("Invalid Parameters", args, 2, $"{label} is not a valid roleID");
			}
			if (crewForIndex.IsValid && crewForIndex.IsNotDead)
			{
				Entity entity = crewForIndex.peepId.FindEntity();
				entity.components.agent.AddXP(1);
				entity.data.agent.xp.SetCrewRole(label);
				return $"Gave crew member {crewForIndex.peepId} the role {label}";
			}
		}
		return DebugConsoleEntry.InvalidParam("Invalid crew index", args, 2, "<role-id> <crew-index>");
	}

	private string CheatIncrementStat(string[] args)
	{
		if (args.Length != 5)
		{
			return DebugConsoleEntry.InvalidParam("Invalid parameters", args, 2, "<stat-name> <crew-index> <increment-value>");
		}
		if (Enum.TryParse<CrewStats>(args[2], ignoreCase: true, out var result))
		{
			if (int.TryParse(args[3], out var result2) && int.TryParse(args[4], out var result3))
			{
				CrewAssignment crewForIndex = _player.crew.GetCrewForIndex(result2);
				if (crewForIndex.IsValid && crewForIndex.IsNotDead && result3 >= 0)
				{
					crewForIndex.GetPeep().components.agent.IncrementStat(result, result3);
					return $"Incremented stat {args[2]} by {args[4]} for crewmember {crewForIndex.peepId}";
				}
			}
			return DebugConsoleEntry.InvalidParam("Invalid crew-index or increment-value", args, 3, "<stat-name> <crew-index> <increment-value>");
		}
		return DebugConsoleEntry.InvalidParam("Invalid stat-name", args, 2, "<stat-name> <crew-index> <increment-value>");
	}

	private string CheatIncreaseCap(string[] args)
	{
		if (args.Length != 3)
		{
			return DebugConsoleEntry.InvalidParam("Invalid parameters", args, 2, "<delta>");
		}
		if (int.TryParse(args[2], out var result))
		{
			_crewdata.crewcap.DebugApplyCapDelta(result);
			return $"Increased cap by {result}, advance turn to see results.";
		}
		return DebugConsoleEntry.InvalidParam("Invalid parameters", args, 2, "<delta>");
	}

	private string CheatSetPoints(string[] args)
	{
		if (args.Length != 5 || !int.TryParse(args[2], out var result) || !int.TryParse(args[3], out var result2) || !int.TryParse(args[4], out var result3))
		{
			return DebugConsoleEntry.InvalidParam("Invalid parameters", args, 2, "<crew-index> <action-points> <movement-points>");
		}
		CrewAssignment crewForIndex = _player.crew.GetCrewForIndex(result);
		if (!crewForIndex.IsValid || crewForIndex.IsNotDead)
		{
			return DebugConsoleEntry.InvalidParam("Invalid crew index", args, 5);
		}
		crewForIndex.peepId.FindEntity().components.agent.CheatSetActionsAndMoves(result2, result3);
		return $"Forced peep {result} points: actions = {result2}, movement = {result3}";
	}

	private string CheatSetXP(string[] args)
	{
		if (args.Length != 4 || !int.TryParse(args[2], out var result) || !int.TryParse(args[3], out var result2))
		{
			return DebugConsoleEntry.InvalidParam("Invalid parameters", args, 2, "<crew-index> <value>");
		}
		CrewAssignment crewForIndex = _player.crew.GetCrewForIndex(result);
		crewForIndex.GetPeep().components.agent.SetXP(result2);
		return $"Set xp for {crewForIndex.peepId} to {result2} points.";
	}

	private string CheatAddXP(string[] args)
	{
		if (args.Length != 4 || !int.TryParse(args[2], out var result) || !int.TryParse(args[3], out var result2))
		{
			return DebugConsoleEntry.InvalidParam("Invalid parameters", args, 2, "<crew-index> <delta>");
		}
		CrewAssignment crewForIndex = _player.crew.GetCrewForIndex(result);
		crewForIndex.GetPeep().components.agent.AddXP(result2);
		return $"Increased xp for {crewForIndex.peepId} by {result2} points.";
	}

	private string CheatSetCrewHealth(string[] args)
	{
		if (args.Length != 4 || !int.TryParse(args[2], out var result) || !int.TryParse(args[3], out var result2))
		{
			return DebugConsoleEntry.InvalidParam("Invalid parameters", args, 2, "<crew-index> <health>");
		}
		CrewAssignment crewForIndex = _player.crew.GetCrewForIndex(result);
		if (!crewForIndex.IsValid || crewForIndex.IsDead)
		{
			return DebugConsoleEntry.InvalidParam("Invalid crew index", args, 4);
		}
		crewForIndex.GetPeep().components.agent.SetHealth(result2);
		return $"Set crew health for {crewForIndex.peepId} to {result2}.";
	}

	private string CheatSetVehicleHealth(string[] args)
	{
		if (args.Length != 4 || !int.TryParse(args[2], out var result) || !int.TryParse(args[3], out var result2))
		{
			return DebugConsoleEntry.InvalidParam("Invalid parameters", args, 2, "<crew-index> <health>");
		}
		CrewAssignment crewForIndex = _player.crew.GetCrewForIndex(result);
		if (!crewForIndex.IsValid || !crewForIndex.IsInVehicle)
		{
			return DebugConsoleEntry.InvalidParam("Invalid crew index, crew not in vehicle", args, 4);
		}
		crewForIndex.GetVehicle().components.mobile.SetHealth(crewForIndex, result2);
		return $"Set vehicle health for {crewForIndex.VehicleID} to {result2}.";
	}

	public static bool HasEthPackAndIsEth(Label eth)
	{
		bool num = Game.ctx.scenario.newgamepars.playerdetails.player.ethnicity == eth;
		bool flag = ETH_TO_PACK_LISTING.Keys.Contains(eth);
		bool flag2 = flag && Game.serv.store.IsPackInstalled(ETH_TO_PACK_LISTING[eth]);
		return num && flag && flag2;
	}

	public static bool HasEthPackForCurrEth()
	{
		Label ethnicity = Game.ctx.scenario.newgamepars.playerdetails.player.ethnicity;
		if (ETH_TO_PACK_LISTING.Keys.Contains(ethnicity))
		{
			return Game.serv.store.IsPackInstalled(ETH_TO_PACK_LISTING[ethnicity]);
		}
		return false;
	}

	public static bool HasEthPackForEth(Label eth)
	{
		if (ETH_TO_PACK_LISTING.Keys.Contains(eth))
		{
			return Game.serv.store.IsPackInstalled(ETH_TO_PACK_LISTING[eth]);
		}
		return false;
	}

	public static bool EthPackHasUniqueAlc()
	{
		return Game.serv.globals.settings.resources.ethPackAlcohols.FirstOrDefault((KeyValuePair<Label, EthnicAlcoholReplacements> x) => x.Value.ethId == Game.ctx.scenario.newgamepars.playerdetails.player.ethnicity).Value?.resource != null;
	}
}
