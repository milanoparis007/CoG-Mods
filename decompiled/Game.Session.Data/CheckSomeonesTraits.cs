using Game.Core;
using Game.Services;

namespace Game.Session.Data;

public abstract class CheckSomeonesTraits : CheckListOfItems
{
	protected abstract TagList GetTraitsOrNull(VisitState visit);

	protected override int Count(VisitState visit)
	{
		TagList traitsOrNull = GetTraitsOrNull(visit);
		int num = 0;
		if (traitsOrNull != null)
		{
			foreach (Label item in of)
			{
				if (traitsOrNull.Contains(item))
				{
					num++;
				}
			}
		}
		return num;
	}

	protected override void VerifyData(VisitState visit)
	{
		TraitList traits = Game.serv.globals.settings.people.traits;
		foreach (Label item in of)
		{
			if (!traits.ContainsById(item))
			{
				Label label = item;
				Logger.Warning("Check owner: unknown skill tag: " + label.ToString());
			}
		}
	}

	protected override string IdToName(Label id)
	{
		Trait trait = Game.serv.globals.settings.people.traits.Find(id);
		if (trait == null)
		{
			return "";
		}
		return trait.GetLocName();
	}

	protected override string MakeExplanationText()
	{
		return MakeExplanationText(Loc.Get("ui.requirements.skills.expected"), Loc.Get("ui.requirements.skills.unexpected"), Loc.Get("ui.requirements.skills.all"), Loc.Get("ui.requirements.skills.any"), Loc.Get("ui.requirements.skills.none"));
	}
}
