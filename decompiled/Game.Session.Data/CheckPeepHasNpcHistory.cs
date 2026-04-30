using Game.Services;
using Game.Session.Entities;

namespace Game.Session.Data;

public sealed class CheckPeepHasNpcHistory : AbstractVisitRequirement
{
	public override bool DoesPass(VisitState visit)
	{
		Entity npc = visit.npc;
		RelationshipList listOrNull = Game.ctx.simman.rels.GetListOrNull(npc.Id);
		if (listOrNull == null)
		{
			return false;
		}
		foreach (Relationship datum in listOrNull.data)
		{
			if (datum.IsRelToAIWithSocialHistory())
			{
				return true;
			}
		}
		return false;
	}

	public override ReqExplanation Explain(VisitState visit)
	{
		return new ReqExplanation(DoesPass(visit), Loc.Get("ui.requirements.ai.history"));
	}
}
