using Game.Session.Data;
using Game.Session.Player;

namespace Game.Session.Sim;

public static class PrecinctIDExtensions
{
	public static PlayerInfo FindPrecinct(this PrecinctID id)
	{
		foreach (PlayerInfo item in Game.ctx.players.all)
		{
			if (item.IsJustCop && item.ai.precinct.PrecinctID == id)
			{
				return item;
			}
		}
		return null;
	}

	public static Ward FindWard(this PrecinctID id)
	{
		return Game.ctx.simman.politics.GetWardForID(id);
	}
}
