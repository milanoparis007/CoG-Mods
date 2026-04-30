using System;
using System.Collections.Generic;
using Game.Services;
using SomaSim.Util;
using TMPro;
using UnityEngine;

namespace Game.UI.Session.HUD;

public sealed class ItemListPopup<T> : BasePopup where T : class, ItemListPopup<T>.IEntry
{
	public interface IEntry
	{
		string GetDebug();

		string GetName();

		string GetDescription();

		string GetIcon();

		bool GetIsAvailable();
	}

	private string _title;

	private string _header;

	private List<T> _entries;

	private Action<T> _callback;

	private T _selected;

	private GameObject _tmplCard;

	private GameObject _templates;

	private GameObject _container;

	private TextMeshProUGUI _description;

	private const string TEMPLATES = "Templates";

	private const string TMPL_CARD = "Templates/Item List Card";

	private const string BTN_CLOSE = "Panel/Close Button";

	private const string TITLE = "Panel/Info/Title";

	private const string HEADER = "Panel/Info/Description";

	private const string CONTAINER = "Panel/Scroll View Items/Viewport/Content";

	private const string DESCRIPTION = "Panel/Scroll View Text/Viewport/Content/Text";

	private const string BTN_OK = "Panel/Footer/Ok";

	private const string BTN_CANCEL = "Panel/Footer/Cancel";

	private const string CARD_TEXT = "Text";

	private const string CARD_GOTO_BUTTON = "Goto";

	private const string CARD_ICON = "Icon";

	public override UIReference UIReference => UIElements.ItemListPopup;

	public ItemListPopup(string title, string desc, List<T> entries, Action<T> callback)
	{
		_title = title;
		_header = desc;
		_entries = entries;
		_callback = callback;
		_selected = null;
	}

	protected override void InitializeOnPush()
	{
		_templates = _go.GetChild("Templates");
		_templates.SetActive(value: false);
		_tmplCard = _go.GetChild("Templates/Item List Card");
		_container = _go.GetChild("Panel/Scroll View Items/Viewport/Content");
		_description = _go.GetText("Panel/Scroll View Text/Viewport/Content/Text");
		_go.GetButton("Panel/Close Button").onClick.SetListener(OnClose);
		_go.GetButton("Panel/Footer/Cancel").onClick.SetListener(OnClose);
		_go.GetButton("Panel/Footer/Ok").onClick.SetListener(OnConfirm);
	}

	private void OnClose()
	{
		Close();
	}

	private void OnConfirm()
	{
		_callback(_selected);
		Close();
	}

	protected override void ReleaseOnPop()
	{
		_container.transform.DestroyAllChildren();
		_description.SetText("");
		_selected = null;
		_templates = (_tmplCard = (_container = null));
		_description = null;
	}

	public override void OnActivated(bool pushed)
	{
		base.OnActivated(pushed);
		RefreshContents();
	}

	private void RefreshContents()
	{
		_go.SetText("Panel/Info/Title", _title);
		_go.SetText("Panel/Info/Description", _header);
		_container.EnsureChildCount(_entries, _tmplCard);
		_container.InitializeChildren(_entries, RefreshCard);
		_selected = null;
		RefreshButtons();
	}

	private void RefreshButtons()
	{
		_go.GetButton("Panel/Footer/Ok").interactable = _selected?.GetIsAvailable() ?? false;
	}

	private void RefreshCard(int i, GameObject card, T entry)
	{
		card.SetText("Text", entry.GetName());
		card.SetText("Icon", entry.GetIcon());
		card.GetButton().onClick.SetListener(delegate
		{
			OnCardClick(entry);
		});
		card.SetActive("Goto", value: false);
	}

	private void OnCardClick(T entry)
	{
		_description.SetText(entry.GetDescription());
		_selected = entry;
		RefreshButtons();
	}
}
