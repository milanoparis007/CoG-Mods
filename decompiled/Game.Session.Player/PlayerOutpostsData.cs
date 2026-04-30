using System.Collections.Generic;
using Game.Core;
using Game.Session.Board;
using Game.Session.Entities;
using SomaSim.Util;

namespace Game.Session.Player;

public sealed class PlayerOutpostsData
{
	public PlayerID pid;

	public List<OutpostEntry> outposts;

	public List<OutpostClosed> closures;

	public SimTime lastClosureTime = SimTime.MIN_DATE;

	public SimTime lastStolenTime = SimTime.MIN_DATE;

	public PlayerOutpostsData()
	{
	}

	public PlayerOutpostsData(PlayerID pid)
	{
		this.pid = pid;
		outposts = new List<OutpostEntry>();
		closures = new List<OutpostClosed>();
	}

	internal int FindEntryIndex(OutpostID outpostId)
	{
		int i = 0;
		for (int count = outposts.Count; i < count; i++)
		{
			if (outposts[i].outpostId == outpostId)
			{
				return i;
			}
		}
		return -1;
	}

	internal OutpostEntry FindByOutpostID(OutpostID outpostId)
	{
		int num = FindEntryIndex(outpostId);
		if (num < 0)
		{
			return null;
		}
		return outposts[num];
	}

	internal OutpostEntry FindByOutpostBuilding(Entity outpost)
	{
		return FindByOutpostID(new OutpostID(outpost));
	}

	internal OutpostEntry FindByOutpostNode(NodeID nodeId)
	{
		int i = 0;
		for (int count = outposts.Count; i < count; i++)
		{
			if (outposts[i].OutpostNode.nodeId == nodeId)
			{
				return outposts[i];
			}
		}
		return null;
	}

	internal (OutpostEntry entry, NodeEntry nodeEntry) FindFirstEntry(NodeID nodeId)
	{
		int i = 0;
		for (int count = outposts.Count; i < count; i++)
		{
			List<NodeEntry> targetNodes = outposts[i].targetNodes;
			int j = 0;
			for (int count2 = targetNodes.Count; j < count2; j++)
			{
				NodeEntry nodeEntry = targetNodes[j];
				if (nodeId == nodeEntry.nodeId)
				{
					return (entry: outposts[i], nodeEntry: nodeEntry);
				}
			}
		}
		return (entry: null, nodeEntry: null);
	}

	internal IEnumerable<(OutpostEntry entry, NodeEntry nodeEntry)> FindAllEntries(NodeID nodeId)
	{
		int oi = 0;
		for (int ocount = outposts.Count; oi < ocount; oi++)
		{
			List<NodeEntry> targetNodes = outposts[oi].targetNodes;
			int ni = 0;
			for (int ncount = targetNodes.Count; ni < ncount; ni++)
			{
				NodeEntry nodeEntry = targetNodes[ni];
				if (nodeId == nodeEntry.nodeId)
				{
					yield return (entry: outposts[oi], nodeEntry: nodeEntry);
				}
			}
		}
	}

	internal Fixnum SumRespectPumpedAt(NodeID nodeId, bool current)
	{
		Fixnum zERO = Fixnum.ZERO;
		int i = 0;
		for (int count = outposts.Count; i < count; i++)
		{
			List<NodeEntry> targetNodes = outposts[i].targetNodes;
			int j = 0;
			for (int count2 = targetNodes.Count; j < count2; j++)
			{
				NodeEntry nodeEntry = targetNodes[j];
				if (nodeEntry.nodeId == nodeId)
				{
					zERO += (current ? nodeEntry.current : nodeEntry.previous);
				}
			}
		}
		return zERO;
	}

	internal int CountOutpostsThatPumpNode(NodeID nodeId)
	{
		int num = 0;
		int i = 0;
		for (int count = outposts.Count; i < count; i++)
		{
			List<NodeEntry> targetNodes = outposts[i].targetNodes;
			int j = 0;
			for (int count2 = targetNodes.Count; j < count2; j++)
			{
				NodeEntry nodeEntry = targetNodes[j];
				if (nodeId == nodeEntry.nodeId)
				{
					num++;
				}
			}
		}
		return num;
	}

	internal bool HasOutpostsThatPumpNode(NodeID nodeId)
	{
		int i = 0;
		for (int count = outposts.Count; i < count; i++)
		{
			List<NodeEntry> targetNodes = outposts[i].targetNodes;
			int j = 0;
			for (int count2 = targetNodes.Count; j < count2; j++)
			{
				NodeEntry nodeEntry = targetNodes[j];
				if (nodeId == nodeEntry.nodeId)
				{
					return true;
				}
			}
		}
		return false;
	}

	internal OutpostID FindOutpostThatPumpsNodeRightNow(NodeID nodeId)
	{
		int i = 0;
		for (int count = outposts.Count; i < count; i++)
		{
			OutpostEntry outpostEntry = outposts[i];
			int nodeIndex = outpostEntry.pump.nodeIndex;
			if (nodeIndex >= 0 && outpostEntry.targetNodes[nodeIndex].nodeId == nodeId)
			{
				return outpostEntry.outpostId;
			}
		}
		return OutpostID.INVALID;
	}

	internal bool Remove(Entity outpost)
	{
		int num = FindEntryIndex(new OutpostID(outpost));
		bool num2 = num >= 0;
		if (num2)
		{
			outposts.RemoveAt(num);
		}
		return num2;
	}

	internal OutpostEntry Add(Entity outpost, List<NodeID> nodeIds)
	{
		OutpostEntry outpostEntry = new OutpostEntry(outpost, nodeIds);
		outposts.Add(outpostEntry);
		return outpostEntry;
	}

	internal bool IsExpanding(OutpostEntry entry)
	{
		return entry?.pump.IsPumping ?? false;
	}

	internal bool IsExpanding(Entity outpost)
	{
		return IsExpanding(FindByOutpostBuilding(outpost));
	}

	internal bool IsAnyExpanding()
	{
		foreach (OutpostEntry outpost in outposts)
		{
			if (IsExpanding(outpost))
			{
				return true;
			}
		}
		return false;
	}

	internal (Fixnum current, Fixnum goal) GetCurrentRespect(NodeEntry entry)
	{
		Respect respect = entry.nodeId.FindNode()?.respect.GetOrNull(pid);
		if (respect == null)
		{
			return (current: Fixnum.ZERO, goal: Fixnum.ZERO);
		}
		return (current: respect.current, goal: respect.goal);
	}

	internal OutpostClosed GetClosureOrNull(OutpostID outpost)
	{
		int i = 0;
		for (int count = closures.Count; i < count; i++)
		{
			if (closures[i].outpostId == outpost)
			{
				return closures[i];
			}
		}
		return null;
	}

	internal OutpostClosed IncrementClosure(Entity building)
	{
		return IncrementClosure(new OutpostID(building));
	}

	internal OutpostClosed IncrementClosure(OutpostID outpost)
	{
		OutpostClosed outpostClosed = GetClosureOrNull(outpost);
		if (outpostClosed == null)
		{
			closures.Add(outpostClosed = new OutpostClosed(outpost, 0));
		}
		outpostClosed.count++;
		return outpostClosed;
	}

	internal int GetClosureCount(OutpostID outpost)
	{
		return GetClosureOrNull(outpost)?.count ?? 0;
	}
}
