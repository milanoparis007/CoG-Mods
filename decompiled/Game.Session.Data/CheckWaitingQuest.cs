using Game.Session.Quests;

namespace Game.Session.Data;

public sealed class CheckWaitingQuest : AbstractVisitRequirement
{
	public enum With
	{
		Anyone,
		Owner
	}

	public With with;

	public bool expected;

	public override bool DoesPass(VisitState visit)
	{
		return HasWaitingQuest(visit) == expected;
	}

	private bool HasWaitingQuest(VisitState visit)
	{
		if (visit?.npc == null)
		{
			return false;
		}
		if (!visit.pid.IsHumanPlayer)
		{
			return false;
		}
		foreach (QuestWaitingRecord value in Game.ctx.quests.GetQuestDataUnsafe().waiting.Values)
		{
			if (with == With.Anyone || value.target == visit.npc.Id)
			{
				return true;
			}
		}
		return false;
	}

	public override ReqExplanation Explain(VisitState visit)
	{
		return new ReqExplanation(DoesPass(visit), null);
	}
}
