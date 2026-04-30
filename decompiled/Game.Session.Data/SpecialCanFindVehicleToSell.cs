using Game.Services;
using Game.UI.Session.Convo;

namespace Game.Session.Data;

public class SpecialCanFindVehicleToSell : IConvoButtonRequirement, IRequirement
{
	public bool DoesPass(VisitState visit, ConvoButtonState bstate)
	{
		if (!(bstate.data is ConvoDataVehicleSales convoDataVehicleSales))
		{
			return false;
		}
		return convoDataVehicleSales.foundVehicle;
	}

	public ReqExplanation Explain(VisitState visit, ConvoButtonState bstate)
	{
		return new ReqExplanation(DoesPass(visit, bstate), Loc.Get("ui.requirements.vehicle.none"));
	}
}
