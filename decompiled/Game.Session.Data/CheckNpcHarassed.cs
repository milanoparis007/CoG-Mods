using Game.Services;
using Game.Session.Player;

namespace Game.Session.Data;

public sealed class CheckNpcHarassed : AbstractVisitRequirement
{
	public override bool DoesPass(VisitState visit)
	{
		PlayerInfo player = visit.npc.components.agent.GetPlayer();
		if (player == null)
		{
			return false;
		}
		return (player.ai?.goon)?.CanPlayerAskUsToStopHarassing(visit.pid) ?? false;
	}

	public override ReqExplanation Explain(VisitState visit)
	{
		return new ReqExplanation(DoesPass(visit), Loc.Get("ui.requirements.ai.hasharrassed.none"));
	}
}
