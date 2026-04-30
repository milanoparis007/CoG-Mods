using System;
using System.Collections.Generic;

namespace AwesomeCharts;

[Serializable]
public class BasicAxisValueFormatter : AxisValueFormatter
{
	public BasicValueFormatterConfig config = new BasicValueFormatterConfig();

	public string FormatAxisValue(int index, float value, float minValue, float maxValue)
	{
		if (ShouldShowCustomValue(config.CustomValues))
		{
			return GetCustomValueForIndex(config.CustomValues, index);
		}
		return GetFormattedValue(value, config.ValueDecimalPlaces);
	}

	private bool ShouldShowCustomValue(List<string> customValues)
	{
		if (customValues != null)
		{
			return customValues.Count > 0;
		}
		return false;
	}

	private string GetCustomValueForIndex(List<string> customValues, int index)
	{
		if (customValues.Count <= index)
		{
			return "";
		}
		return customValues[index];
	}

	private string GetFormattedValue(float value, int decimalPlaces)
	{
		return StringUtils.FormatValue(value, decimalPlaces);
	}
}
