using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UI.Extensions;

namespace AwesomeCharts;

[ExecuteInEditMode]
public class LineChart : AxisBaseChart<LineData>
{
	private const int BEZIER_LINE_SEGMENTS = 10;

	[SerializeField]
	private LineChartAxisConfig axisConfig;

	[SerializeField]
	private LineChartConfig config;

	[SerializeField]
	private LineData data;

	private ChartValuePopup currentValuePopup;

	private LineEntry currentValuePopupEntry;

	private Vector2[][] entriesPoints;

	private Vector2[][] bezierPoints;

	private List<LineEntryIdicator> entryIdicators = new List<LineEntryIdicator>();

	private List<UILineRenderer> lineRenderers = new List<UILineRenderer>();

	private List<LineChartBackground> lineBackgrounds = new List<LineChartBackground>();

	private VerticalAxisLabelEntryProvider verticalLabelsProvider;

	private HorizontalAxisLabelEntryProvider horizontalLabelsProvider;

	private BasicAxisValueFormatter verticalAxisValueFormatter;

	private BasicAxisValueFormatter horizontalAxisValueFormatter;

	private AxisValueFormatter customVerticalAxisValueFormatter;

	private AxisValueFormatter customHorizontalAxisValueFormatter;

	public LineChartConfig Config
	{
		get
		{
			return config;
		}
		set
		{
			config = value;
			config.configChangeListener = OnConfigChanged;
			SetDirty();
		}
	}

	public LineChartAxisConfig AxisConfig
	{
		get
		{
			return axisConfig;
		}
		set
		{
			axisConfig = value;
			SetDirty();
		}
	}

	public AxisValueFormatter CustomVerticalAxisValueFormatter
	{
		get
		{
			return customVerticalAxisValueFormatter;
		}
		set
		{
			customVerticalAxisValueFormatter = value;
			SetDirty();
		}
	}

	public AxisValueFormatter CustomHorizontalAxisValueFormatter
	{
		get
		{
			return customHorizontalAxisValueFormatter;
		}
		set
		{
			customHorizontalAxisValueFormatter = value;
			SetDirty();
		}
	}

	public override LineData GetChartData()
	{
		return data;
	}

	private void Reset()
	{
		data = new LineData(new LineDataSet());
		config = new LineChartConfig();
	}

	protected override void Awake()
	{
		base.Awake();
		if (data == null)
		{
			data = new LineData(new LineDataSet());
		}
		if (config == null)
		{
			config = new LineChartConfig();
		}
		if (axisConfig == null)
		{
			axisConfig = new LineChartAxisConfig();
		}
		verticalLabelsProvider = new VerticalAxisLabelEntryProvider();
		horizontalLabelsProvider = new HorizontalAxisLabelEntryProvider();
		verticalAxisValueFormatter = new BasicAxisValueFormatter();
		horizontalAxisValueFormatter = new BasicAxisValueFormatter();
	}

	private void OnConfigChanged()
	{
		SetDirty();
	}

	protected override List<LegendEntry> CreateLegendViewEntries()
	{
		List<LegendEntry> list = new List<LegendEntry>();
		foreach (LineDataSet dataSet in data.DataSets)
		{
			list.Add(new LegendEntry(dataSet.Title, dataSet.LineColor));
		}
		return list;
	}

	protected override void OnInstantiateViews()
	{
		base.OnInstantiateViews();
		chartDataContainerView.AddComponent<RectMask2D>();
	}

	protected override AxisLabelEntryProvider GetVerticalAxisEntriesProvider()
	{
		return verticalLabelsProvider;
	}

	protected override AxisLabelEntryProvider GetHorizontalAxisEntriesProvider()
	{
		return horizontalLabelsProvider;
	}

	protected override SingleAxisConfig GetVerticalAxisConfig()
	{
		return AxisConfig.VerticalAxisConfig;
	}

	protected override SingleAxisConfig GetHorizontalAxisConfig()
	{
		return AxisConfig.HorizontalAxisConfig;
	}

