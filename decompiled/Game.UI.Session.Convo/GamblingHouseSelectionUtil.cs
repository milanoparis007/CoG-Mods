using Game.Core;
using Game.Session.Data;
using Game.Session.Entities;

namespace Game.UI.Session.Convo;

public static class GamblingHouseSelectionUtil
{
	public static ConvoDataGamblingHouseSelection MakeCasinoBuildData(VisitState visit)
	{
		return new ConvoDataGamblingHouseSelection(BuildingUtil.GetPotentialGamblingHousesOnVisit(visit), Label.NULL);
	}
}
