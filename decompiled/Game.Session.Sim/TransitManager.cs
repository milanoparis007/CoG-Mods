using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Services;
using Game.Session.Actions;
using Game.Session.Board;
using Game.Session.Entities;
using SomaSim.SION;
using SomaSim.Util;
using UnityEngine;

namespace Game.Session.Sim;

public sealed class TransitManager : AbstractSessionManager, ISaveLoadProvider, IAnimatingManager, ISessionManager
{
	public TransitManagerPersistedData data = new TransitManagerPersistedData();

	private EntityManager _entityman;

	private BoardManager _board;

	private float _updateAmbientsDelaySeconds = 1f;

	private float _nextAmbientUpdateTime;

	public static readonly Label AMBIENT_CAR = (Label)"vehicle-ambient-truck";

	public override void OnInitializeDone()
	{
		_entityman = Game.ctx.entityman;
		_board = Game.ctx.board;
		Game.ctx.simman.peoplegen.OnBeforePersonDeath.Add(OnPersonDeath);
		Game.ctx.console.Add(this, new DebugConsoleEntry("visual", "test-cars", CheatSpawnTestCars));
	}

	public override void OnPreInteractive()
	{
		if (Game.ctx.HasSaveFile)
		{
			return;
		}
		EntityConfig configs = _entityman.FindTemplate(EntityConstants.CORNER_TMPL);
		foreach (Node item in _board.nodes.GetAllNodesUnsafe())
		{
			if (item.HasRoad)
			{
				_entityman.CreateByTemplate(configs).data.corner.nid = item.id;
			}
		}
		foreach (RailroadData railroad in data.railroads)
		{
			SpawnTrain(railroad);
		}
	}

	public override void OnReleased()
	{
		Game.ctx.console.Remove(this);
		Game.ctx.simman.peoplegen.OnBeforePersonDeath.Remove(OnPersonDeath);
		EntityID[] array = data.cars.ToArray();
		foreach (EntityID car in array)
		{
			DespawnCar(car, shutdown: true);
		}
		array = data.trainsByID.Keys.ToArray();
		foreach (EntityID train in array)
		{
			DespawnTrain(train, shutdown: true);
		}
		Entity[] array2 = Game.ctx.entityman.GetCachedEntitiesByTemplateUnsafe(EntityConstants.CORNER_TMPL).ToArray();
		foreach (Entity entity in array2)
		{
			_entityman.DestroyEntity(entity, shutdown: true);
		}
		_board = null;
	}

	public void UpdateAnimations(GameAnimUpdate anim)
	{
		if (anim.cumulativeSeconds > _nextAmbientUpdateTime)
		{
			_nextAmbientUpdateTime = anim.cumulativeSeconds + _updateAmbientsDelaySeconds;
			MaybeAddAmbientCar();
		}
	}

	private void OnPersonDeath(Entity agent)
	{
		ClearAgentNode(agent);
	}

	public void SetAgentAtNode(NodeID nodeId, EntityID peepId)
	{
		SetAgentAtNode(nodeId, peepId.FindEntity());
	}

	public void SetAgentAtNode(NodeID nodeId, Entity peep)
	{
		ClearAgentNode(peep);
		peep.components.agent.SetNodeID(nodeId);
		data.nodeToAgents.AddToList(nodeId, peep.Id);
	}

	private void ClearAgentNode(Entity peep)
	{
		NodeID nid = peep.data.agent.nid;
		if (nid.IsValid)
		{
			data.nodeToAgents.RemoveFromList(nid, peep.Id, removeEmptyList: false);
			peep.components.agent.SetNodeID(NodeID.INVALID);
		}
	}

	public List<EntityID> GetAllAgentsAtNodeUnsafe(NodeID nodeId)
	{
		return data.nodeToAgents.FindOrMakeList(nodeId);
	}

	private int DebugCountAgentsAtNode(NodeID nodeId, EntityID agentId)
	{
		return GetAllAgentsAtNodeUnsafe(nodeId).CountFast(agentId);
	}

	public void AddTransitFlagsAt(Node node, TransitFlags transit)
	{
		node.transit |= transit;
	}

	public void AddTransitFlagsAlong(NodeEdge edge, TransitFlags transit)
	{
		edge.transit |= transit;
		AddTransitFlagsAt(_board.nodes.GetNode(edge.a), transit);
		AddTransitFlagsAt(_board.nodes.GetNode(edge.b), transit);
	}

	public void ClearTransitFlagsAlong(NodeEdge edge, TransitFlags transit)
	{
		edge.transit &= ~transit;
		_board.nodes.GetNode(edge.a).transit &= ~transit;
		_board.nodes.GetNode(edge.b).transit &= ~transit;
	}

