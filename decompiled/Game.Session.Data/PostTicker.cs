using Game.Services;
using Game.UI.Session.Tickers;

namespace Game.Session.Data;

public sealed class PostTicker : VisitGrant
{
	public string locdesc;

	public string locicon;

	public override GrantReq RequiredContext => GrantReq.VisitState | GrantReq.VisitNpc;

	public override void Apply(GrantContext ctx)
	{
		TickerBar tickers = Game.ctx.hud.tickers;
		TickerIcon icon = TickerIcon.MakeRaw(Loc.Get(locicon));
		TickerTitle dEFAULT = TickerTitle.DEFAULT;
		string key = locdesc;
		object[] replacements = MakeReplacements(ctx);
		tickers.AddTextTicker(icon, dEFAULT, Loc.Get(key, replacements));
	}

	private string[] MakeReplacements(GrantContext ctx)
	{
		string fullName = ctx.visit.npc.data.person.FullName;
		return new string[2] { "ownername", fullName };
	}
}
