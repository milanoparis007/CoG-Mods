using Game.Session.Entities;

namespace Game.Session.Data;

public class SetTopicFromConversation : VisitGrant
{
	public override GrantReq RequiredContext => GrantReq.VisitState;

	public override void Apply(GrantContext ctx)
	{
		Entity topic = (Game.ctx.hud.convoDialog?.Model?.state?.data)?.GetVisitTopic();
		ctx.visit.topic = topic;
	}
}
