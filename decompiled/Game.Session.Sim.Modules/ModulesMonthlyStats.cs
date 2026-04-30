using Game.Core;
using Game.Session.Data;
using SomaSim.Util;

namespace Game.Session.Sim.Modules;

public class ModulesMonthlyStats
{
	public static readonly Fixnum AVG_BETA = (Fixnum)0.8f;

	public Fixnum valueThisMonth;

	public Fixnum valueLastMonth;

	public Fixnum average;

	public int thisMonth;

	public int lastMonth;

	public void Reset()
	{
		valueLastMonth = (valueThisMonth = (average = 0));
		thisMonth = (lastMonth = 0);
	}

	public void Update(SimTime time)
	{
		MaybePushBackHistory(time.ToDate().Month);
	}

	public void Add(ModuleQuery q, ResourceAndQtyList list, SimTime time)
	{
		Update(time);
		foreach (ResourceAndQty datum in list.data)
		{
			Add(q, datum);
		}
	}

	private void Add(ModuleQuery q, ResourceAndQty item)
	{
		valueThisMonth += item.FindResource().GetPriceWithMultiplier(playerBuying: false, item.qty.Abs, q.pid).cash;
		_ = q.OwnerIsHumanPlayer;
	}

	private void MaybePushBackHistory(int month)
	{
		if (month != thisMonth)
		{
			lastMonth = thisMonth;
			valueLastMonth = valueThisMonth;
			if (average.IsZero)
			{
				average = valueThisMonth;
			}
			average = AVG_BETA * average + (1 - AVG_BETA) * valueThisMonth;
			thisMonth = month;
			valueThisMonth = 0;
		}
	}
}
