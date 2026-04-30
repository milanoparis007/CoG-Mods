using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Session.Player;
using SomaSim.Util;

namespace Game.Session.Sim;

public abstract class VictoryAbstractWorthSubgoal : VictorySubgoal
{
	private Money humanWorth;

	public VictoryAbstractWorthSubgoal(string locdesc)
		: base(locdesc)
	{
	}

	public override void RecomputeState()
	{
		List<PlayerInfo> allOutfits = Game.ctx.players.all.Where((PlayerInfo p) => p.IsHuman || p.IsJustGang).ToList();
		List<Money> list = ProduceWorthPerPlayer(allOutfits);
		humanWorth = list.GetOrDefaultFast(0);
		list.StableSort((Money a, Money b) => (int)(b.cash - a.cash));
		int num = list.LastIndexOf(humanWorth) + 1;
		int count = list.Count;
		bool pass = num == 1;
		state = new VictorySubgoalState(num, count, pass);
	}

	protected abstract List<Money> ProduceWorthPerPlayer(List<PlayerInfo> allOutfits);

	public override string ExplainState()
	{
		return ExplainStateAsRanking();
	}
}
