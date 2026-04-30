using Game.Core;
using Game.Services;
using Game.Session.Entities;
using Game.Session.Player;
using Game.UI.Session;

namespace Game.Session.Data;

public class GrantBuilding : VisitGrant
{
	public enum Target
	{
		VisitLocation,
		District
	}

	public Target at;

	public Label districtTag;

	public Label newBuildingTag;

	public override GrantReq RequiredContext => GrantReq.PlayerID | GrantReq.VisitState | GrantReq.VisitPeep;

	public override void Apply(GrantContext ctx)
	{
		PlayerInfo player = ctx.GetPlayer();
		PlayerTerritory territory = player.territory;
		PlayerTerritory.TakeoverData td = ((at == Target.VisitLocation) ? territory.FindTakeoverData(ctx.visit) : ((at == Target.District) ? territory.FindTakeoverDataForDistrictGrant(districtTag) : default(PlayerTerritory.TakeoverData)));
		if (td.IsValid)
		{
			td.cost = Price.ZERO;
			Entity entity = territory.PerformTakeover(ctx.visit.crew, td);
			if (at == Target.District)
			{
				Node node = entity.components.board.GetNode();
				player.meetings.MarkNodeAsKnown(node, expectedSeen: true, instant: true);
				territory.PerformGrantMarkKnown(node, 3, 50);
				PersonInfoUtil.TweenCameraToEntity(entity);
			}
			if (entity != null && newBuildingTag.IsSet)
			{
				entity.components.building.AddTag(newBuildingTag);
			}
		}
	}

	public override string Describe(GrantContext ctx)
	{
		return Loc.Get("ui.grants.grantbuilding.describe");
	}
}
