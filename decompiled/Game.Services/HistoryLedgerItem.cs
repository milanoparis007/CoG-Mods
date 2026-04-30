using Game.Core;

namespace Game.Services;

public struct HistoryLedgerItem
{
	public string eventname;

	public SimTime time;

	public SocialActionInfo info;

	public EntityID from;

	public EntityID to;

	public EntityID target;

	public EntityID actor;

	public string method;

	public NodeID node;
}
