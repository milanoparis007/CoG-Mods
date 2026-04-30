using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Services.Maps;
using Game.Session.Board;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Player;
using Game.Session.Sim;
using Game.Session.Sim.Modules;
using SomaSim.Util;

namespace Game.Session.Setup;

internal abstract class CreatePlayers
{
	protected SetupOrchestratorContext _ctx;

	protected List<EntityConfig> _frontConfigs;

	protected CreatePlayers(SetupOrchestratorContext ctx)
	{
		_ctx = ctx;
		if (_ctx.playerSetup == null)
		{
			_ctx.playerSetup = new PlayerSetup();
		}
		_frontConfigs = MakeValidStartingBusinesses();
	}

	protected static IRandom MakeRNG(PlayerID pid)
	{
		return Game.ctx.scenario.MakeSeededRng(pid);
	}

	private static List<EntityConfig> MakeValidStartingBusinesses()
	{
		VisitState visit = VisitState.MakeForBuilding(null);
		return (from name in Game.serv.globals.settings.people.businessSettings.startingBusinesses
			select Game.ctx.entityman.FindTemplate(name) into config
			where CheckReqs(config.biz.reqsToInstall)
			select config).ToList();
		bool CheckReqs(VisitRequirementList reqs)
		{
			return reqs?.AllPass(visit) ?? true;
		}
	}

	protected PlayerStartData GetStartingArea(PlayerSetup setup, PlayerInfo player, Entity peep)
	{
		using (ListPool<EntityConfig>.PooledBlockList pooledBlockList = ListPool<EntityConfig>.Allocate())
		{
			pooledBlockList.AddRange(_frontConfigs);
			MakeRNG(player.PID).Shuffle(pooledBlockList);
			foreach (EntityConfig item in pooledBlockList)
			{
				EntityConfig frontConfig = (player.OfInterest ? item : null);
				PlayerSetup.SetupCtx ctx = new PlayerSetup.SetupCtx(player.PID, frontConfig, !player.IsCopOrFed);
				var (flag, result) = setup.ScoreAndRemoveBestArea(player, peep, frontConfig, ctx);
				if (flag)
				{
					return result;
				}
			}
		}
		return null;
	}

	internal static bool DoesNodeHaveBuildingToReplace(Node node, PlayerSetup.SetupCtx ctx)
	{
		return node.contained.Any((EntityID eid) => IsCandidateForReplacement(eid, node, ctx));
	}

	internal static EntityID FindReplacementBuilding(Node node, PlayerSetup.SetupCtx ctx)
	{
		return node.contained.Find((EntityID eid) => IsCandidateForReplacement(eid, node, ctx));
	}

	private static bool IsCandidateForReplacement(EntityID buildingId, Node node, PlayerSetup.SetupCtx ctx)
	{
		if (!node.interesting.Contains(buildingId) && !node.potential.Equals(buildingId))
		{
			return DoesBuildingMatchType(buildingId, ctx);
		}
		return false;
	}

	private static bool DoesBuildingMatchType(EntityID buildingId, PlayerSetup.SetupCtx ctx)
	{
		Entity entity = buildingId.FindEntity();
		if (ctx.skipCopBldgs && entity.config.police != null)
		{
			return false;
		}
		if (entity.config.building.type == ZoneType.Unknown)
		{
			return false;
		}
		if (ctx.reqsToAttachToBuilding != null)
		{
			BuildingAndBusinessData bbdata = BuildingUtil.FindDataForBuilding(buildingId);
			VisitState visit = new VisitState(CrewAssignment.EMPTY, bbdata, Game.ctx.clock.Now, ctx.pid);
			if (!ctx.reqsToAttachToBuilding.AllPass(visit))
			{
				return false;
			}
		}
		if (!(entity.config.Template == ctx.movesInto))
		{
			return ctx.movesInto.IsNotSet;
		}
		return true;
	}

	protected static Entity CreateSafehouseOrBusiness(PlayerInfo player, PlayerStartData start, Entity bizOwner)
	{
		Node node = start.node;
		if (node == null)
		{
			Logger.Warning("Malformed start data");
			return null;
		}
		Entity entity = FindSafehouse(player, start, node, bizOwner);
		if (entity == null)
		{
			Logger.Warning("Failed to find the replacement business?!", player.PID);
			return null;
		}
		if (entity.components.building.IsControlledByAnyPlayer())
		{
			Logger.Warning("Creating safehouse for " + player.PID.ToString() + " - node " + node?.ToString() + " already controlled by player " + entity.components.building.GetControllingPlayer().ToString());
			return null;
		}
		player.territory.SafehouseData.SetSafehouse(entity);
		player.territory.ScopeOutAndMeetOwner(entity, procgen: true, player.social.PlayerPeepId, setControlled: true);
		FixUpBackModuleAndInventory(player, start, entity);
		GrantStarterPacks(player, entity);
		return entity;
	}

	private static void FixUpBackModuleAndInventory(PlayerInfo player, PlayerStartData start, Entity building)
	{
		ModulesComponent modules = building.components.modules;
		if (start.InstallSafeHouseInBusiness && player.IsHuman)
		{
			modules.RemoveBackroomModules();
		}
		if (player.IsHuman)
		{
			modules.ApplyInstallGrantsAtStartup();
		}
		if (player.IsGangOrGoon && ModulesUtil.GetInventory(building) == null)
		{
			modules?.InstallSmallInventory();
		}
		if (player.HasBusinessAdvisor)
		{
			building.components.modules.RemoveBackroomModules();
		}
	}

