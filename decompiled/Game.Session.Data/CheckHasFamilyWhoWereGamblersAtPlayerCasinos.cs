using Game.Session.Entities;
using Game.Session.Player;

namespace Game.Session.Data;

public sealed class CheckHasFamilyWhoWereGamblersAtPlayerCasinos : AbstractVisitRequirement
{
	public bool expected = true;

	public override bool DoesPass(VisitState visit)
	{
		PlayerInfo player = visit.GetPlayer();
		Entity npc = visit.npc;
		return player.gambling.HasFormerGamblerRelative(npc.Id) == expected;
	}

	public override ReqExplanation Explain(VisitState visit)
	{
		return new ReqExplanation(DoesPass(visit), null);
	}
}
