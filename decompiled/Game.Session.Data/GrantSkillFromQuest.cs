using Game.Services;
using Game.Session.Player;

namespace Game.Session.Data;

public class GrantSkillFromQuest : VisitGrant
{
	public override GrantReq RequiredContext => GrantReq.PlayerID | GrantReq.VisitState | GrantReq.VisitNpc | GrantReq.QuestUUID;

	private SkillDef GetSkillDefFromQuest(GrantContext ctx)
	{
		return ctx.pid.FindPlayer().skills.FindSkillForQuest(ctx.quuid);
	}

	public override void Apply(GrantContext ctx)
	{
		SkillDef skillDefFromQuest = GetSkillDefFromQuest(ctx);
		ctx.pid.FindPlayer().skills.DoLearnPaidSkill(ctx.visit, ctx.quuid, skillDefFromQuest.id);
	}

	public override string Describe(GrantContext ctx)
	{
		string text = GetSkillDefFromQuest(ctx)?.GetName();
		return Loc.Get("ui.grants.grantskillfromquest.describe", "name", text);
	}
}
