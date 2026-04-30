using Game.Session.Quests;

namespace Game.Session.Data;

public sealed class CheckHasReverseTimeoutQuestWithOwner : AbstractVisitRequirement
{
	public bool expected;

	public override bool DoesPass(VisitState visit)
	{
		if (visit?.npc == null)
		{
			return false;
		}
		QuestUUID uuid = Game.ctx.quests.FindWaitingQuestForTarget(visit.npc.Id);
		if (uuid.IsNotSet)
		{
			return false;
		}
		QuestWaitingRecord questWaitingRecord = Game.ctx.quests.FindWaitingQuestUnsafe(uuid);
		return expected == (questWaitingRecord?.FindQuestDefinition()?.unhappyOnSuccess == true);
	}

	public override ReqExplanation Explain(VisitState visit)
	{
		return new ReqExplanation(DoesPass(visit), null);
	}
}
