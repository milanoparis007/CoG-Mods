using System.Diagnostics;
using Game.Session.Data;

namespace Game.Services;

[DebuggerDisplay("{DebugString}")]
public sealed class QuestGrantChoice
{
	public string locdesc;

	public VisitRequirementList visreqs;

	public VisitGrantList grants;

	private string DebugString => "QuestGrantChoice: " + locdesc;
}
