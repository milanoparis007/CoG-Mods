using System;
using UnityEngine;

namespace AwesomeCharts;

[Serializable]
public class GridConfig
{
	[SerializeField]
	private int verticalLinesCount = 5;

	[SerializeField]
	private int horizontalLinesCount = 5;

	[SerializeField]
	private GridLineConfig verticalLinesConfig = new GridLineConfig();

	[SerializeField]
	private GridLineConfig horizontalLinesConfig = new GridLineConfig();

	public int VerticalLinesCount
	{
		get
		{
			return verticalLinesCount;
		}
		set
		{
			verticalLinesCount = value;
		}
	}

	public int HorizontalLinesCount
	{
		get
		{
			return horizontalLinesCount;
		}
		set
		{
			horizontalLinesCount = value;
		}
	}

	public GridLineConfig VerticalLinesConfig
	{
		get
		{
			return verticalLinesConfig;
		}
		set
		{
			verticalLinesConfig = value;
		}
	}

	public GridLineConfig HorizontalLinesConfig
	{
		get
		{
			return horizontalLinesConfig;
		}
		set
		{
			horizontalLinesConfig = value;
		}
	}
}
