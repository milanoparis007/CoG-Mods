using System.Collections.Generic;
using Game.Core;
using Game.Session.Data;

namespace Game.Session.Entities;

public sealed class ModuleExpansions
{
	public ModValue maxcount;

	public List<ModuleExpansionConfig> configs;

	public int FindMaxExpansions(VisitState visit)
	{
		return (int)maxcount.Evaluate(visit.MakeOwnerModQuery());
	}

	public ModuleExpansionConfig FindExpansionOrNull(Label id)
	{
		int num = FindExpansionIndex(id);
		if (num < 0)
		{
			return null;
		}
		return configs[num];
	}

	public int FindExpansionIndex(Label id)
	{
		int i = 0;
		for (int count = configs.Count; i < count; i++)
		{
			if (configs[i].id == id)
			{
				return i;
			}
		}
		return -1;
	}
}
