using Game.Services;
using SomaSim.Util;
using UnityEngine;

namespace Game.UI.Session.Popups;

public class DemoOverPopup : GameOverPopup
{
	private static void ShowPageAndQuit()
	{
		Game.serv.store.handler.OpenStorePage();
		TimerUtil.RunAfterTime(delegate
		{
			Game.ctx.QuitGame();
		}, 0.1f);
	}

	public DemoOverPopup()
		: base(ShowPageAndQuit)
	{
	}

	protected override void RefreshContents()
	{
		Sprite crewSprite = HUDUtil.GetCrewSprite(Game.ctx.players.Human.social.GetPlayerPeep());
		_go.SetImageOrHide("Panel/Portrait/Portrait", crewSprite);
		_go.SetText("Panel/Name", Loc.Get("ui.demodone.title"));
		_go.SetText("Panel/Text", Loc.Get("ui.demodone.line"));
		_go.SetChildText("Panel/Footer/Ok", Loc.Get("ui.demodone.button"));
	}
}
