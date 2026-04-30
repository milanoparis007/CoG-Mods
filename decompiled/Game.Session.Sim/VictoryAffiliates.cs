using System.Linq;
using Game.Session.Data;
using SomaSim.Util;

namespace Game.Session.Sim;

public class VictoryAffiliates : VictorySubgoal
{
	public VictoryAffiliates(string locdesc)
		: base(locdesc)
	{
	}

	public override void RecomputeState()
	{
		Fixnum numberOfAffiliates = base.Settings.numberOfAffiliates;
		int num = CountAffiliates();
		state = new VictorySubgoalState(num, numberOfAffiliates, num >= numberOfAffiliates);
	}

	private int CountAffiliates()
	{
		return (from r in Game.ctx.players.Human.social.GetAllPlayerRelationshipsUnsafe()
			select Game.ctx.simman.rels.GetOrNull(r.to, r.@from)).WhereNotNull().Count((Relationship r) => r.IsAffiliate());
	}

	public override string ExplainState()
	{
		return ExplainStateAsGoalNumbers();
	}
}
