using System;
using System.Collections.Generic;
using System.Linq;
using Game.Services;
using SomaSim.Util;
using UnityEngine;

namespace Game.UI.Session.Popups;

public class EncyclopediaPopup : BasePopup
{
	public class CardContext : MonoBehaviour
	{
		public Encyclopedia.Topic topic;
	}

	public class PanelContext : MonoBehaviour
	{
		public EncyclopediaSettings.Category cat;

		public bool expanded;
	}

	public const string TEMPLATES = "Templates";

	public const string TMPL_CARD = "Templates/Encyclopedia Topic Card";

	public const string TITLE_TEXT = "Panel/Title";

	public const string BUTTON_CLOSE = "Panel/Close";

	public const string BUTTON_BACK = "Panel/Left";

	public const string BUTTON_TOGGLE = "Panel/Toggle";

	public const string CARD_CONTAINER = "Panel/List/Viewport/Content";

	public const string CATEGORY_PANEL = "Templates/Category Panel";

	public const string DESCRIPTION_TEXT = "Panel/Description/Viewport/Content/Text";

	private GameObject _tmplCard;

	private GameObject _container;

	private GameObject _catpanel;

	private List<EncyclopediaSettings.Category> _allCategories;

	private List<Encyclopedia.Topic> _allTopics;

	private List<string> _stack;

	private const string CARD_BUTTON = "Button";

	private const string CARD_TEXT = "Button/Text";

	private const string CARD_ICON = "Button/Icon";

	private const string PANEL_BUTTON = "Button";

	private const string PANEL_CARDS = "Cards";

	private const string PANEL_TEXT = "Text";

	private const string PANEL_DROPDOWN = "Drop";

	private const string DROPDOWN_BUTTON_TEXT = "Drop/Text";

	public override UIReference UIReference => UIElements.EncyclopediaPopup;

	protected override void InitializeOnPush()
	{
		_container = _go.GetChild("Panel/List/Viewport/Content");
		_catpanel = _go.GetChild("Templates/Category Panel");
		_tmplCard = _go.GetChild("Templates/Encyclopedia Topic Card");
		_go.SetActive("Templates", value: false);
		_go.SetButtonListener("Panel/Close", Close);
		_go.SetButtonListener("Panel/Left", PopAndShow);
		_go.SetToggleListener("Panel/Toggle", ToggleSort);
		_go.SetText("Panel/Title", Loc.Get("pedia.dialog.title"));
		_go.SetText("Panel/Description/Viewport/Content/Text", Loc.Get("pedia.dialog.default-text"));
		_stack = new List<string>();
	}

	protected override void ReleaseOnPop()
	{
		_stack = null;
		_container = (_tmplCard = null);
		_allTopics = null;
		_allCategories = null;
	}

	public IEnumerable<Encyclopedia.Topic> FindTopicsForCategory(EncyclopediaSettings.Category cat)
	{
		return _allTopics.Where((Encyclopedia.Topic topic) => cat.entries.Contains("info." + topic.id));
	}

	public override void OnActivated(bool pushed)
	{
		base.OnActivated(pushed);
		if (pushed)
		{
			_allTopics = Game.serv.loc.pedia.FindAllSorted().ToList();
			_allCategories = Game.serv.globals.settings.general.encyclopedia.categories.ToList();
			List<string> list = new List<string>();
			for (int i = 0; i < _allCategories.Count; i++)
			{
				list.AddRange(Game.serv.globals.settings.general.encyclopedia.categories[i].entries);
			}
			foreach (Encyclopedia.Topic allTopic in _allTopics)
			{
				list.Contains("info." + allTopic.id);
			}
			foreach (string topic in list)
			{
				_allTopics.Find((Encyclopedia.Topic topic2) => "info." + topic2.id == topic);
			}
		}
		_go.GetToggle("Panel/Toggle").SetIsOnWithoutNotify(value: false);
		ToggleSort(isSortAlpha: false);
		RefreshBackButton();
	}

