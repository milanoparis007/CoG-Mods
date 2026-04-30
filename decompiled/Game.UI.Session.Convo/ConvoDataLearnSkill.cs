using System.Collections.Generic;
using Game.Core;
using Game.Services;
using Game.Session.Data;

namespace Game.UI.Session.Convo;

public sealed class ConvoDataLearnSkill : ConvoData
{
	public Label skillId;

	public List<Label> learnableSkills;

	public Price price;

	public string strIntro;

	public string strPrereqs;

	public override string[] MakeReplacements(VisitState visit, int index)
	{
		return new string[6]
		{
			"introflavor",
			strIntro,
			"prereqs",
			strPrereqs,
			"skillname",
			Game.serv.globals.settings.skills.GetSkill(skillId)?.GetName() ?? ""
		};
	}

	public ConvoDataLearnSkill()
	{
	}

	public ConvoDataLearnSkill(List<Label> learnableSkills, VisitState visit, Label selected)
	{
		this.learnableSkills = learnableSkills;
		skillId = selected;
		SkillDef skill = Game.serv.globals.settings.skills.GetSkill(selected);
		if (skill != null)
		{
			price = skill.GetCashPrice(visit);
			strIntro = Loc.Get("convo.ticket-skills-info.short", "line", skill.GetConvo(), "name", skill.GetName());
			string text = visit.GetPlayer().skills.ExplainSkillPrereqs(skill, visit, brief: true);
			strPrereqs = ((text != null) ? Loc.Get("convo.ticket-skills-info.explanation", "exp", text) : "");
		}
	}
}
