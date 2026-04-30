using Game.Core;

namespace Game.Services.Maps;

public sealed class DistrictConfig
{
	public static readonly Label TAG_COMCENTER = (Label)"bizcenter";

	public static readonly Label TAG_INDCENTER = (Label)"indcenter";

	public static readonly Label TAG_DOWNTOWN = (Label)"downtown";

	public TagList tags;

	public string locname;

	public string customname;

	public WorldPos start;

	public int radius;

	public string GetName()
	{
		string text = customname;
		if (text == null)
		{
			if (locname == null)
			{
				return "";
			}
			text = Loc.Get(locname);
		}
		return text;
	}
}
