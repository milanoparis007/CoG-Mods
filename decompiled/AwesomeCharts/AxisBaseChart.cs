using System;
using UnityEngine;

namespace AwesomeCharts;

public abstract class AxisBaseChart<T> : BaseChart<T> where T : AxisChartData
{
	[SerializeField]
	private GridConfig gridConfig;

	[SerializeField]
	private GridFrameConfig frameConfig;

	protected GridRenderer gridRenderer;

	protected FrameRenderer frameRenderer;

	protected AxisLabelRenderer verticalAxisLabelRenderer;

	protected AxisLabelRenderer horizontalAxisLabelRenderer;

	public GridConfig GridConfig
	{
		get
		{
			return gridConfig;
		}
		set
		{
			gridConfig = value;
			SetDirty();
		}
	}

	public GridFrameConfig FrameConfig
	{
		get
		{
			return frameConfig;
		}
		set
		{
			frameConfig = value;
			SetDirty();
		}
	}

	protected abstract AxisLabelEntryProvider GetVerticalAxisEntriesProvider();

	protected abstract AxisLabelEntryProvider GetHorizontalAxisEntriesProvider();

	protected abstract SingleAxisConfig GetVerticalAxisConfig();

	protected abstract SingleAxisConfig GetHorizontalAxisConfig();

	protected virtual void OnUpdateAxis()
	{
	}

	protected override void Awake()
	{
		base.Awake();
		if (gridConfig == null)
		{
			gridConfig = new GridConfig();
		}
		if (frameConfig == null)
		{
			frameConfig = new GridFrameConfig();
		}
	}

	protected override void OnInstantiateViews()
	{
		base.OnInstantiateViews();
		gridRenderer = InstantiateGridRenderer();
		gridRenderer.transform.SetSiblingIndex(0);
		frameRenderer = InstantiateFrameRenderer();
		frameRenderer.raycastTarget = false;
		frameRenderer.transform.SetSiblingIndex(chartDataContainerView.transform.GetSiblingIndex() + 1);
		verticalAxisLabelRenderer = InstantiateVerticalAxisLabelRenderer();
		horizontalAxisLabelRenderer = InstantiateHorizontalAxisLabelRenderer();
	}

	private GridRenderer InstantiateGridRenderer()
	{
		return viewCreator.InstantiateGridRenderer("GridRenderer", contentView.transform, PivotValue.BOTTOM_LEFT);
	}

	private FrameRenderer InstantiateFrameRenderer()
	{
		return viewCreator.InstantiateFrameRenderer("FrameRenderer", contentView.transform, PivotValue.BOTTOM_LEFT);
	}

	private AxisLabelRenderer InstantiateVerticalAxisLabelRenderer()
	{
		return viewCreator.InstantiateAxisLabelRenderer("VerticalAxisLabelRenderer", contentView.transform, PivotValue.BOTTOM_LEFT);
	}

	private AxisLabelRenderer InstantiateHorizontalAxisLabelRenderer()
	{
		return viewCreator.InstantiateAxisLabelRenderer("HorizontalAxisLabelRenderer", contentView.transform, PivotValue.BOTTOM_LEFT);
	}

	protected override void OnUpdateViewsSize(Vector2 size)
	{
		base.OnUpdateViewsSize(size);
		gridRenderer.GetComponent<RectTransform>().sizeDelta = size;
		frameRenderer.GetComponent<RectTransform>().sizeDelta = size;
		verticalAxisLabelRenderer.GetComponent<RectTransform>().sizeDelta = size;
		horizontalAxisLabelRenderer.GetComponent<RectTransform>().sizeDelta = size;
	}

	protected override void OnDrawChartContent()
	{
		base.OnDrawChartContent();
		UpdateAxis();
	}

	private void UpdateAxis()
	{
		if (GetChartData() != null)
		{
			OnUpdateAxis();
			gridRenderer.GridConfig = GridConfig;
			frameRenderer.GridFrameConfig = FrameConfig;
			verticalAxisLabelRenderer.ObjectPrefab = GetVerticalAxisConfig().AxisLabelPrefab;
			verticalAxisLabelRenderer.Entries = GetVerticalAxisEntriesProvider().getLabelRendererEntries();
			verticalAxisLabelRenderer.LabelsConfig = GetVerticalAxisConfig().LabelsConfig;
			verticalAxisLabelRenderer.Reload();
			horizontalAxisLabelRenderer.ObjectPrefab = GetHorizontalAxisConfig().AxisLabelPrefab;
			horizontalAxisLabelRenderer.Entries = GetHorizontalAxisEntriesProvider().getLabelRendererEntries();
			horizontalAxisLabelRenderer.LabelsConfig = GetHorizontalAxisConfig().LabelsConfig;
			horizontalAxisLabelRenderer.Reload();
		}
	}

	protected AxisBounds GetAxisBounds()
	{
		AxisValue bounds = GetVerticalAxisConfig().Bounds;
		AxisValue bounds2 = GetHorizontalAxisConfig().Bounds;
		float xMin = (bounds2.MinAutoValue ? GetChartData().GetMinPosition() : bounds2.Min);
		float xMax = (bounds2.MaxAutoValue ? GetChartData().GetMaxPosition() : bounds2.Max);
		float yMin = (bounds.MinAutoValue ? ((float)GetClosestRoundValue(GetChartData().GetMinValue(), GetChartData().GetMinValue() < 0f)) : bounds.Min);
		float yMax = (bounds.MaxAutoValue ? ((float)GetClosestRoundValue(GetChartData().GetMaxValue(), GetChartData().GetMaxValue() > 0f)) : bounds.Max);
		return new AxisBounds(xMin, xMax, yMin, yMax);
	}

	private int GetClosestRoundValue(float value, bool up)
	{
		if (value == 0f)
		{
			return 0;
		}
		float num = CalculateRoundingDifferenceForValue(value);
		if (up)
		{
			return (int)(value + num);
		}
		return (int)(value - num);
	}

	private float CalculateRoundingDifferenceForValue(float value)
	{
		int num = ((value >= 0f) ? 1 : (-1));
		float f = Math.Abs(value * 1.1f);
		float num2 = Mathf.FloorToInt(Mathf.Log10(f) + 1f);
		f = Mathf.Ceil(f);
		if (num2 > 2f)
		{
			f = (float)((int)(f / Mathf.Pow(10f, num2 - 2f)) + 1) * Mathf.Pow(10f, num2 - 2f);
		}
		else if (num2 >= 1f)
		{
			f = (float)((int)(f / Mathf.Pow(10f, num2 - 1f)) + 1) * Mathf.Pow(10f, num2 - 1f);
		}
		return (f - Math.Abs(value)) * (float)num;
	}
}
