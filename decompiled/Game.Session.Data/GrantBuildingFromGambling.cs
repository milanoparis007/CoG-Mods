using Game.Session.Entities;
using Game.Session.Player;
using Game.UI.Session;

namespace Game.Session.Data;

public class GrantBuildingFromGambling : VisitGrant
{
	public override GrantReq RequiredContext => GrantReq.PlayerID | GrantReq.VisitState | GrantReq.VisitNpc;

	public override void Apply(GrantContext ctx)
	{
		PlayerInfo player = ctx.GetPlayer();
		PlayerTerritory.TakeoverData td = player.territory.FindTakeoverData(ctx.visit);
		if (td.IsValid)
		{
			td.cost = 0;
			Entity entity = player.territory.PerformTakeover(player.crew.GetCrewForPlayerPeep(), td);
			if (entity != null)
			{
				PersonInfoUtil.TweenCameraToEntity(entity);
			}
		}
	}
}
