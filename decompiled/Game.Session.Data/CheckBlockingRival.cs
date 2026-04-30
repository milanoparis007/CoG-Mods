using Game.Services;
using Game.Session.Board;

namespace Game.Session.Data;

public sealed class CheckBlockingRival : AbstractVisitRequirement
{
	public bool expected;

	public override bool DoesPass(VisitState visit)
	{
		bool flag = BoardUtil.RivalBlocking(visit).Count != 0;
		return expected == flag;
	}

	public override ReqExplanation Explain(VisitState visit)
	{
		return new ReqExplanation(DoesPass(visit), Loc.Get("ui.requirements.rivalatnode"));
	}
}
