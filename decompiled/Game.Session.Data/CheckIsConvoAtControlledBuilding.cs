using Game.Services;

namespace Game.Session.Data;

public class CheckIsConvoAtControlledBuilding : AbstractVisitRequirement
{
	public bool expected;

	public override bool DoesPass(VisitState visit)
	{
		return GetPlayer(visit).territory.IsControlled(visit.building) == expected;
	}

	public override ReqExplanation Explain(VisitState visit)
	{
		return new ReqExplanation(DoesPass(visit), expected ? Loc.Get("ui.requirements.controlled.expected") : Loc.Get("ui.requirements.controlled.unexpected"));
	}
}
