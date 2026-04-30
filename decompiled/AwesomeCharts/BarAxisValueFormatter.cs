using System;
using System.Collections.Generic;

namespace AwesomeCharts;

[Serializable]
public class BarAxisValueFormatter : AxisValueFormatter
{
	public BarAxisValueFormatterConfig config = new BarAxisValueFormatterConfig();

	public string FormatAxisValue(int index, float value, float minValue, float maxValue)
	{
		if (ShouldShowCustomValue(config.CustomValues))
		{
			return GetCustomValueForIndex(config.CustomValues, index);
		}
		return index.ToString();
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
}
