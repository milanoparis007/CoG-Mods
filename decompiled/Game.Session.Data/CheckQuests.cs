using Game.Core;
using Game.Services;
using Game.Session.Quests;

namespace Game.Session.Data;

public class CheckQuests : CheckListOfItems
{
	public enum CheckType
	{
		NeverStarted,
		Active,
		Complete
	}

	public enum CheckScope
	{
		Anyone,
		Owner
	}

	public CheckType status;

	public CheckScope with;

	protected override int Count(VisitState visit)
	{
		int num = 0;
		QuestManager quests = Game.ctx.quests;
		EntityID target = ((with == CheckScope.Owner) ? visit.npc.Id : EntityID.INVALID);
		foreach (Label item in of)
		{
			bool active = quests.IsQuestActiveByID(item.String, target);
			bool waiting = quests.IsQuestWaitingByID(item.String, target);
			bool completed = quests.IsQuestCompletedByID(item.String, target);
			if (DoesMatchExpectedStatus(status, active, waiting, completed))
			{
				num++;
			}
		}
		return num;
	}

	private static bool DoesMatchExpectedStatus(CheckType status, bool active, bool waiting, bool completed)
	{
		switch (status)
		{
		case CheckType.NeverStarted:
			if (!active && !waiting)
			{
				return !completed;
			}
			return false;
		case CheckType.Active:
			return active;
		case CheckType.Complete:
			return completed;
		default:
			return false;
		}
	}

	protected override void VerifyData(VisitState _)
	{
		QuestManager quests = Game.ctx.quests;
		foreach (Label item in of)
		{
			if (quests.FindQuestDefinition(item.String) == null)
			{
				Label label = item;
				Logger.Warning("Check quest complete: unknown quest id " + label.ToString());
			}
		}
	}

	protected override string MakeExplanationText()
	{
		return MakeExplanationText(Loc.Get("ui.requirements.quests.expected"), Loc.Get("ui.requirements.quests.unexpected"), Loc.Get("ui.requirements.quests.all"), Loc.Get("ui.requirements.quests.any"), Loc.Get("ui.requirements.quests.none"));
	}

	protected override string IdToName(Label id)
	{
		return Loc.Get(Game.ctx.quests.FindQuestDefinition(id.String).locname);
	}
}
