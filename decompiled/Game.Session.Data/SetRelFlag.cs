using Game.Core;
using Game.Session.Entities;

namespace Game.Session.Data;

public class SetRelFlag : AbstractRelGrant
{
	public ModValue dayz;

	public override void Apply(GrantContext ctx)
	{
		Entity entity = FindPeep(ctx);
		if (entity != null)
		{
			ctx.GetPlayer().social.FindOrMakeRelationshipsWith(entity.Id).from.AddFlag(id, FindExpiration(ctx));
			Game.ctx.events.SendImmediate(SessionEventType.PlayerRelFlagsChangedImmediate, ctx.pid);
		}
	}

	public SimTime? FindExpiration(GrantContext ctx)
	{
		if (dayz == null)
		{
			return null;
		}
		ModQuery query = ctx.visit.MakeOwnerModQuery();
		int deltaDays = (int)dayz.Evaluate(query);
		return Game.ctx.clock.Now.IncrementDays(deltaDays);
	}
}
