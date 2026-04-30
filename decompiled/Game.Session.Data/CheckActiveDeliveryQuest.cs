using Game.Services;
using Game.Session.Quests;

namespace Game.Session.Data;

public sealed class CheckActiveDeliveryQuest : AbstractVisitRequirement
{
	public enum With
	{
		Anyone,
		Owner
	}

	public enum DeliveryType
	{
		Any,
		Manual
	}

	public With with;

	public DeliveryType type;

	public override bool DoesPass(VisitState visit)
	{
		if (visit?.npc == null)
		{
			return false;
		}
		if (!visit.pid.IsHumanPlayer)
		{
			return false;
		}
		foreach (QuestActiveRecord value in Game.ctx.quests.GetQuestDataUnsafe().active.Values)
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
		string message = Loc.Get("ui.requirements.deliveries");
		return new ReqExplanation(DoesPass(visit), message);
	}
}
