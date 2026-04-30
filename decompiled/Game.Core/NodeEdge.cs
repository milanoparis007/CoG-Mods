using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Game.Core;

[DebuggerDisplay("{DebugString}")]
public class NodeEdge
{
	public NodeEdgeID neid;

	public NodeID a;

	public NodeID b;

	public Direction abDir;

	public Direction baDir;

	public bool gridConnection;

	public bool isBridge;

	public ProcGenType pcgtype;

	public List<RoadBead> roadBeads = new List<RoadBead>();

	public List<BridgeBead> bridgeBeads = new List<BridgeBead>();

	public List<LotBead> lotBeads = new List<LotBead>();

	public Label roadName = Label.NULL;

	public bool beadsCreated;

	public bool transitTilesCreated;

	public TransitFlags transit;

	public bool IsValid => neid.IsValid;

	public bool IsNotValid => neid.IsNotValid;

	public bool IsRoad => HasTransitType(TransitFlags.Road);

	public bool IsRail => HasTransitType(TransitFlags.Rail);

	public bool IsTransitEmpty => transit == TransitFlags.Empty;

	public bool IsVertical => DirectionUtil.IsVertical(abDir);

	public bool IsPcgAny => pcgtype != ProcGenType.None;

	public bool IsPcgConnection => pcgtype == ProcGenType.Connection;

	public bool IsPcgBoardwalkAny
	{
		get
		{
			if (pcgtype != ProcGenType.BoardwalkOnLeft)
			{
				return pcgtype == ProcGenType.BoardwalkOnRight;
			}
			return true;
		}
	}

	private string DebugString => $"EDGE {a} -> {b}";

	public NodeEdge()
	{
	}

	public NodeEdge(NodeEdgeID neid, NodeID a, NodeID b, Direction abDir, Direction baDir, bool isGridConnection)
	{
		this.neid = neid;
		this.a = a;
		this.b = b;
		this.abDir = abDir;
		this.baDir = baDir;
		gridConnection = isGridConnection;
	}

	public NodeID GetOtherNodeID(NodeID node)
	{
		return NodeID.GetOtherNode(node, a, b);
	}

	public bool IsEdgeBetween(NodeID s, NodeID t)
	{
		if (!a.Equals(s) || !b.Equals(t))
		{
			if (a.Equals(t))
			{
				return b.Equals(s);
			}
			return false;
		}
		return true;
	}

	public int GetLotBeadIndex(LotBead bead)
	{
		return lotBeads.IndexOf(bead);
	}

	public int GetRoadBeadIndex(RoadBead bead)
	{
		return roadBeads.IndexOf(bead);
	}

	public bool HasTransitType(TransitFlags test)
	{
		return (test & transit) != 0;
	}

	public bool HasBoardwalkOnSide(bool left)
	{
		if (!left)
		{
			return pcgtype == ProcGenType.BoardwalkOnRight;
		}
		return pcgtype == ProcGenType.BoardwalkOnLeft;
	}

	public static Node PickNodeA(Node n1, Node n2)
	{
		if (!(n1.pos.MagnitudeSquared < n2.pos.MagnitudeSquared))
		{
			return n2;
		}
		return n1;
	}

	public static Node PickCloserNode(Node n1, Node n2, WorldPos pos)
	{
		double num = Math.Round((n1.pos - pos).MagnitudeSquared);
		double num2 = Math.Round((n2.pos - pos).MagnitudeSquared);
		if (!(num < num2))
		{
			return n2;
		}
		return n1;
	}

	public override string ToString()
	{
		return DebugString;
	}
}
