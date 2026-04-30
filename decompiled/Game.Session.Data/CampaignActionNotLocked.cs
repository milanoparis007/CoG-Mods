using Game.Core;
using Game.Services;
using Game.Session.Sim;

namespace Game.Session.Data;

public sealed class CampaignActionNotLocked : AbstractVisitRequirement
{
	public Label actionId;

	public override bool DoesPass(VisitState visit)
	{
		PoliticsSettings.CandidateAction action = Game.serv.globals.settings.politics.FindPlayerCandidateAction(actionId);
		Ward wardForBuilding = Game.ctx.simman.politics.GetWardForBuilding(visit.building.Id);
		if (wardForBuilding == null)
		{
			return false;
		}
		return !wardForBuilding.currElection.ActionLockedByUsage(visit.npc.Id, action);
	}

	public override ReqExplanation Explain(VisitState visit)
	{
		PoliticsSettings.CandidateAction candidateAction = Game.serv.globals.settings.politics.FindPlayerCandidateAction(actionId);
		return new ReqExplanation(DoesPass(visit), candidateAction.oncePerCandidate ? Loc.Get("ui.requirements.campaign-action-locked.candidate") : (candidateAction.oncePerElection ? Loc.Get("ui.requirements.campaign-action.locked.election") : Loc.Get("ui.requirements.campaign-action.locked.none")));
	}
}
