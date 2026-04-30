using Game.Core;
using Game.Services;
using Game.Session.Player;

namespace Game.Session.Data;

public class CheckHasOutpost : AbstractVisitRequirement
{
	public enum LocType
	{
		ThisBuilding,
		ThisNode,
		ThisNodeOrNearby
	}

	public LocType location;

	public bool expected;

	public override bool DoesPass(VisitState visit)
	{
		Node bldgNode = visit.GetBldgNode();
		PlayerOutposts outposts = GetPlayer(visit).outposts;
		switch (location)
		{
		case LocType.ThisBuilding:
			return outposts.GetOutpostAtNode(bldgNode).buildingId == visit.building.Id == expected;
		case LocType.ThisNode:
			return outposts.GetOutpostAtNode(bldgNode).IsValid == expected;
		case LocType.ThisNodeOrNearby:
			return outposts.HasOutpostNear(bldgNode) == expected;
		default:
			Logger.Warning("Unknown location", location);
			return false;
		}
	}

	public override ReqExplanation Explain(VisitState visit)
	{
		string message = null;
		switch (location)
		{
		case LocType.ThisNode:
			message = (expected ? Loc.Get("ui.requirements.hasoutpost.thisnode.expected") : Loc.Get("ui.requirements.hasoutpost.thisnode.unexpected"));
			break;
		case LocType.ThisNodeOrNearby:
			message = (expected ? Loc.Get("ui.requirements.hasoutpost.nearby.expected") : Loc.Get("ui.requirements.hasoutpost.nearby.unexpected"));
			break;
		}
		return new ReqExplanation(DoesPass(visit), message);
	}
}
