using Game.Core;

namespace Game.Session.Data;

public sealed class CheckHasTrophy : AbstractVisitRequirement
{
	public Label id;

	public bool expected;

	public override bool DoesPass(VisitState visit)
	{
		return visit.GetPlayer().throne.IsInThrone(id) == expected;
	}

	public override ReqExplanation Explain(VisitState visit)
	{
		return new ReqExplanation(DoesPass(visit), null);
	}
}
