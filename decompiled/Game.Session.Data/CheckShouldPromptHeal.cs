namespace Game.Session.Data;

public class CheckShouldPromptHeal : AbstractVisitRequirement
{
	public override bool DoesPass(VisitState visit)
	{
		return Game.ctx.players.Human.kb.HealShouldPrompt(visit);
	}

	public override ReqExplanation Explain(VisitState visit)
	{
		return new ReqExplanation(DoesPass(visit), null);
	}
}
