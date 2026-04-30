using Game.Core;
using Game.Services;

namespace Game.Session.Data;

public class CheckModuleWithSkillTags : CheckListOfItems
{
	protected override int Count(VisitState visit)
	{
		TagList skillTagsForAllModules = visit.building.components.modules.GetSkillTagsForAllModules();
		int num = 0;
		foreach (Label item in skillTagsForAllModules)
		{
			if (of.Contains(item))
			{
				num++;
			}
		}
		return num;
	}

	protected override void VerifyData(VisitState _)
	{
		TagList allTags = Game.serv.globals.settings.tags.allTags;
		foreach (Label item in of)
		{
			if (!allTags.Contains(item))
			{
				Label label = item;
				Logger.Warning("Check module: unknown skill tag: " + label.ToString());
			}
		}
	}

	protected override string MakeExplanationText()
	{
		return MakeExplanationText(Loc.Get("ui.requirements.modules.skills.expected"), Loc.Get("ui.requirements.modules.skills.unexpected"), Loc.Get("ui.requirements.modules.skills.all"), Loc.Get("ui.requirements.modules.skills.any"), Loc.Get("ui.requirements.modules.skills.none"));
	}

	protected override string IdToName(Label id)
	{
		return Game.serv.globals.settings.skills.GetSkill(id).GetName();
	}
}
