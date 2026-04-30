namespace Game.Services;

public struct LocKey
{
	public string lang;

	public string key;

	public bool IsDefaultLang => lang == "en";

	public LocKey(string key, string lang)
	{
		this.key = key;
		this.lang = lang;
	}

	public LocKey(string key)
		: this(key, "en")
	{
	}

	public LocKey ToDefaultLang()
	{
		return new LocKey(key);
	}

	public override string ToString()
	{
		return $"{lang}/{key}";
	}
}
