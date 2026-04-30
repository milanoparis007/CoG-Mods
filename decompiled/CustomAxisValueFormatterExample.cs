using AwesomeCharts;
using UnityEngine;

[ExecuteInEditMode]
public class CustomAxisValueFormatterExample : MonoBehaviour
{
	private class PriceValueFormatter : AxisValueFormatter
	{
		public string FormatAxisValue(int index, float value, float minValue, float maxValue)
		{
			return value.ToString("F" + 0) + " $";
		}
	}

	public BarChart barChart;

	private void Start()
	{
		SetupCustomValueFormatter();
	}

	private void SetupCustomValueFormatter()
	{
		if (!(barChart == null))
		{
			barChart.CustomVerticalAxisValueFormatter = new PriceValueFormatter();
		}
	}
}
