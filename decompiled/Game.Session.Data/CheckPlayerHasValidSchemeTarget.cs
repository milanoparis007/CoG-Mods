using Game.Core;

namespace Game.Session.Data;

public class CheckPlayerHasValidSchemeTarget : AbstractVisitRequirement
{
	public Label id;

	public override bool DoesPass(VisitState visit)
	{
		visit.crew.GetPeep();
		return Game.ctx.players.Human.schemes.HasTargetsForScheme(id, visit.crew.peepId);
	}

	public override ReqExplanation Explain(VisitState visit)
	{
		return new ReqExplanation(DoesPass(visit), null);
	}
}
