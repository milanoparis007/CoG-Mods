namespace Game.Session.Data;

public class CheckConvoDemandType : AbstractVisitRequirement
{
	public Demand.Type has;

	public bool expected = true;

	public override bool DoesPass(VisitState visit)
	{
		return (Game.ctx.simman.demands.FindOrNull(visit)?.HasDemandSet(has) ?? false) == expected;
	}

	public override ReqExplanation Explain(VisitState visit)
	{
		return new ReqExplanation(DoesPass(visit), null);
	}
}
