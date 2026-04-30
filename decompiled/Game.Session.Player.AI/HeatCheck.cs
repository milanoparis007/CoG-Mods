using Game.Core;
using SomaSim.Util;

namespace Game.Session.Player.AI;

public struct HeatCheck
{
	public PlayerID pid;

	public NodeID nid;

	public Fixnum val;

	public HeatCheck(PlayerID pid, NodeID nid, Fixnum val)
	{
		this.pid = pid;
		this.nid = nid;
		this.val = val;
	}
}
