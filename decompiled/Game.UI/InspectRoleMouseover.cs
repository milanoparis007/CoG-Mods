using Game.Services;
using Game.UI.Mouseovers;

namespace Game.UI;

public class InspectRoleMouseover : BaseCustomTextMouseover
{
	protected override string ProduceText()
	{
		if (!(Game.serv.ui.TopPopupUnsafe is CrewPeepInspectPopup crewPeepInspectPopup))
		{
			return null;
		}
		RoleDef roleDef = crewPeepInspectPopup.CrewMember.data.agent.xp?.GetCrewRole();
		if (roleDef != null)
		{
			return Loc.Get(Game.serv.globals.settings.people.social.crew.roleSettings.GetRoleById(roleDef.id).locdesc);
		}
		if (crewPeepInspectPopup.CrewMember.components.agent.IsBoss().pass)
		{
			return Loc.Get("ui.crewinspect.role.boss.mo");
		}
		if (crewPeepInspectPopup.CrewMember.components.agent.IsCaptain())
		{
			return Loc.Get("ui.crewinspect.role.captain.mo");
		}
		return Loc.Get("ui.crewinspect.role.muscle.mo");
	}
}
