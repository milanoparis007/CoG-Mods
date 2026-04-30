using System.Collections.Generic;
using Game.Services;
using SomaSim.Util;

namespace Game.Session.Data;

public sealed class CurrentMonthMod : BaseDeltaMultiplierModifier
{
	public enum Test
	{
		Any,
		None
	}

	public Test @is;

	public List<int> of;

	public override ModQueryElement QueryMustProvide => ModQueryElement.Nothing;

	public override bool DoesPass(ModQuery _)
	{
		bool flag = Game.ctx.clock.IsMonthOneOf(of);
		if (@is != Test.Any)
		{
			return !flag;
		}
		return flag;
	}

	public override string Explain(ModQuery query, Fixnum delta)
	{
		return Loc.Get("mod.time-of-year", "delta", delta);
	}
}
