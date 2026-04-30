using Game.Core;
using Game.Services;
using SomaSim.Util;

namespace Game.Session.Data;

public class CheckPlayerHasTooMany : VisitWithValueRequirement
{
	public Label resource;

	protected override Fixnum GetCurrentValue(VisitState visit)
	{
		return GetPlayer(visit).territory.FindInStorageTotal(resource);
	}

	public override ReqExplanation Explain(VisitState visit)
	{
		string text = Resource.Find(resource)?.GetIconNameAndUnits(value);
		string message = Loc.Get("ui.requirements.has-too-many", "resource-and-qty", text);
		return new ReqExplanation(DoesPass(visit), message);
	}
}
