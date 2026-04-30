using System;

namespace Game.UI.Session.HUD;

public sealed class ReportsButtonDef : HUDDialogButtonDef
{
	private ReportsButtonDef()
	{
	}

	public static HUDDialogButtonDef MakeShelfButton(string shelf, string spriteName, string text, string mo, Action onShow, Action onHide, Func<bool> visFunc)
	{
		return new ReportsButtonDef
		{
			spriteName = spriteName,
			icon = null,
			text = text,
			mo = mo,
			shelf = shelf,
			onShow = onShow,
			onHide = onHide,
			visFunc = visFunc
		};
	}
}
