using Game.Session.Board;
using Game.Session.Player.AI;
using SomaSim.Util;

namespace Game.Core;

public class NodeRaidInfo
{
	public EntityID officer;

	public SimTime lastRaid = SimTime.MIN_DATE;

	public bool HasActiveRaid => officer.IsValid;

	public bool HasEverBeenRaided => !lastRaid.IsMinDate;

	public bool WasRecentlyRaided()
	{
		SimTime now = Game.ctx.clock.Now;
		Fixnum fixnum = RaidChecker.Settings.raidNodeCooldownDayz.Evaluate(PlayerID.HumanPlayer);
		return lastRaid.IncrementDays((int)fixnum) > now;
	}

	public void MarkRaidStart(EntityID officerId, NodeID nodeId)
	{
		nodeId.FindNode();
		lastRaid = Game.ctx.clock.Now;
		officer = officerId;
	}

	public void MarkRaidEnd(EntityID officerId)
	{
		officer = EntityID.INVALID;
	}
}
