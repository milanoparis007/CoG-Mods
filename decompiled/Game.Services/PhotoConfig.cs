namespace Game.Services;

public struct PhotoConfig
{
	public string sprite;

	public string text;

	public string lockey;

	public bool HasSprite => sprite != null;

	public bool HasText => text != null;

	public bool HasLockey => lockey != null;

	public PhotoConfig(string sprite, string text = null)
	{
		this.sprite = sprite;
		this.text = text;
		lockey = null;
	}

	public string ProduceText()
	{
		string obj = text;
		if (obj == null)
		{
			if (lockey == null)
			{
				return null;
			}
			obj = Loc.Get(lockey);
		}
		return obj;
	}

	public static implicit operator PhotoConfig(string sprite)
	{
		return new PhotoConfig(sprite);
	}
}