	private AxisValueFormatter GetCorrectVerticalAxisValueFormatter()
	{
		if (CustomVerticalAxisValueFormatter == null)
		{
			return verticalAxisValueFormatter;
		}
		return CustomVerticalAxisValueFormatter;
	}

	private AxisValueFormatter GetCorrectHorizontalAxisValueFormatter()
	{
		if (CustomHorizontalAxisValueFormatter == null)
		{
			return horizontalAxisValueFormatter;
		}
		return CustomHorizontalAxisValueFormatter;
	}

	protected override void OnUpdateAxis()
	{
		AxisBounds axisBounds = GetAxisBounds();
		horizontalAxisValueFormatter.config = AxisConfig.HorizontalAxisConfig.ValueFormatterConfig;
		verticalAxisValueFormatter.config = AxisConfig.VerticalAxisConfig.ValueFormatterConfig;
		horizontalLabelsProvider.valueMin = axisBounds.XMin;
		horizontalLabelsProvider.valueMax = axisBounds.XMax;
		horizontalLabelsProvider.labelCount = AxisConfig.HorizontalAxisConfig.LabelsCount;
		horizontalLabelsProvider.firstEntryVisible = AxisConfig.HorizontalAxisConfig.DrawStartValue;
		horizontalLabelsProvider.lastEntryVisible = AxisConfig.HorizontalAxisConfig.DrawEndValue;
		horizontalLabelsProvider.labelsGravity = AxisConfig.HorizontalAxisConfig.LabelsAlignment;
		horizontalLabelsProvider.valueFormatter = GetCorrectHorizontalAxisValueFormatter();
		horizontalLabelsProvider.axisLength = horizontalAxisLabelRenderer.GetComponent<RectTransform>().sizeDelta.x;
		verticalLabelsProvider.valueMin = axisBounds.YMin;
		verticalLabelsProvider.valueMax = axisBounds.YMax;
		verticalLabelsProvider.labelCount = AxisConfig.VerticalAxisConfig.LabelsCount;
		verticalLabelsProvider.firstEntryVisible = AxisConfig.VerticalAxisConfig.DrawStartValue;
		verticalLabelsProvider.lastEntryVisible = AxisConfig.VerticalAxisConfig.DrawEndValue;
		verticalLabelsProvider.labelsGravity = AxisConfig.VerticalAxisConfig.LabelsAlignment;
		verticalLabelsProvider.valueFormatter = GetCorrectVerticalAxisValueFormatter();
		verticalLabelsProvider.axisLength = verticalAxisLabelRenderer.GetComponent<RectTransform>().sizeDelta.y;
	}

	protected override void OnUpdateViewsSize(Vector2 size)
	{
		base.OnUpdateViewsSize(size);
		lineRenderers.ForEach(delegate(UILineRenderer renderer)
		{
			renderer.GetComponent<RectTransform>().sizeDelta = GetSize();
		});
		lineBackgrounds.ForEach(delegate(LineChartBackground renderer)
		{
			renderer.GetComponent<RectTransform>().sizeDelta = GetSize();
		});
	}

	protected override void OnDrawChartContent()
	{
		base.OnDrawChartContent();
		HideCurrentValuePopup();
		CalculateLinesPoints();
		UpdateLineRendererInstances(GetChartData().DataSets.Count);
		UpdateBackgroundInstances(GetChartData().DataSets.Count);
		UpdateEntryIndicatorInstances(GetRequiredValueIndicatorsCount());
		DrawLines();
	}

	private int GetRequiredValueIndicatorsCount()
	{
		if (!Config.ShowValueIndicators)
		{
			return 0;
		}
		return GetAllVisibleEntriesCount();
	}

	private int GetAllVisibleEntriesCount()
	{
		int num = 0;
		for (int i = 0; i < entriesPoints.Length; i++)
		{
			num += entriesPoints[i].Length;
		}
		return num;
	}

