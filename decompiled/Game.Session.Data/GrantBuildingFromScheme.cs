using Game.Services;
using Game.Session.Entities;
using Game.Session.Player;
using Game.UI.Session;

namespace Game.Session.Data;

public class GrantBuildingFromScheme : VisitGrant
{
	public override GrantReq RequiredContext => GrantReq.PlayerID | GrantReq.VisitBuilding;

	public override void Apply(GrantContext ctx)
	{
		PlayerInfo player = ctx.GetPlayer();
		PlayerTerritory.TakeoverData td = player.territory.FindTakeoverData(ctx.visit, ctx.visit.building.Id, PlayerSocial.FindAnyoneToOwnBiz());
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

	public override string Describe(GrantContext ctx)
	{
		return Loc.Get("ui.grants.building-from-scheme");
	}
}
