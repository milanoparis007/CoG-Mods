using System.Collections.Generic;
using Game.Core;
using Game.Services;

namespace Game.Session.Data;

public sealed class CheckLaws : AbstractVisitRequirement
{
	public enum Test
	{
		Any,
		All,
		None
	}

	public List<Label> of;

	public bool active;

	public Test has;

	public override bool DoesPass(VisitState visit)
	{
		int num = 0;
		foreach (Label item in of)
		{
			bool flag = Game.ctx.simman.politics.IsLawEnacted(item);
			if ((flag && active) || (!flag && !active))
			{
				num++;
			}
		}
		return has switch
		{
			Test.None => num == 0, 
			Test.Any => num >= 1, 
			_ => num == of.Count, 
		};
	}

	public override ReqExplanation Explain(VisitState visit)
	{
		string text = "";
		for (int i = 0; i < of.Count; i++)
		{
			Label id = of[i];
			text = ((i != 0) ? ((i != of.Count - 1 || of.Count == 1) ? (text + ", " + Loc.Get(Game.serv.globals.settings.politics.FindLawDef(id).locname)) : (text + " " + GetAndOrOr() + " " + Loc.Get(Game.serv.globals.settings.politics.FindLawDef(id).locname))) : (text + Loc.Get(Game.serv.globals.settings.politics.FindLawDef(id).locname)));
		}
		if (has == Test.None)
		{
			return new ReqExplanation(DoesPass(visit), Loc.GetPluralized("ui.requirements.check-law-enacted.none", of.Count, "law", text));
		}
		if (has == Test.All)
		{
			return new ReqExplanation(DoesPass(visit), Loc.GetPluralized("ui.requirements.check-law-enacted.all", of.Count, "law", text));
		}
		return new ReqExplanation(DoesPass(visit), Loc.GetPluralized("ui.requirements.check-law-enacted.any", of.Count, "law", text));
		string GetAndOrOr()
		{
			switch (has)
			{
			case Test.Any:
			case Test.None:
				return Loc.Get("ui.politics.law-conjunction.or");
			case Test.All:
				return Loc.Get("ui.politics.law-conjunction.and");
			default:
				return "";
			}
		}
	}
}
