using System.Collections.Generic;
using Game.Core;
using Game.Services;
using SomaSim.Util;

namespace Game.Session.Data;

public sealed class LawMod : AbstractModifier
{
	public List<Label> laws;

	public bool enacted;

	public override ModQueryElement QueryMustProvide => ModQueryElement.Nothing;

	public override Fixnum Evaluate(ModQuery query, Fixnum source)
	{
		Fixnum result = source;
		foreach (Label law2 in laws)
		{
			PoliticsSettings.Law law = Game.serv.globals.settings.politics.FindLawDef(law2);
			bool flag = Game.ctx.simman.politics.IsLawEnacted(law2);
			if (enacted == flag)
			{
				result = law.mods.Evaluate(query, source);
			}
		}
		return result;
	}

	public override string Explain(ModQuery query, Fixnum delta)
	{
		string text = "";
		int num = 0;
		for (int i = 0; i < laws.Count; i++)
		{
			Label label = laws[i];
			PoliticsSettings.Law law = Game.serv.globals.settings.politics.FindLawDef(label);
			bool flag = Game.ctx.simman.politics.IsLawEnacted(label);
			if (enacted == flag)
			{
				num++;
				text = ((i != 0) ? ((i != laws.Count - 1 || laws.Count == 1) ? (text + ", " + Loc.Get(law.locname)) : (text + Loc.Get("ui.politics.law-conjunction.and") + " " + Loc.Get(law.locname))) : (text + Loc.Get(law.locname)));
			}
		}
		return Loc.GetPluralized("mod.law-mod", num, "delta", AbstractModifier.FormatDelta(delta), "law", text);
	}
}
