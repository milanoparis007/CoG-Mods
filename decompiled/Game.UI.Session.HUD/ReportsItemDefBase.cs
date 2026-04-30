namespace Game.UI.Session.HUD;

public abstract class ReportsItemDefBase
{
	public string spriteName;

	public string text;

	public string mo;

	public string shelf;

	public bool HasSpriteName => !string.IsNullOrEmpty(spriteName);

	public bool HasTextField => !string.IsNullOrEmpty(text);

	public bool HasMoField => !string.IsNullOrEmpty(mo);
}
