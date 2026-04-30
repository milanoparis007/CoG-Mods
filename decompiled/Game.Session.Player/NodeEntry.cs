using Game.Core;
using Game.Session.Board;
using SomaSim.Util;

namespace Game.Session.Player;

public sealed class NodeEntry
{
	public bool outpost;

	public NodeID nodeId;

	public Fixnum previous;

	public Fixnum current;

	public Label expansionId;

	public int priority;

	private const int OUTPOST_PRIOROTY = 1000000;

	public bool IsPumped => current.IsNotZero;

	public NodeEntry()
	{
	}

	public NodeEntry(bool outpost, NodeID nodeId)
	{
		this.outpost = outpost;
		this.nodeId = nodeId;
		current = (previous = Fixnum.ZERO);
		expansionId = Label.NULL;
	}

	public void ClearPumpedAndExpansion()
	{
		SetOrClearPumped(Fixnum.ZERO, Fixnum.ZERO);
	}

	public void SetExpansion(Label expId, bool isHuman)
	{
		expansionId = expId;
	}

	public void SetOrClearPumped(Fixnum previous, Fixnum current)
	{
		this.current = current;
		this.previous = previous;
		if (current.IsZero)
		{
			expansionId = Label.NULL;
		}
	}

	public void RecomputePriority(PlayerID pid)
	{
		priority = (outpost ? 1000000 : ((int)(nodeId.FindNode()?.respect.GetOrNull(pid)?.current ?? ((Fixnum)0))));
	}

	public static int Comparator(NodeEntry a, NodeEntry b)
	{
		return b.priority - a.priority;
	}
}
