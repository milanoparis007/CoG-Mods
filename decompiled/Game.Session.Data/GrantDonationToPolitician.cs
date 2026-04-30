using Game.Core;
using Game.Session.Player;
using Game.Session.Sim;
using SomaSim.Util;

namespace Game.Session.Data;

public class GrantDonationToPolitician : VisitGrant
{
	public Fixnum amount;

	public override GrantReq RequiredContext => GrantReq.VisitState | GrantReq.VisitVehicle;

	public override void Apply(GrantContext ctx)
	{
		Ward wardForBuilding = Game.ctx.simman.politics.GetWardForBuilding(ctx.visit.building.Id);
		if (wardForBuilding != null && ctx.GetPlayer().finances.CanChangeMoney(ctx.visit.vehicle, new Price(-amount)))
		{
			ctx.GetPlayer().finances.DoChangeMoney(ctx.visit.vehicle, new Price(-amount), MoneyReason.CampaignDonations);
			wardForBuilding.currElection.ModifyCandidateWarchest(ctx.visit.npc.Id, amount);
			wardForBuilding.currElection.TryUpgradeSponsorLevel(ctx.visit.npc.Id, Election.SupportLevel.Donor);
		}
	}
}
