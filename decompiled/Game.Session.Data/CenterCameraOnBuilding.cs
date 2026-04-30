using Game.UI.Session;

namespace Game.Session.Data;

public class CenterCameraOnBuilding : VisitGrant
{
	public override GrantReq RequiredContext => GrantReq.VisitState | GrantReq.VisitNpc;

	public override void Apply(GrantContext ctx)
	{
		PersonInfoUtil.TweenCameraToEntity((ctx.visit.building != null) ? ctx.visit.building.Id : ctx.visit.npc.Id);
	}
}
