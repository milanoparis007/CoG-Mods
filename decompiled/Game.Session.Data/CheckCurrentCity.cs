using System.Collections.Generic;
using Game.Services;
using Game.Services.Maps;

namespace Game.Session.Data;

public class CheckCurrentCity : AbstractVisitRequirement
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

	public override bool DoesPass(VisitState _)
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

	public override ReqExplanation Explain(VisitState visit)
	{
		bool num = DoesPass(visit);
		string key = (num ? "ui.requirements.city.true" : "ui.requirements.city.false");
		return new ReqExplanation(num, Loc.Get(key));
	}
}
