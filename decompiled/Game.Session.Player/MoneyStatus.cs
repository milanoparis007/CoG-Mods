using SomaSim.Util;

namespace Game.Session.Player;

public sealed class MoneyStatus
{
	public int months;

	public Fixnum collected;

	public Fixnum expenses;

	public Fixnum Delta => collected + expenses;

	public bool NeedsSupport => Delta < 0;

	public bool NeedsCollect => Delta > 0;

	public void Reset()
	{
		months = 0;
		collected = (expenses = 0);
	}
}
