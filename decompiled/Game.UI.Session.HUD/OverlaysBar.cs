using System.Collections.Generic;
using Game.Services;
using Game.UI.Mouseovers;
using SomaSim.Util;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.Session.HUD;

public sealed class OverlaysBar : HUDDialogBar
{
	public override UIReference UIReference => UIElements.OverlaysBar;

	internal override void Initialize()
	{
		base.Initialize();
		OverlaysBarItems.InitializeEth();
		_subShelf = _go.GetChild("Subshelf");
		SetShelfButtons(_mainShelf, OverlaysBarItems.GetDefs("main"));
		SetShelfButtons(_subShelf, HUDDialogBar.EMPTY);
		Game.serv.mouseovers.Register(MouseoverType.HUDDialogBar, new HUDDialogBarMouseover());
	}

	internal override void Release()
	{
		Game.serv.mouseovers.Unregister(MouseoverType.HUDDialogBar);
		base.Release();
	}

	protected override List<HUDDialogItemDefBase> GetItemDefs(string id)
	{
		return OverlaysBarItems.GetDefs(id);
	}

	protected override string GetMainShelfID()
	{
		return "main";
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

	private bool KeepSubshelfOnButtonClick(HUDDialogButtonDef def)
	{
		bool result = false;
		if (def != null && _current != null && def.shelf != GetMainShelfID())
		{
			result = _current.shelfToShow == def.shelf || _current.shelf == def.shelf;
		}
		return result;
	}

	public void ShowOrHide(HUDDialogButtonDef def)
	{
		if (_current != def)
		{
			bool flag = KeepSubshelfOnButtonClick(def);
			if (_current != null)
			{
				if (_current.ShowsSubshelf && !flag)
				{
					HideSubShelf();
				}
				_current.onHide?.Invoke();
				_current = null;
			}
			if (def != null)
			{
				_current = def;
				_current.onShow?.Invoke();
				if (_current.shelf == GetMainShelfID())
				{
					HideSubShelf();
				}
				if (_current.ShowsSubshelf)
				{
					ShowSubShelf(_current.shelfToShow);
				}
			}
		}
		HighlightButtonInAllShelves(_current);
	}

	protected override void OnFinish()
	{
		ShowOrHide(null);
	}
}