	private (WorldPos pos, float deg) WobblePosition(WorldPos pos)
	{
		return (pos: pos.Increment(data.rng.Generate(-0.5f, 0.5f), data.rng.Generate(-0.5f, 0.5f)), deg: data.rng.Generate(-30f, 30f));
	}

	public Entity SpawnCarPossiblyHidden(PlayerID pid, Label template, WorldPos pos, bool forceReveal = false)
	{
		(WorldPos pos, float deg) tuple = WobblePosition(pos);
		WorldPos item = tuple.pos;
		float item2 = tuple.deg;
		Entity entity = Game.ctx.board.CreateEntity(template, item, item2);
		entity.data.mobile.pid = pid;
		data.cars.Add(entity.Id);
		if (forceReveal)
		{
			Game.ctx.models.RevealModelIfHidden(entity);
		}
		return entity;
	}

	public void DespawnCar(EntityID car, bool shutdown)
	{
		data.cars.SwapRemove(car);
		Game.ctx.board.DestroyEntity(car.FindEntity(), shutdown);
	}

	public bool TeleportCarToNode(Entity car, NodeID nodeId)
	{
		WorldPos worldpos = car.data.mobile.worldpos;
		WorldPos pos = nodeId.FindNode().pos;
		car.components.mobile.Move(worldpos, WobblePosition(pos).pos);
		return true;
	}

	public void FindDrivingPath(PlayerID pid, Entity agent, WorldPos target, Action<Pathfinding.Result> callback)
	{
		WorldPos pos = agent.components.agent.GetNode().pos;
		FindDrivingPath(pid, agent.Id, pos, target, callback);
	}

	private void FindDrivingPath(PlayerID pid, EntityID agentId, WorldPos source, WorldPos target, Action<Pathfinding.Result> callback)
	{
		RoadPathContext ctx = new RoadPathContext(_board);
		Game.ctx.board.GetGridPath(pid, agentId, source, target, ctx, callback);
	}

	public void DriveOnPath(PlayerID pid, Entity car, PathData path)
	{
		car.components.script.queue.Run(new GameScript(GameScriptType.CarNavigation, new ActionNavigate(pid, path.world, path.fuzznode.FindNode())));
	}

	public void DestroyTransit(Entity entity)
	{
		if (_board != null)
		{
			switch (entity.config.mobile.type)
			{
			case AvatarType.Train:
				KillAndRespawnTrain(entity.Id);
				break;
			case AvatarType.Car:
			case AvatarType.Truck:
				DespawnCar(entity.Id, shutdown: false);
				break;
			}
		}
	}

	private void KillAndRespawnTrain(EntityID train)
	{
		TrainData trainData = data.trainsByID.FindOrNull(train);
		DespawnTrain(train, shutdown: false);
		if (trainData != null)
		{
			SpawnTrain(GetRailroad(trainData.railroadId));
		}
	}

	private void MaybeAddAmbientCar()
	{
		if (CanSpawnAmbientCar())
		{
			var (flag, start, mid, end) = FindNodesForAmbientCar();
			if (flag)
			{
				SpawnAmbientCar(start, mid, end);
			}
		}
	}

	private bool CanSpawnAmbientCar()
	{
		if (!Game.serv.saveload.prefs.game.trafficEnabled)
		{
			return false;
		}
		if (Game.ctx.seasons.FindDisplayedTimeOfDay() < 6f)
		{
			return false;
		}
		if (!data.rng.CheckProbability(0.2f))
		{
			return false;
		}
		int num = data.cars.Where((EntityID eid) => eid.FindEntity().config.Template == AMBIENT_CAR).Count();
		if ((float)num >= 20f)
		{
			return false;
		}
		double num2 = Math.Ceiling((float)Game.ctx.players.Human.territory.GetAllOwnedNodesUnsafe().Count / 3f);
		if ((double)num >= num2)
		{
			return false;
		}
		return true;
	}

	private bool IsDesirableToVisit(Node n)
	{
		if (n.contained.Count > 0 && !n.HasActiveRaid)
		{
			return GetAllAgentsAtNodeUnsafe(n.id).Count == 0;
		}
		return false;
	}

	private (bool valid, Node start, Node mid, Node end) FindNodesForAmbientCar()
	{
		Node start = null;
		Node mid = null;
		Node end = null;
		return (valid: FindNode(), start: start, mid: mid, end: end);
		bool FindNode()
		{
			List<Node> list = (from n in Game.ctx.players.Human.territory.GetAllKnownNodesExpensive()
				where n.HasRoad
				select n).ToList();
			if (list.Count <= 1)
			{
				return false;
			}
			List<float> weights = list.Select((Node n) => (!IsDesirableToVisit(n)) ? 1f : 10000f).ToList();
			mid = data.rng.PickElement(list, weights);
			if (mid == null)
			{
				return false;
			}
			List<Node> list2 = new List<Node>();
			List<Node> list3 = new List<Node>();
			foreach (Node item in list)
			{
				item.FindRoadNeighbors(list3);
				foreach (Node item2 in list3)
				{
					if (item2.HasRoad && !item2.known.Get(PlayerID.HumanPlayer))
					{
						list2.Add(item2);
					}
				}
			}
			start = data.rng.PickElementOrDefault(list2);
			end = data.rng.PickElementOrDefault(list2);
			if (start == null)
			{
				return false;
			}
			if (end == null)
			{
				end = start;
			}
			return true;
		}
	}

