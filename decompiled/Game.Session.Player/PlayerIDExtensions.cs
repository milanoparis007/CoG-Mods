using Game.Core;

namespace Game.Session.Player;

public static class PlayerIDExtensions
{
	public static PlayerInfo FindPlayer(this PlayerID pid)
	{
		if (!pid.IsValid)
		{
			return null;
		}
		return Game.ctx.players.WithID(pid);
	}
}