	private static void GrantStarterPacks(PlayerInfo player, Entity building)
	{
		if (!player.IsHuman && !player.IsJustGang)
		{
			return;
		}
		if (player.crew.LivingCrewCount == 0)
		{
			Logger.Warning("Missing crew for player", player, "can't grant starter pack");
			return;
		}
		Entity entity = BuildingUtil.FindBizForBuilding(building);
		if (entity == null)
		{
			Logger.Warning("No starter biz for player", player, "can't grant starter pack");
			return;
		}
		bool flag = player.IsHuman && Game.serv.globals.settings.general.debug.powerfulGuns;
		BizComponent biz = entity.components.biz;
		biz.GrantStarterPackToThisBuilding(player);
		foreach (CrewAssignment item in player.crew.AllCrew)
		{
			biz.GrantStarterPackToCrew(player, item);
			if (flag)
			{
				ModulesUtil.GetInventory(item.GetVehicle()).data.Increment((Label)"weapon-thompson", 1);
			}
		}
	}

	protected static Entity FindSafehouse(PlayerInfo player, PlayerStartData start, Node node, Entity bizOwner)
	{
		if (start.InstallSafeHouseAnywhere)
		{
			return PickAnySafehouseInNode(node);
		}
		PlayerSetup.SetupCtx ctx = new PlayerSetup.SetupCtx(player.PID, start.frontBiz, !player.IsCopOrFed);
		EntityID bid = FindReplacementBuilding(node, ctx);
		if (!bid.IsNotValid)
		{
			return PutSafehouseInBusiness(start, bid, node, bizOwner);
		}
		return null;
	}

	protected static Entity PickAnySafehouseInNode(Node node)
	{
		foreach (EntityID item in node.contained)
		{
			if (!node.interesting.Contains(item))
			{
				Entity entity = item.FindEntity();
				if (entity.data.police == null && !entity.components.building.IsControlledByAnyPlayer() && entity.config.building.type != ZoneType.Unknown)
				{
					return entity;
				}
			}
		}
		return null;
	}

	private static Entity PutSafehouseInBusiness(PlayerStartData start, EntityID bid, Node node, Entity newOwner)
	{
		BusinessTracker businesses = Game.ctx.simman.businesses;
		BuildingAndBusinessData buildingAndBusinessData = BuildingUtil.FindDataForBuilding(bid);
		businesses.ClearOwner(buildingAndBusinessData.biz, shutdown: false);
		businesses.DetachBusinessFromBuilding(buildingAndBusinessData.biz, buildingAndBusinessData.building, shutdown: false);
		businesses.DestroyBusinessUnattached(buildingAndBusinessData.biz, shutdown: false);
		Entity business = businesses.CreateBusinessUnattached(start.frontBiz);
		businesses.AttachBusinessToBuilding(business, buildingAndBusinessData.building);
		if (newOwner != null)
		{
			businesses.ForceAssignOwner(business, newOwner);
		}
		return buildingAndBusinessData.building;
	}

	public static void ExploreNodesAroundSafehouse(PlayerInfo player, Node startNode)
	{
		MapConfig.PlayerStartConfig playerStart = Game.ctx.session.mapconfig.playerStart;
		List<Node> nodes = MakeListOfNodesToScope(player, startNode, playerStart.nodesKnownAtStart);
		ExploreNodesAfterAddingSafehouse(player, nodes, playerStart.nodesFriendlyAtStart);
	}

	private static List<Node> MakeListOfNodesToScope(PlayerInfo player, Node startNode, int nodeCount)
	{
		if (player.IsJustGoon)
		{
			return new List<Node> { startNode };
		}
		HashSet<Node> todos = new HashSet<Node>();
		Game.ctx.board.nodes.VisitNeighborhoodBFS(startNode, nodeCount, delegate(Node node)
		{
			todos.Add(node);
		}, null, (NodeEdge edge) => edge.IsRoad);
		return todos.ToList();
	}

	private static void ExploreNodesAfterAddingSafehouse(PlayerInfo player, List<Node> nodes, int maxNodesToScope)
	{
		foreach (Node node in nodes)
		{
			player.meetings.MarkNodeAsKnown(node, expectedSeen: true, instant: true);
			using ListPool<Entity>.PooledBlockList pooledBlockList = ListPool<Entity>.Allocate();
			node.FindBuildingsToScopeOut(player.PID, pooledBlockList);
			if (pooledBlockList.Count <= 0 || maxNodesToScope <= 0)
			{
				continue;
			}
			maxNodesToScope--;
			foreach (Entity item in pooledBlockList)
			{
				player.territory.ScopeOutAndMeetOwner(item, procgen: true, player.social.PlayerPeepId, setControlled: false);
			}
		}
	}

	internal static void AddStartupCrewAtNode(PlayerInfo player, Node node, Entity crewPeep, bool isFirstCrewPeep)
	{
		player.crew.HireNewCrewInVehicle(node, crewPeep, null, isFirstCrewPeep);
		if (isFirstCrewPeep)
		{
			player.social.SetBossInfo(crewPeep);
			player.social.ExploreFamilyAtStartup(crewPeep);
		}
	}

	internal static Entity CheatGenerateCrewForPlayer(PlayerInfo player)
	{
		SplitMix64 rng = new SplitMix64((uint)player.PID.id);
		return new PlayerSetup().GetAndRemoveCandidatePeep(rng, player.IsJustGoon);
	}
}
