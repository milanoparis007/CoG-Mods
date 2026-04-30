using System;
using System.Globalization;
using SomaSim.SION;

namespace Game.Services;

public static class DateTimeSerializer
{
	public static object Serialize(DateTime date, Serializer _)
	{
		return ToString(date);
	}

	public static DateTime Deserialize(object value, Serializer _)
	{
		return ((value is string date) ? FromString(date) : ((DateTime?)null)).GetValueOrDefault();
	}

	public static string ToString(DateTime date)
	{
		return date.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture);
	}

	public static DateTime? FromString(string date)
	{
		if (date == null)
		{
			return null;
		}
		if (!DateTime.TryParseExact(date, "o", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var result))
		{
			return null;
		}
		return result;
	}
}
