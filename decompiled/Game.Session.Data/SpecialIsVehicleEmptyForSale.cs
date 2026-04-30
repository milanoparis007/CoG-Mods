using Game.Services;
using Game.UI.Session.Convo;

namespace Game.Session.Data;

public class SpecialIsVehicleEmptyForSale : IConvoButtonRequirement, IRequirement
{
	private bool IsVehicleEmpty(VisitState visit)
	{
		return visit.vehicle?.components.modules.inventory?.data.FindIsEmpty() == true;
	}

	private bool HasMoreVehicles(VisitState visit)
	{
		return visit.GetPlayer().crew.CountVehicles > 1;
	}

	public bool DoesPass(VisitState visit, ConvoButtonState bstate)
	{
		if (HasMoreVehicles(visit))
		{
			return IsVehicleEmpty(visit);
		}
		return false;
	}

	public ReqExplanation Explain(VisitState visit, ConvoButtonState bstate)
	{
		bool passed = DoesPass(visit, bstate);
		string message = ((!HasMoreVehicles(visit)) ? Loc.Get("ui.requirements.checkvehicleforsale.only") : Loc.Get("ui.requirements.checkvehicleforsale.empty"));
		return new ReqExplanation(passed, message);
	}
}
