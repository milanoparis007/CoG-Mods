using Game.Core;
using Game.Services;
using Game.Session.Player;

namespace Game.Session.Data;

public class AddSkill : VisitGrant
{
	public Label id;

	public override GrantReq RequiredContext => GrantReq.PlayerID | GrantReq.VisitState;

	public override void Apply(GrantContext ctx)
	{
		PlayerSkills skills = ctx.GetPlayer().skills;
		if (!skills.HasSkill(id))
		{
			skills.DoLearnPaidSkill(ctx.visit, QuestUUID.EMPTY, id);
		}
	}

	public override string Describe(GrantContext ctx)
	{
		string text = Game.serv.globals.settings.skills.GetSkill(id)?.GetName();
		return Loc.Get("ui.grants.addskill.describe", "name", text);
	}
}
