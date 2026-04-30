using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Services;
using Game.Session.Data;
using Game.Session.Sim.Modules;
using SomaSim.Util;

namespace Game.Session.Player;

public sealed class SkillModuleUnlocksCache
{
	public LabelDictionary<List<Label>> skillToModules = new LabelDictionary<List<Label>>();

	public SkillModuleUnlocksCache(PlayerID pid)
	{
		foreach (IModuleConfig item in ModulesUtil.FindAllModuleDefsExpensive().ToList())
		{
			VisitRequirementList reqs = item.Common.reqs;
			if (reqs == null)
			{
				continue;
			}
			foreach (IVisitRequirement item2 in reqs)
			{
				if (!(item2 is CheckPlayerSkills checkPlayerSkills))
				{
					continue;
				}
				foreach (Label item3 in checkPlayerSkills.of)
				{
					skillToModules.AddToList(item3, item.Id);
				}
			}
		}
	}

	private List<Label> GetUnlockedModuleIDs(Label skill)
	{
		return skillToModules.FindOrMakeList(skill);
	}

	public List<string> GetUnlockedModuleNames(Label skill)
	{
		return (from id in GetUnlockedModuleIDs(skill)
			select ModulesUtil.FindModuleDef(id) into module
			where module != null
			select Loc.Get(module.Common.display.locname)).ToList();
	}
}
