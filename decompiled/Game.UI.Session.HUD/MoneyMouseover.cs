using System.Linq;
using System.Text;
using Game.Core;
using Game.Services;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Player;
using Game.UI.Mouseovers;
using Game.UI.Util;
using SomaSim.Util;

namespace Game.UI.Session.HUD;

public sealed class MoneyMouseover : BaseCustomTextMouseover
{
	protected override string ProduceText()
	{
		PlayerFinances finances = Game.ctx.players.Human.finances;
		MoneyPerTurnListing moneyThisTurn = finances.GetMoneyThisTurn();
		string text = Loc.Money(moneyThisTurn.endMoney);
		string text2 = SummarizeStoredMoney(finances.Data);
		string text3 = SummarizeTurnData(moneyThisTurn);
		return Loc.Get("moneyreason.summary.block", "money", text, "stored", text2, "changes", text3);
	}

	private static string SummarizeStoredMoney(PlayerFinanceData findata)
	{
		StringBuilder stringBuilder = StringBuilderPool.AllocateInstance();
		stringBuilder.AppendLine(Loc.Get("moneyreason.summary.stored.header"));
		foreach (PlayerFinanceData.MoneyStoredStruct item in (from x in findata.GetMoneyStoredPerLocationUnsafe(Game.ctx.players.Human)
			orderby x.amt.cash descending
			select x).ToList())
		{
			if (!(item.amt == new Money(0)))
			{
				stringBuilder.AppendLine(Loc.Get("moneyreason.summary.changes.line", "amount", Loc.Money(item.amt), "explanation", GetStoredLocationName(item)));
			}
		}
		return stringBuilder.ToStringAndReturnToPool().TrimEnd();
	}

	private static string GetStoredLocationName(PlayerFinanceData.MoneyStoredStruct entry)
	{
		if (entry.isCrew && entry.crewPeepId.IsValid)
		{
			return NameUtils.GetPeepFullName(entry.crewPeepId);
		}
		if (entry.isCrew)
		{
			return Loc.Get("moneyreason.summary.stored.unassigned-vehicle");
		}
		if (entry.isBuilding)
		{
			return BuildingUtil.FindBuildingName(entry.eid);
		}
		return "?";
	}

	private static string SummarizeTurnData(MoneyPerTurnListing thisTurn)
	{
		StringBuilder stringBuilder = StringBuilderPool.AllocateInstance();
		stringBuilder.AppendLine(Loc.Get("moneyreason.summary.changes.header"));
		bool flag = false;
		foreach (var (reason, price) in thisTurn.GetSummaries())
		{
			if (!(price.cash == 0))
			{
				flag = true;
				stringBuilder.AppendLine(Loc.Get("moneyreason.summary.changes.line", "amount", TextUtil.ColorGreenRed(price.cash, Loc.Price(price)), "explanation", Loc.GetMoneyReason(reason)));
			}
		}
		if (flag)
		{
			return stringBuilder.ToStringAndReturnToPool();
		}
		stringBuilder.ToStringAndReturnToPool();
		return "";
	}
}
