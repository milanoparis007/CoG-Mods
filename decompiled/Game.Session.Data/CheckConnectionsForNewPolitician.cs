using Game.Services;

namespace Game.Session.Data;

public sealed class CheckConnectionsForNewPolitician : AbstractVisitRequirement
{
	public override bool DoesPass(VisitState visit)
	{
		return Game.ctx.simman.politics.GetBestPolCandidateFrom(visit.npc.Id).IsValid;
	}

	public override ReqExplanation Explain(VisitState visit)
	{
		return new ReqExplanation(DoesPass(visit), Loc.Get("ui.requirements.connections.none"));
	}
}
