using Game.Core;
using Game.Services;
using Game.Session.Entities;

namespace Game.Session.Data;

public class AddRelBuffAtNode : VisitGrant
{
	public Label id;

	public override GrantReq RequiredContext => GrantReq.PlayerID | GrantReq.VisitState | GrantReq.VisitPeep;

	public override void Apply(GrantContext ctx)
	{
		foreach (EntityID item in ctx.visit.peep.components.agent.GetNode().interesting)
		{
			Entity entity = BuildingUtil.FindOwnerForAnyBuilding(item);
			if (entity != null)
			{
				ApplyToOne(ctx.visit, entity);
			}
		}
	}

	public void ApplyToOne(VisitState visit, Entity realOwner)
	{
		visit.GetPlayer().social.AddBuffFrom(realOwner.Id, id);
	}

	public override string Describe(GrantContext ctx)
	{
		return Loc.Get("ui.grants.addrelbuffatnode.describe");
	}
}
