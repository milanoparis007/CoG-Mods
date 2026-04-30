using System;
using Game.Core;

namespace Game.UI.Session.HUD;

public class HUDDialogButtonDef : HUDDialogItemDefBase
{
	public string shelfToShow;

	public Action onShow;

	public Action onHide;

	public Label resId;

	public Func<bool> visFunc;

	public bool ShowsSubshelf => shelfToShow != null;

	protected HUDDialogButtonDef()
	{
	}
}
