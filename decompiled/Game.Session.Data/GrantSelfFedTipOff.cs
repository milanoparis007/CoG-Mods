using System.Collections.Generic;
using System.Linq;
using Game.Services;
using Game.Session.Player;
using SomaSim.Util;

namespace Game.Session.Data;

public class GrantSelfFedTipOff : VisitGrant
{
	public override GrantReq RequiredContext => GrantReq.PlayerID;

	public override void Apply(GrantContext ctx)
	{
		PlayerInfo player = ctx.GetPlayer();
		List<PlayerInfo> list = Game.ctx.players.all.Where((PlayerInfo x) => x.IsJustGang && x.ai.combat.IsAggroAnyType(Game.ctx.players.Human.PID)).ToList();
		player.crew.GetCrewForIndex(0).GetPeep().data.ident.rng.PickElement(list);
		Game.ctx.players.all.Find((PlayerInfo x) => x.IsJustFed).ai.feds.RequestInvestigation(player.PID);
	}

	public override string Describe(GrantContext ctx)
	{
		return Loc.Get("ui.grants.fed-tip-off");
	}
}