	private void UpdateLineRendererInstances(int requiredCount)
	{
		int count = lineRenderers.Count;
		for (int num = requiredCount - count; num > 0; num--)
		{
			lineRenderers.Add(CreateLineRenderer());
		}
		for (int num2 = count - requiredCount; num2 > 0; num2--)
		{
			UILineRenderer uILineRenderer = lineRenderers[lineRenderers.Count - 1];
			Object.DestroyImmediate(uILineRenderer.gameObject);
			lineRenderers.Remove(uILineRenderer);
		}
	}

	private UILineRenderer CreateLineRenderer()
	{
		UILineRenderer uILineRenderer = viewCreator.InstantiateLineRenderer("Line", chartDataContainerView.transform, PivotValue.BOTTOM_LEFT);
		uILineRenderer.gameObject.AddComponent<CanvasRenderer>();
		uILineRenderer.GetComponent<RectTransform>().sizeDelta = GetSize();
		return uILineRenderer;
	}

	private void UpdateBackgroundInstances(int requiredCount)
	{
		int count = lineBackgrounds.Count;
		for (int num = requiredCount - count; num > 0; num--)
		{
			LineChartBackground lineChartBackground = CreateLineBackground();
			lineChartBackground.transform.SetSiblingIndex(0);
			lineBackgrounds.Add(lineChartBackground);
		}
		for (int num2 = count - requiredCount; num2 > 0; num2--)
		{
			LineChartBackground lineChartBackground2 = lineBackgrounds[lineBackgrounds.Count - 1];
			Object.DestroyImmediate(lineChartBackground2.gameObject);
			lineBackgrounds.Remove(lineChartBackground2);
		}
	}

	private LineChartBackground CreateLineBackground()
	{
		LineChartBackground lineChartBackground = viewCreator.InstantiateLineBackground("LineBackground", chartDataContainerView.transform, PivotValue.BOTTOM_LEFT);
		lineChartBackground.gameObject.AddComponent<CanvasRenderer>();
		lineChartBackground.GetComponent<RectTransform>().sizeDelta = GetSize();
		return lineChartBackground;
	}

	private void UpdateEntryIndicatorInstances(int requiredCount)
	{
		int count = entryIdicators.Count;
		for (int num = requiredCount - count; num > 0; num--)
		{
			LineEntryIdicator item = viewCreator.InstantiateCircleImage("dot", contentView.transform);
			entryIdicators.Add(item);
		}
		for (int num2 = count - requiredCount; num2 > 0; num2--)
		{
			LineEntryIdicator lineEntryIdicator = entryIdicators[entryIdicators.Count - 1];
			Object.DestroyImmediate(lineEntryIdicator.gameObject);
			entryIdicators.Remove(lineEntryIdicator);
		}
	}

	private void CalculateLinesPoints()
	{
		int count = GetChartData().DataSets.Count;
		entriesPoints = new Vector2[count][];
		bezierPoints = new Vector2[count][];
		for (int i = 0; i < count; i++)
		{
			CalculateDataSetLinePoints(GetChartData().DataSets[i], i);
		}
	}

	private void CalculateDataSetLinePoints(LineDataSet dataSet, int dataSetIndex)
	{
		entriesPoints[dataSetIndex] = CreateLinePointsFromDataSet(dataSet);
		if (dataSet.UseBezier && dataSet.GetEntriesCount() > 2)
		{
			bezierPoints[dataSetIndex] = BezierUtils.CreateBezierPointsFromLinePoints(entriesPoints[dataSetIndex]);
		}
		else
		{
			bezierPoints[dataSetIndex] = new Vector2[0];
		}
	}

	private Vector2[] CreateLinePointsFromDataSet(LineDataSet dataSet)
	{
		if (dataSet.GetEntriesCount() < 2 || dataSet.PositionDelta() <= 0f)
		{
			return new Vector2[0];
		}
		AxisBounds axisBounds = GetAxisBounds();
		List<LineEntry> sortedEntries = dataSet.GetSortedEntries();
		Vector2 chartSize = GetSize();
		float positionDelta = axisBounds.XMax - axisBounds.XMin;
		float valueDelta = axisBounds.YMax - axisBounds.YMin;
		int index = 0;
		Vector2[] result = new Vector2[dataSet.Entries.Count];
		sortedEntries.ForEach(delegate(LineEntry entry)
		{
			float x = (entry.Position - axisBounds.XMin) / positionDelta * chartSize.x;
			float y = (entry.Value - axisBounds.YMin) / valueDelta * chartSize.y;
			result[index] = new Vector2(x, y);
			index++;
		});
		return result;
	}

