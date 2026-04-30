using System;
using UnityEngine;

namespace AwesomeCharts;

[Serializable]
public class LinearSingleAxisConfig : SingleAxisConfig
{
	[SerializeField]
	private int labelsCount = 5;

	[SerializeField]
	private AxisLabelGravity labelsAlignment;

	[SerializeField]
	private bool drawStartValue = true;

	[SerializeField]
	private bool drawEndValue = true;

	[SerializeField]
	private BasicValueFormatterConfig valueFormatterConfig = new BasicValueFormatterConfig();

	public int LabelsCount
	{
		get
		{
			return labelsCount;
		}
		set
		{
			labelsCount = value;
		}
	}

	public bool DrawStartValue
	{
		get
		{
			return drawStartValue;
		}
		set
		{
			drawStartValue = value;
		}
	}

	public bool DrawEndValue
	{
		get
		{
			return drawEndValue;
		}
		set
		{
			drawEndValue = value;
		}
	}

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

	public BasicValueFormatterConfig ValueFormatterConfig
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
}
