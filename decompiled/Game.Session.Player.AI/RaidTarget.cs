using Game.Core;

namespace Game.Session.Player.AI;

public struct RaidTarget
{
	public static readonly RaidTarget INVALID;

	public NodeID nid;

	public SimTimeSpan duration;

	public bool IsValid => nid.IsValid;

	public bool IsNotValid => nid.IsNotValid;

	public RaidTarget(NodeID nid, SimTimeSpan duration)
	{
		this = default(RaidTarget);
		this.nid = nid;
		this.duration = duration;
	}

	public override string ToString()
	{
		return $"Raid on {nid} starting {Game.ctx.clock.Now} for {duration.deltadays} days";
	}
}
