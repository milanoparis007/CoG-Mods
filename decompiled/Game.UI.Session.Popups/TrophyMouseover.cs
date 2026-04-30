using Game.Services;
using Game.UI.Mouseovers;

namespace Game.UI.Session.Popups;

public class TrophyMouseover : BaseCustomTextMouseover
{
	public override UIMouseoverName MouseoverAssetName => UIMouseoverName.TextMouseoverBoundedTL;

	protected override string ProduceText()
	{
		TrophyCtx component = context.GetComponent<TrophyCtx>();
		string text = (Game.ctx.players.Human.throne.HasSeen(component.id) ? "" : ("\n" + Loc.Get("ui.throne.new-trophy-text")));
		Trophy trophyForId = Game.serv.globals.settings.throne.GetTrophyForId(component.id);
		return string.Concat(string.Concat("" + Loc.Get("ui.throne.title-formatting", "text", Loc.Get(trophyForId.loctitle)) + text + "\n\n", Loc.Get(trophyForId.locdesc), "\n\n"), component.signature);
	}
}
