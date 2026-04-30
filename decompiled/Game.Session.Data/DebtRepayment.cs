using Game.Core;
using Game.Session.Entities;
using Game.Session.Player;
using SomaSim.Util;

namespace Game.Session.Data;

public class DebtRepayment : VisitGrant
{
	public override GrantReq RequiredContext => GrantReq.PlayerID | GrantReq.VisitPeep | GrantReq.VisitVehicle | GrantReq.VisitNpc;

	public override void Apply(GrantContext ctx)
	{
		Fixnum cash = ctx.pid.FindPlayer().gambling.FindGamblerState(ctx.visit.npc).cash.cash;
		PlayerInfo player = ctx.GetPlayer();
		Entity vehicle = ctx.visit.vehicle;
		player.finances.DoChangeMoney(vehicle, new Price(cash), MoneyReason.GamblingDebt);
	}
}
