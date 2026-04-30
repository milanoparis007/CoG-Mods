using System;
using UnityEngine;

namespace AwesomeCharts;

[Serializable]
public class GridLineConfig
{
	[SerializeField]
	private int thickness = 2;

	[SerializeField]
	private Color color = Defaults.AXIS_LINE_COLOR;

	[SerializeField]
	private bool dashed;

	[SerializeField]
	private int dashLength;

	[SerializeField]
	private int dashSpacing;

	public int Thickness
	{
		get
		{
			return thickness;
		}
		set
		{
			thickness = value;
		}
	}

	public Color Color
	{
		get
		{
			return color;
		}
		set
		{
			color = value;
		}
	}

	public bool Dashed
	{
		get
		{
			return dashed;
		}
		set
		{
			dashed = value;
		}
	}

	public int DashLenght
	{
		get
		{
			return dashLength;
		}
		set
		{
			dashLength = value;
		}
	}

	public int DashSpacing
	{
		get
		{
			return dashSpacing;
		}
		set
		{
			dashSpacing = value;
		}
	}

	public bool ShouldDrawDashedLines()
	{
		if (dashed && dashLength > 0)
		{
			return dashSpacing > 0;
		}
		return false;
	}
}
