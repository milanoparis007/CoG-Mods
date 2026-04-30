using System.Collections.Generic;
using Game.Services;
using Game.Session.Entities;
using Game.Session.Player;
using Game.Session.Sim;
using Game.UI.Mouseovers;
using SomaSim.Util;
using UnityEngine;

namespace Game.UI.Session.Victory;

public class LandingSubview : VictoryDialogSubview
{
	private List<VictoryCategory> _victoryCats;

	private GameObject _catContainer;

	private GameObject _catCard;

	private const string CATEGORY_CONTAINER = "Categories/List/Viewport/Content";

	private const string CATEGORY_CARD_TEMPLATE = "Templates/Victory Cat Card";

	private const string CATEGORY_HEADER = "Categories/Text";

	private const string LANDING_INFO = "Description/Text";

	private const string FLAVOR_PHOTO = "Photo/Image";

	private const string BUTTON_THRONE = "ThroneButton";

	private const string CARD_ICON = "Icon";

	private const string CARD_BUTTON = "Button";

	private const string CARD_TEXT = "Button/Text";

	public LandingSubview(GameObject go, string name, VictoryController c)
		: base(go, name, c)
	{
	}

	public override void RefreshSubview()
	{
		RefreshCards();
	}

	public override void Activate()
	{
		base.Activate();
		_catContainer = _go.GetChild("Categories/List/Viewport/Content");
		_catCard = _go.GetChild("Templates/Victory Cat Card");
		_victoryCats = Game.ctx.simman.victory.GetVictoryCategories();
		Entity playerPeep = Game.ctx.players.Human.social.GetPlayerPeep();
		panel.SetText("Categories/Text", Loc.Get("victory.title"));
		panel.SetText("Description/Text", Loc.Get("victory.desc", "date", Loc.FormatDateLong(Game.serv.globals.settings.general.generator.GetEndOfGame()), "status", " " + Game.ctx.simman.victory.ExplainPointsMidgame().Item2) + "\n\n" + Loc.GetGendered("victory.flavor", playerPeep.data.person.g, "cityname", Game.ctx.session.mapconfig.CityName));
		bool isSet = Game.ctx.players.Human.throne.GetThroneStyle().IsSet;
		panel.GetButton("ThroneButton").interactable = isSet;
		panel.GetButton("ThroneButton").onClick.SetListener(Controller.OpenThrone);
		panel.GetChild("ThroneButton").GetComponent<MouseoverTrigger>().enabled = !isSet;
		panel.GetChild("ThroneButton").SetActive(PlayerThrone.CanSeeThrone());
	}

	public override void Deactivate()
	{
		base.Deactivate();
		_catCard = null;
		_victoryCats = null;
	}

	private void RefreshCards()
	{
		_catContainer.EnsureChildCount(_victoryCats, _catCard);
		_catContainer.InitializeChildren(_victoryCats, RefreshCard);
	}

	private void RefreshCard(int ind, GameObject card, VictoryCategory cat)
	{
		card.SetTextOrHide("Icon", cat.GetIcon());
		card.SetTextOrHide("Button/Text", cat.GetName());
		card.GetButton("Button").onClick.SetListener(delegate
		{
			Controller.Force(ind + 1);
		});
	}
}
