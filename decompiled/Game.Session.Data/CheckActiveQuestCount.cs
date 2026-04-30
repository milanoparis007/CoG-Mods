using Game.Services;
using SomaSim.Util;

namespace Game.Session.Data;

public sealed class CheckActiveQuestCount : VisitWithValueRequirement
{
	public enum With
	{
		Anyone,
		Owner
	}

	public With with;

	protected override Fixnum GetCurrentValue(VisitState visit)
	{
		if (!visit.pid.IsHumanPlayer)
		{
			return 0;
		}
		if (with == With.Anyone)
		{
			return Game.ctx.quests.ActiveQuestsCount;
		}
		return Game.ctx.quests.CountActiveQuestsWith(visit.npc.Id);
	}

	public override ReqExplanation Explain(VisitState visit)
	{
		string message = ((value == 0 && @is == Test.EqualTo && with == With.Owner) ? Loc.Get("ui.requirements.questcount.unexpected") : ((value == 0 && @is == Test.EqualTo && with == With.Anyone) ? Loc.Get("ui.requirements.questcount.unexpected.any") : Loc.Get("ui.requirements.questcount.expected", "value", value)));
		return new ReqExplanation(DoesPass(visit), message);
	}
}
