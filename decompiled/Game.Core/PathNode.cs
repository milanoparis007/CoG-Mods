using System.Diagnostics;
using SomaSim.Util;

namespace Game.Core;

[DebuggerDisplay("{DebugString}")]
public sealed class PathNode
{
	public enum Status
	{
		Unvisited,
		InOpenSet,
		InClosedSet
	}

	public Node node;

	public Status status;

	public Fixnum costTotal;

	public Fixnum costSoFar;

	public Fixnum estToGoal;

	public bool IsReset => costTotal == Fixnum.MAX_VALUE;

	private string DebugString => ToString();

	public PathNode(Node node)
	{
		this.node = node;
		ResetVisitState();
	}

	public void ResetVisitState()
	{
		costTotal = (costSoFar = (estToGoal = Fixnum.MAX_VALUE));
		status = Status.Unvisited;
	}

	public void SetVisitState(Fixnum costSoFar, Fixnum estToGoal)
	{
		this.costSoFar = costSoFar;
		this.estToGoal = estToGoal;
		costTotal = costSoFar + estToGoal;
	}

	public override string ToString()
	{
		return $"[node {node} total f={costTotal}, so far g={costSoFar}, est h={estToGoal}]";
	}
}
