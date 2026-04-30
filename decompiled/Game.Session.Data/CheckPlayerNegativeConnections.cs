using System.Linq;
using Game.Services;
using SomaSim.Util;

namespace Game.Session.Data;

public class CheckPlayerNegativeConnections : VisitWithValueRequirement
{
	protected override Fixnum GetCurrentValue(VisitState visit)
	{
		return (from rel in GetPlayer(visit).social.GetAllPlayerRelationshipsUnsafe()
			select Game.ctx.simman.rels.GetOrNull(rel.to, rel.@from)).Count((Relationship rel) => rel != null && rel.Evaluate().current < 0);
	}

	public override ReqExplanation Explain(VisitState visit)
	{
		return new ReqExplanation(DoesPass(visit), Loc.Get("ui.requirements.negconn.count", "value", value));
	}
}
