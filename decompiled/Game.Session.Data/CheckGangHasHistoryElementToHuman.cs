using Game.Session.Player;

namespace Game.Session.Data;

public sealed class CheckGangHasHistoryElementToHuman : AbstractCheckHumanAndPlayerHistory
{
	protected override Relationship GetRelationship(PlayerInfo visitingPlayer, PlayerInfo targetPlayer)
	{
		return targetPlayer?.social.GetRelationshipFromPlayerTo(visitingPlayer.PID);
	}
}