	private void DrawLines()
	{
		int count = GetChartData().DataSets.Count;
		int currentIndicatorPosition = 0;
		for (int i = 0; i < count; i++)
		{
			DrawLineBackground(lineBackgrounds[i], GetChartData().DataSets[i], i);
			currentIndicatorPosition = DrawLine(lineRenderers[i], GetChartData().DataSets[i], i, currentIndicatorPosition);
		}
	}

	private void DrawLineBackground(LineChartBackground lineBackground, LineDataSet dataSet, int dataSetIndex)
	{
		lineBackground.color = dataSet.FillColor;
		lineBackground.Texture = dataSet.FillTexture;
		lineBackground.AxisBounds = GetAxisBounds();
		lineBackground.Points = (dataSet.UseBezier ? CalculateBezierSegmentsPoints(bezierPoints[dataSetIndex]) : entriesPoints[dataSetIndex]);
	}

	private Vector2[] CalculateBezierSegmentsPoints(Vector2[] controlPoints)
	{
		BezierPath bezierPath = new BezierPath();
		bezierPath.SegmentsPerCurve = 10;
		bezierPath.SetControlPoints(controlPoints);
		return bezierPath.GetDrawingPoints0().ToArray();
	}

	private int DrawLine(UILineRenderer lineRenderer, LineDataSet dataSet, int dataSetIndex, int currentIndicatorPosition)
	{
		List<LineEntry> sortedEntries = dataSet.GetSortedEntries();
		Color32 color = dataSet.LineColor;
		lineRenderer.lineThickness = dataSet.LineThickness;
		lineRenderer.color = color;
		lineRenderer.m_points = (dataSet.UseBezier ? bezierPoints[dataSetIndex] : entriesPoints[dataSetIndex]);
		lineRenderer.BezierSegmentsPerCurve = 10;
		lineRenderer.BezierMode = (dataSet.UseBezier ? UILineRenderer.BezierType.Basic : UILineRenderer.BezierType.None);
		lineRenderer.SetAllDirty();
		if (Config.ShowValueIndicators)
		{
			return UpdateCirclesAtPosition(entriesPoints[dataSetIndex], sortedEntries.ToArray(), color, currentIndicatorPosition, dataSetIndex);
		}
		return 0;
	}

	private int UpdateCirclesAtPosition(Vector2[] positions, LineEntry[] entries, Color32 color, int firstAvailableIndicatorPosition, int dataSetIndex)
	{
		Vector2 contentSize = GetContentSize();
		for (int i = 0; i < positions.Length; i++)
		{
			Vector2 vector = positions[i];
			bool flag = vector.x < 0f || vector.x > contentSize.x || vector.y < 0f || vector.y > contentSize.y;
			LineEntryIdicator indicator = entryIdicators[firstAvailableIndicatorPosition + i];
			indicator.GetComponent<RectTransform>().sizeDelta = new Vector2(Config.ValueIndicatorSize, Config.ValueIndicatorSize);
			indicator.transform.localPosition = vector;
			indicator.entry = entries[i];
			indicator.image.color = color;
			indicator.gameObject.SetActive(!flag);
			indicator.button.onClick.RemoveAllListeners();
			indicator.button.onClick.AddListener(delegate
			{
				OnEntryClick(indicator, dataSetIndex);
			});
		}
		return firstAvailableIndicatorPosition + positions.Length;
	}

	private void OnEntryClick(LineEntryIdicator indicator, int dataSetIndex)
	{
		if (Config.OnValueClickAction != null)
		{
			Config.OnValueClickAction(indicator.entry, dataSetIndex);
		}
		ShowHideValuePopup(indicator);
	}

