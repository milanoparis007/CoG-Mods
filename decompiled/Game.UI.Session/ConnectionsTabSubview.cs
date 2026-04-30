using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Services;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Overlays;
using Game.Session.Player;
using Game.Session.Sim;
using SomaSim.Util;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.Session;

public sealed class ConnectionsTabSubview : PersonInfoSubview
{
	private class Filter
	{
		public string name;

		public ConnFilter filter;

		public Filter(string name, ConnFilter filter)
		{
			this.name = name;
			this.filter = filter;
		}
	}

	private const string FILTER_DD = "Tabs/Dropdown";

	private const string SCROLLVIEW = "Scroll View";

	private const string SCROLLVIEW_CONTENT = "Viewport/Content";

	private const string GO_TEMPLATE = "Templates/Person Info Card";

	private GameObject _connScrollView;

	private GameObject _cardTemplate;

	private TMP_Dropdown _dropdown;

	private List<Filter> _filters;

	private const string CARD_NAME = "Name";

	private const string CARD_PORTRAIT = "Portrait";

	private const string CARD_PORTRAIT_SPRITE = "Portrait/Portrait";

	public ConnectionsTabSubview(GameObject go, PersonInfoController controller, PanelType type, string locicon)
		: base(go, "Panel Connections", controller, type, locicon)
	{
		_connScrollView = panel.GetChild("Scroll View");
		_cardTemplate = go.GetChild("Templates/Person Info Card");
		_dropdown = go.GetChild("Tabs/Dropdown").GetComponent<TMP_Dropdown>();
		_filters = new List<Filter>
		{
			new Filter(Loc.Get("ui.personinfo.filter.everyone"), ConnFilter.None),
			new Filter(Loc.Get("ui.personinfo.filter.family"), ConnFilter.Family),
			new Filter(Loc.Get("ui.personinfo.filter.friends"), ConnFilter.Friends),
			new Filter(Loc.Get("ui.personinfo.filter.gangs"), ConnFilter.Gangs),
			new Filter(Loc.Get("ui.personinfo.filter.goons"), ConnFilter.Goons),
			new Filter(Loc.Get("ui.personinfo.filter.cops"), ConnFilter.Cops)
		};
		if (Game.settings.IsEditor)
		{
			_filters.Add(new Filter("(debug)", ConnFilter.Debug));
		}
		_dropdown.SetOptions(_filters.Select((Filter f) => f.name).ToArray());
		_dropdown.onValueChanged.SetListener(delegate(int index)
		{
			ConnFilter connectionFilter = _filters.GetOrDefaultFast(index)?.filter ?? ConnFilter.None;
			Controller.SetConnectionFilter(connectionFilter);
		});
	}

	public override void RefreshSubview()
	{
		RefreshAllCards();
		_connScrollView.GetComponent<ScrollRect>().normalizedPosition = new Vector2(0f, 1f);
		ShowRelationshipArrows();
	}

	public override void Deactivate()
	{
		Game.ctx.overlays.arrows.HideRelationshipArrows();
		base.Deactivate();
	}

	private void ShowRelationshipArrows()
	{
		Game.ctx.overlays.arrows.HideRelationshipArrows();
		if (!_connScrollView.activeInHierarchy)
		{
			return;
		}
		CardContext[] componentsInChildren = _connScrollView.GetChild("Viewport/Content").GetComponentsInChildren<CardContext>();
		List<EntityID> list = new List<EntityID>();
		CardContext[] array = componentsInChildren;
		for (int i = 0; i < array.Length; i++)
		{
			CardContextData data = array[i].data;
			if (data.CanInteract(Model.connFilter) && data.ShouldShow(Model.connFilter))
			{
				list.Add(data.rel.to);
			}
		}
		if (list.Count != 0)
		{
			Game.ctx.overlays.arrows.ShowRelationshipArrows(Model.entity, list);
		}
	}

	private void RefreshAllCards()
	{
		GameObject child = _connScrollView.GetChild("Viewport/Content");
		List<CardContextData> list = CreateFamilyLinks();
		child.EnsureChildCount(list.Count, _cardTemplate);
		for (int i = 0; i < list.Count; i++)
		{
			InitializeCard(child.transform.GetChild(i).gameObject, list[i]);
		}
	}

