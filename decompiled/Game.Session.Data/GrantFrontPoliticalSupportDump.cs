using Game.Core;
using Game.Session.Board;
using Game.Session.Entities;
using Game.Session.Player;
using Game.Session.Sim;

namespace Game.Session.Data;

public class GrantFrontPoliticalSupportDump : VisitGrant
{
	public Label id;

	public override GrantReq RequiredContext => GrantReq.PlayerID | GrantReq.VisitState;

	public override void Apply(GrantContext ctx)
	{
		Ward ward = ((ctx.visit.building != null) ? Game.ctx.simman.politics.GetWardForBuilding(ctx.visit.building.Id) : Game.ctx.simman.politics.GetWardForID(ctx.visit.GetCrewNode().precinctId));
		if (ward == null || ward.currElection == null)
		{
			return;
		}
		OutpostEntry outpostEntryUnsafe = ctx.GetPlayer().outposts.GetOutpostEntryUnsafe(ctx.visit.building);
		if (outpostEntryUnsafe == null)
		{
			return;
		}
		foreach (NodeEntry targetNode in outpostEntryUnsafe.targetNodes)
		{
			if (!(targetNode.nodeId.FindNode().owner.Get() == ctx.pid))
			{
				continue;
			}
			foreach (EntityID item in targetNode.nodeId.FindNode().interesting)
			{
				Entity entity = BuildingUtil.FindOwnerForAnyBuilding(item);
				Relationship relationshipFromSourceToPlayer = ctx.GetPlayer().social.GetRelationshipFromSourceToPlayer(entity.Id);
				if (!relationshipFromSourceToPlayer.HasBuff(BuffConstants.RELBUFF_TICKET_POL_BOOST))
				{
					relationshipFromSourceToPlayer.AddBuff(BuffConstants.RELBUFF_TICKET_POL_BOOST, ctx.visit.crew.peepId);
				}
			}
		}
		ward.currElection.PerformPlayerCampaignAction(ctx.pid, id);
	}
}
