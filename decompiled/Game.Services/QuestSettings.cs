using System.Collections.Generic;
using Game.Session.Data;

namespace Game.Services;

public class QuestSettings
{
	public QuestGlobals global;

	public QuestGroup root;

	public List<QuestDefinition> GetAllQuests()
	{
		List<QuestDefinition> list = new List<QuestDefinition>();
		PopulateAllQuests(list);
		return list;
	}

	internal void PopulateAllQuests(List<QuestDefinition> results)
	{
		PopulateQuests(root, results);
	}

	internal void PopulateMatchingQuests(VisitState visit, List<QuestDefinition> results)
	{
		PopulateQuests(root, results, visit);
	}

	private void PopulateQuests(QuestGroup group, List<QuestDefinition> results, VisitState visit = null)
	{
		if (group == null || (visit != null && !group.reqs.AllPassLogging(visit, "quests")))
		{
			return;
		}
		if (group.quests != null)
		{
			foreach (QuestDefinition quest in group.quests)
			{
				if (visit == null || quest.reqs.AllPassLogging(visit, "quests"))
				{
					results.Add(quest);
				}
			}
		}
		if (group.groups == null)
		{
			return;
		}
		foreach (QuestGroup group2 in group.groups)
		{
			PopulateQuests(group2, results, visit);
		}
	}
}
