using Game.Session.Player;

namespace Game.Session.Data;

public sealed class CheckHumanHasHistoryElementToGang : AbstractCheckHumanAndPlayerHistory
{
	protected override Relationship GetRelationship(PlayerInfo visitingPlayer, PlayerInfo targetPlayer)
	{
		return visitingPlayer?.social.GetRelationshipFromPlayerTo(targetPlayer.PID);
	}
}
