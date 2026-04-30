namespace Game.UI.Session.HUD;

public class OverlaysButtonSetDef : HUDDialogButtonSetDef
{
	private OverlaysButtonSetDef()
	{
	}

	public static OverlaysButtonSetDef MakeButtonSet(string shelf, string icon, string text, string mo)
	{
		return new OverlaysButtonSetDef
		{
			spriteName = null,
			icon = icon,
			text = text,
			mo = mo,
			shelf = shelf
		};
	}
}
