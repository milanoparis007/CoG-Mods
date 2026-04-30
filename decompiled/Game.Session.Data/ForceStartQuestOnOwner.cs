using Game.Core;
using Game.Services;

namespace Game.Session.Data;

public class ForceStartQuestOnOwner : VisitGrant
{
	public string id;

	public override GrantReq RequiredContext => GrantReq.PlayerID | GrantReq.VisitState | GrantReq.VisitNpc;

	public override void Apply(GrantContext ctx)
	{
		QuestDefinition def = Game.ctx.quests.FindQuestDefinition(id);
		EntityID entityID = ctx.visit.npc.Id;
		Game.ctx.quests.Requests.OnGrantStartingARequest(entityID, def);
		Game.ctx.quests.StartQuest(id, entityID, fromRequest: true);
	}

	public override string Describe(GrantContext ctx)
	{
		QuestDefinition questDefinition = Game.ctx.quests.FindQuestDefinition(id);
		return Loc.Get("ui.grants.forcestartquest.describe", "name", Loc.Get(questDefinition?.locname));
	}
}
