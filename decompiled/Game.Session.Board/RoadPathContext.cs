using System.Collections.Generic;
using Game.Core;
using Game.Services;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Player;
using SomaSim.Util;

namespace Game.Session.Board;

public class RoadPathContext : IPathContext
{
	private Fixnum _knownTileCost;

	private Fixnum _ownedTileCost;

	private Fixnum _unknownTileCost;

	private Fixnum _unknownPathPenalty;

	private PlayerID _pid;

	private Entity _entity;

	public RoadPathContext(BoardManager _)
	{
	}

	public void OnSearchStart(PlayerID pid, EntityID eid)
	{
		_pid = pid;
		_entity = eid.FindEntity();
		CrewCostSettings costs = Game.serv.globals.settings.people.social.costs;
		ModQuery query = new ModQuery(_pid, eid, eid);
		_knownTileCost = costs.moveOnKnownNode.Evaluate(query);
		_ownedTileCost = costs.moveOnOwnedNode.Evaluate(query);
		_unknownTileCost = costs.moveOnUnknownNode.Evaluate(query);
		_unknownPathPenalty = costs.pathOnUnknownPenalty.Evaluate(query);
	}

	public void OnSearchEnd()
	{
		_pid = PlayerID.INVALID;
		_entity = null;
	}

	public Fixnum LeastCostEstimate(PlayerID pid, Node current, Node target)
	{
		Fixnum fixnum = (Fixnum)(current.pos - target.pos).Magnitude * (Fixnum)0.05f;
		Fixnum fixnum2 = (current.known.Get(pid) ? ((Fixnum)0) : _unknownPathPenalty);
		return fixnum * _knownTileCost + fixnum2;
	}

	public void GetNeighbors(PathElement pathSoFar, List<PathContextNeighbor> outNeighbors)
	{
		Node node = pathSoFar.target.node;
		PlayerMeetings meetings = _pid.FindPlayer().meetings;
		NodeEdgeID[] edges = node.edges;
		for (int i = 0; i < edges.Length; i++)
		{
			NodeEdge nodeEdge = edges[i].FindEdge();
			if (nodeEdge == null || !nodeEdge.IsRoad)
			{
				continue;
			}
			Node node2 = nodeEdge.FindOtherNode(node);
			if (node2 != null)
			{
				PlayerID playerID = node2.owner.Get();
				Fixnum cost = _knownTileCost;
				if (node2.IsOnWater || node2.IsConnectionNode)
				{
					cost = 0;
				}
				else if (!meetings.IsNodeKnown(node2))
				{
					cost = _unknownTileCost;
				}
				else if (playerID.IsAIPlayer && playerID != _pid)
				{
					cost = _ownedTileCost;
				}
				outNeighbors.Add(new PathContextNeighbor(node2, nodeEdge, cost));
			}
		}
	}
}
