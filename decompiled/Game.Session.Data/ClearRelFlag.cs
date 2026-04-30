using Game.Session.Entities;

namespace Game.Session.Data;

public class ClearRelFlag : AbstractRelGrant
{
	public override void Apply(GrantContext ctx)
	{
		Entity entity = FindPeep(ctx);
		if (entity != null)
		{
			ctx.GetPlayer().social.FindOrMakeRelationshipsWith(entity.Id).from.RemoveFlag(id);
			Game.ctx.events.SendImmediate(SessionEventType.PlayerRelFlagsChangedImmediate, ctx.pid);
		}
	}
}
