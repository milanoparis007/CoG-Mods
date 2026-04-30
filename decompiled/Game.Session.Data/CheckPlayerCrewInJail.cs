using System.Linq;
using Game.Services;
using Game.Session.Sim;
using SomaSim.Util;

namespace Game.Session.Data;

public class CheckPlayerCrewInJail : VisitWithValueRequirement
{
	public enum Select
	{
		InJail,
		InJailBribed,
		InJailNotBribed
	}

	public Select select;

	protected override Fixnum GetCurrentValue(VisitState visit)
	{
		return (from e in Game.ctx.simman.cops.FindAllArrestsFor(visit.pid)
			where @select switch
			{
				Select.InJailBribed => e.paidOff, 
				Select.InJailNotBribed => !e.paidOff, 
				_ => true, 
			}
			select e).Count();
	}

	public override ReqExplanation Explain(VisitState visit)
	{
		string key = ((select == Select.InJailBribed) ? "ui.requirements.in-jail-bribed" : ((select == Select.InJailNotBribed) ? "ui.requirements.in-jail-not-bribed" : "ui.requirements.in-jail"));
		return new ReqExplanation(DoesPass(visit), Loc.Get(key));
	}
}
