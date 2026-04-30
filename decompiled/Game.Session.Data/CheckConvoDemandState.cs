using Game.Services;

namespace Game.Session.Data;

public class CheckConvoDemandState : AbstractVisitRequirement
{
	public enum CheckType
	{
		Set,
		Notset
	}

	public CheckType @is;

	public Demand.State to;

	public override bool DoesPass(VisitState visit)
	{
		(bool present, Demand.State state) tuple = Game.ctx.simman.demands.FindDemandState(visit);
		bool item = tuple.present;
		Demand.State item2 = tuple.state;
		Demand.State state = (item ? item2 : Demand.State.Neutral);
		if (@is != CheckType.Set)
		{
			return state != to;
		}
		return state == to;
	}

	public override ReqExplanation Explain(VisitState visit)
	{
		bool flag = @is == CheckType.Set;
		return new ReqExplanation(message: to switch
		{
			Demand.State.Compliant => flag ? Loc.Get("ui.requirements.demands.compliant.expected") : Loc.Get("ui.requirements.demands.compliant.unexpected"), 
			Demand.State.Defiant => flag ? Loc.Get("ui.requirements.demands.defiant.expected") : Loc.Get("ui.requirements.demands.defiant.unexpected"), 
			_ => flag ? Loc.Get("ui.requirements.demands.neutral.expected") : Loc.Get("ui.requirements.demands.neutral.unexpected"), 
		}, passed: DoesPass(visit));
	}
}
