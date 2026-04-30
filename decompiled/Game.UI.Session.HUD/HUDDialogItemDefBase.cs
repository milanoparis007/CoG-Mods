using Game.Services.Store;

namespace Game.UI.Session.HUD;

public abstract class HUDDialogItemDefBase
{
	public string spriteName;

	public string text;

	public string mo;

	public string icon;

	public string shelf;

	public bool HasSpriteName => !string.IsNullOrEmpty(spriteName);

	public bool HasText => !string.IsNullOrEmpty(text);

	public bool HasMo => !string.IsNullOrEmpty(mo);

	public bool HasIcon => !string.IsNullOrEmpty(icon);

	public static bool defaultVisFunc()
	{
		return true;
	}

	public static bool hasShadowGovVisFunc()
	{
		return Game.serv.store.IsPackInstalled(PackID.ShadowGovernment);
	}
}
