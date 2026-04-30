using Game.Core;
using Game.Services;
using Game.Session.Sim;
using SomaSim.Util;

namespace Game.Session.Data;

public sealed class CanPayForCampaignAction : AbstractVisitRequirement
{
	public Label actionId;

	public override bool DoesPass(VisitState visit)
	{
		Fixnum warchestCost = Game.serv.globals.settings.politics.FindPlayerCandidateAction(actionId).warchestCost;
		Ward wardForBuilding = Game.ctx.simman.politics.GetWardForBuilding(visit.building.Id);
		if (wardForBuilding == null)
		{
			return false;
		}
		return wardForBuilding.currElection.GetCandidateWarchest(visit.npc.Id) >= warchestCost;
	}

	public override ReqExplanation Explain(VisitState visit)
	{
		Fixnum warchestCost = Game.serv.globals.settings.politics.FindPlayerCandidateAction(actionId).warchestCost;
		Ward wardForBuilding = Game.ctx.simman.politics.GetWardForBuilding(visit.building.Id);
		if (wardForBuilding == null)
		{
			return new ReqExplanation(DoesPass(visit), Loc.Get("ui.requirements.campaign-action-cost", "cost", Loc.Money(warchestCost), "value", Loc.Money(0)));
		}
		Fixnum candidateWarchest = wardForBuilding.currElection.GetCandidateWarchest(visit.npc.Id);
		return new ReqExplanation(DoesPass(visit), Loc.Get("ui.requirements.campaign-action-cost", "cost", Loc.Money(warchestCost), "value", Loc.Money(candidateWarchest)));
	}
}