	private void ShowHideValuePopup(LineEntryIdicator indicator)
	{
		if (currentValuePopup == null)
		{
			currentValuePopup = viewCreator.InstantiateChartPopup(contentView.transform, Config.PopupPrefab);
		}
		if (indicator.entry != currentValuePopupEntry)
		{
			UpdateValuePopup(indicator);
			currentValuePopupEntry = indicator.entry;
		}
		else
		{
			HideCurrentValuePopup();
		}
	}

	private void HideCurrentValuePopup()
	{
		if (currentValuePopup != null)
		{
			currentValuePopup.gameObject.SetActive(value: false);
			currentValuePopupEntry = null;
		}
	}

	private void UpdateValuePopup(LineEntryIdicator indicator)
	{
		currentValuePopup.transform.localPosition = PopupPositionFromEntry(indicator);
		currentValuePopup.text.text = indicator.entry.Value.ToString() ?? "";
		currentValuePopup.gameObject.SetActive(value: true);
	}

	private Vector3 PopupPositionFromEntry(LineEntryIdicator indicator)
	{
		return new Vector3(indicator.transform.localPosition.x, indicator.transform.localPosition.y + (float)(Config.ValueIndicatorSize / 2), 0f);
	}

	public Vector2 CalculateCurvePointForPosition(float position, int dataSetIndex)
	{
		return TransformViewPointIntoAxisPoint(CalculateCurveViewPointForPosition(position, dataSetIndex));
	}

	public Vector2[] CalculateCurvePointsForPositions(float[] positions, int dataSetIndex)
	{
		Vector2[] array = new Vector2[positions.Length];
		for (int i = 0; i < positions.Length; i++)
		{
			array[i] = CalculateCurveViewPointForPosition(positions[i], dataSetIndex);
		}
		return TransformViewPointsIntoAxisPoints(array);
	}

	private Vector2 CalculateCurveViewPointForPosition(float position, int dataSetIndex)
	{
		Vector2[] array = CalculateBezierSegmentsPoints(bezierPoints[dataSetIndex]);
		float num = position * (1f / GetPositionAxisViewScale());
		int num2 = -1;
		for (int i = 1; i < array.Length; i++)
		{
			if (array[i].x > num)
			{
				num2 = i - 1;
				break;
			}
		}
		if (num2 < 0)
		{
			return Vector2.zero;
		}
		Vector2 vector = array[num2];
		Vector2 vector2 = array[num2 + 1];
		float num3 = vector2.x - vector.x;
		float num4 = vector2.y - vector.y;
		float num5 = (num - vector.x) / num3;
		return new Vector2(num, vector.y + num4 * num5);
	}

	private float GetPositionAxisViewScale()
	{
		return (GetAxisBounds().XMax - GetAxisBounds().XMin) / GetSize().x;
	}

	private float GetValueAxisViewScale()
	{
		return (GetAxisBounds().YMax - GetAxisBounds().YMin) / GetSize().y;
	}

	private Vector2 TransformViewPointIntoAxisPoint(Vector2 point)
	{
		return new Vector2(point.x * GetPositionAxisViewScale(), point.y * GetValueAxisViewScale());
	}

	private Vector2 TransformAxisPointIntoViewPoint(Vector2 point)
	{
		return new Vector2(point.x * (1f / GetPositionAxisViewScale()), point.y * (1f / GetValueAxisViewScale()));
	}

	private Vector2[] TransformViewPointsIntoAxisPoints(Vector2[] points)
	{
		float positionAxisViewScale = GetPositionAxisViewScale();
		float valueAxisViewScale = GetValueAxisViewScale();
		Vector2[] array = new Vector2[points.Length];
		for (int i = 0; i < points.Length; i++)
		{
			array[i] = new Vector2(points[i].x * positionAxisViewScale, points[i].y * valueAxisViewScale);
		}
		return array;
	}
}
