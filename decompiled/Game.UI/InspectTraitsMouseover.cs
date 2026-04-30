using Game.UI.Mouseovers;
using Game.UI.Session;

namespace Game.UI;

public class InspectTraitsMouseover : BaseCustomTextMouseover
{
	protected override string ProduceText()
	{
		if (!(Game.serv.ui.TopPopupUnsafe is CrewPeepInspectPopup crewPeepInspectPopup))
		{
			return null;
		}
		return PersonInfoUtil.GenerateTraitsList(crewPeepInspectPopup.CrewMember, showDesc: true, showDescLong: true);
	}
}
