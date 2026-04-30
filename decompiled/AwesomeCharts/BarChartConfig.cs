using System;
using UnityEngine;

namespace AwesomeCharts;

[Serializable]
public class BarChartConfig
{
	public delegate void ConfigChangeListener();

	[SerializeField]
	private int barWidth = 40;

	[SerializeField]
	private int barSpacing = 15;

	[SerializeField]
	private int innerBarSpacing = 5;

	[SerializeField]
	private BarSizingMethod sizingMethod;

	[SerializeField]
	private Bar barPrefab;

	[SerializeField]
	private ChartValuePopup popupPrefab;

	[SerializeField]
	private BarChartAction barChartClickAction;

	internal ConfigChangeListener configChangeListener;

	public int BarWidth
	{
		get
		{
			return barWidth;
		}
		set
		{
			barWidth = value;
			OnConfigChanged();
		}
	}

	public int BarSpacing
	{
		get
		{
			return barSpacing;
		}
		set
		{
			barSpacing = value;
			OnConfigChanged();
		}
	}

	public int InnerBarSpacing
	{
		get
		{
			return innerBarSpacing;
		}
		set
		{
			innerBarSpacing = value;
			OnConfigChanged();
		}
	}

	public BarSizingMethod SizingMethod
	{
		get
		{
			return sizingMethod;
		}
		set
		{
			sizingMethod = value;
			OnConfigChanged();
		}
	}

	public Bar BarPrefab
	{
		get
		{
			return barPrefab;
		}
		set
		{
			barPrefab = value;
			OnConfigChanged();
		}
	}

	public ChartValuePopup PopupPrefab
	{
		get
		{
			return popupPrefab;
		}
		set
		{
			popupPrefab = value;
			OnConfigChanged();
		}
	}

	public BarChartAction BarChartClickAction
	{
		get
		{
			return barChartClickAction;
		}
		set
		{
			barChartClickAction = value;
			OnConfigChanged();
		}
	}

	private void OnConfigChanged()
	{
		if (configChangeListener != null)
		{
			configChangeListener();
		}
	}
}
