using Game.Session.Player;

namespace Game.Session.Data;

public class CheckIsConvoGoonSpecial : AbstractVisitRequirement
{
	public bool expected;

	public override bool DoesPass(VisitState visit)
	{
		PlayerInfo playerInfo = visit.npc?.data.agent?.pid.FindPlayer();
		if (playerInfo == null || !playerInfo.IsJustGoon)
		{
			return false;
		}
		return playerInfo.ai.goon.IsSpecial() == expected;
	}

	public override ReqExplanation Explain(VisitState visit)
	{
		return new ReqExplanation(DoesPass(visit), null);
	}
}
