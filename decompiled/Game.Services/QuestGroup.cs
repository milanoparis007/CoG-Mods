using System.Collections.Generic;
using System.Diagnostics;
using Game.Session.Data;

namespace Game.Services;

[DebuggerDisplay("{DebugString}")]
public sealed class QuestGroup
{
	public string debugname;

	public VisitRequirementList reqs = new VisitRequirementList();

	public List<QuestGroup> groups;

	public List<QuestDefinition> quests;

	private string DebugString => $"QuestGroup: {groups?.Count} groups and {quests?.Count} quests";
}
