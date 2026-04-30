using System;
using System.Collections.Generic;
using Game.Core;
using Game.Services;
using Game.Session.Board;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Sim.Modules;
using SomaSim.Util;

namespace Game.Session.Player.AI;

public sealed class GoonAdvisor : AIAdvisor
{
	private GoonAdvisorConfig _def;

	private GoonAdvisorData _data;

	private static readonly List<Label> GOON_HARRASS_TYPES = new List<Label>
	{
		ScriptNames.BURGLE,
		ScriptNames.VANDALIZE,
		ScriptNames.EXTORT
	};

	private const int MAX_TARGETS = 3;

	private const int MAX_NODES = 256;

	public Label GoonType => _data.goontype;

	public GoonLootState LootStatus => _data.rewards?.status.currentstate ?? GoonLootState.Invalid;

	public GoonAdvisor(PlayerAI manager, NPCDefinition def)
		: base(manager, def)
	{
		_def = Game.serv.globals.settings.npc.FindAdvisorConfig<GoonAdvisorConfig>(def.goon);
		_data = _manager.Data.goon;
		_data.PickGoonType(manager.PlayerInfo);
		_data.rewards = (_data.IsSpecial ? (_data.rewards ?? new GoonLootTableRewards(_pid)) : null);
	}

	public override void OnTurnUpdate()
	{
		_data.rewards?.CheckRelationshipRewards();
		Label label = RollForScript();
		_data.harassTypePlanned = (GOON_HARRASS_TYPES.Contains(label) ? label : Label.NULL);
		Predicate<Entity> test = ((_data.harassTypePlanned == ScriptNames.BURGLE) ? ((Predicate<Entity>)((Entity building) => BuildingHasInventoryToSteal(building))) : ((Predicate<Entity>)((Entity building) => true)));
		_data.businessToHarass = FindTarget(_manager.PlayerInfo.territory.GetHeadquartersNode(), test);
	}

	public override void ProduceRequests(List<AdvisorRequest> results)
	{
		results.AddIfNotNull(ProduceSingleRequest());
	}

	private AdvisorRequest ProduceSingleRequest()
	{
		AdvisorRequest advisorRequest = null;
		bool flag = _data.businessToHarass.IsValid && _data.harassTypePlanned.IsSet && CanHarass();
		if (advisorRequest == null && flag)
		{
			EntityID andClear = _data.GetAndClear(ref _data.businessToHarass);
			Entity entity = BuildingUtil.FindOwnerForAnyBuilding(andClear);
			if (entity != null)
			{
				AILog.LogAIDecision(_pid, this, $"Sending goon to {_data.harassTypePlanned} at {BuildingUtil.FindBuildingName(andClear)}");
				advisorRequest = new AdvisorRequest(this, _data.GetAndClear(ref _data.harassTypePlanned), AdvisorRequest.Priority.HarassBusiness, new Deictics
				{
					targetBuilding = andClear,
					targetPeep = entity.Id
				});
			}
		}
		return advisorRequest;
	}

	private bool CanHarass()
	{
		Demand demand = Game.ctx.simman.demands.FindOrNull(Game.ctx.players.Human.PID, _pid);
		if (demand != null && demand.IsStateCompliant && demand.HasDemandSet(Demand.Type.StopHarassing))
		{
			return false;
		}
		return true;
	}

	public GoonAdvisorConfig GetPersonality()
	{
		return _def;
	}

	public void RecordHarassment(EntityID buildingId, string script)
	{
		Entity entity = BuildingUtil.FindOwnerForAnyBuilding(buildingId);
		if (entity != null)
		{
			_data.RecordHarassment(entity.Id, buildingId, script);
		}
	}

	internal bool IsSpecial()
	{
		return _data.IsSpecial;
	}

	private Label RollForScript()
	{
		List<GoonAdvisorConfig.ScriptWeightPair> scriptWeights = _def.scriptWeights;
		float num = _data.rng.GenerateFloat();
		foreach (GoonAdvisorConfig.ScriptWeightPair item in scriptWeights)
		{
			num -= item.weight;
			if (num <= 0f)
			{
				return item.id;
			}
		}
		return Label.NULL;
	}

	private static bool BuildingHasInventoryToSteal(Entity building)
	{
		InventoryModule inventory = ModulesUtil.GetInventory(building);
		if (inventory != null)
		{
			return inventory.CalculateUsedCapacity().cubicfeet > 0;
		}
		return false;
	}

	private EntityID FindTarget(Node start, Predicate<Entity> test)
	{
		return (FindTargetIfAggro(start, test) ?? FindTargetDefault(start, test))?.Id ?? EntityID.INVALID;
	}

