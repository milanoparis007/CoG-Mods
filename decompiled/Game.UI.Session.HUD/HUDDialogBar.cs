using System.Collections.Generic;
using Game.Core;
using SomaSim.Util;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.Session.HUD;

public abstract class HUDDialogBar : BaseHUDDialog
{
	internal sealed class ButtonContext : MonoBehaviour
	{
		public HUDDialogItemDefBase def;
	}

	internal sealed class ButtonSetContext : MonoBehaviour
	{
		public HUDDialogItemDefBase def;

		public bool isExpanded = true;
	}

	public static readonly List<HUDDialogItemDefBase> EMPTY = new List<HUDDialogItemDefBase>();

	public const string TEMPLATES = "Templates";

	public const string TMPL_BUTTON = "Templates/HUDDialog Button";

	public const string TMPL_BUTTON_SET = "Templates/HUDDialog Button Set";

	public const string BUTTON_IMAGE = "Image";

	public const string BUTTON_ICON_TEXT = "Text";

	public const string BUTTON_NAME_TEXT = "Name";

	public const string BUTTON_GROUP = "Button Group";

	public const string COLLAPSE_BUTTON = "Collapse Button";

	public const string COLLAPSE_BUTTON_TEXT = "Collapse Button/Text";

	public const string MAINSHELF_NAME = "Shelf";

	public const string SUBSHELF_NAME = "Subshelf";

	public const string CONTENTS_PATH = "Scroll View/Viewport/Content";

	public const string FAVED = "Faved";

	protected GameObject _mainShelf;

	protected GameObject _subShelf;

	protected GameObject _buttonTemplate;

	protected GameObject _buttonSetTemplate;

	protected HUDDialogButtonDef _current;

	private const string BORDER = "Border";

	public override bool ShowAtStartup => false;

	public override TweenType Tween => TweenType.Right;

	public override GroupType Group => GroupType.ConvoGroup;

	internal override void Initialize()
	{
		base.Initialize();
		_mainShelf = _go.GetChild("Shelf");
		_buttonTemplate = _go.GetChild("Templates/HUDDialog Button");
		_buttonSetTemplate = _go.GetChild("Templates/HUDDialog Button Set");
		_go.SetActive("Templates", value: false);
	}

	internal override void Release()
	{
		if (_current != null)
		{
			OnFinish();
		}
		if (_subShelf != null)
		{
			SetShelfButtons(_subShelf, EMPTY);
		}
		SetShelfButtons(_mainShelf, EMPTY);
		_buttonTemplate = null;
		_buttonSetTemplate = null;
		_mainShelf = null;
		_subShelf = null;
		base.Release();
	}

	public override void Toggle()
	{
		if (!base.IsShowing)
		{
			Game.ctx.selection.ClearActive();
		}
		base.Toggle();
	}

	protected override void OnBeforeShow()
	{
		base.OnBeforeShow();
		HideSubShelf();
		Game.ctx.hud.quests.Hide();
	}

	protected override void OnBeforeHide()
	{
		base.OnBeforeHide();
		if (!Game.ctx.IsPreReleaseDone)
		{
			Game.ctx.hud.quests.Show();
		}
		if (_current != null)
		{
			OnFinish();
		}
	}

