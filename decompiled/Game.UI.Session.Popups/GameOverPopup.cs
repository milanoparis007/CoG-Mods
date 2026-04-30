using System;
using Game.Services;
using SomaSim.Util;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.Session.Popups;

public class GameOverPopup : AbstractFancyPopup
{
	protected const string BG = "Popup Background";

	protected const string BUTTON_CONTINUE = "Panel/Footer/Continue";

	protected const string PANEL = "Panel";

	public override UIReference UIReference => UIElements.NewGameIntroPopup;

	public GameOverPopup(Action fn)
		: base(fn)
	{
	}

	public GameOverPopup()
		: this(OnOK)
	{
	}

	private static void OnOK()
	{
		if (!Game.ctx.clock.SkipEnd)
		{
			TimerUtil.RunAfterTime(delegate
			{
				Game.ctx.QuitGame();
			}, 0.1f);
		}
	}

	protected void Continue()
	{
		Game.ctx.clock.AllowPostgame();
		Close();
	}

	public override void OnActivated(bool pushed)
	{
		base.OnActivated(pushed);
		Image bg = _go.GetImage("Popup Background");
		GameObject panel = _go.GetChild("Panel");
		Color color = bg.color.SetAlpha(0f);
		Color endcolor = bg.color;
		float num = 2f;
		panel.SetActive(value: false);
		bg.color = color;
		LeanTween.cancelAll(_go);
		LeanTween.value(_go, delegate(float a)
		{
			bg.color = bg.color.SetAlpha(a);
		}, color.a, endcolor.a, num).setEase(LeanTweenType.easeInOutSine);
		TimerUtil.RunAfterTime(delegate
		{
			bg.color = endcolor;
			panel.SetActive(value: true);
		}, num + 0.1f);
	}

	protected override void RefreshContents()
	{
		Sprite crewSprite = HUDUtil.GetCrewSprite(Game.ctx.players.Human.social.GetPlayerPeep());
		_go.SetImageOrHide("Panel/Portrait/Portrait", crewSprite);
		_go.SetText("Panel/Name", Loc.Get("ui.humandeath.title.simple"));
		_go.SetText("Panel/Text", Loc.Get("victory.gameover.continue.dead"));
	}
}