	private Entity FindTargetIfAggro(Node start, Predicate<Entity> test)
	{
		if (!_player.ai.combat.IsAggroOnAnybody())
		{
			return null;
		}
		return FindTargetWithBFS(start, (Entity e) => IsAggroOnDeliveriesTo(e) && test(e));
	}

	private Entity FindTargetDefault(Node start, Predicate<Entity> test)
	{
		return FindTargetWithBFS(start, test);
	}

	private Entity FindTargetWithBFS(Node start, Predicate<Entity> test)
	{
		float maxdistance = _def.targetRange;
		ListPool<Entity>.PooledBlockList buildings = ListPool<Entity>.Allocate();
		try
		{
			Game.ctx.board.nodes.VisitNeighborhoodBFS(start, 256, delegate(Node node)
			{
				buildings.AddIfNotNull(FindBuildingAtNode(node, test));
			}, (Node node) => buildings.Count < 3 && (start.pos - node.pos).Magnitude <= maxdistance, null, null, onlyBizNodes: true);
			return _data.rng.PickElementOrDefault(buildings);
		}
		finally
		{
			if (buildings != null)
			{
				((IDisposable)buildings).Dispose();
			}
		}
	}

	private bool IsAggroOnDeliveriesTo(Entity building)
	{
		PlayerID pid = building?.components.delivery?.HasRecentDelivery() ?? PlayerID.INVALID;
		if (pid.IsAnyPlayer)
		{
			return _player.ai.combat.IsAttackAllowed(pid);
		}
		return false;
	}

	private Entity FindBuildingAtNode(Node node, Predicate<Entity> test)
	{
		if (IsNodeSafeFromTargetting(node))
		{
			return null;
		}
		using (ListPool<Entity>.PooledBlockList pooledBlockList = ListPool<Entity>.Allocate())
		{
			node.FindAllInterestingBuildings(pooledBlockList);
			_data.rng.Shuffle(pooledBlockList);
			foreach (Entity item in pooledBlockList)
			{
				if (item.data.building.business.IsValid && test(item))
				{
					return item;
				}
			}
		}
		return null;
	}

	public bool IsNodeSafeFromTargetting(Node node)
	{
		if (node == null)
		{
			return true;
		}
		if (node.owner.IsSet)
		{
			if (node.owner.pid == _pid)
			{
				return true;
			}
			if (TooEarlyInTheGame())
			{
				return true;
			}
			if (IsPlayerFriendly(node.owner.pid, node))
			{
				return true;
			}
		}
		foreach (EntityID item in node.interesting)
		{
			PlayerID other = item.FindEntity()?.components?.delivery?.HasRecentDelivery() ?? PlayerID.INVALID;
			if (other.IsAnyPlayer && IsPlayerFriendly(other, node))
			{
				return true;
			}
		}
		return false;
		bool TooEarlyInTheGame()
		{
			Fixnum days = _def.nonHostileDayz.Evaluate(new ModQuery(_pid));
			Fixnum fixnum = Game.ctx.clock.DaysToTurns(days);
			return Game.ctx.clock.CurrentTurn < fixnum;
		}
	}

	public bool IsPlayerFriendly(PlayerID other, Node node)
	{
		if (other == _pid)
		{
			return true;
		}
		if (_player.ai.combat.IsAttackAllowed(other))
		{
			return false;
		}
		if (_player.ai.combat.IsAggroAndTruce(other))
		{
			return true;
		}
		Relationship relationshipFromPlayerTo = _player.social.GetRelationshipFromPlayerTo(other);
		if (relationshipFromPlayerTo != null)
		{
			Fixnum fixnum = _def.friendlyAt.Evaluate(new ModQuery(_pid, _player.social.PlayerPeepId, node.id));
			if (relationshipFromPlayerTo.Evaluate().current >= fixnum)
			{
				return true;
			}
		}
		return false;
	}

	public bool CanPlayerAskUsToStopHarassing(PlayerID other)
	{
		Demand demand = Game.ctx.simman.demands.FindOrNull(other, _pid);
		if (demand != null && demand.HasDemandSet(Demand.Type.StopHarassing))
		{
			return false;
		}
		return _data.DidHarassAnyone();
	}

	public override void OnCompliance(Demand demand)
	{
		if (demand.HasDemandSet(Demand.Type.StopHarassing))
		{
			ClearBizSocialHistoriesOfHarassment();
		}
	}

	private void ClearBizSocialHistoriesOfHarassment()
	{
		foreach (GoonAdvisorData.HarassmentRecord item in _data.CopyAndClearAllHarassments())
		{
			_player.social.RemoveAllSocialActionsOn(item.owner);
		}
	}
}
