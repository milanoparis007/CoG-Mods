using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AwesomeCharts;

[Serializable]
public class LineData : AxisChartData
{
	[SerializeField]
	private List<LineDataSet> dataSets;

	public List<LineDataSet> DataSets => dataSets;

	public LineData()
	{
		dataSets = new List<LineDataSet>();
	}

	public LineData(LineDataSet dataSet)
		: this()
	{
		dataSets.Add(dataSet);
	}

	public void Clear()
	{
		dataSets.Clear();
	}

	public bool HasAnyData()
	{
		return dataSets.Count > 0;
	}

	public override float GetMinPosition()
	{
		if (!HasAnyData())
		{
			return 0f;
		}
		return DataSets.Select((LineDataSet dataSet) => dataSet.GetMinPosition()).Min();
	}

	public override float GetMaxPosition()
	{
		if (!HasAnyData())
		{
			return 0f;
		}
		return DataSets.Select((LineDataSet dataSet) => dataSet.GetMaxPosition()).Max();
	}

	public override float GetMinValue()
	{
		if (!HasAnyData())
		{
			return 0f;
		}
		return DataSets.Select((LineDataSet dataSet) => dataSet.GetMinValue()).Min();
	}

	public override float GetMaxValue()
	{
		if (!HasAnyData())
		{
			return 0f;
		}
		return DataSets.Select((LineDataSet dataSet) => dataSet.GetMaxValue()).Max();
	}
}
