using System.Collections.Generic;
using Game.Core;
using Game.Services;

namespace Game.Session.Data;

public sealed class CheckNpcPoliticianArchetype : AbstractVisitRequirement
{
	public enum Test
	{
		Any,
		None
	}

	public Test @is;

	public List<Label> of;

	public override bool DoesPass(VisitState visit)
	{
		Label archetypeId = Game.ctx.simman.politics.GetPoliticianData(visit.npc.Id).archetypeId;
		bool flag = of.Contains(archetypeId);
		if (@is != Test.Any)
		{
			return !flag;
		}
		return flag;
	}

	public override ReqExplanation Explain(VisitState visit)
	{
		bool passed = DoesPass(visit);
		string text = "";
		for (int i = 0; i < of.Count; i++)
		{
			Label id = of[i];
			text = ((i != 0) ? ((i != of.Count - 1 || of.Count == 1) ? (text + ", " + Loc.Get(Game.serv.globals.settings.politics.FindPoliticalArchetype(id).locname)) : (text + " " + Loc.Get("ui.politics.law-conjunction.or") + " " + Loc.Get(Game.serv.globals.settings.politics.FindPoliticalArchetype(id).locname))) : (text + Loc.Get(Game.serv.globals.settings.politics.FindPoliticalArchetype(id).locname)));
		}
		return new ReqExplanation(passed, Loc.Get("ui.requirements.political-archetype", "archetypes", text));
	}
}
