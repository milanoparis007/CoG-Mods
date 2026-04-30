using Game.Core;
using Game.Services;
using Game.Session.Board;
using Game.Session.Entities;

namespace Game.Session.Data;

public class RevealNodes : VisitGrant
{
	public enum RevealType
	{
		MakeVisited,
		MakeScoped
	}

	public RevealType type;

	public int maxcount;

	public int maxdistance;

	public override GrantReq RequiredContext => GrantReq.PlayerID | GrantReq.VisitState | GrantReq.VisitPeep;

	public override void Apply(GrantContext ctx)
	{
		Entity peep = ctx.visit.crew.GetPeep();
		NodeID nid = peep.data.agent.nid;
		switch (type)
		{
		case RevealType.MakeVisited:
			ctx.GetPlayer().territory.PerformGrantMarkKnown(nid.FindNode(), maxcount, maxdistance);
			break;
		case RevealType.MakeScoped:
			ctx.GetPlayer().territory.PerformGrantMarkScoped(peep.Id, nid.FindNode(), maxcount, maxdistance);
			break;
		}
	}

	public override string Describe(GrantContext ctx)
	{
		if (type != RevealType.MakeVisited)
		{
			return Loc.Get("ui.grants.revealnodes.describe.default");
		}
		return Loc.Get("ui.grants.revealnodes.describe.makevisited");
	}
}
