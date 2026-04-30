using Game.Session.Data;
using Game.Session.Player;

namespace Game.UI.Session.Convo;

public sealed class ConvoDataCombat : ConvoData
{
	public CrewAssignment attacker;

	public CrewAssignment target;

	public override string[] MakeReplacements(VisitState visit, int index)
	{
		return new string[0];
	}
}
