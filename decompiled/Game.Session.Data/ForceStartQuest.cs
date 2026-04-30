using Game.Services;
using Game.Session.Quests;

namespace Game.Session.Data;

public class ForceStartQuest : VisitGrant
{
	public string id;

	public override GrantReq RequiredContext => GrantReq.PlayerID | GrantReq.VisitState | GrantReq.VisitNpc | GrantReq.QuestUUID;

	public override void Apply(GrantContext ctx)
	{
		QuestDefinition questDefinition = Game.ctx.quests.FindQuestDefinition(id);
		QuestCompletedRecord questCompletedRecord = Game.ctx.quests.FindCompletedQuestUnsafe(ctx.quuid);
		if (questDefinition == null || questCompletedRecord == null)
		{
			Logger.Warning($"Cound not find just completed quest {ctx.quuid}, unable to start a new one of type {id}");
			return;
		}
		Game.ctx.quests.Requests.OnGrantStartingARequest(questCompletedRecord.target, questDefinition);
		Game.ctx.quests.StartQuest(id, questCompletedRecord.target, fromRequest: true);
	}

	public override string Describe(GrantContext ctx)
	{
		QuestDefinition questDefinition = Game.ctx.quests.FindQuestDefinition(id);
		return Loc.Get("ui.grants.forcestartquest.describe", "name", Loc.Get(questDefinition?.locname));
	}
}