	protected void SetShelfButtons(GameObject shelf, List<HUDDialogItemDefBase> defs)
	{
		GameObject child = shelf.GetChild("Scroll View/Viewport/Content");
		child.DestroyAllChildren();
		int num = 0;
		for (int i = 0; i < defs.Count; i++)
		{
			GameObject original = null;
			HUDDialogButtonSetDef hUDDialogButtonSetDef = null;
			HUDDialogItemDefBase hUDDialogItemDefBase = defs[i];
			if (!(hUDDialogItemDefBase is HUDDialogButtonDef hUDDialogButtonDef))
			{
				if (hUDDialogItemDefBase is HUDDialogButtonSetDef hUDDialogButtonSetDef2)
				{
					original = _buttonSetTemplate;
					hUDDialogButtonSetDef = hUDDialogButtonSetDef2;
				}
			}
			else
			{
				original = _buttonTemplate;
				if (!hUDDialogButtonDef.visFunc())
				{
					num++;
					continue;
				}
			}
			Object.Instantiate(original, child.transform);
			_ = child.transform.childCount;
			Transform child2 = child.transform.GetChild(i - num);
			HUDDialogItemDefBase def = defs[i];
			bool isMainShelf = shelf == _mainShelf;
			InitializeItem(i, child2.gameObject, def, isMainShelf);
			if (hUDDialogButtonSetDef == null)
			{
				continue;
			}
			foreach (HUDDialogButtonDef button in hUDDialogButtonSetDef.buttons)
			{
				GameObject card = Object.Instantiate(_buttonTemplate, child2.gameObject.GetChild<Transform>("Button Group"));
				InitializeItem(i, card, button, isMainShelf);
			}
		}
	}

	protected void InitializeItem(int i, GameObject card, HUDDialogItemDefBase def, bool isMainShelf)
	{
		if (!(def is HUDDialogButtonSetDef def2))
		{
			if (def is HUDDialogButtonDef def3)
			{
				InitializeButton(i, card, def3, isMainShelf);
			}
		}
		else
		{
			InitializeButtonSet(i, card, def2);
		}
	}

	protected virtual void InitializeButton(int i, GameObject card, HUDDialogButtonDef def, bool isMainShelf)
	{
		Image image = card.GetImage("Image");
		image.gameObject.SetActive(def.HasSpriteName);
		if (def.HasSpriteName)
		{
			image.sprite = Game.ctx.hud.uisprites.uiatlas.Find(def.spriteName);
		}
		TextMeshProUGUI text = card.GetText("Text");
		text.gameObject.SetActive(def.HasIcon);
		if (def.HasIcon)
		{
			text.SetText(def.icon);
		}
		TextMeshProUGUI text2 = card.GetText("Name");
		text2.gameObject.SetActive(def.HasText);
		if (def.HasText)
		{
			text2.SetText(def.text);
		}
		card.GetOrAddComponent<ButtonContext>().def = def;
	}

	protected virtual void InitializeButtonSet(int i, GameObject card, HUDDialogButtonSetDef def)
	{
		TextMeshProUGUI text = card.GetText("Text");
		text.gameObject.SetActive(def.HasIcon);
		if (def.HasIcon)
		{
			text.SetText(def.icon);
		}
		TextMeshProUGUI text2 = card.GetText("Name");
		text2.gameObject.SetActive(def.HasText);
		if (def.HasText)
		{
			text2.SetText(def.text);
		}
		ButtonSetContext context = card.GetOrAddComponent<ButtonSetContext>();
		context.def = def;
		GameObject buttonGroup = card.GetChild("Button Group");
		TextMeshProUGUI collapseText = card.GetText("Collapse Button/Text");
		card.GetButton("Collapse Button").onClick.AddListener(delegate
		{
			ShowOrHide(!context.isExpanded);
		});
		ShowOrHide(context.isExpanded);
		void ShowOrHide(bool show)
		{
			context.isExpanded = show;
			buttonGroup.SetActive(show);
			collapseText.text = (context.isExpanded ? "-" : "+");
			buttonGroup.ForceRebuildLayoutImmediate();
			card.ForceRebuildLayoutImmediate();
		}
	}

	protected virtual void HighlightButtonInAllShelves(HUDDialogButtonDef def)
	{
		HighlightButtonInContainer(_mainShelf, def);
		HighlightButtonInContainer(_subShelf, def);
	}

	protected void ToggleHighlightsInContainer(GameObject button, GameObject container)
	{
		if (button.GetOrAddComponent<ButtonSetContext>().isExpanded)
		{
			ToggleHighlightButtonsBulkInContainer(container, Game.ctx.hud.resourcesBar.SelectedResourcesInOverlay);
		}
		else
		{
			HighlightButtonInContainer(container, null);
		}
	}

