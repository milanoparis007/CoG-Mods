using Game.Services;
using Game.Session.Entities;
using Game.Session.Player;
using Game.UI.Session.Convo;
using SomaSim.Util;

namespace Game.Session.Data;

public class IntroToUsefulFriend : VisitGrant
{
	public override GrantReq RequiredContext => GrantReq.PlayerID | GrantReq.VisitState | GrantReq.VisitNpc;

	public override void Apply(GrantContext ctx)
	{
		ConvoDataNPCSelection.Entry entry = TicketModuleIntroductions.MakeModuleBasedIntro(ctx.visit)?.entries.FirstOrDefaultFast();
		if (entry != null)
		{
			TicketIntroductions.PerformIntro(entry.targetId, ctx.visit.crew.peepId);
		}
	}

	public override string Describe(GrantContext ctx)
	{
		string text = (TicketModuleIntroductions.MakeModuleBasedIntro(ctx.visit)?.entries.FirstOrDefaultFast())?.targetId.FindEntity()?.data.person.FullName;
		return Loc.Get("ui.grants.introtousefulfriend.describe", "name", text);
	}
}
