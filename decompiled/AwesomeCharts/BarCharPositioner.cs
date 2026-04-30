using System;
using System.Collections.Generic;
using UnityEngine;

namespace AwesomeCharts;

[Serializable]
public class BarCharPositioner
{
	public AxisBounds axisBounds;

	public BarData data;

	public BarChartConfig barChartConfig;

	public Vector2 containerSize;

	private float calculatedBarWidth;

	public int GetVisibleEntriesRange()
	{
		if (axisBounds == null)
		{
			return 0;
		}
		return (int)(axisBounds.XMax - axisBounds.XMin) + 1;
	}

	public void RecalculatePositioner()
	{
		switch (barChartConfig.SizingMethod)
		{
		case BarSizingMethod.STANDARD:
			calculatedBarWidth = GetBarWidthWithStandardMethod();
			break;
		case BarSizingMethod.SIZE_TO_FIT:
			calculatedBarWidth = GetBarWidthWithSizeToFitMethod();
			break;
		}
	}

	private float GetBarWidthWithStandardMethod()
	{
		return Mathf.Min(GetBarWidthWithSizeToFitMethod(), barChartConfig.BarWidth);
	}

	private float GetBarWidthWithSizeToFitMethod()
	{
		int num = GetVisibleEntriesRange() * data.DataSets.Count;
		float num2 = (GetVisibleEntriesRange() + 1) * barChartConfig.BarSpacing;
		float num3 = GetVisibleEntriesRange() * Mathf.Max(0, data.DataSets.Count - 1) * barChartConfig.InnerBarSpacing;
		return (containerSize.x - num2 - num3) / (float)num;
	}

	public Vector3 GetBarPosition(int position, int dataSetIndex)
	{
		if (axisBounds == null)
		{
			return Vector3.zero;
		}
		int num = position - (int)axisBounds.XMin;
		return new Vector3((float)(num * data.DataSets.Count) * calculatedBarWidth + (float)dataSetIndex * calculatedBarWidth + (float)((num + 1) * barChartConfig.BarSpacing) + (float)(num * Mathf.Max(0, data.DataSets.Count - 1) * barChartConfig.InnerBarSpacing) + (float)(dataSetIndex * barChartConfig.InnerBarSpacing), 0f, 0f);
	}

	public Vector3 GetBarCenterPosition(int position)
	{
		int num = Mathf.Max(1, data.DataSets.Count);
		float num2 = calculatedBarWidth * (float)num + (float)(barChartConfig.InnerBarSpacing * (num - 1));
		return new Vector3(GetBarPosition(position, 0).x + num2 / 2f, 0f, 0f);
	}

	public float GetMaxBarHeight()
	{
		return containerSize.y;
	}

	public Vector2 GetBarSize(float value)
	{
		return new Vector2(calculatedBarWidth, CalculateBarHeight(value));
	}

	private float CalculateBarHeight(float value)
	{
		return (value - axisBounds.YMin) / axisBounds.YMax * GetMaxBarHeight();
	}

	public Vector3 GetValuePopupPosition(BarEntry entry, int dataSetIndex)
	{
		Vector3 barPosition = GetBarPosition((int)entry.Position, dataSetIndex);
		Vector2 barSize = GetBarSize(entry.Value);
		return new Vector3(barPosition.x + calculatedBarWidth / 2f, barPosition.y + barSize.y, 0f);
	}

	public int GetAllVisibleEntriesCount()
	{
		if (axisBounds == null || data == null || !data.HasAnyData())
		{
			return 0;
		}
		int result = 0;
		data.DataSets.ForEach(delegate(BarDataSet dataSet)
		{
			result += FilterVisibleEntries(dataSet).Count;
		});
		return result;
	}

	public List<BarEntry> GetVisibleEntries(int dataSetIndex)
	{
		if (axisBounds == null || data == null || !data.HasAnyData())
		{
			return new List<BarEntry>();
		}
		return FilterVisibleEntries(data.DataSets[dataSetIndex]);
	}

	private List<BarEntry> FilterVisibleEntries(BarDataSet dataSet)
	{
		return dataSet.Entries.FindAll((BarEntry entry) => (float)entry.Position >= axisBounds.XMin && (float)entry.Position <= axisBounds.XMax);
	}
}
