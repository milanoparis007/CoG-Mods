using Game.Services;
using Game.Session;
using Game.Session.Entities;
using Game.Session.Player;
using Game.UI.Mouseovers;
using SomaSim.Util;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.Session.Victory;

public class VictoryEndSubview : VictoryDialogSubview
{
	private GameObject _catContainer;

	private const string LANDING_INFO = "Description/Text";

	private const string GAMEOVER_TITLE = "Title";

	private const string FLAVOR_PHOTO = "Photo/Image";

	private const string FLAVOR_PHOTO_TWO = "Photo (1)/Image";

	private const string FLAVOR_PHOTO_THREE = "Photo (2)/Image";

	private const string DEATH_P1 = "Attack With Weapon";

	private const string DEATH_P2 = "Police Van";

	private const string DEATH_P3 = "Result Death";

	private const string VICTORY_P1 = "Victory 1";

	private const string VICTORY_P2 = "Victory 2";

	private const string VICTORY_P3 = "Victory 3";

	private const string BUTTON_THRONE = "ThroneButton";

	public VictoryEndSubview(GameObject go, string name, VictoryController c)
		: base(go, name, c)
	{
	}

	public override void RefreshSubview()
	{
	}

	public override void Activate()
	{
		base.Activate();
		if (Model.dead)
		{
			DoDeathDisplay();
		}
		else
		{
			DoEndDisplay();
		}
		bool isSet = Game.ctx.players.Human.throne.GetThroneStyle().IsSet;
		panel.GetButton("ThroneButton").interactable = isSet;
		panel.GetButton("ThroneButton").onClick.SetListener(Controller.OpenThrone);
		panel.GetChild("ThroneButton").GetComponent<MouseoverTrigger>().enabled = !isSet;
		panel.GetChild("ThroneButton").SetActive(PlayerThrone.CanSeeThrone());
	}

	private void SetSprite(string dest, string sprite)
	{
		panel.GetChild(dest).GetComponent<Image>().sprite = Game.ctx.hud.uisprites.photos.Find(sprite);
	}

	public void DoDeathDisplay()
	{
		Entity playerPeep = Game.ctx.players.Human.social.GetPlayerPeep();
		string[] array = new string[4]
		{
			"name",
			playerPeep.data.person.ShortName,
			"groupname",
			Game.ctx.players.Human.social.PlayerGroupName
		};
		string[] array2 = new string[10]
		{
			"name",
			playerPeep.data.person.FullName,
			"date",
			Loc.FormatDateLong(Game.ctx.clock.Now),
			"enddate",
			Loc.FormatDateShort(Game.ctx.clock.Now.IncrementDays(7)),
			"years",
			Loc.FormatNumber(playerPeep.data.person.GetAge(Game.ctx.clock.Now).YearsInt),
			"cityname",
			Game.ctx.session.mapconfig.CityName
		};
		GameObject dialog = panel;
		object[] replacements = array;
		dialog.SetText("Title", Loc.Get("victory.gameover.title.dead", replacements));
		GameObject dialog2 = panel;
		string text = Loc.Get("ui.humandeath.title", "name", playerPeep.data.person.FullName);
		Gender g = playerPeep.data.person.g;
		replacements = array2;
		dialog2.SetText("Description/Text", text + "\n\n\n" + Loc.GetGendered("ui.humandeath.line", g, replacements));
		SetSprite("Photo/Image", "Attack With Weapon");
		SetSprite("Photo (1)/Image", "Police Van");
		SetSprite("Photo (2)/Image", "Result Death");
	}

	public void DoEndDisplay()
	{
		Entity playerPeep = Game.ctx.players.Human.social.GetPlayerPeep();
		string[] array = new string[4]
		{
			"name",
			playerPeep.data.person.ShortName,
			"groupname",
			Game.ctx.players.Human.social.PlayerGroupName
		};
		(int, string) tuple = Game.ctx.simman.victory.ExplainGameOverPoints();
		string text = Game.ctx.simman.victory.ExplainGameOverRanking();
		string text2 = Loc.Get("victory.gameover.header") + tuple.Item2 + "\n\n" + text;
		GameObject dialog = panel;
		object[] replacements = array;
		dialog.SetText("Title", Loc.Get("victory.gameover.title", replacements));
		panel.SetText("Description/Text", text2);
		SetSprite("Photo/Image", "Victory 1");
		SetSprite("Photo (1)/Image", "Victory 2");
		SetSprite("Photo (2)/Image", "Victory 3");
		Game.ctx.events.SendImmediate(new SessionEvent(SessionEventType.EndOfGameHappened, playerPeep.Id, Game.ctx.players.Human.PID));
	}

	public override void Deactivate()
	{
		base.Deactivate();
	}
}
