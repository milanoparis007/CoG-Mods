using SomaSim.Util;
using UnityEngine;

namespace Game.Core;

public static class ColorExtensions
{
	public static string ToTMProFormat(this Color c)
	{
		return "#" + ColorUtil.ColorToHex(c);
	}

	public static string ToTMProFormat(this Color32 c)
	{
		return "#" + ColorUtil.ColorToHex(c);
	}

	public static float GetLuminosity(this Color c)
	{
		return c.r * 0.3f + c.g * 0.59f + c.b * 0.11f;
	}
}
