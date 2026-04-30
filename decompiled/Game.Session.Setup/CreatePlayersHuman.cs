using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Services;
using Game.Session.Board;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Player;
using Game.Session.Player.Commands;
using Game.Session.Sim;
using Game.Session.Tutorial;
using SomaSim.Util;
using UnityEngine;

namespace Game.Session.Setup;

internal class CreatePlayersHuman : CreatePlayers
{
	private struct Buyer
	{
		public bool valid;

		public Entity building;

		public Entity owner;

		public float distance;
	}

	private struct Pair
	{
		public Node ply1;

		public Node ply2;
	}

	private const bool DEBUG = false;

	private PlayerStartData _start;

	private PlayerInfo _player;

	private CreatePlayersHumanHelper.Results _results;

	public CreatePlayersHuman(SetupOrchestratorContext ctx)
		: base(ctx)
	{
	}

	internal void RunBlocking()
	{
		_player = Game.ctx.players.Human;
		_results = new CreatePlayersHumanHelper(_player.PID).CreatePlayerPeepAndFrancine();
		_start = GetStartingArea(_ctx.playerSetup, _player, _results.player);
		CreateCrew();
		CreatePlayers.CreateSafehouseOrBusiness(_player, _start, _results.francine);
		CreatePlayers.ExploreNodesAroundSafehouse(_player, _start.node);
		if (_results.details.tutorial)
		{
			RunTutorialFixups();
		}
	}

	private void CreateCrew()
	{
		PutPlayerAtNode(_start.node);
	}

	private void PutPlayerAtNode(Node node)
	{
		Entity player = _results.player;
		Entity francine = _results.francine;
		CreatePlayers.AddStartupCrewAtNode(_player, node, player, isFirstCrewPeep: true);
		_player.social.ForceGroupNameIfValid(Game.ctx.scenario.newgamepars.playerdetails.player.group);
		_player.social.AddFamilyRelBuffAtStartup(player.Id, francine.Id);
		_player.social.AddFrancineRelBuff(player.Id, francine.Id);
		int startingTrucks = Game.serv.globals.settings.general.debug.startingTrucks;
		for (int i = 0; i < startingTrucks; i++)
		{
			_player.crew.CreateAndTrackVehicle(EntityConstants.VEHICLE_TRUCK, node.pos);
		}
		PlayerMeetings.RevealAllUnitsOfPlayer(_player.PID);
	}

	private void RunTutorialFixups()
	{
		TutorialSettings tutorial = Game.serv.globals.settings.general.tutorial;
		TutorialContext tutorialContext = new TutorialContext();
		tutorialContext.francineId = _results.francine.Id;
		tutorialContext.ziggyId = _results.ziggy.Id;
		Game.ctx.tutorial.SetContext(tutorialContext);
		Entity entity = _player.territory.Safehouse.FindEntity();
		entity.components.modules.RemoveFrontroomModules();
		entity.components.modules.InstallModuleManually(tutorial.safehouseFrontModule, Game.ctx.clock.Now);
		Game.ctx.events.EnqueueOnce(new SessionEvent(SessionEventType.BuildingConstructionStateChanged, entity.Id, _player.PID));
		entity.components.modules.inventory.data.ClearResources();
		BuildingAndBusinessData bbdata = BuildingUtil.FindDataForBuilding(entity);
		VisitState visit = new VisitState(_player.crew.GetCrewForPlayerPeep(), bbdata, Game.ctx.clock.Now, _player.PID);
		GrantContext ctx = new GrantContext
		{
			pid = _player.PID,
			visit = visit
		};
		tutorial.grants.ApplyAll(ctx);
		BuildingAndBusinessData bbd = FindBusinessForZiggy(tutorialContext);
		if (bbd.building != null)
		{
			Entity building = bbd.building;
			building.components.modules.RemoveBackroomModules();
			building.components.modules.InstallModuleManually(tutorial.ziggyBackModule, Game.ctx.clock.Now);
			AddZiggyAsOwner(_results.ziggy, bbd);
			var (entity2, bto) = FindFriendForZiggy(bbdata.building, _results.ziggy);
			if (entity2 != null)
			{
				tutorialContext.ziggyFriendID = entity2.Id;
				ClearPathTo(bto);
			}
			else
			{
				Logger.Error("Can't find a friend for Ziggy who would buy tutorial beer?!");
			}
			Game.ctx.events.EnqueueOnce(new SessionEvent(SessionEventType.BuildingConstructionStateChanged, building.Id, _player.PID));
		}
		else
		{
			Logger.Error("Failed to find a valid building for ziggy?!");
		}
		if (tutorialContext.Francine != null && tutorialContext.Ziggy != null)
		{
			Game.ctx.players.Human.social.GetPlayerPeep().data.person.traitIds = new TagList
			{
				new Label("trait-friendly"),
				new Label("trait-organized"),
				new Label("trait-hardworking")
			};
			tutorialContext.Francine.data.person.traitIds = new TagList
			{
				new Label("trait-friendly"),
				new Label("trait-talkative"),
				new Label("trait-cautious")
			};
			tutorialContext.Ziggy.data.person.traitIds = new TagList
			{
				new Label("trait-talkative"),
				new Label("trait-hardworking"),
				new Label("trait-ugly")
			};
		}
	}