	private void InitializeCard(GameObject card, CardContextData data)
	{
		bool flag = data.ShouldShow(Model.connFilter);
		card.SetActive(flag);
		if (!flag)
		{
			return;
		}
		bool flag2 = data.cardPeep == Model.entity;
		bool flag3 = data.CanInteract(Model.connFilter) && !flag2;
		HoverEventButton component = card.GetComponent<HoverEventButton>();
		component.interactable = flag3;
		card.GetOrAddComponent<CardContext>().Set(Model, data);
		string peepInfoForCard = GetPeepInfoForCard(data);
		bool flag4 = true;
		card.SetText("Name", peepInfoForCard);
		if (flag3 || flag2)
		{
			Sprite crewSprite = HUDUtil.GetCrewSprite(flag4 ? data.cardPeep : null);
			card.SetImage("Portrait/Portrait", crewSprite);
			card.SetActive("Portrait", value: true);
		}
		else
		{
			card.SetActive("Portrait", value: false);
		}
		if (flag3)
		{
			component.onClick.SetListener(delegate
			{
				Controller.SwitchToPerson(data.cardPeep);
			});
			component.onPointerEnter.SetListener(delegate
			{
				OverlayArrows.HighlightConnection(data.cardPeep.Id, hl: true);
			});
			component.onPointerExit.SetListener(delegate
			{
				OverlayArrows.HighlightConnection(data.cardPeep.Id, hl: false);
			});
		}
		else
		{
			component.onClick.RemoveAllListeners();
			component.onPointerEnter.RemoveAllListeners();
			component.onPointerExit.RemoveAllListeners();
		}
	}

	private string GetPeepInfoForCard(CardContextData ctx)
	{
		PersonInfoUtil.Overview overview = ctx.overview;
		Entity cardPeep = ctx.cardPeep;
		string item = PersonInfoUtil.GetRelationshipDetails(Model.entity, cardPeep).text;
		return Loc.Get("ui.rel.subview", "name", overview.name, "rel", item, "age", overview.age, "workplace", overview.workplace);
	}

	private List<CardContextData> CreateFamilyLinks()
	{
		List<Relationship> data = Game.ctx.simman.rels.GetListOrNull(Model.entity.Id).data;
		PlayerSocial social = Game.ctx.players.Human.social;
		List<CardContextData> list = new List<CardContextData>(data.Count);
		SimTime now = Game.ctx.clock.Now;
		foreach (Relationship item2 in data)
		{
			Entity entity = item2.to.FindEntity();
			if (entity.data.person != null && entity.data.person != Model.entity.data.person)
			{
				Relationship relationshipFromSourceToPlayer = social.GetRelationshipFromSourceToPlayer(entity.Id);
				CardContextData item = new CardContextData
				{
					cardPeep = entity,
					overview = PersonInfoUtil.GenerateOverview(entity, details: false),
					rel = item2,
					isKnownByHuman = (relationshipFromSourceToPlayer != null),
					isAlive = entity.data.person.IsAlive,
					isAdult = entity.components.person.IsOldEnoughToOwnBiz(now)
				};
				list.Add(item);
			}
		}
		list.StableSort(CardSorter);
		return list;
	}

	private int CardSorter(CardContextData a, CardContextData b)
	{
		PersonData person = a.cardPeep.data.person;
		PersonData person2 = b.cardPeep.data.person;
		if (a.cardPeep.data.agent.pid.IsAnyPlayer)
		{
			return -1;
		}
		if (b.cardPeep.data.agent.pid.IsAnyPlayer)
		{
			return 1;
		}
		if (person.IsEmployed)
		{
			return -1;
		}
		if (person2.IsEmployed)
		{
			return 1;
		}
		if (!person2.IsAlive)
		{
			return -1;
		}
		if (!person.IsAlive)
		{
			return 1;
		}
		RelationshipTracker rels = Game.ctx.simman.rels;
		RelationshipType typeOrNone = rels.GetTypeOrNone(Model.entity.Id, a.cardPeep.Id);
		RelationshipType typeOrNone2 = rels.GetTypeOrNone(Model.entity.Id, b.cardPeep.Id);
		return typeOrNone - typeOrNone2;
	}
}