	private void RefreshCard(int _, GameObject card, Encyclopedia.Topic topic)
	{
		if (card.transform.Find("Button") != null)
		{
			card.GetOrAddComponent<CardContext>().topic = topic;
			card.SetTextOrHide("Button/Text", topic.cachedName);
			card.SetTextOrHide("Button/Icon", topic.cachedIcon);
			card.GetButton("Button").onClick.SetListener(delegate
			{
				PushAndShow(topic.id);
			});
		}
	}

	private void RefreshCategory(int _, GameObject catpanel, EncyclopediaSettings.Category cat)
	{
		List<Encyclopedia.Topic> data = FindTopicsForCategory(cat).ToList();
		catpanel.GetOrAddComponent<PanelContext>().cat = cat;
		catpanel.GetOrAddComponent<PanelContext>().expanded = true;
		GameObject child = catpanel.GetChild("Cards");
		child.EnsureChildCount(data, _tmplCard);
		child.InitializeChildren(data, RefreshCard);
		catpanel.SetTextOrHide("Text", Loc.Get(cat.locname));
		catpanel.GetButton("Drop").onClick.SetListener(delegate
		{
			ToggleDrop(catpanel);
		});
	}

	private void RefreshBackButton()
	{
		_go.GetButton("Panel/Left").interactable = _stack.Count > 1;
	}

	private void ShowEntryHelper()
	{
		string id = _stack.LastOrDefaultFast();
		Encyclopedia.Topic topic = _allTopics.Find((Encyclopedia.Topic t) => t.id == id);
		if (topic != null)
		{
			string text = Loc.Get("pedia.dialog.entry", "name", topic.cachedName, "desc", topic.cachedDesc);
			_go.SetText("Panel/Description/Viewport/Content/Text", text);
		}
	}

	private void PushAndShow(string id)
	{
		if (!(id == _stack.LastOrDefaultFast()))
		{
			_stack.Add(id);
			ShowEntryHelper();
			RefreshBackButton();
		}
	}

	private void PopAndShow()
	{
		_stack.RemoveLastOrDefault();
		ShowEntryHelper();
		RefreshBackButton();
	}

	private void ToggleSort(bool isSortAlpha)
	{
		try
		{
			_container.DestroyAllChildren();
			if (isSortAlpha)
			{
				_container.EnsureChildCount(_allTopics, _tmplCard);
				_container.InitializeChildren(_allTopics, RefreshCard);
			}
			else
			{
				_container.EnsureChildCount(_allCategories, _catpanel);
				_container.InitializeChildren(_allCategories, RefreshCategory);
			}
		}
		catch (Exception ex)
		{
			Game.serv.stats.LogException(ex);
		}
	}

	private void ToggleDrop(GameObject catpanel)
	{
		if (catpanel.GetOrAddComponent<PanelContext>().expanded)
		{
			foreach (Transform item in catpanel.GetChild("Cards").transform)
			{
				item.gameObject.SetActive(value: false);
			}
			catpanel.SetTextOrHide("Drop/Text", "+");
			catpanel.GetOrAddComponent<PanelContext>().expanded = false;
		}
		else
		{
			foreach (Transform item2 in catpanel.GetChild("Cards").transform)
			{
				item2.gameObject.SetActive(value: true);
			}
			catpanel.SetTextOrHide("Drop/Text", "-");
			catpanel.GetOrAddComponent<PanelContext>().expanded = true;
		}
		catpanel.ForceRebuildLayoutImmediate();
	}

	internal static void HandleLinkClick(string link)
	{
		EncyclopediaPopup encyclopediaPopup = Game.serv.ui.TopPopupUnsafe as EncyclopediaPopup;
		if (encyclopediaPopup == null)
		{
			encyclopediaPopup = Game.serv.ui.AddPopup<EncyclopediaPopup>();
		}
		encyclopediaPopup.PushAndShow(link);
	}
}
