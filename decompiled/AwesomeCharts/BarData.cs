using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AwesomeCharts;

[Serializable]
public class BarData : AxisChartData
{
	[SerializeField]
	private List<BarDataSet> dataSets;

	public List<BarDataSet> DataSets => dataSets;

	public BarData()
	{
		dataSets = new List<BarDataSet>();
	}

	public BarData(BarDataSet dataSet)
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
		return HasAnyData() ? DataSets.Select((BarDataSet dataSet) => dataSet.GetMinPosition()).Min() : 0;
	}

	public override float GetMaxPosition()
	{
		return HasAnyData() ? DataSets.Select((BarDataSet dataSet) => dataSet.GetMaxPosition()).Max() : 0;
	}

	public override float GetMinValue()
	{
		return 0f;
	}

	public override float GetMaxValue()
	{
		if (!HasAnyData())
		{
			return 0f;
		}
		return DataSets.Select((BarDataSet dataSet) => dataSet.GetMaxValue()).Max();
	}
}
