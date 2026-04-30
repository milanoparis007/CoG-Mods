using Game.Services;
using Game.Session.Player;

namespace Game.Session.Data;

public sealed class CheckGangConflictTruce : AbstractVisitRequirement
{
	public bool expected;

	public override bool DoesPass(VisitState visit)
	{
		PlayerInfo playerInfo = visit.npc?.components.agent?.GetPlayer();
		if (playerInfo == null || !playerInfo.IsGangOrGoon)
		{
			return false;
		}
		var (flag, flag2) = playerInfo.ai.combat.GetAggroAndTruce(visit.pid);
		if (flag)
		{
			return flag2 == expected;
		}
		return false;
	}

	public override ReqExplanation Explain(VisitState visit)
	{
		string key = (expected ? "ui.requirements.gang-conflict-truce-expected" : "ui.requirements.gang-conflict-truce-not-expected");
		return new ReqExplanation(DoesPass(visit), Loc.Get(key));
	}
}
