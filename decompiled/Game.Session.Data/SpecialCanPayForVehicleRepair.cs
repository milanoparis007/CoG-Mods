using Game.Core;
using Game.UI.Session.Convo;

namespace Game.Session.Data;

public class SpecialCanPayForVehicleRepair : AbstractSpecialCanPayButton
{
	public override Price? FindPrice(VisitState visit, ConvoButtonState bstate)
	{
		if (!(bstate.data is ConvoDataVehicleCosts convoDataVehicleCosts))
		{
			return null;
		}
		return convoDataVehicleCosts.repairPrice;
	}
}
