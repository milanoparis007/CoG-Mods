using Game.Core;
using Game.Services;
using Game.Session.Entities;

namespace Game.Session.Data;

public class CheckCrewConvoRole : AbstractVisitRequirement
{
	public Label id;

	public override bool DoesPass(VisitState visit)
	{
		Entity npc = visit.npc;
		RoleDef roleById = Game.serv.globals.settings.people.social.crew.roleSettings.GetRoleById(id);
		RoleDef crewRole = npc.data.agent.xp.GetCrewRole();
		return roleById == crewRole;
	}

	public override ReqExplanation Explain(VisitState visit)
	{
		return new ReqExplanation(DoesPass(visit), null);
	}
}
