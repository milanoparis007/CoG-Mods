using System;
using UnityEngine;

namespace AwesomeCharts;

[Serializable]
public class BarSingleAxisConfig : SingleAxisConfig
{
	[SerializeField]
	private BarAxisValueFormatterConfig valueFormatterConfig = new BarAxisValueFormatterConfig();

	[SerializeField]
	private AxisLabelGravity labelsAlignment;

	public AxisLabelGravity LabelsAlignment
	{
		get
		{
			return labelsAlignment;
		}
		set
		{
			labelsAlignment = value;
		}
	}

	public BarAxisValueFormatterConfig ValueFormatterConfig
	{
		get
		{
			return valueFormatterConfig;
		}
		set
		{
			valueFormatterConfig = value;
		}
	}

	public BarSingleAxisConfig()
	{
		base.Bounds.MinAutoValue = true;
		base.Bounds.MaxAutoValue = true;
	}
}
