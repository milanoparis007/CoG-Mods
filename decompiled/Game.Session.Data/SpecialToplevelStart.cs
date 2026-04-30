namespace Game.Session.Data;

public sealed class SpecialToplevelStart : AbstractVisitRequirement
{
	public bool firststate;

	public bool firstconvo;

	public override bool DoesPass(VisitState visit)
	{
		if (firststate)
		{
			return Game.ctx.hud.convoDialog?.Controller?.Model?.shared.statecount == 1;
		}
		if (firstconvo)
		{
			return ((GetPlayer(visit)?.social.GetRelationshipFromSourceToPlayer(visit))?.convos ?? 0) == 1;
		}
		return false;
	}

	public override ReqExplanation Explain(VisitState visit)
	{
		return new ReqExplanation(DoesPass(visit), null);
	}
}
