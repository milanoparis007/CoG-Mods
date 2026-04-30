using Game.Core;
using Game.Services;
using Game.Session.Entities;
using Game.UI.Session.Convo;

namespace Game.Session.Data;

public class SpecialCheckResEventPrice : IConvoButtonRequirement, IRequirement
{
	private (bool hasevent, Price price) GetInfo(VisitState visit)
	{
		ResEventData resEventData = visit.building?.components.residence.GetResEventOrNull();
		if (resEventData == null)
		{
			return (hasevent: false, price: default(Price));
		}
		return (hasevent: true, price: resEventData.FindPrice(PlayerID.HumanPlayer));
	}

	public bool DoesPass(VisitState visit, ConvoButtonState _)
	{
		var (flag, delta) = GetInfo(visit);
		if (flag)
		{
			return Game.ctx.players.Human.finances.CanChangeMoneyOnCrew(visit, delta);
		}
		return false;
	}

	public ReqExplanation Explain(VisitState visit, ConvoButtonState bstate)
	{
		Money money = new Money(GetInfo(visit).price.Abs.cash);
		return new ReqExplanation(DoesPass(visit, bstate), Loc.Get("ui.requirements.resevent", "cash", Loc.Money(money)));
	}
}
