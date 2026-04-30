using Game.Core;
using Game.Services;

namespace Game.Session.Data;

internal static class IConvoButtonRequirementExtensions
{
	public static Money GetPlayerMoney(this IConvoButtonRequirement _, VisitState visit)
	{
		return visit.GetPlayer().finances.GetMoney(visit.vehicle);
	}

	public static bool CanChangePlayerMoney(this IConvoButtonRequirement _, VisitState visit, Price price)
	{
		return visit.GetPlayer().finances.CanChangeMoney(visit.vehicle, price);
	}

	public static string MakeCostExplanation(this IConvoButtonRequirement req, VisitState visit, Price price, bool canAfford)
	{
		string text = Loc.Get("ui.requirements.cost-exp.cost", "price", Loc.Price(price));
		if (!canAfford)
		{
			Money playerMoney = req.GetPlayerMoney(visit);
			text += Loc.Get("ui.requirements.cost-exp.current", "current", Loc.Money(playerMoney));
		}
		return text;
	}
}
