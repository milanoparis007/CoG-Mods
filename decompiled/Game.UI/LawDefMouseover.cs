using Game.Core;
using Game.Services;
using Game.Session.Data;
using Game.Session.Player;
using Game.UI.Mouseovers;

namespace Game.UI;

public class LawDefMouseover : BaseCustomTextMouseover
{
	protected override string ProduceText()
	{
		LawCtx component = context.GetComponent<LawCtx>();
		PoliticsSettings.Law law = Game.serv.globals.settings.politics.FindLawDef(component.id);
		bool flag = Game.ctx.simman.politics.IsLawEnacted(component.id);
		string text = law.reqs?.Explain(new VisitState(CrewAssignment.EMPTY, Game.ctx.clock.Now, PlayerID.HumanPlayer)) ?? "";
		string text2 = (flag ? Loc.Get("ui.law-shop.law-mo.revoke-price", "amt", law.GetRevokePrice()) : "");
		string text3 = (flag ? "" : ((law.buyPrice != 0) ? (Loc.Get("ui.law-shop.law-mo.enact-price", "amt", law.buyPrice) + "\n") : ""));
		string text4 = ((Game.ctx.clock.Now.ToDate().Month != Game.serv.globals.settings.politics.elections.legislationStartMonth) ? Loc.Get("ui.law-shop.law-mo.wrong-time") : "");
		string text5 = (law.expireNextElectionYear ? (Loc.Get("ui.law-shop.law-mo.is-temp") + "\n\n") : "");
		string text6 = ((text != "") ? (text4 + text3 + text2 + "\n\n") : (text4 + text3 + text2));
		return Loc.Get(law.locdesc) + "\n\n" + text5 + text6 + text;
	}
}
