using Game.Core;
using Game.Services;
using Game.Session.Entities;

namespace Game.Session.Data;

public sealed class CheckNpcGangStolePlayerOutpost : AbstractVisitRequirement
{
	public bool expected;

	public override bool DoesPass(VisitState visit)
	{
		PlayerID pid = visit.npc.data.agent.pid;
		if (pid.IsNotAnyPlayer)
		{
			return false;
		}
		return GetPlayer(visit).social.ContainsSocialActionBy(pid, SocialConstants.HAS_STOLEN_OUTPOST) == expected;
	}

	public override ReqExplanation Explain(VisitState visit)
	{
		string message = (expected ? Loc.Get("ui.requirements.ai.stoleoutpost.expected") : Loc.Get("ui.requirements.ai.stoleoutpost.unexpected"));
		return new ReqExplanation(DoesPass(visit), message);
	}
}
