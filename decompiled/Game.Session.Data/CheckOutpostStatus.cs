using Game.Services;
using Game.Session.Player;

namespace Game.Session.Data;

public class CheckOutpostStatus : AbstractVisitRequirement
{
	public enum StatusType
	{
		ReadyToCollect,
		CanCrewCollect
	}

	public StatusType @is;

	public override bool DoesPass(VisitState visit)
	{
		PlayerOutposts outposts = GetPlayer(visit).outposts;
		OutpostID outpostAt = outposts.GetOutpostAt(visit.building);
		if (outpostAt.IsNotValid)
		{
			return false;
		}
		return @is switch
		{
			StatusType.ReadyToCollect => outposts.IsOutpostReadyForCollection(outpostAt), 
			StatusType.CanCrewCollect => outposts.CanCrewCollectFromOutpost(visit, outpostAt), 
			_ => false, 
		};
	}

	public override ReqExplanation Explain(VisitState visit)
	{
		string message = ((@is == StatusType.ReadyToCollect) ? Loc.Get("ui.requirements.outpost.status.ready") : ((@is == StatusType.CanCrewCollect) ? Loc.Get("ui.requirements.outpost.status.crew") : Loc.Get("ui.requirements.outpost.status.default")));
		return new ReqExplanation(DoesPass(visit), message);
	}
}
