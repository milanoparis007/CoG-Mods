using Game.Core;
using Game.Session.Board;

namespace Game.Session.Entities;

public sealed class CornerData : BaseData
{
	public NodeID nid;

	public Node FindNode()
	{
		return nid.FindNode();
	}
}
