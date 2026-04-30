using System.Collections.Generic;
using Game.Core;
using Game.Services;
using Game.Session.Player;

namespace Game.Session.Data;

public abstract class AbstractCheckHumanAndPlayerHistory : AbstractVisitRequirement
{
	public List<Label> labelled;

	protected abstract Relationship GetRelationship(PlayerInfo visitingPlayer, PlayerInfo targetPlayer);

	public override bool DoesPass(VisitState visit)
	{
		PlayerInfo targetPlayer = visit.npc.data.agent.pid.FindPlayer();
		SocialHistoryData socialHistoryData = GetRelationship(GetPlayer(visit), targetPlayer)?.GetHistoryOrNull();
		if (socialHistoryData == null)
		{
			return false;
		}
		foreach (SocialActionInfo item in socialHistoryData.items)
		{
			if (labelled.Contains(item.defid))
			{
				return true;
			}
		}
		return false;
	}

	public override ReqExplanation Explain(VisitState visit)
	{
		return new ReqExplanation(DoesPass(visit), null);
	}
}
