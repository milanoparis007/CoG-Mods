using System;
using Game.Services;
using Game.Session.Entities;
using Game.Session.Tutorial;
using Game.UI.Session.Popups;
using SomaSim.Util;

namespace Game.UI.Session.Tutorial;

public class NewGameIntroPopup : AbstractFancyPopup
{
	public NewGameIntroPopup(Action onOk)
		: base(onOk)
	{
	}

	protected override void RefreshContents()
	{
		Entity playerPeep = Game.ctx.players.Human.social.GetPlayerPeep();
		UIUtil.SetImageOrHide(sprite: HUDUtil.GetCrewSprite(playerPeep), dialog: _go, path: "Panel/Portrait/Portrait");
		PersonInfoUtil.Overview overview = PersonInfoUtil.GenerateOverview(playerPeep, details: false);
		string name = overview.name;
		string ageAndEth = overview.GetAgeAndEth();
		_go.SetText("Panel/Name", Loc.Get("tut.newgamepopup.name", "name", name, "ageinfo", ageAndEth));
		Entity entity = Game.ctx.players.Human.social.FindFrancine();
		PersonData person = entity.data.person;
		string cityName = Game.ctx.session.mapconfig.CityName;
		string[] replacements = new string[4] { "name", person.FullName, "cityname", cityName };
		string text = TutorialManager.MakeMultiLineBlurb("tut.newgamepopup.text", replacements, entity);
		_go.SetText("Panel/Text", text);
	}
}
