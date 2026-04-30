using System.Collections.Generic;
using Game.Services;
using SomaSim.Util;
using UnityEngine;

namespace Game.UI.Session.HUD;

public sealed class ReportsBar : HUDDialogBar
{
	public override bool ShowAtStartup => false;

	public override TweenType Tween => TweenType.Right;

	public override GroupType Group => GroupType.ConvoGroup;

	public override UIReference UIReference => UIElements.ReportsBar;

	internal override void Initialize()
	{
		base.Initialize();
		SetShelfButtons(_mainShelf, ReportsBarItems.GetDefs("main"));
		HighlightButtonInAllShelves(null);
	}

	protected override List<HUDDialogItemDefBase> GetItemDefs(string id)
	{
		return ReportsBarItems.GetDefs(id);
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
			OnStart(def);
		});
	}

	private void OnStart(HUDDialogButtonDef def)
	{
		bool flag = false;
		if (def != null && _current != null && def.shelf != GetMainShelfID())
		{
			flag = _current.shelfToShow == def.shelf || _current.shelf == def.shelf;
		}
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

	protected override void OnFinish()
	{
		OnStart(null);
	}

	protected override void OnBeforeShow()
	{
		base.OnBeforeShow();
		Game.ctx.hud.itemList.Hide();
	}
}
