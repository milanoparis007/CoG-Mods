using Game.Core;
using Game.Services;
using Game.Session.Player;

namespace Game.Session.Data;

public sealed class CheckGangConflictTrucedays : AbstractVisitRequirement
{
	private PlayerInfo GetNpcPlayer(VisitState visit)
	{
		return visit.npc?.components.agent?.GetPlayer();
	}

	public override bool DoesPass(VisitState visit)
	{
		PlayerInfo npcPlayer = GetNpcPlayer(visit);
		if (npcPlayer == null || !npcPlayer.IsGangOrGoon)
		{
			return false;
		}
		var (flag, _, flag2) = npcPlayer.ai.combat.GetMinTimeToStartTruce(visit.pid);
		return flag && flag2;
	}

	public override ReqExplanation Explain(VisitState visit)
	{
		(bool isAggro, SimTime time, bool ready) minTimeToStartTruce = GetNpcPlayer(visit).ai.combat.GetMinTimeToStartTruce(visit.pid);
		bool item = minTimeToStartTruce.isAggro;
		SimTime item2 = minTimeToStartTruce.time;
		string message = (item ? Loc.Get("ui.requirements.gang-conflict-days", "date", Loc.FormatDateLong(item2)) : null);
		return new ReqExplanation(DoesPass(visit), message);
	}
}
