using Game.Services;
using Game.Session.Entities;

namespace Game.Session.Data;

public class ConsumeActions : VisitGrant
{
	public enum Type
	{
		None,
		Convo
	}

	public int count = 1;

	public bool all;

	public Type @for;

	public override GrantReq RequiredContext => GrantReq.VisitState | GrantReq.VisitPeep;

	public override void Apply(GrantContext ctx)
	{
		Entity entity = ctx.visit.crew.peepId.FindEntity();
		CrewCost amount = GetAmount(entity);
		entity.components.agent.DoPay(amount);
	}

	private CrewCost GetAmount(Entity agent)
	{
		switch (@for)
		{
		case Type.None:
		{
			int actions = (all ? agent.data.agent.actionsLeft : count);
			return new CrewCost(0, actions);
		}
		case Type.Convo:
			return Game.serv.globals.settings.people.social.costs.convoCost;
		default:
			Logger.Warning("Unknown type: " + @for);
			return new CrewCost(0, count);
		}
	}
}
