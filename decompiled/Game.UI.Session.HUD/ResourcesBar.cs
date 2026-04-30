using System.Collections.Generic;
using Game.Core;
using Game.Services;
using Game.Session;
using SomaSim.Util;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.Session.HUD;

public sealed class ResourcesBar : HUDDialogBar
{
	private bool _isResDirty;

	private bool _inFaves;

	private List<Label> _selected = new List<Label>();

	public const string FAVORITES_BAR = "Favorites Bar";

	public const string FAVORITES_BAR_TEXT = "Favorites Bar/Text";

	public const string ADD_FAVORITES = "Favorites Bar/Add Faves";

	public const string TOGGLE_FAVORITES = "Favorites Bar/Toggle Faves";

	public const string ADD_FAVORITES_ICON = "Favorites Bar/Add Faves/Text";

	public const string TOGGLE_FAVORITES_ICON = "Favorites Bar/Toggle Faves/Text";

	public override UIReference UIReference => UIElements.ResourcesBar;

	public List<Label> SelectedResourcesInOverlay => _selected;

	internal override void Initialize()
	{
		base.Initialize();
		_isResDirty = true;
		Game.ctx.events.AddListener(SessionEventType.PlayerResourcesChanged, OnResourcesChanged);
		_mainShelf.SetTextOrHide("Favorites Bar/Text", Loc.Get("ui.resources.favorite-bar"));
		_mainShelf.SetTextOrHide("Favorites Bar/Add Faves/Text", Loc.Get("ui.resources.add-favorite.icon"));
		_mainShelf.SetTextOrHide("Favorites Bar/Toggle Faves/Text", Loc.Get("ui.resources.toggle-favorite.icon.off"));
		_mainShelf.GetButton("Favorites Bar/Add Faves").onClick.SetListener(AddFavorites);
		_mainShelf.GetButton("Favorites Bar/Toggle Faves").onClick.SetListener(ToggleFavorites);
	}

	private void OnResourcesChanged(SessionEvent ev)
	{
		if (ev.pid.IsHumanPlayer)
		{
			_isResDirty = true;
			RefreshSilentOrShow(silent: true);
		}
	}

	protected override List<HUDDialogItemDefBase> GetItemDefs(string _)
	{
		return ResourcesBarItems.BUTTONS;
	}

	protected override string GetMainShelfID()
	{
		return "main";
	}

	protected override void RefreshContents()
	{
		RefreshSilentOrShow();
	}

	private void RefreshSilentOrShow(bool silent = false)
	{
		base.RefreshContents();
		if (_isResDirty)
		{
			ResourcesBarItems.InitializeResourcesButtonDefs();
			_isResDirty = false;
		}
		SetShelfButtons(_mainShelf, ResourcesBarItems.BUTTONS);
		_mainShelf.ForceRebuildLayoutImmediate();
		if (!silent)
		{
			ToggleFavorites();
			ToggleFavoriteDisplay(Game.ctx.overlays.data.favorites);
		}
	}

	protected override void InitializeButton(int i, GameObject card, HUDDialogButtonDef def, bool isMainShelf)
	{
		base.InitializeButton(i, card, def, isMainShelf);
		card.GetButton().onClick.SetListener(delegate
		{
			ShowOrHide(def);
		});
	}

	protected override void InitializeButtonSet(int i, GameObject card, HUDDialogButtonSetDef def)
	{
		base.InitializeButtonSet(i, card, def);
		Button button = card.GetButton("Collapse Button");
		GameObject collapseGO = card.GetChild("Collapse Button");
		GameObject buttonGroup = card.GetChild("Button Group");
		button.onClick.AddListener(delegate
		{
			ToggleHighlightsInContainer(collapseGO, buttonGroup);
		});
		card.ForceRebuildLayoutImmediate();
	}

	private void ShowOrHide(HUDDialogButtonDef def)
	{
		if (_current != null)
		{
			_current.onHide?.Invoke();
			_current = null;
		}
		if (def != null)
		{
			_current = def;
			_current.onShow?.Invoke();
			Label resId = def.resId;
			if (_selected.Contains(resId))
			{
				_selected.Remove(resId);
			}
			else
			{
				_selected.Add(resId);
			}
		}
		_inFaves = false;
		_mainShelf.SetTextOrHide("Favorites Bar/Toggle Faves/Text", Loc.Get("ui.resources.toggle-favorite.icon.off"));
		ToggleHighlightButton(_current);
	}

	private void ShowOrHideBulk(List<Label> defs)
	{
		List<Resource> list = new List<Resource>();
		foreach (Label def in defs)
		{
			list.Add(Game.ctx.simman.FindResource(def));
			if (_selected.Contains(def))
			{
				_selected.Remove(def);
			}
			else
			{
				_selected.Add(def);
			}
		}
		Game.ctx.overlays.ToggleResourceInOverlay(list);
		ToggleHighlightButtonsBulk(defs);
	}

	protected override void OnFinish()
	{
		ShowOrHide(null);
	}

	protected override void OnBeforeHide()
	{
		HighlightButtonInAllShelves(null);
		_inFaves = false;
		_selected.Clear();
		Game.ctx.overlays.HideAnyOverlay();
		base.OnBeforeHide();
	}

	private void AddFavorites()
	{
		List<Label> list = new List<Label>();
		foreach (Label item in _selected)
		{
			list.Add(item);
		}
		ToggleFavoriteDisplay(list);
		Game.ctx.overlays.data.favorites = new List<Label>(list);
		if (Game.ctx.overlays.data.favorites.Count != 0)
		{
			_mainShelf.SetTextOrHide("Favorites Bar/Toggle Faves/Text", Loc.Get("ui.resources.toggle-favorite.icon.on"));
		}
		_inFaves = true;
	}

	private void ToggleFavorites()
	{
		if (Game.ctx.overlays.data.favorites.Count != 0)
		{
			List<Label> defs = new List<Label>(_selected);
			if (_inFaves)
			{
				_mainShelf.SetTextOrHide("Favorites Bar/Toggle Faves/Text", Loc.Get("ui.resources.toggle-favorite.icon.off"));
				ShowOrHideBulk(defs);
				_inFaves = false;
			}
			else
			{
				_mainShelf.SetTextOrHide("Favorites Bar/Toggle Faves/Text", Loc.Get("ui.resources.toggle-favorite.icon.on"));
				ShowOrHideBulk(defs);
				ShowOrHideBulk(Game.ctx.overlays.data.favorites);
				_inFaves = true;
			}
		}
	}
}
