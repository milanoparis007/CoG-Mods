using System.Collections.Generic;
using Game.Core;
using Game.Services;
using SomaSim.Util;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.UI.Session.Popups;

public sealed class ThroneSelectionPopup : BasePopup
{
	private const string CLOSE_BUTTON = "Close Button";

	private const string CONFIRM_BUTTON = "Footer/Confirm";

	private const string TEMPLATES = "Templates";

	private const string CARD_TEMPLATE = "Templates/Throne Selection Card";

	private const string SELECTION_CONTAINER = "Panel/Scroll View List/Viewport/Content";

	private const string PATTERN = "Panel/Pattern";

	private GameObject _tmplCard;

	private GameObject _cardContainer;

	private Label selectedId;

	private const string TITLE = "Name";

	private const string BACKGROUND = "Image/BG";

	public const string PATTERN_PATH = "UI Images/Decos/";

	public const string RIGHT_DECO = "Name/Deco Right";

	public const string THRONE_NAME = "Name";

	public override UIReference UIReference => UIElements.ThroneRoomSelect;

	protected override void InitializeOnPush()
	{
		_go.GetButton("Close Button").onClick.SetListener(Close);
		_go.GetButton("Footer/Confirm").onClick.SetListener(ConfirmThrone);
		_tmplCard = _go.GetChild("Templates/Throne Selection Card");
		_cardContainer = _go.GetChild("Panel/Scroll View List/Viewport/Content");
		_go.SetActive("Templates", value: false);
		RefreshCards();
	}

	protected override void ReleaseOnPop()
	{
		_cardContainer.DestroyAllChildren();
		_cardContainer = (_tmplCard = null);
	}

	private void RefreshCards()
	{
		List<ThroneSettings.ThroneType> throneTypes = Game.serv.globals.settings.throne.throneTypes;
		_cardContainer.EnsureChildCount(throneTypes, _tmplCard);
		_cardContainer.InitializeChildren(throneTypes, InitalizeCard);
	}

	public override void OnActivated(bool pushed)
	{
		base.OnActivated(pushed);
		GameObject gameObject = _cardContainer.transform.GetChild(0).gameObject;
		gameObject.GetToggle().isOn = true;
		EventSystem.current.SetSelectedGameObject(gameObject);
		gameObject.GetChild("Name/Deco Right").SetActive(value: true);
		gameObject.GetText("Name").color = new Color32(byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue);
	}

	private void InitalizeCard(int i, GameObject card, ThroneSettings.ThroneType data)
	{
		card.SetText("Name", Loc.Get(data.loctitle));
		card.SetImage("Image/BG", Game.ctx.players.Human.throne.GetThroneSprite(data));
		card.GetToggle().onValueChanged.SetListener(delegate(bool val)
		{
			OnCardSelected(data.id, card, val, data);
		});
	}

	private void ConfirmThrone()
	{
		Game.ctx.players.Human.throne.ChooseThroneStyle(selectedId);
		Game.serv.ui.AddPopup(new ThronePopup());
		Close();
	}

	private void OnCardSelected(Label id, GameObject toggle, bool isOn, ThroneSettings.ThroneType data)
	{
		Color32 color = new Color32(150, 150, 150, byte.MaxValue);
		TextMeshProUGUI text = toggle.GetText("Name");
		if (isOn)
		{
			selectedId = id;
			EventSystem.current.SetSelectedGameObject(toggle);
			_go.SetImage("Panel/Pattern", Resources.Load<Sprite>("UI Images/Decos/" + data.selectionBG));
			color = new Color32(byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue);
		}
		toggle.GetChild("Name/Deco Right").SetActive(isOn);
		text.color = color;
	}
}
