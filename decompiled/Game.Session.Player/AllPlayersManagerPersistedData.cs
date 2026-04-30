using System.Collections.Generic;
using Game.Services;
using Game.Services.Maps;
using SomaSim.Util;

namespace Game.Session.Player;

public sealed class AllPlayersManagerPersistedData
{
	public Xorshift rng = Game.ctx.scenario.MakeSeededRng<AllPlayersManager>();

	public int totalPlayers;

	public List<PlayerData> players;

	public AllPlayersManagerPersistedData()
	{
	}

	public AllPlayersManagerPersistedData(MapConfig mapcfg)
	{
		short num = 0;
		players = new List<PlayerData>
		{
			new PlayerData(num++, PlayerType.SystemPlayer)
		};
		_ = players.LastOrDefaultFast().pid;
		players.Add(new PlayerData(num++, PlayerType.HumanPlayer));
		_ = players.LastOrDefaultFast().pid;
		PlayerType[] array = new PlayerType[4]
		{
			PlayerType.AgentPlayer,
			PlayerType.CopPlayer,
			PlayerType.GangPlayer,
			PlayerType.GoonPlayer
		};
		foreach (PlayerType type in array)
		{
			int count = mapcfg.groups.GetCount(type);
			for (int j = 0; j < count; j++)
			{
				players.Add(new PlayerData(num++, type));
			}
		}
		totalPlayers = num;
	}
}
