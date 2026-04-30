using Game.Core;
using Game.Services;

namespace Game.Session.Data;

public class CheckNumCrewInRole : AbstractVisitRequirement
{
	public Label id;

	public Test @is;

	public int value;

	public override bool DoesPass(VisitState visit)
	{
		return ValueUtil.TestCurrentValue(visit.GetPlayer().crew.GetNumCrewInRole(id), @is, value);
	}

	public override ReqExplanation Explain(VisitState visit)
	{
		RoleDef roleById = Game.serv.globals.settings.people.social.crew.roleSettings.GetRoleById(id);
		return new ReqExplanation(DoesPass(visit), Loc.Get("ui.requirements.crew-role", "role", Loc.Get(roleById.loctitle)));
	}
}
