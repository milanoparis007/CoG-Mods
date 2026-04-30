using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Services;
using Game.Session.Sim.Modules;

namespace Game.Session.Data;

public class CheckModuleInConvo : CheckListOfItems
{
	protected override int Count(VisitState visit)
	{
		TagList tagList = visit.building?.components.modules?.GetIdsOfAllBizModules();
		int num = 0;
		if (tagList != null)
		{
			foreach (Label item in tagList)
			{
				if (of.Contains(item))
				{
					num++;
				}
			}
		}
		return num;
	}

	protected override void VerifyData(VisitState _)
	{
		HashSet<Label> hashSet = new HashSet<Label>(from c in ModulesUtil.FindAllModuleDefsExpensive()
			select c.Id);
		foreach (Label item in of)
		{
			if (!hashSet.Contains(item))
			{
				Label label = item;
				Logger.Warning("Check module in convo: unknown module id: " + label.ToString());
			}
		}
	}

	protected override string MakeExplanationText()
	{
		return MakeExplanationText(Loc.Get("ui.requirements.modules.expected"), Loc.Get("ui.requirements.modules.unexpected"), Loc.Get("ui.requirements.modules.all"), Loc.Get("ui.requirements.modules.any"), Loc.Get("ui.requirements.modules.none"));
	}

	protected override string IdToName(Label id)
	{
		return Loc.Get(ModulesUtil.FindModuleDef(id).Common.display.locname);
	}
}
