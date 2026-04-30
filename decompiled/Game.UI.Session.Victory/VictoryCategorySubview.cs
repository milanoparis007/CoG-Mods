using System.Collections.Generic;
using Game.Services;
using Game.Session.Player;
using Game.Session.Sim;
using Game.UI.Mouseovers;
using SomaSim.Util;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.Session.Victory;

public class VictoryCategorySubview : VictoryDialogSubview
{
	private VictoryCatPage _currPage;

	private List<VictoryGoal> _goals;

	private GameObject _goalContainer;

	private GameObject _goalCard;

	private const string GOAL_CONTAINER = "Description/List/Viewport/Content";

	private const string GOAL_CARD_TEMPLATE = "Templates/Goal Desc Card";

	private const string CATEGORY_TITLE = "Categories/Title";

	private const string CATEGORY_DESCRIPTION = "Categories/Description";

	private const string SCROLLBAR = "Description/List/Scrollbar Vertical";

	private const string PICTURE = "Photo/Image";

	private const string BUTTON_THRONE = "ThroneButton";

	private const string CARD_ICON = "Icon";

	private const string CARD_TITLE = "Title";

	private const string CARD_TEXT = "Description";

	public VictoryCategorySubview(GameObject go, string name, VictoryController c)
		: base(go, name, c)
	{
	}

	public override void RefreshSubview()
	{
		_currPage = Model.GetReport();
		_goals = _currPage.catGoals;
		panel.SetText("Categories/Title", Loc.Get(_currPage.catName));
		panel.SetText("Categories/Description", Loc.Get(_currPage.catDesc));
		panel.GetChild("Photo/Image").GetComponent<Image>().sprite = Game.ctx.hud.uisprites.photos.Find(_currPage.catPhoto.sprite);
		panel.GetChild("Description/List/Scrollbar Vertical").GetComponent<Scrollbar>().value = 1f;
		RefreshCards();
	}

	public override void Activate()
	{
		base.Activate();
		_goalContainer = _go.GetChild("Description/List/Viewport/Content");
		_goalCard = _go.GetChild("Templates/Goal Desc Card");
		RefreshSubview();
		bool isSet = Game.ctx.players.Human.throne.GetThroneStyle().IsSet;
		panel.GetButton("ThroneButton").interactable = isSet;
		panel.GetButton("ThroneButton").onClick.SetListener(Controller.OpenThrone);
		panel.GetChild("ThroneButton").GetComponent<MouseoverTrigger>().enabled = !isSet;
		panel.GetChild("ThroneButton").SetActive(PlayerThrone.CanSeeThrone());
	}

	public override void Deactivate()
	{
		base.Deactivate();
		_goalCard = null;
		_currPage = null;
	}

	private void RefreshCards()
	{
		_goalContainer.EnsureChildCount(_goals, _goalCard);
		_goalContainer.InitializeChildren(_goals, RefreshCard);
	}

	private void RefreshCard(int _, GameObject card, VictoryGoal goal)
	{
		card.SetTextOrHide("Icon", Loc.Get("victory.star"));
		card.SetTextOrHide("Title", goal.GetName() + "  " + goal.GetIcon());
		card.SetTextOrHide("Description", goal.Explain());
	}
}
