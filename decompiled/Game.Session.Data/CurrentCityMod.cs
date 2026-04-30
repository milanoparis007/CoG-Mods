using System.Collections.Generic;
using Game.Services.Maps;
using SomaSim.Util;

namespace Game.Session.Data;

public class CurrentCityMod : BaseDeltaMultiplierModifier
{
	public enum Test
	{
		Any,
		None
	}

	public enum Check
	{
		Citytype,
		IdOnly
	}

	public Test @is;

	public List<string> of;

	public Check check;

	public override ModQueryElement QueryMustProvide => ModQueryElement.Nothing;

	public override bool DoesPass(ModQuery _)
	{
		MapConfig mapconfig = Game.ctx.session.mapconfig;
		string item = ((check == Check.Citytype) ? mapconfig.citytype : mapconfig.id);
		bool flag = of.Contains(item);
		if (@is != Test.Any)
		{
			return !flag;
		}
		return flag;
	}

	public override string Explain(ModQuery query, Fixnum delta)
	{
		return null;
	}
}
