using System;
using Game.Core;

namespace Game.UI.Session.HUD;

public sealed class OverlaysButtonDef : HUDDialogButtonDef
{
	private OverlaysButtonDef()
	{
	}

	public static OverlaysButtonDef MakeShelfButton(string shelf, string spriteName, string icon, string text, string mo, Action onShow, Action onHide, Func<bool> visFunc)
	{
		return new OverlaysButtonDef
		{
			spriteName = spriteName,
			icon = icon,
			text = text,
			mo = mo,
			shelf = shelf,
			shelfToShow = null,
			onShow = onShow,
			onHide = onHide,
			visFunc = visFunc
		};
	}

	public static OverlaysButtonDef MakeShelfButton(string shelf, string spriteName, string icon, string text, string mo, string shelfToShow)
	{
		return new OverlaysButtonDef
		{
			spriteName = spriteName,
			icon = icon,
			text = text,
			mo = mo,
			shelf = shelf,
			shelfToShow = shelfToShow,
			onShow = null,
			onHide = null,
			visFunc = HUDDialogItemDefBase.defaultVisFunc
		};
	}

	public static OverlaysButtonDef MakeShelfButton(string shelf, string spriteName, string icon, string text, string mo, Action onShow, Action onHide, Label resid)
	{
		return new OverlaysButtonDef
		{
			spriteName = spriteName,
			icon = icon,
			text = text,
			mo = mo,
			shelf = shelf,
			shelfToShow = null,
			onShow = onShow,
			onHide = onHide,
			resId = resid,
			visFunc = HUDDialogItemDefBase.defaultVisFunc
		};
	}
}
