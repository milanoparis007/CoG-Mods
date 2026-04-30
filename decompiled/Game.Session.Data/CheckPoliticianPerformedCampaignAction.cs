using System.Collections.Generic;
using Game.Core;
using Game.Session.Sim;

namespace Game.Session.Data;

public sealed class CheckPoliticianPerformedCampaignAction : AbstractVisitRequirement
{
	public Label id;

	public bool expected;

	public override bool DoesPass(VisitState visit)
	{
		Ward wardForBuilding = Game.ctx.simman.politics.GetWardForBuilding(visit.building.Id);
		if (wardForBuilding == null)
		{
			return false;
		}
		EntityID entityID = ((Game.ctx.simman.politics.GetPoliticianData(visit.npc.Id) != null) ? visit.npc.Id : wardForBuilding.currentPolitician);
		List<Election.ElectionEvent> list = (wardForBuilding.ElectionOngoing ? wardForBuilding.currElection.electionLog : wardForBuilding.mostRecentElectionResult.events);
		if (list == null)
		{
			return !expected;
		}
		foreach (Election.ElectionEvent item in list)
		{
			if (item.id == id && item.candidate == entityID)
			{
				return expected;
			}
		}
		return !expected;
	}

	public override ReqExplanation Explain(VisitState visit)
	{
		return new ReqExplanation(DoesPass(visit), null);
	}
}
