using System.Collections.Generic;
using System.Linq;
using Game.Core;

namespace Game.Session.Sim;

public sealed class RailroadData
{
	public int id;

	public List<NodeID> nodes;

	public NodeID TerminalNode => nodes[0];

	public NodeID ExitNode => nodes[nodes.Count - 1];

	public static RailroadData FromNodeList(int id, List<PathElementResult> nodes)
	{
		return new RailroadData
		{
			id = id,
			nodes = nodes.Select((PathElementResult node) => node.source.id).ToList()
		};
	}
}
