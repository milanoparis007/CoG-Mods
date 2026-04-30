using Game.Core;
using Game.Session.Entities;
using Game.Session.Player;

namespace Game.Session.Data;

public abstract class AbstractRelGrant : VisitGrant
{
	public enum With
	{
		Owner,
		Player,
		Topic
	}

	public Label id;

	public With with;

	public override GrantReq RequiredContext => GrantReq.PlayerID | GrantReq.VisitState | GrantReq.VisitNpc;

	public Entity FindPeep(GrantContext ctx)
	{
		switch (with)
		{
		case With.Owner:
			return ctx.visit.npc;
		case With.Player:
			return FindPlayerPeep(ctx.visit.npc);
		case With.Topic:
			return ctx.visit.topic;
		default:
			Logger.Warning("Unknown with type: " + with);
			return null;
		}
	}

	protected Entity FindPlayerPeep(Entity npc)
	{
		return npc.data.agent.pid.FindPlayer()?.social.PlayerPeepId.FindEntity() ?? npc;
	}
}
