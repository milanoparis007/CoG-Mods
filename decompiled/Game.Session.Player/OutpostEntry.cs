using System.Collections.Generic;
using Game.Core;
using Game.Session.Data;
using Game.Session.Entities;
using SomaSim.Util;

namespace Game.Session.Player;

public sealed class OutpostEntry
{
	public sealed class PumpStatus
	{
		public int nodeIndex = -1;

		public Label expId = Label.NULL;

		public bool IsPumping => nodeIndex >= 0;

		public void Set(int nodeIndex, Label expId)
		{
			this.expId = expId;
			this.nodeIndex = nodeIndex;
		}

		public void Reset()
		{
			expId = Label.NULL;
			nodeIndex = -1;
		}
	}

	public const int OUTPOST_NODE_INDEX = 0;

	public OutpostID outpostId;

	public List<NodeEntry> targetNodes;

	public PumpStatus pump = new PumpStatus();

	public MoneyStatus money = new MoneyStatus();

	public NodeEntry OutpostNode => targetNodes[0];

	public OutpostEntry()
	{
	}

	public OutpostEntry(Entity outpost, List<NodeID> nodeIds)
	{
		NodeID outpostNode = outpost.components.board.GetNodeID();
		outpostId = new OutpostID(outpost);
		pump = new PumpStatus();
		money = new MoneyStatus();
		targetNodes = nodeIds.SelectIntoNewList((NodeID nid) => new NodeEntry(nid == outpostNode, nid));
	}

	public ModQuery MakeOutpostQuery(PlayerID pid)
	{
		EntityID targetId = BuildingUtil.FindOwnerForAnyBuilding(outpostId.buildingId)?.Id ?? EntityID.INVALID;
		return new ModQuery(pid, targetId, OutpostNode.nodeId);
	}

	internal void RecomputeNodePriorities(PlayerID pid)
	{
		foreach (NodeEntry targetNode in targetNodes)
		{
			targetNode.RecomputePriority(pid);
		}
		targetNodes.StableSort(NodeEntry.Comparator);
	}
}
