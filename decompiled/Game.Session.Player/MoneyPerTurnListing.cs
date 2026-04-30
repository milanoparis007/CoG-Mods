using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;

namespace Game.Session.Player;

public class MoneyPerTurnListing
{
	public static readonly MoneyReason[] ALL_REASONS = Enum.GetValues(typeof(MoneyReason)) as MoneyReason[];

	public const int EXPENSES_BEGIN = 0;

	public const int EXPENSES_END = 20;

	public const int REVENUES_BEGIN = 20;

	public const int REVENUES_END = 30;

	public const int OTHERS_BEGIN = 30;

	public const int OTHERS_END = 99;

	public Money startMoney;

	public Money endMoney;

	public SimTime time = SimTime.MAX_DATE;

	public List<MoneyLedgerEntry> entries = new List<MoneyLedgerEntry>(128);

	public Price DeltaMoney => new Price(endMoney.cash - startMoney.cash);

	public bool IsValid => !time.NeverHappens;

	public void Reset(SimTime time, Money startMoney)
	{
		this.time = time;
		this.startMoney = (endMoney = startMoney);
		entries.Clear();
	}

	public void Add(MoneyReason reason, Price delta, EntityID? target = null)
	{
		EntityID target2 = target ?? EntityID.INVALID;
		entries.Add(new MoneyLedgerEntry(reason, delta, target2));
		endMoney += delta;
	}

	public Price SumFast(MoneyReason reason)
	{
		Price zERO = Price.ZERO;
		int i = 0;
		for (int count = entries.Count; i < count; i++)
		{
			if (entries[i].reason == reason)
			{
				zERO += entries[i].delta;
			}
		}
		return zERO;
	}

	public IEnumerable<(MoneyReason, Price)> GetSummaries()
	{
		MoneyReason[] aLL_REASONS = ALL_REASONS;
		foreach (MoneyReason moneyReason in aLL_REASONS)
		{
			yield return (moneyReason, SumFast(moneyReason));
		}
	}

	public List<MoneyLedgerEntry> GetExpenses()
	{
		return entries.Where((MoneyLedgerEntry e) => IsAnExpense(e.reason)).ToList();
	}

	public List<MoneyLedgerEntry> GetRevenues()
	{
		return entries.Where((MoneyLedgerEntry e) => IsARevenue(e.reason)).ToList();
	}

	public List<MoneyLedgerEntry> GetOthers()
	{
		return entries.Where((MoneyLedgerEntry e) => IsAnOther(e.reason)).ToList();
	}

	public static bool IsAnExpense(MoneyReason reason)
	{
		if (reason >= MoneyReason.Unknown)
		{
			return reason < MoneyReason.BusinessIncome;
		}
		return false;
	}

	public static bool IsARevenue(MoneyReason reason)
	{
		if (reason >= MoneyReason.BusinessIncome)
		{
			return reason < MoneyReason.BuySell;
		}
		return false;
	}

	public static bool IsAnOther(MoneyReason reason)
	{
		if (reason >= MoneyReason.BuySell)
		{
			return reason < (MoneyReason)99;
		}
		return false;
	}
}
