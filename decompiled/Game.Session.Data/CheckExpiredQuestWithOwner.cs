namespace Game.Session.Data;

public sealed class CheckExpiredQuestWithOwner : AbstractVisitRequirement
{
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
		return Game.ctx.quests.FindWaitingQuestUnsafe(uuid)?.IsExpiredAndWaiting ?? false;
	}

	public override ReqExplanation Explain(VisitState visit)
	{
		return new ReqExplanation(DoesPass(visit), null);
	}
}