	private Entity SpawnAmbientCar(Node start, Node mid, Node end)
	{
		Entity entity = SpawnCarPossiblyHidden(PlayerID.System, AMBIENT_CAR, start.pos, forceReveal: true);
		PathData firstPath = new PathData();
		FindDrivingPath(PlayerID.System, EntityID.INVALID, start.pos, mid.pos, delegate(Pathfinding.Result result)
		{
			result.PopulatePath(firstPath, start.pos, Fixnum.MAX_VALUE);
		});
		PathData secondPath = new PathData();
		FindDrivingPath(PlayerID.System, EntityID.INVALID, mid.pos, end.pos, delegate(Pathfinding.Result result)
		{
			result.PopulatePath(secondPath, mid.pos, Fixnum.MAX_VALUE);
		});
		List<GameAction> list = new List<GameAction>();
		if (firstPath.nodes.Count > 1)
		{
			list.AddRange(new ActionPause(1f), new ActionNavigate(PlayerID.System, firstPath.world));
		}
		if (secondPath.nodes.Count > 1)
		{
			list.AddRange(new ActionPause(4f), new ActionNavigate(PlayerID.System, secondPath.world));
		}
		list.Add(new ActionDestroySelf());
		GameScript script = new GameScript(GameScriptType.CarNavigation, list);
		entity.components.script.queue.Run(script);
		return entity;
	}

	private Entity SpawnTrain(RailroadData railroad)
	{
		Node node = railroad.ExitNode.FindNode();
		Entity entity = Game.ctx.board.CreateEntityAtStartup(EntityConstants.TRAIN, node.pos);
		List<WorldPos> item = _board.NodeToWorldPath(railroad.nodes).result;
		GameScript script = new GameScript(GameScriptType.TrainNavigation, new ActionNavigate(PlayerID.System, item, null, reversed: true), new ActionDestroySelf());
		entity.components.script.queue.Run(script);
		data.trainsByID.Add(entity.Id, new TrainData
		{
			railroadId = railroad.id
		});
		return entity;
	}

	private void DespawnTrain(EntityID train, bool shutdown)
	{
		if (!data.trainsByID.Remove(train))
		{
			EntityID entityID = train;
			Logger.Warning("Invalid train removal, entity " + entityID.ToString());
		}
		Game.ctx.board.DestroyEntity(train.FindEntity(), shutdown);
	}

	public void AddRailroad(List<PathElementResult> track)
	{
		foreach (PathElementResult item in track)
		{
			AddTransitFlagsAlong(item.edge, TransitFlags.Rail);
		}
		Node source = track[0].source;
		AddTransitFlagsAt(source, TransitFlags.Terminal);
		data.railroads.Add(RailroadData.FromNodeList(source.id.index, track));
	}

	public RailroadData GetRailroad(int id)
	{
		return data.railroads.Find((RailroadData rrdata) => rrdata.id == id);
	}

	private string CheatSpawnTestCars(string[] args)
	{
		if (args.Length != 3)
		{
			return DebugConsoleEntry.InvalidParam("Invalid parameters", args, 2, "<count>");
		}
		if (!int.TryParse(args[2], out var count))
		{
			return DebugConsoleEntry.InvalidParam("Invalid count", args, 2, "<count>");
		}
		Game.serv.sequencer.StartCoroutine(CarSpawner());
		return "Spawning " + count + " test cars!";
		IEnumerator CarSpawner()
		{
			for (int i = 0; i < count; i++)
			{
				SpawnTestCar();
				yield return new WaitForSecondsRealtime(1f);
			}
		}
		Entity SpawnTestCar()
		{
			var (flag, start, mid, end) = FindNodesForAmbientCar();
			if (!flag)
			{
				return null;
			}
			return SpawnAmbientCar(start, mid, end);
		}
	}

	public void Save(Serializer s, ConcurrentSaveTable results)
	{
		results.Set("data", s.Serialize(data));
	}

	public IEnumerator Load(Hashtable data)
	{
		SaveLoadUtils.DeserializeSingleKey(data, "data", delegate(TransitManagerPersistedData result)
		{
			this.data = result;
		});
		yield break;
	}
}
