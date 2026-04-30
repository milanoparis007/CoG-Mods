using Game.Core;
using SomaSim.Util;
using UnityEngine;

namespace Game.UI.Util;

public static class TextUtil
{
	public static string ColorWrap(string message, Color color)
	{
		return ColorWrap(message, color.ToTMProFormat());
	}

	public static string ColorWrap(string message, string colorHex)
	{
		if (string.IsNullOrEmpty(colorHex))
		{
			return message;
		}
		return $"<color={colorHex}>{message}</color>";
	}

	public static string ColorIf(bool predicate, string message, string colorHex)
	{
		if (!predicate)
		{
			return message;
		}
		return ColorWrap(message, colorHex);
	}

	public static string ColorRedIf(bool predicate, string message)
	{
		return ColorIf(predicate, message, ColorConstants.TEXT_HEX_RED);
	}

	public static string ColorRedIfNot(bool predicate, string message)
	{
		return ColorIf(!predicate, message, ColorConstants.TEXT_HEX_RED);
	}

	public static string ColorGreenRed(Fixnum value, string message)
	{
		if (!(value > 0))
		{
			if (!(value < 0))
			{
				return message;
			}
			return ColorWrap(message, ColorConstants.TEXT_HEX_RED);
		}
		return ColorWrap(message, ColorConstants.TEXT_HEX_GREEN);
	}

	public static string ColorEnabledIf(bool predicate, string message)
	{
		return ColorIf(!predicate, message, ColorConstants.TEXT_HEX_DISABLED);
	}

	public static string ColorDisabledIf(bool predicate, string message)
	{
		return ColorIf(predicate, message, ColorConstants.TEXT_HEX_DISABLED);
	}

	public static string ColorDisabled(string message)
	{
		return ColorWrap(message, ColorConstants.TEXT_HEX_DISABLED);
	}
}
