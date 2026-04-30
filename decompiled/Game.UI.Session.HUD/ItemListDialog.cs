using System;
using System.Collections.Generic;
using Game.Services;
using Game.Session;
using SomaSim.Util;
using TMPro;
using UnityEngine;

namespace Game.UI.Session.HUD;

public sealed class ItemListDialog : BaseHUDDialog
{
	public interface IEntry : IComparable
	{
		string GetDebug();

		string GetName();

		string GetDescription();

		bool ShowGoTo();

		void OnGoTo();

		string GetIcon();
	}

	private string _title;

	private string _header;

	private List<IEntry> _entries;

	private GameObject _tmplCard;

	private GameObject _templates;

	private GameObject _container;

	private TextMeshProUGUI _description;

	private const string TEMPLATES = "Templates";

	private const string TMPL_CARD = "Templates/Item List Dialog Card";

	private const string CLOSE_BUTTON = "Panel/Close Button";

	private const string TITLE = "Panel/Info/Title";

	private const string HEADER = "Panel/Info/Description";

	private const string CONTAINER = "Panel/Scroll View Items/Viewport/Content";

	private const string DESCRIPTION = "Panel/Scroll View Text/Viewport/Content/Text";

	private const string CARD_TEXT = "Text";

	private const string CARD_GOTO_BUTTON = "Goto";

	private const string CARD_ICON_BG = "Icon BG";

	private const string CARD_ICON = "Icon";

	public override bool ShowAtStartup => false;

	public override TweenType Tween => TweenType.Right;

	public override UIReference UIReference => UIElements.ItemListDialog;

	public override void Show()
	{
		throw new Exception("Use ShowEntries() instead of Show()");
	}

	public void ShowEntries(string title, string desc, List<IEntry> entries, IEntry selected = null)
	{
		if (base.IsShowing)
		{
			Hide();
		}
		_title = title;
		_header = desc;
		_entries = new List<IEntry>(entries);
		_entries.Sort();
		base.Show();
		FakeCardClick(selected);
	}

	protected override void OnBeforeShow()
	{
		base.OnBeforeShow();
		_description.SetText("");
	}

	protected override void OnBeforeHide()
	{
		_entries.Clear();
		_container.gameObject.transform.DestroyAllChildren();
		_description.SetText("");
		base.OnBeforeHide();
	}

	internal override void Initialize()
	{
		base.Initialize();
		_templates = _go.GetChild("Templates");
		_templates.SetActive(value: false);
		_tmplCard = _go.GetChild("Templates/Item List Dialog Card");
		_container = _go.GetChild("Panel/Scroll View Items/Viewport/Content");
		_description = _go.GetText("Panel/Scroll View Text/Viewport/Content/Text");
		_go.GetButton("Panel/Close Button").onClick.SetListener(OnClose);
		Game.ctx.events.AddListener(SessionEventType.SelectionActivationChange, OnCurrentActiveChanged);
	}

	internal override void Release()
	{
		Game.ctx.events.RemoveListener(SessionEventType.SelectionActivationChange, OnCurrentActiveChanged);
		_templates = (_tmplCard = (_container = null));
		_description = null;
		base.Release();
	}

	private void OnCurrentActiveChanged(SessionEvent _)
	{
		Hide();
	}

	private void OnClose()
	{
		Hide();
	}

	protected override void RefreshContents()
	{
		_go.SetText("Panel/Info/Title", _title);
		_go.SetText("Panel/Info/Description", _header);
		_container.EnsureChildCount(_entries, _tmplCard);
		_container.InitializeChildren(_entries, RefreshCard);
	}

	private void RefreshCard(int i, GameObject card, IEntry entry)
	{
		string icon = entry.GetIcon();
		string name = entry.GetName();
		card.SetText("Text", name);
		card.SetText("Icon", icon);
		card.SetActive("Icon BG", !string.IsNullOrEmpty(icon));
		card.GetButton().onClick.SetListener(delegate
		{
			OnCardClick(entry);
		});
		card.GetButton("Goto").onClick.SetListener(entry.OnGoTo);
		card.SetActive("Goto", entry.ShowGoTo());
	}

	private void OnCardClick(IEntry entry)
	{
		_description.SetText(entry.GetDescription());
	}

	private void FakeCardClick(IEntry entry = null)
	{
		if (entry == null)
		{
			return;
		}
		int num = _entries.IndexOf(entry);
		if (num >= 0)
		{
			Transform child = _container.transform.GetChild(num);
			if (!(child == null))
			{
				child.gameObject.GetButton().Select();
				OnCardClick(entry);
			}
		}
	}
}
