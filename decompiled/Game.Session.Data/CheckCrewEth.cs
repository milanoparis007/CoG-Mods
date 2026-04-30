using System.Collections.Generic;
using Game.Core;
using Game.Services;

namespace Game.Session.Data;

public class CheckCrewEth : AbstractVisitRequirement
{
	public List<Label> oneof;

	private Label GetEth(VisitState visit)
	{
		return visit.crew.GetPeep().data.person.eth;
	}

	public override bool DoesPass(VisitState visit)
	{
		return oneof?.Contains(GetEth(visit)) ?? false;
	}

	public override ReqExplanation Explain(VisitState visit)
	{
		return new ReqExplanation(DoesPass(visit), Loc.Get("ui.requirements.crew-ethnicity"));
	}
}
