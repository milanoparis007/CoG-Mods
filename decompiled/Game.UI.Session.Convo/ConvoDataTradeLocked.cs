using Game.Core;
using Game.Session.Data;
using Game.Session.Player;

namespace Game.UI.Session.Convo;

public sealed class ConvoDataTradeLocked : ConvoData
{
	public enum Reason
	{
		None,
		TiedHouse,
		Territory,
		ForcedClosed
	}

	public PlayerID otherPlayer = PlayerID.INVALID;

	public Reason reason;

	public override string[] MakeReplacements(VisitState visit, int index)
	{
		return new string[2]
		{
			"groupname",
			otherPlayer.FindPlayer()?.social.FindPlayerGroupNameColorized()
		};
	}

	public void Set(PlayerID pid, Reason reason)
	{
		otherPlayer = pid;
		this.reason = reason;
	}
}
