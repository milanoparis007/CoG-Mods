using Game.Core;
using Game.Services;

namespace Game.Session.Data;

public class CheckOutpostNonhuman : AbstractVisitRequirement
{
	public bool expected;

	public override bool DoesPass(VisitState visit)
	{
		return (visit.building?.components.building?.OutpostOwner ?? PlayerID.INVALID).IsAIPlayer == expected;
	}

	public override ReqExplanation Explain(VisitState visit)
	{
		string message = (expected ? Loc.Get("ui.requirements.hasoutpost.nonhuman.expected") : Loc.Get("ui.requirements.hasoutpost.nonhuman.unexpected"));
		return new ReqExplanation(DoesPass(visit), message);
	}
}
