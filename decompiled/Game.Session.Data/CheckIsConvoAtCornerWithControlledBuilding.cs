using Game.Core;
using Game.Services;
using Game.Session.Entities;

namespace Game.Session.Data;

public class CheckIsConvoAtCornerWithControlledBuilding : AbstractVisitRequirement
{
	public bool expected;

	public override bool DoesPass(VisitState visit)
	{
		return HasControlledBuilding(visit) == expected;
	}

	private bool HasControlledBuilding(VisitState visit)
	{
		Node bldgNode = visit.GetBldgNode();
		PlayerID pid = visit.pid;
		foreach (EntityID item in bldgNode.contained)
		{
			if (item.FindEntity()?.components.building?.IsControlledBy(pid) == true)
			{
				return true;
			}
		}
		return false;
	}

	public override ReqExplanation Explain(VisitState visit)
	{
		return new ReqExplanation(DoesPass(visit), expected ? Loc.Get("ui.requirements.controlled.corner.expected") : Loc.Get("ui.requirements.controlled.corner.unexpected"));
	}
}
