using Game.Session.Quests;

namespace Game.Session.Data;

public sealed class CheckCompletedQuestFrom : AbstractVisitRequirement
{
	public enum MemberOf
	{
		Family,
		CloseFamily
	}

	public MemberOf memberof;

	public override bool DoesPass(VisitState visit)
	{
		RelationshipList listOrNull = Game.ctx.simman.rels.GetListOrNull(visit.npc.Id);
		QuestManager quests = Game.ctx.quests;
		foreach (Relationship datum in listOrNull.data)
		{
			if (((memberof == MemberOf.CloseFamily) ? datum.IsCloseFamily : datum.IsAnyFamily) && quests.HasCompletedQuestForTarget(datum.to))
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
