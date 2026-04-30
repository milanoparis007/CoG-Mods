namespace Game.Session.Data;

public class CheckPlayerNumControlled : AbstractVisitRequirement
{
	public Test @is;

	public int value;

	public override bool DoesPass(VisitState visit)
	{
		return ValueUtil.TestCurrentValue(visit.GetPlayer().territory.CountControlledBuildings(), @is, value);
	}

	public override ReqExplanation Explain(VisitState visit)
	{
		return new ReqExplanation(DoesPass(visit), null);
	}
}
