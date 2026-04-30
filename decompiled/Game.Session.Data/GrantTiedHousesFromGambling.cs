using Game.Core;
using Game.Session.Entities;
using Game.Session.Player;

namespace Game.Session.Data;

public class GrantTiedHousesFromGambling : VisitGrant
{
	public override GrantReq RequiredContext => GrantReq.PlayerID | GrantReq.VisitState | GrantReq.VisitNpc;

	public override void Apply(GrantContext ctx)
	{
		RelationshipList listOrNull = Game.ctx.simman.rels.GetListOrNull(ctx.visit.npc.Id);
		PlayerInfo player = ctx.GetPlayer();
		foreach (Relationship datum in listOrNull.data)
		{
			if (!datum.IsAnyFamily || player.social.GetRelationshipFromSourceToPlayer(datum.to) == null)
			{
				continue;
			}
			Entity entity = BuildingUtil.FindBizForOwner(datum.to);
			if (entity != null)
			{
				BizComponent biz = entity.components.biz;
				if (!biz.IsTiedButNotTo(player.PID) && !BuildingUtil.FindBuildingForBiz(entity).components.building.IsControlledByAnyPlayer())
				{
					int item = biz.GetTiedHouseParameters(datum.to, PlayerID.HumanPlayer, null).days;
					biz.SetTiedHouse(player.PID, item);
				}
			}
		}
	}
}
