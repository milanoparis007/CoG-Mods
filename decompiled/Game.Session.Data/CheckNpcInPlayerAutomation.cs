using Game.Services;

namespace Game.Session.Data;

public sealed class CheckNpcInPlayerAutomation : AbstractVisitRequirement
{
	public bool expected;

	public bool paused = true;

	public bool active = true;

	public override bool DoesPass(VisitState visit)
	{
		if (!visit.AtValidBiz)
		{
			return false;
		}
		return GetPlayer(visit).automation.CountForTarget(visit.building.Id, active, paused) > 0 == expected;
	}

	public override ReqExplanation Explain(VisitState visit)
	{
		return new ReqExplanation(DoesPass(visit), Loc.Get("ui.requirements.ai.trades.none"));
	}
}
