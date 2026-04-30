using System.Collections.Generic;
using Game.Services;

namespace Game.Session.Data;

public class CheckCurrentMonth : AbstractVisitRequirement
{
	public enum Test
	{
		Any,
		None
	}

	public Test @is;

	public List<int> of;

	public override bool DoesPass(VisitState _)
	{
		bool flag = Game.ctx.clock.IsMonthOneOf(of);
		if (@is != Test.Any)
		{
			return !flag;
		}
		return flag;
	}

	public override ReqExplanation Explain(VisitState visit)
	{
		bool num = DoesPass(visit);
		string key = (num ? "ui.requirements.month.pass" : "ui.requirements.month.fail");
		return new ReqExplanation(num, Loc.Get(key));
	}
}
