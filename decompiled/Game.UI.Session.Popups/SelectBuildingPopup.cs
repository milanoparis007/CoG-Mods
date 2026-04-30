using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Services;
using Game.Session.Entities;
using SomaSim.Util;
using UnityEngine;

namespace Game.UI.Session.Popups;

public sealed class SelectBuildingPopup : BasePopup
{
	public enum Type
	{
		SelectSafehouses
	}

	private sealed class CardContext : MonoBehaviour
	{
		public CardData data;
	}

	private struct CardData
	{
		public EntityID buildingId;

		public string text;
	}

	public const string CLOSE_BUTTON = "Close Button";

	public const string TEMPLATES = "Templates";

	public const string CARD_TEMPLATE = "Templates/Select Building Card";

	public const string POPUP_TITLE = "Info/Title";

	public const string POPUP_DESC = "Info/Description";

	public const string CONTENTS = "Scroll View/Viewport/Content";

	public const string CARD_GOTO_BUTTON = "Goto";

	private PlayerID _pid;

	private Type _type;

	private string _title;

	private string _message;

	private GameObject _tmplCard;

	private GameObject _cardContainer;

	private Action<EntityID> _callback;

	public override UIReference UIReference => UIElements.SelectBuildingPopup;

	public SelectBuildingPopup(PlayerID pid, Type type, string title, string message, Action<EntityID> callback)
	{
		_pid = pid;
		_type = type;
		_title = title;
		_message = message;
		_callback = callback;
	}

	protected override void InitializeOnPush()
	{
		_go.GetButton("Close Button").onClick.SetListener(OnCancel);
		_cardContainer = _go.GetChild("Scroll View/Viewport/Content");
		_tmplCard = _go.GetChild("Templates/Select Building Card");
		_go.SetActive("Templates", value: false);
		_go.SetText("Info/Title", _title);
		_go.SetText("Info/Description", _message);
		RefreshCards();
	}

	protected override void ReleaseOnPop()
	{
		_cardContainer.DestroyAllChildren();
		_cardContainer = (_tmplCard = null);
		_callback = null;
	}

	private void RefreshCards()
	{
		List<CardData> data = CreateCardData();
		_cardContainer.EnsureChildCount(data, _tmplCard);
		_cardContainer.InitializeChildren(data, InitalizeCard);
	}

	private void InitalizeCard(int i, GameObject card, CardData data)
	{
		card.GetOrAddComponent<CardContext>().data = data;
		card.GetChildText().SetText(data.text);
		card.GetButton().onClick.SetListener(delegate
		{
			OnClick(data);
		});
		card.GetButton("Goto").onClick.SetListener(delegate
		{
			OnGotoClick(data);
		});
	}

	private void OnGotoClick(CardData data)
	{
		PersonInfoUtil.TweenCameraToEntity(data.buildingId);
	}

	private void OnClick(CardData data)
	{
		ClickHelper(data.buildingId);
	}

	private void OnCancel()
	{
		ClickHelper(EntityID.INVALID);
	}

	private void ClickHelper(EntityID eid)
	{
		Action<EntityID> callback = _callback;
		Close();
		callback(eid);
	}

	private List<CardData> CreateCardData()
	{
		if (_type == Type.SelectSafehouses)
		{
			return CreatePlayerSafehouses();
		}
		Logger.Warning("Unknown type: " + _type);
		return new List<CardData>();
	}

	private List<CardData> CreatePlayerSafehouses()
	{
		return (from eid in Game.ctx.players.WithID(_pid).territory.GetAllControlledBuildingsUnsafe()
			select MakeSafehouseCardData(eid.FindEntity())).ToList();
	}

	private CardData MakeSafehouseCardData(Entity building)
	{
		BuildingUtil.FindDataForBuilding(building);
		string text = BuildingUtil.FindBuildingName(building.Id);
		string text2 = BuildingUtil.FindBuildingOwnerOrManagerName(building.Id);
		string text3 = Loc.Get("ui.overlays.safehouse.card", "bizName", text, "ownerName", text2);
		return new CardData
		{
			buildingId = building.Id,
			text = text3
		};
	}
}
