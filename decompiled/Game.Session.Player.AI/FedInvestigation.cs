using System.Linq;
using Game.Core;
using Game.Session.Board;
using Game.Session.Sim;

namespace Game.Session.Player.AI;

public class FedInvestigation
{
	public NodeID target;

	public SimTimeSpan duration;

	public FedInvestigation()
	{
	}

	public FedInvestigation(NodeID target, SimTimeSpan duration)
	{
		this.target = target;
		this.duration = duration;
	}

	public EntityID FindPrecinctStation()
	{
		return CopUtil.FindPrecinctOrNull(target.FindNode()).ai.precinct.StationBuilding;
	}

	public static EntityID FindFirstPrecinctStation()
	{
		return Game.ctx.players.all.First((PlayerInfo p) => p.IsJustCop).ai.precinct.StationBuilding;
	}
}
