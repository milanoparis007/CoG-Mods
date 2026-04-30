using System.Collections.Generic;
using Game.Core;

namespace Game.Session.Player.AI;

public class CopBeat
{
	public EntityID officerId;

	public List<NodeID> nodes = new List<NodeID>();

	public int nextBeat;

	public CopBeat()
	{
	}

	public CopBeat(EntityID officerId, List<NodeID> nodes)
	{
		this.officerId = officerId;
		nextBeat = 0;
		this.nodes = nodes;
	}

	public void ResetNextBeat()
	{
		nextBeat = 0;
	}

	public int GetAndIncrementNextIndex()
	{
		int num = nextBeat;
		nextBeat = (num + 1) % nodes.Count;
		return num;
	}
}
