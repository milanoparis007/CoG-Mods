using Game.UI.Session.Convo;

namespace Game.Session.Data;

public class SpecialCanPayForSkillsButtonReq : IConvoButtonRequirement, IRequirement
{
	public bool DoesPass(VisitState visit, ConvoButtonState bstate)
	{
		ConvoDataLearnSkill convoDataLearnSkill = bstate.data as ConvoDataLearnSkill;
		return visit.GetPlayer().skills.CanPayForSkill(visit, convoDataLearnSkill.skillId);
	}

	public ReqExplanation Explain(VisitState visit, ConvoButtonState bstate)
	{
		ConvoDataLearnSkill convoDataLearnSkill = bstate.data as ConvoDataLearnSkill;
		bool flag = visit.GetPlayer().skills.CanPayForSkill(visit, convoDataLearnSkill.skillId);
		return new ReqExplanation(flag, this.MakeCostExplanation(visit, convoDataLearnSkill.price, flag));
	}
}
