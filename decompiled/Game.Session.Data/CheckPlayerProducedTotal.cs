using Game.Core;
using Game.Services;
using SomaSim.Util;

namespace Game.Session.Data;

public class CheckPlayerProducedTotal : VisitWithValueRequirement
{
	public Label resource;

	protected override Fixnum GetCurrentValue(VisitState visit)
	{
		return GetPlayer(visit).territory.FindInProductionHistory(resource);
	}

	public override ReqExplanation Explain(VisitState visit)
	{
		string text = Resource.Find(resource)?.GetIconNameAndUnits(value);
		string message = Loc.Get("ui.requirements.produced-qty", "resource-and-qty", text);
		return new ReqExplanation(DoesPass(visit), message);
	}
}
