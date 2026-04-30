using Game.Core;
using Game.Session.Data;
using SomaSim.Util;

namespace Game.Session.Entities;

public sealed class PlayerTradeSummary
{
	public PlayerID pid;

	public ResourceAndQtyList list = new ResourceAndQtyList();

	public SimTime lastUpdate = SimTime.MIN_DATE;

	public void Increment(ResourceAndQty res)
	{
		list.Increment(res);
		lastUpdate = Game.ctx.clock.Now;
	}

	public Fixnum Get(Label resId)
	{
		return list.Get(resId);
	}

	public ResourceAndQty GetLargestIfRecent(SimTime minUpdateTime)
	{
		if (!(lastUpdate < minUpdateTime))
		{
			return GetLargest();
		}
		return ResourceAndQty.NONE;
	}

	public ResourceAndQty GetLargestIfRecent(SimTimeSpan maxAge)
	{
		return GetLargestIfRecent(Game.ctx.clock.Now.IncrementDays(-maxAge.deltadays));
	}

	public ResourceAndQty GetLargest()
	{
		ResourceAndQty result = ResourceAndQty.NONE;
		foreach (ResourceAndQty datum in list.data)
		{
			Fixnum qty = datum.qty;
			if (qty.Abs > result.qty.Abs)
			{
				result = datum;
			}
		}
		return result;
	}

	public bool HasAnything()
	{
		return list.HasAnything();
	}
}
