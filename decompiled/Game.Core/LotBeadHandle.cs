using System.Diagnostics;

namespace Game.Core;

[DebuggerDisplay("{DebugString}")]
public struct LotBeadHandle
{
	public static readonly LotBeadHandle INVALID;

	public NodeID nodeId;

	public NodeEdgeID edgeId;

	public int beadIndex;

	public bool left;

	public int roadBeadIndex;

	public bool hasRoadIndex;

	public bool IsValid => nodeId.IsValid;

	public bool IsNotValid => nodeId.IsNotValid;

	public bool HasValidRoadBead => hasRoadIndex;

	private string DebugString => ToString();

	public LotBeadHandle(NodeID nodeId, NodeEdgeID edgeId, int beadIndex, bool left, int roadBeadIndex = 0, bool validRoadIndex = false)
	{
		this.nodeId = nodeId;
		this.edgeId = edgeId;
		this.beadIndex = beadIndex;
		this.left = left;
		this.roadBeadIndex = roadBeadIndex;
		hasRoadIndex = validRoadIndex;
	}

	public void Clear()
	{
		nodeId = NodeID.INVALID;
		edgeId = NodeEdgeID.INVALID;
		beadIndex = 0;
		left = false;
		roadBeadIndex = 0;
		hasRoadIndex = false;
	}

	public override string ToString()
	{
		return $"HBEAD {nodeId} {edgeId} # {beadIndex}";
	}
}
