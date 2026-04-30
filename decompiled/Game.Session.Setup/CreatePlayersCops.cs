using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Services;
using Game.Session.Board;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Player;
using Game.Session.Player.AI;
using Game.Session.Sim;
using SomaSim.Util;

namespace Game.Session.Setup;

internal class CreatePlayersCops : CreatePlayers
{
	private static Dictionary<EntityID, Queue<Node>> _queues = new Dictionary<EntityID, Queue<Node>>();

	private static List<Node> _roadNeighbors = new List<Node>(4);

	public CreatePlayersCops(SetupOrchestratorContext ctx)
		: base(ctx)
	{
	}

	internal void RunBlocking()
	{
		List<PlayerInfo> list = Game.ctx.players.all.Where((PlayerInfo p) => p.IsJustCop).ToList();
		List<EntityID> stationIDs = Game.ctx.simman.cops.data.stationIDs;
		int num = 0;
		for (int count = stationIDs.Count; num < count; num++)
		{
			SetUpPolicePlayer(stationIDs[num], list[num]);
		}
	}

	private void SetUpPolicePlayer(EntityID stationID, PlayerInfo player)
	{
		Entity entity = stationID.FindEntity();
		entity.data.police.copPlayerId = player.PID;
		PrecinctAdvisor.GiveStationToAI(player.ai, stationID);
		player.territory.ScopeOutBuilding(entity, procgen: true, setControlled: true);
		new ModQuery(player.PID, stationID, entity.components.board.GetNodeID());
		int number = 1;
		PrecinctID precinct = entity.data.police.precinctID;
		List<Entity> candidates = FindCandidates();
		List<EntityID> list = AssignEntitiesAsOfficers(entity, candidates, number);
		Entity entity2 = list[0].FindEntity();
		player.crew.AddToCrewUnassigned(entity2, null, isBoss: true);
		player.social.SetBossInfo(entity2);
		foreach (EntityID item in list)
		{
			ulong id = item.id;
			if (!id.Equals(entity2.Id))
			{
				player.crew.AddToCrewUnassigned(item.FindEntity(), null, isBoss: false);
			}
		}
		EstablishBeats(entity);
		List<Entity> FindCandidates()
		{
			List<Entity> list2 = (from person in Game.ctx.simman.peoplegen.GetAllTrackedPeople()
				where EligibleOfficer(Game.ctx.clock.Now, person)
				where PersonInPrecinct(person, precinct)
				select person).ToList();
			if (list2.Count == 0)
			{
				list2 = (from person in Game.ctx.simman.peoplegen.GetAllTrackedPeople()
					where EligibleOfficer(Game.ctx.clock.Now, person)
					select person).ToList();
			}
			return list2;
		}
	}

	private List<EntityID> AssignEntitiesAsOfficers(Entity station, List<Entity> candidates, int number)
	{
		for (int i = 0; i < number; i++)
		{
			Entity entity = _ctx.playerSetup.rng.PickAndRemoveElement(candidates);
			station.components.police.AssignOfficerToStation(entity.Id);
			entity.data.person.SetTitle(Loc.Get("ui.name.title-cops"));
		}
		return station.data.police.officers;
	}

	public static bool EligibleOfficer(SimTime now, Entity person)
	{
		PersonData person2 = person.data.person;
		if (person2.IsAlive && person2.GetAge(now).YearsFloat >= 20f && person2.business.IsNotValid)
		{
			return person.data.agent.pid.id == 0;
		}
		return false;
	}

	private static bool PersonInPrecinct(Entity person, PrecinctID precinct)
	{
		int famId = person.data.person.famId;
		return FamilyToPrecinct(Game.ctx.simman.peoplegen.data.FindFamilyTree(famId)).Equals(precinct);
	}

	private static PrecinctID FamilyToPrecinct(FamilyTree tree)
	{
		WorldPos pos = tree.anchor.pos;
		Node node = Game.ctx.board.nodes.FindNearestNodeAround(pos, 10f);
		return (node ?? Game.ctx.board.nodes.FindNearestNodeAround(pos, 100f))?.precinctId ?? PrecinctID.INVALID;
	}

	private static void EstablishBeats(Entity station)
	{
		HashSet<Node> seen = new HashSet<Node>();
		List<Node> precinctNodes = new List<Node>();
		PrecinctID precinctId = station.data.police.precinctID;
		List<EntityID> officers = station.data.police.officers;
		int count = officers.Count;
		Game.ctx.board.nodes.VisitNeighborhoodBFS(station.data.board.bead.nodeId.FindNode(), int.MaxValue, delegate(Node node)
		{
			if (IsInteresting(node))
			{
				precinctNodes.Add(node);
			}
		}, (Node node) => node.precinctId == precinctId);
		List<List<Node>> list = new List<List<Node>>(count);
		for (int num = 0; num < count; num++)
		{
			list.Add(new List<Node>());
			_queues[officers[num]] = new Queue<Node>();
		}
		int num2 = 0;
		int num3 = precinctNodes.Count / count;
		for (int num4 = 0; num4 < count; num4++)
		{
			Node item = precinctNodes[num2];
			_queues[officers[num4]].Enqueue(item);
			num2 += num3;
		}
		bool flag;
		do
		{
			flag = false;
			for (int num5 = 0; num5 < count; num5++)
			{
				bool flag2 = ProcessBeatQueue(seen, list[num5], _queues[officers[num5]], precinctId);
				flag = flag || flag2;
			}
		}
		while (flag);
		for (int num6 = 0; num6 < count; num6++)
		{
			EntityID officer = officers[num6];
			List<NodeID> nodes = ResortBeatNodesByProximity(list[num6], station);
			station.components.police.AssignBeatToOfficer(officer, nodes);
		}
	}

	private static bool IsInteresting(Node node)
	{
		if (node != null)
		{
			return node.contained.Count > 0;
		}
		return false;
	}

	private static List<NodeID> ResortBeatNodesByProximity(List<Node> officerBeat, Entity station)
	{
		List<Node> list = new List<Node>(officerBeat);
		List<NodeID> list2 = new List<NodeID>(officerBeat.Count);
		Node nextnode = station.components.board.GetNode();
		while (list.Count > 0)
		{
			list.Sort(delegate(Node a, Node b)
			{
				int num = FindDist(a, nextnode);
				int num2 = FindDist(b, nextnode);
				return Math.Sign(num - num2);
			});
			nextnode = list.SwapRemoveAt(0);
			list2.Add(nextnode.id);
		}
		return list2;
		static int FindDist(Node n, Node next)
		{
			int num = (int)Math.Round((n.pos - next.pos).Magnitude * 10f);
			NodeEdge nodeEdge = n.FindEdgeOrNull(next);
			int num2 = ((nodeEdge == null) ? 50 : ((!nodeEdge.IsRoad) ? 30 : 0));
			return num + num2;
		}
	}

	private static bool ProcessBeatQueue(HashSet<Node> seen, List<Node> beatnodes, Queue<Node> queue, PrecinctID pid)
	{
		if (queue.Count == 0)
		{
			return false;
		}
		Node node = queue.Dequeue();
		if (seen.Contains(node))
		{
			return true;
		}
		seen.Add(node);
		beatnodes.Add(node);
		node.FindRoadNeighbors(_roadNeighbors);
		foreach (Node roadNeighbor in _roadNeighbors)
		{
			if (roadNeighbor.precinctId == pid)
			{
				queue.Enqueue(roadNeighbor);
			}
		}
		return true;
	}
}
