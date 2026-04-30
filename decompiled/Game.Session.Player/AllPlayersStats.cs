using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using SomaSim.Util;

namespace Game.Session.Player;

public class AllPlayersStats
{
	public class PlayerTurnStats
	{
		public int turn;

		public int pid;

		public int crew;

		public int nodes;

		public int buildings;

		public int skills;

		public string Export()
		{
			return $"{turn},{pid},{crew},{nodes},{buildings},{skills}";
		}

		public static string Header()
		{
			return "TURN,PID,CREW,NODES,BUILDINGS,SKILLS";
		}
	}

	public class TurnStats : List<PlayerTurnStats>
	{
		public int turn;

		public string Export()
		{
			return this.SelectToString((PlayerTurnStats s) => s.Export(), "\n");
		}
	}

	public List<TurnStats> allstats = new List<TurnStats>();

	public void OnGlobalTurnSetAdvanced()
	{
		allstats.Add(MakeCurrentTurnStats());
	}

	[Conditional("UNITY_EDITOR")]
	public void ExportStatsToCsv()
	{
		_ = PlayerTurnStats.Header() + "\n" + allstats.SelectToString((TurnStats s) => s.Export(), "\n");
	}

	private PlayerTurnStats MakeCurrentTurnStatsRow(PlayerInfo player)
	{
		return new PlayerTurnStats
		{
			turn = Game.ctx.clock.CurrentTurn,
			pid = player.PID.id,
			crew = player.crew.LivingCrewCount,
			nodes = player.territory.OwnedNodeCount,
			buildings = player.territory.CountControlledBuildings(),
			skills = player.skills.CurrentSkillCount
		};
	}

	private TurnStats MakeCurrentTurnStats()
	{
		IEnumerable<PlayerTurnStats> collection = from p in Game.ctx.players.all
			where p.IsHuman || p.IsJustGang
			select MakeCurrentTurnStatsRow(p);
		TurnStats turnStats = new TurnStats();
		turnStats.turn = Game.ctx.clock.CurrentTurn;
		turnStats.AddRange(collection);
		return turnStats;
	}
}