	private void ClearPathTo(Entity bto)
	{
		PlayerInfo human = Game.ctx.players.Human;
		Node node = bto.components.board.GetNode();
		foreach (Node item in (from n in CommandGoto.MakePath(human.PID, human.social.GetPlayerPeep(), node, Fixnum.MAX_VALUE).nodes
			select n.node into n
			where !human.meetings.IsNodeKnown(n)
			select n).ToList())
		{
			human.meetings.MarkNodeAsKnown(item, expectedSeen: true, instant: true);
		}
	}

	private void AddZiggyAsOwner(Entity ziggy, BuildingAndBusinessData bbd)
	{
		Entity biz = bbd.biz;
		biz.components.biz.ClearOwner(shutdown: false);
		biz.components.biz.AssignRealOwner(ziggy);
		Node node = bbd.building.components.board.GetNode();
		if (!_player.meetings.IsNodeKnown(node))
		{
			_player.meetings.MarkNodeAsKnown(node, expectedSeen: true, instant: true);
		}
		if (!_player.territory.IsScoped(bbd.building))
		{
			_player.territory.ScopeOutBuilding(bbd.building, procgen: false, setControlled: false);
		}
		_player.social.AddFamilyRelBuffAtStartup(_player.social.PlayerPeepId, ziggy.Id);
		Relationship item = _player.social.FindOrMakeRelationshipsWith(ziggy.Id).from;
		item.AddBuff(BuffConstants.STARTING_BUFF_OLDFRIENDS, _player.social.PlayerPeepId);
		item.GrantFreebieTickets(1);
	}

	private (Entity friend, Entity friendBuilding) FindFriendForZiggy(Entity ziggyBuilding, Entity ziggy)
	{
		Resource beer = Game.ctx.tutorial.TutorialResource;
		TutorialSettings tutorial = Game.serv.globals.settings.general.tutorial;
		EntityID playerPeepId = _player.social.PlayerPeepId;
		Buyer buyer = (from b in (from bldg in Game.ctx.entityman.GetCachedEntitiesBuildingsUnsafe()
				select MakeBuyer(bldg) into buyer2
				where buyer2.valid
				select buyer2).ToList()
			orderby b.distance
			select b).ToList().FirstOrDefault((Buyer b) => Game.ctx.simman.rels.GetOrNull(playerPeepId, b.owner.Id) == null);
		Entity owner = buyer.owner;
		if (owner == null)
		{
			return (friend: null, friendBuilding: null);
		}
		(Relationship toTarget, Relationship fromTarget) orMakeSymmetrical = Game.ctx.simman.rels.GetOrMakeSymmetrical(owner.Id, ziggy.Id, RelationshipType.Acquaintance, warnOnExisting: false);
		var (relationship, _) = orMakeSymmetrical;
		orMakeSymmetrical.fromTarget.AddBuff(BuffConstants.BUFF_ON_SCOPEOUT_NPC, EntityID.INVALID);
		relationship.AddBuff(BuffConstants.BUFF_ON_SCOPEOUT_NPC, EntityID.INVALID);
		Entity building = buyer.building;
		building.components.modules.RemoveBackroomModules();
		building.components.modules.InstallModuleManually(tutorial.ziggyFriendBackModule, Game.ctx.clock.Now);
		Game.serv.debugvis.AddCubeIf(pred: false, building.data.board.worldpos, Color.cyan, 3f);
		RemoveOtherBusinessesOnTheCorner(building);
		return (friend: owner, friendBuilding: building);
		bool DoesBuyBeer(Entity bldg)
		{
			return (bldg.components.modules?.ProduceAllItemsPlayerCanBuyOrSell(PlayerID.HumanPlayer, playerBuys: false, playerSells: true))?.Any((BuySellElement e) => e.item.id == beer.resid) ?? false;
		}
		Buyer MakeBuyer(Entity bldg)
		{
			bool num = DoesBuyBeer(bldg);
			Entity entity = (num ? BuildingUtil.FindOwnerOrManagerForAnyBuilding(bldg) : null);
			bool valid = num && entity != null;
			return new Buyer
			{
				valid = valid,
				building = bldg,
				owner = entity,
				distance = (bldg.data.board.worldpos - ziggyBuilding.data.board.worldpos).Magnitude
			};
		}
	}

