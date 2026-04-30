using Game.Core;
using Game.Session.Sim;

namespace Game.Session.Data;

public sealed class CheckPoliticalRel : AbstractVisitRequirement
{
	public PoliticalRelationshipType type;

	public override bool DoesPass(VisitState visit)
	{
		Ward wardForBuilding = Game.ctx.simman.politics.GetWardForBuilding(visit.building.Id);
		if (wardForBuilding == null)
		{
			return false;
		}
		PoliticalRelationshipType politicalRelationshipType;
		if (wardForBuilding.ElectionOngoing)
		{
			bool flag = wardForBuilding.currElection.HasSponsoredACandidate(visit.pid);
			EntityID sponsoredCandidate = wardForBuilding.currElection.GetSponsoredCandidate(visit.pid);
			if (wardForBuilding.currElection.candidateStats[visit.npc.Id] == null)
			{
				return false;
			}
			politicalRelationshipType = (flag ? ((sponsoredCandidate == visit.npc.Id) ? PoliticalRelationshipType.Sponsored : PoliticalRelationshipType.Opponent) : PoliticalRelationshipType.None);
		}
		else
		{
			politicalRelationshipType = Game.ctx.simman.politics.GetPoliticianData(visit.npc.Id)?.relToHumanInLastElection ?? PoliticalRelationshipType.None;
		}
		return type == politicalRelationshipType;
	}

	public override ReqExplanation Explain(VisitState visit)
	{
		return new ReqExplanation(DoesPass(visit), null);
	}
}
