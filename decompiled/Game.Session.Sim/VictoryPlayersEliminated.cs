using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Services;
using Game.Session.Data;
using Game.Session.Player;
using SomaSim.Util;

namespace Game.Session.Sim;

public class VictoryPlayersEliminated : VictorySubgoal
{
	public readonly PlayerType type;

	public VictoryPlayersEliminated(string locdesc, PlayerType type)
		: base(locdesc)
	{
		this.type = type;
	}

	public override void RecomputeState()
	{
		List<PlayerInfo> list = Game.ctx.players.all.Where((PlayerInfo p) => p.PlayerType == type).ToList();
		Fixnum fixnum = list.Count;
		Fixnum fixnum2 = list.Count(IsEliminated);
		_ = fixnum == 0;
		Fixnum eliminationFactor = GetEliminationFactor();
		Fixnum fixnum3 = (fixnum.IsNotZero ? (fixnum2 / fixnum) : eliminationFactor);
		state = new VictorySubgoalState(fixnum3, eliminationFactor, fixnum3 >= eliminationFactor);
	}

	public override string ExplainState()
	{
		return ExplainStateAsGoalPercent();
	}

	private Fixnum GetEliminationFactor()
	{
		return type switch
		{
			PlayerType.GangPlayer => base.Settings.gangsEliminated, 
			PlayerType.GoonPlayer => base.Settings.goonsNeutralized, 
			_ => 1, 
		};
	}

	private bool IsEliminated(PlayerInfo p)
	{
		if (p.crew.IsCrewDefeated)
		{
			return true;
		}
		if (type == PlayerType.GoonPlayer)
		{
			var (flag, state) = Game.ctx.simman.demands.FindDemandState(PlayerID.HumanPlayer, Demand.Target.MakeForPlayer(p.PID));
			if (flag && state == Demand.State.Compliant)
			{
				return true;
			}
		}
		return false;
	}
}
