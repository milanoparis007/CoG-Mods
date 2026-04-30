using Game.Services;
using SomaSim.Util;

namespace Game.Session.Data;

public sealed class CheckActiveQuestsUnderCap : AbstractVisitRequirement
{
	private Fixnum FindCap()
	{
		return Game.ctx.quests.GetActiveQuestCap();
	}

	public override bool DoesPass(VisitState visit)
	{
		if (!visit.pid.IsHumanPlayer)
		{
			return false;
		}
		Fixnum fixnum = FindCap();
		return Game.ctx.quests.ActiveQuestsCount < fixnum;
	}

	public override ReqExplanation Explain(VisitState visit)
	{
		return new ReqExplanation(DoesPass(visit), Loc.Get("ui.requirements.questcount.undercap", "count", FindCap()));
	}
}
