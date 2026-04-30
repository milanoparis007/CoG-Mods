using System.Collections.Generic;
using Game.Core;

namespace Game.Session.Board;

public sealed class NodePersistedData
{
	public List<Node> nodes = new List<Node>
	{
		new Node()
	};

	public List<NodeEdge> edges = new List<NodeEdge>
	{
		new NodeEdge()
	};
}
