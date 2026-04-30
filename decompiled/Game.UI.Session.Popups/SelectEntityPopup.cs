using System;
using System.Collections.Generic;
using Game.Core;
using Game.Services;
using Game.Session;
using Game.Session.Entities;
using Game.Session.Overlays;
using SomaSim.Util;
using UnityEngine;

namespace Game.UI.Session.Popups;

public sealed class SelectEntityPopup : BasePopup
{
	private enum SelectEntityType
	{
		Person,
		Building
	}

	private sealed class CardContext : MonoBehaviour
	{
		public CardData data;
	}

	private struct CardData
	{
		public Entity peep;

		public Sprite portrait;

		public string text;
	}

	private const string CLOSE_BUTTON = "Close Button";

	private const string TEMPLATES = "Templates";

	private const string CARD_TEMPLATE = "Templates/Select Person Card";

	private const string POPUP_TITLE = "Info/Title";

	private const string CONTENTS = "Scroll View/Viewport/Content";

	private const string CARD_GOTO_BUTTON = "Goto";

	private const string CARD_PORTRAIT_PARENT = "Portrait";

	private const string CARD_PORTRAIT_IMG = "Portrait/Portrait";

	private EntityID _npcSource;

	private List<EntityID> _ids;

	private string _title;

	private GameObject _tmplCard;

	private GameObject _cardContainer;

	private Action<EntityID> _callback;

	private SelectEntityType _type;

	public override UIReference UIReference => UIElements.SelectPersonPopup;

	public SelectEntityPopup(IEnumerable<EntityID> peeps, EntityID npcSource, string title, Action<EntityID> callback)
	{
		_npcSource = npcSource;
		_ids = new List<EntityID>(peeps);
		_title = title;
		_callback = callback;
		_type = ((_ids[0].FindEntity().data.person == null) ? SelectEntityType.Building : SelectEntityType.Person);
	}

	protected override void InitializeOnPush()
	{
		Game.ctx.events.AddListener(SessionEventType.ConversationEnded, OnConvoEnded);
		_go.GetButton("Close Button").onClick.SetListener(OnCancel);
		_cardContainer = _go.GetChild("Scroll View/Viewport/Content");
		_tmplCard = _go.GetChild("Templates/Select Person Card");
		_go.SetActive("Templates", value: false);
		_go.SetText("Info/Title", _title);
		Game.ctx.overlays.arrows.ShowRelationshipArrows(_npcSource.FindEntity(), _ids, suppress: true, _type == SelectEntityType.Person);
		Game.ctx.events.AddListener(SessionEventType.UIRelSelectMade, ChooseFromOverlay);
		RefreshCards();
	}

	protected override void ReleaseOnPop()
	{
		Game.ctx.events.RemoveListener(SessionEventType.ConversationEnded, OnConvoEnded);
		Game.ctx.events.RemoveListener(SessionEventType.UIRelSelectMade, ChooseFromOverlay);
		Game.ctx.overlays.arrows.HideRelationshipArrows();
		_cardContainer.DestroyAllChildren();
		_cardContainer = (_tmplCard = null);
		_callback = null;
		_ids = null;
	}

	private void OnConvoEnded(SessionEvent sev)
	{
		if (sev.pid.IsHumanPlayer)
		{
			Close();
		}
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
		HoverEventButton component = card.GetComponent<HoverEventButton>();
		component.onClick.SetListener(delegate
		{
			OnClick(data);
		});
		component.onPointerEnter.SetListener(delegate
		{
			OverlayArrows.HighlightConnection(data.peep.Id, hl: true);
		});
		component.onPointerExit.SetListener(delegate
		{
			OverlayArrows.HighlightConnection(data.peep.Id, hl: false);
		});
		card.GetButton("Goto").onClick.SetListener(delegate
		{
			OnGotoClick(data);
		});
		if (data.portrait != null)
		{
			card.SetImage("Portrait/Portrait", data.portrait);
		}
		else
		{
			card.GetChild("Portrait").SetActive(value: false);
		}
	}

	private static void OnGotoClick(CardData data)
	{
		PersonInfoUtil.TweenCameraToEntity(data.peep.Id);
	}

	private void OnClick(CardData data)
	{
		ClickHelper(data.peep.Id);
	}

	private void OnCancel()
	{
		ClickHelper(EntityID.INVALID);
	}

	public void ChooseFromOverlay(SessionEvent sessionEvent)
	{
		ClickHelper(sessionEvent.eid);
	}

	private void ClickHelper(EntityID eid)
	{
		Action<EntityID> callback = _callback;
		Close();
		callback(eid);
	}

	private List<CardData> CreateCardData()
	{
		if (_ids[0].FindEntity().components.person == null)
		{
			return _ids.SelectIntoNewList(delegate(EntityID entityId)
			{
				Entity entity = entityId.FindEntity();
				string gamblingHouseName = BuildingUtil.GetGamblingHouseName(entity);
				int potentialGamblingCustomers = BuildingUtil.GetPotentialGamblingCustomers(PlayerID.HumanPlayer, entity.components.board.GetNode(), entity);
				string text = Loc.Get("convo.ticket-gambling-house.population", "population", potentialGamblingCustomers);
				string text2 = Loc.Get("convo.select.gambling.card", "name", gamblingHouseName, "population", text);
				return new CardData
				{
					peep = entity,
					portrait = null,
					text = text2
				};
			});
		}
		return _ids.SelectIntoNewList(delegate(EntityID peepId)
		{
			Entity entity = peepId.FindEntity();
			Sprite crewSprite = HUDUtil.GetCrewSprite(entity);
			string text = PersonInfoUtil.GenerateEmploymentString(entity);
			string text2 = PersonInfoUtil.GeneratePeepName(entity, showRank: false);
			string text3 = Loc.Get("convo.select.intro.card", "name", text2, "emp", text);
			return new CardData
			{
				peep = entity,
				portrait = crewSprite,
				text = text3
			};
		});
	}
}
