using Game.Core;

namespace Game.Session.Player;

public struct MoneyLedgerEntry
{
	public MoneyReason reason;

	public Price delta;

	public EntityID target;

	public bool IsSet => delta.cash != 0;

	public bool IsNotSet => delta.cash == 0;

	public MoneyLedgerEntry(MoneyReason reason, Price delta, EntityID target)
	{
		this = default(MoneyLedgerEntry);
		this.reason = reason;
		this.delta = delta;
		this.target = target;
	}
}
