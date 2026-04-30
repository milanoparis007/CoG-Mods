using Game.Core;
using Game.UI.Session.Convo;

namespace Game.Session.Data;

public class SpecialCanPayForVehicleOnSale : AbstractSpecialCanPayButton
{
	public override Price? FindPrice(VisitState visit, ConvoButtonState bstate)
	{
		if (!(bstate.data is ConvoDataVehicleSales convoDataVehicleSales))
		{
			return null;
		}
		return convoDataVehicleSales.info.salePrice;
	}
}
