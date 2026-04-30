namespace Game.Session.Data;

public class SpecialMustSupportOutpost : AbstractVisitRequirement
{
	public override bool DoesPass(VisitState visit)
	{
		return GetPlayer(visit).outposts.GetOutpostEntryUnsafe(visit.building)?.money.NeedsSupport ?? false;
	}

	public override ReqExplanation Explain(VisitState visit)
	{
		return new ReqExplanation(DoesPass(visit), null);
	}
}
