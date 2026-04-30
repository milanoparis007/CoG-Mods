using System.Collections.Generic;
using Game.Core;
using Game.Services;

namespace Game.Session.Data;

public sealed class CheckPeepHasHistoryElement : AbstractVisitRequirement
{
	public List<Label> labelled;

	public override bool DoesPass(VisitState visit)
	{
		RelationshipList listOrNull = Game.ctx.simman.rels.GetListOrNull(visit.npc.Id);
		if (listOrNull == null)
		{
			return false;
		}
		foreach (Relationship datum in listOrNull.data)
		{
			if (datum.IsRelToAIWithSocialHistory() && datum.GetHistoryOrNull().CountActions(labelled) > 0)
			{
				return true;
			}
		}
		return false;
	}

	public override ReqExplanation Explain(VisitState visit)
	{
		return new ReqExplanation(DoesPass(visit), Loc.Get("ui.requirements.player.history.element"));
	}
}
