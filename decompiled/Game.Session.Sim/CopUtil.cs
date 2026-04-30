using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Session.Board;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Player;
using SomaSim.Util;

namespace Game.Session.Sim;

public static class CopUtil
{
	public static bool IsFed(EntityID peepId)
	{
		return IsFed(peepId.FindEntity());
	}

	public static bool IsFed(Entity peep)
	{
		return FindFedAgencyOrNull(peep) != null;
	}

	public static bool IsCop(EntityID peepId)
	{
		return IsCop(peepId.FindEntity());
	}

	public static bool IsCop(Entity peep)
	{
		return FindPrecinctOrNull(peep) != null;
	}

	public static PlayerInfo FindPrecinctOrNull(Node node)
	{
		return node.precinctId.FindPrecinct();
	}

	public static PlayerInfo FindPrecinctOrNull(EntityID peepId)
	{
		return FindPrecinctOrNull(peepId.FindEntity());
	}

	public static PlayerInfo FindPrecinctOrNull(Entity peep)
	{
		PlayerInfo playerInfo = peep?.data.agent?.pid.FindPlayer();
		if (!playerInfo.IsJustCop)
		{
			return null;
		}
		return playerInfo;
	}

	public static IEnumerable<EntityID> FindBuildingsInPrecinct(PrecinctID precinct)
	{
		return FindNodesInPrecinct(precinct).SelectMany((Node x) => x.contained);
	}

	public static IEnumerable<Node> FindNodesInPrecinct(PrecinctID precinct)
	{
		return from x in Game.ctx.board.nodes.GetAllNodesUnsafe()
			where x.precinctId == precinct
			select x;
	}

	public static PlayerInfo FindFedAgencyOrNull(EntityID peepId)
	{
		return FindFedAgencyOrNull(peepId.FindEntity());
	}

	public static PlayerInfo FindFedAgencyOrNull(Entity peep)
	{
		PlayerInfo playerInfo = peep?.data.agent?.pid.FindPlayer();
		if (!playerInfo.IsJustFed)
		{
			return null;
		}
		return playerInfo;
	}

	public static (bool atNode, bool nearby) HasBlockingCop(PlayerID visitor, NodeID nodeId)
	{
		NodeID item = FindNeighboringNodeWithBlockingCop(visitor, nodeId, includeSource: true).nodeId;
		return (atNode: item.IsValid && item == nodeId, nearby: item.IsValid && item != nodeId);
	}

	private static (NodeID nodeId, PlayerID cop) FindNeighboringNodeWithBlockingCop(PlayerID visitor, NodeID nodeId, bool includeSource)
	{
		if (includeSource)
		{
			PlayerID firstBlockingCopAtNode = GetFirstBlockingCopAtNode(visitor, nodeId);
			if (firstBlockingCopAtNode.IsValid)
			{
				return (nodeId: nodeId, cop: firstBlockingCopAtNode);
			}
		}
		using (ListPool<Node>.PooledBlockList pooledBlockList = ListPool<Node>.Allocate())
		{
			nodeId.FindNode().FindRoadNeighbors(pooledBlockList, clearFirst: false);
			foreach (Node item in pooledBlockList)
			{
				PlayerID firstBlockingCopAtNode2 = GetFirstBlockingCopAtNode(visitor, nodeId);
				if (firstBlockingCopAtNode2.IsValid)
				{
					return (nodeId: item.id, cop: firstBlockingCopAtNode2);
				}
			}
		}
		return (nodeId: NodeID.INVALID, cop: PlayerID.INVALID);
	}

	private static PlayerID GetFirstBlockingCopAtNode(PlayerID visitor, NodeID nodeId)
	{
		foreach (EntityID item in Game.ctx.transit.GetAllAgentsAtNodeUnsafe(nodeId))
		{
			PlayerInfo playerInfo = FindFedAgencyOrNull(item);
			if (playerInfo != null)
			{
				return playerInfo.PID;
			}
			PlayerInfo playerInfo2 = FindPrecinctOrNull(item);
			if (playerInfo2 != null && playerInfo2.ai.precinct.IsBlockingTradesWith(visitor))
			{
				return playerInfo2.PID;
			}
		}
		return PlayerID.INVALID;
	}
}
