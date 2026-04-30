using Game.Core;
using Game.Session.Data;
using Game.Session.Player;

namespace Game.UI.Session.Convo;

public sealed class ConvoDataStoreForceClosed : ConvoData
{
	public PlayerID enemy;

	public override string[] MakeReplacements(VisitState visit, int index)
	{
		return new string[2]
		{
			"groupname",
			enemy.FindPlayer()?.social.FindPlayerGroupNameColorized()
		};
	}
}
