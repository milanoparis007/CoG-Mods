using System.Collections.Generic;
using Game.Services;
using Game.Session.Entities;
using Game.Session.Sim;
using Game.UI.Session.Ledger;
using SomaSim.Util;
using UnityEngine;

namespace Game.UI.Session.Popups;

public class VictoryPopup : BasePopup
{
	public class CardContext : MonoBehaviour
	{
		public VictoryGoal goal;
	}

	public const string TEMPLATES = "Templates";

	public const string TMPL_CARD = "Templates/Victory Popup Card";

	public const string MAIN_TITLE_TEXT = "Panel/Title";

	public const string MAIN_DESC_TEXT = "Panel/Description";

	public const string BUTTON_CLOSE = "Panel/Close";

	public const string CARD_CONTAINER = "Panel/List/Viewport/Content";

	public const string DESCRIPTION_TEXT = "Panel/Description/Viewport/Content/Text";

	private GameObject _tmplCard;

	private GameObject _container;

	private bool showGameOver;

	private bool showLedger;

	private const string CARD_BUTTON = "Button";

	private const string CARD_TEXT = "Button/Text";

	private const string CARD_ICON = "Button/Icon";

	public override UIReference UIReference => UIElements.VictoryPopup;

	public VictoryPopup(bool showGameOverOnClose, bool showLedgerOnClose)
	{
		showGameOver = showGameOverOnClose;
		showLedger = showLedgerOnClose;
	}

	protected override void InitializeOnPush()
	{
		_container = _go.GetChild("Panel/List/Viewport/Content");
		_tmplCard = _go.GetChild("Templates/Victory Popup Card");
		_go.SetActive("Templates", value: false);
		_go.SetButtonListener("Panel/Close", Close);
		Entity playerPeep = Game.ctx.players.Human.social.GetPlayerPeep();
		_go.SetText("Panel/Title", Loc.Get("victory.title"));
		_go.SetText("Panel/Description", Loc.Get("victory.desc", "date", Loc.FormatDateLong(Game.serv.globals.settings.general.generator.GetEndOfGame())));
		_go.SetText("Panel/Description/Viewport/Content/Text", Loc.GetGendered("victory.flavor", playerPeep.data.person.g, "cityname", Game.ctx.session.mapconfig.CityName));
	}

	protected override void ReleaseOnPop()
	{
		_container = null;
		_tmplCard = null;
		if (showGameOver)
		{
			TimerUtil.RunAfterTime(ShowGameOverNextFrame, 0.1f);
		}
		else if (showLedger)
		{
			Game.ctx.hud.ledger.Controller.ShowReport(ReportType.NetWorth);
		}
	}

	private static void ShowGameOverNextFrame()
	{
		VictorySettings victory = Game.serv.globals.settings.general.victory;
		string header = Loc.Get(victory.endHeadline);
		PhotoConfig endPhoto = victory.endPhoto;
		Game.serv.ui.AddPopup(new NewspaperPopup(header, endPhoto, delegate
		{
			Game.serv.ui.AddPopup(new GameFinishedPopup());
		}));
	}

	public override void OnActivated(bool pushed)
	{
		base.OnActivated(pushed);
		if (pushed)
		{
			List<VictoryGoal> allGoals = Game.ctx.simman.victory.GetAllGoals();
			_container.EnsureChildCount(allGoals, _tmplCard);
			_container.InitializeChildren(allGoals, RefreshCard);
		}
	}

	private void RefreshCard(int _, GameObject card, VictoryGoal goal)
	{
		card.GetOrAddComponent<CardContext>().goal = goal;
		card.SetTextOrHide("Button/Text", goal.GetName());
		card.SetTextOrHide("Button/Icon", goal.GetIcon());
		card.GetButton("Button").onClick.SetListener(delegate
		{
			Show(goal);
		});
	}

	private void Show(VictoryGoal goal)
	{
		string text = goal.Explain();
		_go.SetText("Panel/Description/Viewport/Content/Text", text);
	}
}