	private void RemoveOtherBusinessesOnTheCorner(Entity building)
	{
		BusinessTracker businesses = Game.ctx.simman.businesses;
		foreach (EntityID item in new List<EntityID>(building.components.board.GetNode().interesting))
		{
			if (!(item == building.Id))
			{
				BuildingAndBusinessData buildingAndBusinessData = BuildingUtil.FindDataForBuilding(item);
				if (buildingAndBusinessData.building != null && buildingAndBusinessData.biz != null)
				{
					businesses.ClearOwner(buildingAndBusinessData.biz, shutdown: false);
					businesses.DetachBusinessFromBuilding(buildingAndBusinessData.biz, buildingAndBusinessData.building, shutdown: false);
					businesses.DestroyBusinessUnattached(buildingAndBusinessData.biz, shutdown: false);
					Game.ctx.board.ClearInterestingAndPotential(buildingAndBusinessData.building, shutdown: false);
				}
			}
		}
	}

	private BuildingAndBusinessData FindBusinessForZiggy(TutorialContext context)
	{
		Node safehouseNode = _player.territory.GetHeadquartersNode();
		BuildingAndBusinessData buildingAndBusinessData = FindRandomBusiness(safehouseNode);
		if (buildingAndBusinessData.owner != null)
		{
			Relationship item = _player.social.FindOrMakeRelationshipsWith(buildingAndBusinessData.owner.Id).from;
			item.AddBuff(BuffConstants.STARTING_BUFF_OLDFRIENDS, _player.social.PlayerPeepId);
			item.GrantFreebieTickets(1);
		}
		else
		{
			Logger.Error("There's no other business on this node - find a different tutorial seed!");
		}
		Game.ctx.simman.rels.GetOrMakeSymmetrical(buildingAndBusinessData.owner.Id, context.francineId, RelationshipType.Acquaintance, warnOnExisting: false);
		List<Node> ply1Nodes = (from p1 in safehouseNode.FindRoadNeighbors()
			where p1 != null
			where p1.interesting.Count > 0
			select p1).ToList();
		foreach (Pair item2 in (from pair in ply1Nodes.SelectMany((Node p1) => from ply in p1.FindRoadNeighbors()
				select new Pair
				{
					ply1 = p1,
					ply2 = ply
				})
			where pair.ply2 != null && pair.ply2 != safehouseNode && pair.ply2.interesting.Count > 0 && !ply1Nodes.Contains(pair.ply2)
			select pair).ToList())
		{
			BuildingAndBusinessData result = FindRandomBusiness(item2.ply2);
			if (result.building != null)
			{
				Entity building = FindRandomBusiness(item2.ply1).building;
				if (building != null)
				{
					context.ziggyBuildingId = result.building.Id;
					context.neighborToVisitId = building.Id;
					context.sameNodeBizToVisitId = buildingAndBusinessData.building.Id;
					Game.serv.debugvis.AddCubeIf(pred: false, result.building.data.board.worldpos, Color.green, 3f);
					return result;
				}
			}
		}
		return BuildingAndBusinessData.NONE;
	}

	private BuildingAndBusinessData FindRandomBusiness(Node node)
	{
		return FindFirstBusiness(node, (BuildingAndBusinessData bbd) => bbd.biz != null && bbd.owner != null && bbd.ownerinfo.IsReal && !bbd.building.components.building.IsControlledByAnyPlayer());
	}

	private BuildingAndBusinessData FindFirstBusiness(Node node, Predicate<BuildingAndBusinessData> test)
	{
		using (ListPool<Entity>.PooledBlockList pooledBlockList = ListPool<Entity>.Allocate())
		{
			node.FindBuildingsForInteraction(_player.PID, pooledBlockList);
			foreach (Entity item in pooledBlockList)
			{
				BuildingAndBusinessData buildingAndBusinessData = BuildingUtil.FindDataForBuilding(item);
				if (test(buildingAndBusinessData))
				{
					return buildingAndBusinessData;
				}
			}
		}
		return BuildingAndBusinessData.NONE;
	}
}
