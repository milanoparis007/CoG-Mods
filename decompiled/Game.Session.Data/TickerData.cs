using Game.Core;

namespace Game.Session.Data;

public class TickerData
{
	public TickerType type;

	public TickerIcon icon;

	public TickerTitle title;

	public string message;

	public TickerTarget target;

	public SimTime date;

	public TickerPersistType persisted;

	public virtual bool OnClickCallback()
	{
		return false;
	}
}
