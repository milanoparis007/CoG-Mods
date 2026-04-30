using Game.Core;
using Game.Services;
using Game.UI.Session.Convo;

namespace Game.Session.Data;

public class SpecialCanPayForOutpost : IConvoButtonRequirement, IRequirement
{
	public enum CostType
	{
		Install,
		Monthly
	}

	public CostType type;

	private Price GetCost(ConvoButtonState bstate)
	{
		ConvoDataStartOutpost convoDataStartOutpost = bstate.data as ConvoDataStartOutpost;
		switch (type)
		{
		case CostType.Install:
			return convoDataStartOutpost.install;
		case CostType.Monthly:
			return convoDataStartOutpost.monthly;
		default:
			Logger.Warning("Unknown type", type);
			return Price.ZERO;
		}
	}

	public bool DoesPass(VisitState visit, ConvoButtonState bstate)
	{
		return Game.ctx.players.Human.finances.CanChangeMoneyOnCrew(visit, GetCost(bstate));
	}

	public ReqExplanation Explain(VisitState visit, ConvoButtonState bstate)
	{
		return new ReqExplanation(DoesPass(visit, bstate), Loc.Get("ui.requirements.outpost.canpay", "value", Loc.Price(GetCost(bstate) * -1)));
	}
}
