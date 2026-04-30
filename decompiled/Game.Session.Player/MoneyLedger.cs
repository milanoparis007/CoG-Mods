using System.Collections.Generic;
using Game.Core;
using SomaSim.Util;

namespace Game.Session.Player;

public class MoneyLedger
{
	public const int TURNS = 7;

	public List<MoneyPerTurnListing> turns;

	public Money lifetimeMax;

	public MoneyPerTurnListing ThisTurn => turns[0];

	public Money CurrentMoney => ThisTurn.endMoney;

	public Money HighWatermark => lifetimeMax;

	public MoneyLedger()
	{
		turns = ListGenerators.ListOfNewInstances(7, () => new MoneyPerTurnListing());
	}

	public void OnNewGame(SimTime time)
	{
		OnNewTurn(time);
		while (turns.Count > 1)
		{
			turns.RemoveLast();
		}
	}

	public void OnNewTurn(SimTime time)
	{
		Money endMoney = turns[0].endMoney;
		MoneyPerTurnListing moneyPerTurnListing = ((turns.Count >= 7) ? turns.RemoveLast() : new MoneyPerTurnListing());
		moneyPerTurnListing.Reset(time, endMoney);
		turns.Insert(0, moneyPerTurnListing);
	}

	public void Add(MoneyReason reason, Price delta, EntityID? target = null)
	{
		ThisTurn.Add(reason, delta, target);
		if (ThisTurn.endMoney > lifetimeMax)
		{
			lifetimeMax = ThisTurn.endMoney;
		}
	}
}
