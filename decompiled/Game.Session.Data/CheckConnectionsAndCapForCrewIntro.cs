using Game.Services;
using Game.Session.Player;

namespace Game.Session.Data;

public class CheckConnectionsAndCapForCrewIntro : AbstractVisitRequirement
{
	public bool skipConnections;

	public bool skipCap;

	public override bool DoesPass(VisitState visit)
	{
		PlayerCrew crew = GetPlayer(visit).crew;
		bool flag = skipConnections || crew.CrewGrowth.HasAnyCrewCandidatesFrom(visit.npc.Id);
		return (skipCap || crew.CrewFreeCapacity() >= 1) && flag;
	}

	public override ReqExplanation Explain(VisitState visit)
	{
		bool flag = Game.ctx.players.WithID(visit.pid).crew.CrewFreeCapacity() >= 1;
		return new ReqExplanation(DoesPass(visit), flag ? Loc.Get("ui.requirements.connections.none") : Loc.Get("ui.requirements.connections.territory.size"));
	}
}
