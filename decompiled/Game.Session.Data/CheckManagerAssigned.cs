using Game.Services;
using Game.Session.Sim.Modules;

namespace Game.Session.Data;

public class CheckManagerAssigned : AbstractVisitRequirement
{
	public bool expected;

	public override bool DoesPass(VisitState visit)
	{
		return ModulesUtil.GetManagerOrNull(visit.building).manager != null;
	}

	public override ReqExplanation Explain(VisitState visit)
	{
		string message = (expected ? Loc.Get("ui.requirements.manager.expected") : Loc.Get("ui.requirements.manager.unexpected"));
		return new ReqExplanation(DoesPass(visit), message);
	}
}
