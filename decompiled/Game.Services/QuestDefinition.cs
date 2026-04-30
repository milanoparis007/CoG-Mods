using System.Collections.Generic;
using System.Diagnostics;
using Game.Session.Data;
using Game.Session.Quests;

namespace Game.Services;

[DebuggerDisplay("{DebugString}")]
public sealed class QuestDefinition
{
	public sealed class RequestInfo
	{
		public List<string> introsteps = new List<string>();

		public string introending;
	}

	public string id;

	public string locname;

	public string locdesc;

	public string locicon;

	public bool skillquest;

	public bool unhappyOnSuccess;

	public VisitRequirementList reqs = new VisitRequirementList();

	public List<BaseGoal> goals = new List<BaseGoal>();

	public RequestInfo requestinfo;

	public List<QuestGrantChoice> choices = new List<QuestGrantChoice>();

	public string beforechoice;

	public string afterchoice;

	public QuestExpirationDefinition expiration;

	private string DebugString => "QuestDef: " + id;

	public string GetName()
	{
		return Loc.Get(locname);
	}

	public static bool Matcher(string id, QuestDefinition item)
	{
		return id == item.id;
	}
}
