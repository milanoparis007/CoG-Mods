using Game.Services;
using Game.Session.Player;
using Game.Session.Player.AI;

namespace Game.Session.Data;

public sealed class GoonLootIsInState : AbstractVisitRequirement
{
	public GoonLootState state;

	public override bool DoesPass(VisitState visit)
	{
		return visit.npc?.data.agent?.pid.FindPlayer().ai.goon?.LootStatus == state;
	}

	public override ReqExplanation Explain(VisitState visit)
	{
		return new ReqExplanation(DoesPass(visit), Loc.Get("ui.requirements.no-rewards"));
	}
}