	protected void HighlightButtonInContainer(GameObject container, HUDDialogItemDefBase def)
	{
		if (container == null)
		{
			return;
		}
		container.WalkChildren(delegate(Transform tr)
		{
			(bool, Transform, ButtonContext) tuple = CheckButtonWithBorder(tr);
			if (tuple.Item1)
			{
				ButtonContext item = tuple.Item3;
				Transform item2 = tuple.Item2;
				bool active = item.def == def;
				item2.gameObject.SetActive(active);
			}
		});
	}

	protected void ToggleHighlightButton(HUDDialogButtonDef def)
	{
		_mainShelf.WalkChildren(delegate(Transform tr)
		{
			(bool, Transform, ButtonContext) tuple = CheckButtonWithBorder(tr);
			if (tuple.Item1)
			{
				ButtonContext item = tuple.Item3;
				Transform item2 = tuple.Item2;
				bool show = item.def == def;
				ToggleBorder(item2, show);
			}
		});
	}

	protected void ToggleHighlightButtonsBulk(List<Label> items)
	{
		_mainShelf.WalkChildren(delegate(Transform tr)
		{
			(bool, Transform, ButtonContext) tuple = CheckButtonWithBorder(tr);
			if (tuple.Item1)
			{
				ButtonContext item = tuple.Item3;
				Transform item2 = tuple.Item2;
				if (item.def is HUDDialogButtonDef hUDDialogButtonDef)
				{
					bool show = items.Contains(hUDDialogButtonDef.resId);
					ToggleBorder(item2, show);
				}
			}
		});
	}

	protected void ToggleHighlightButtonsBulkInContainer(GameObject container, List<Label> items)
	{
		container.WalkChildren(delegate(Transform tr)
		{
			(bool, Transform, ButtonContext) tuple = CheckButtonWithBorder(tr);
			if (tuple.Item1)
			{
				ButtonContext item = tuple.Item3;
				Transform item2 = tuple.Item2;
				if (item.def is HUDDialogButtonDef hUDDialogButtonDef)
				{
					bool show = items.Contains(hUDDialogButtonDef.resId);
					ToggleBorder(item2, show);
				}
			}
		});
	}

	protected void ToggleBorder(Transform border, bool show)
	{
		if (border.gameObject.activeSelf && show)
		{
			border.gameObject.SetActive(value: false);
		}
		else if (show)
		{
			border.gameObject.SetActive(show);
		}
	}

	protected void ToggleFavoriteDisplay(List<Label> items)
	{
		_mainShelf.WalkChildren(delegate(Transform tr)
		{
			(bool, Transform, ButtonContext) tuple = CheckButtonWithBorder(tr);
			if (tuple.Item1)
			{
				ButtonContext item = tuple.Item3;
				Transform transform = tr.Find("Faved");
				if (!(transform == null) && item.def is HUDDialogButtonDef hUDDialogButtonDef)
				{
					bool active = items.Contains(hUDDialogButtonDef.resId);
					transform.gameObject.SetActive(active);
				}
			}
		});
	}

	internal (bool valid, Transform tr, ButtonContext ctx) CheckButtonWithBorder(Transform tr)
	{
		ButtonContext component = tr.gameObject.GetComponent<ButtonContext>();
		if (component == null)
		{
			return (valid: false, tr: null, ctx: component);
		}
		Transform transform = tr.Find("Border");
		if (!(transform != null))
		{
			return (valid: false, tr: null, ctx: component);
		}
		return (valid: true, tr: transform, ctx: component);
	}

	protected abstract void OnFinish();

	protected void HideSubShelf()
	{
		if (_subShelf != null)
		{
			_subShelf.SetActive(value: false);
		}
	}

	protected void ShowSubShelf(string id)
	{
		List<HUDDialogItemDefBase> itemDefs = GetItemDefs(id);
		SetShelfButtons(_subShelf, itemDefs);
		if (_subShelf != null)
		{
			_subShelf.SetActive(value: true);
		}
	}

	protected abstract List<HUDDialogItemDefBase> GetItemDefs(string id);

	protected abstract string GetMainShelfID();
}
