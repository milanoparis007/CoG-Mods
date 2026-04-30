using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace AwesomeCharts;

[ExecuteInEditMode]
public class BarChart : AxisBaseChart<BarData>
{
	[SerializeField]
	private BarChartAxisConfig axisConfig;

	[SerializeField]
	private BarChartConfig config;

	[SerializeField]
	internal BarData data;

	private BarCharPositioner positioner;

	private ChartValuePopup currentValuePopup;

	private BarEntry currentValuePopupEntry;

	private VerticalAxisLabelEntryProvider verticalLabelsProvider;

	private BarAxisLabelEntryProvider horizontalLabelsProvider;

	private BasicAxisValueFormatter verticalAxisValueFormatter;

	private BarAxisValueFormatter horizontalAxisValueFormatter;

	private AxisValueFormatter customVerticalAxisValueFormatter;

	private AxisValueFormatter customHorizontalAxisValueFormatter;

	private List<Bar> barInstances;

	public BarChartConfig Config
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

	public BarChartAxisConfig AxisConfig
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

	public override BarData GetChartData()
	{
		return data;
	}

	private void Reset()
	{
		data = new BarData(new BarDataSet());
		Config = new BarChartConfig();
	}

	protected override void Awake()
	{
		base.Awake();
		if (data == null)
		{
			data = new BarData(new BarDataSet());
		}
		if (config == null)
		{
			Config = new BarChartConfig();
		}
		if (axisConfig == null)
		{
			axisConfig = new BarChartAxisConfig();
		}
		positioner = new BarCharPositioner();
		barInstances = new List<Bar>();
		verticalLabelsProvider = new VerticalAxisLabelEntryProvider();
		horizontalLabelsProvider = new BarAxisLabelEntryProvider();
		verticalAxisValueFormatter = new BasicAxisValueFormatter();
		horizontalAxisValueFormatter = new BarAxisValueFormatter();
	}

	private void OnConfigChanged()
	{
		SetDirty();
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
		return axisConfig.VerticalAxisConfig;
	}

	protected override SingleAxisConfig GetHorizontalAxisConfig()
	{
		return axisConfig.HorizontalAxisConfig;
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
		base.OnUpdateAxis();
		UpdateBarChartPositioner();
		UpdateHorizontalAxisEntriesProvider();
		UpdateVerticalAxisEntriesProvider();
	}

	private void UpdateBarChartPositioner()
	{
		positioner.data = data;
		positioner.barChartConfig = Config;
		positioner.containerSize = GetSize();
		positioner.axisBounds = GetAxisBounds();
		positioner.RecalculatePositioner();
	}

	private void UpdateVerticalAxisEntriesProvider()
	{
		verticalAxisValueFormatter.config = AxisConfig.VerticalAxisConfig.ValueFormatterConfig;
		AxisBounds axisBounds = GetAxisBounds();
		verticalLabelsProvider.valueMin = axisBounds.YMin;
		verticalLabelsProvider.valueMax = axisBounds.YMax;
		verticalLabelsProvider.labelCount = AxisConfig.VerticalAxisConfig.LabelsCount;
		verticalLabelsProvider.firstEntryVisible = AxisConfig.VerticalAxisConfig.DrawStartValue;
		verticalLabelsProvider.lastEntryVisible = AxisConfig.VerticalAxisConfig.DrawEndValue;
		verticalLabelsProvider.labelsGravity = AxisConfig.VerticalAxisConfig.LabelsAlignment;
		verticalLabelsProvider.valueFormatter = GetCorrectVerticalAxisValueFormatter();
		verticalLabelsProvider.axisLength = verticalAxisLabelRenderer.GetComponent<RectTransform>().sizeDelta.y;
	}

	private void UpdateHorizontalAxisEntriesProvider()
	{
		horizontalAxisValueFormatter.config = AxisConfig.HorizontalAxisConfig.ValueFormatterConfig;
		horizontalLabelsProvider.barChartPositioner = positioner;
		horizontalLabelsProvider.valueFormatter = GetCorrectHorizontalAxisValueFormatter();
		horizontalLabelsProvider.labelsGravity = AxisConfig.HorizontalAxisConfig.LabelsAlignment;
	}

	protected override List<LegendEntry> CreateLegendViewEntries()
	{
		List<LegendEntry> list = new List<LegendEntry>();
		foreach (BarDataSet dataSet in data.DataSets)
		{
			list.Add(new LegendEntry(dataSet.Title, dataSet.GetColorForIndex(0)));
		}
		return list;
	}

	protected override void OnDrawChartContent()
	{
		base.OnDrawChartContent();
		HideCurrentValuePopup();
		UpdateBarInstances(positioner.GetAllVisibleEntriesCount());
		if (GetChartData().HasAnyData())
		{
			ShowBars();
		}
	}

	private void UpdateBarInstances(int requiredCount)
	{
		int count = barInstances.Count;
		for (int num = requiredCount - count; num > 0; num--)
		{
			Bar item = viewCreator.InstantiateBar(chartDataContainerView.transform, Config.BarPrefab);
			barInstances.Add(item);
		}
		for (int num2 = count - requiredCount; num2 > 0; num2--)
		{
			Bar bar = barInstances[barInstances.Count - 1];
			DestroyDelayed(bar.gameObject);
			barInstances.Remove(bar);
		}
	}

	private void ShowBars()
	{
		int nextBarInstanceIndex = 0;
		for (int i = 0; i < data.DataSets.Count; i++)
		{
			nextBarInstanceIndex = UpdatedBars(i, nextBarInstanceIndex);
		}
	}

	private int UpdatedBars(int dataSetIndex, int nextBarInstanceIndex)
	{
		List<BarEntry> visibleEntries = positioner.GetVisibleEntries(dataSetIndex);
		for (int i = 0; i < visibleEntries.Count; i++)
		{
			UpdateBarWithEntry(barInstances[nextBarInstanceIndex], visibleEntries[i], data.DataSets[dataSetIndex].GetColorForIndex(i), dataSetIndex);
			nextBarInstanceIndex++;
		}
		return nextBarInstanceIndex;
	}

	private Bar UpdateBarWithEntry(Bar barInstance, BarEntry entry, Color color, int dataSetIndex)
	{
		barInstance.transform.localPosition = positioner.GetBarPosition((int)entry.Position, dataSetIndex);
		barInstance.GetComponent<RectTransform>().sizeDelta = positioner.GetBarSize(entry.Value);
		barInstance.SetColor(color);
		barInstance.button.onClick.RemoveAllListeners();
		barInstance.button.onClick.AddListener(delegate
		{
			OnBarClick(entry, dataSetIndex);
		});
		return barInstance;
	}

	private void OnBarClick(BarEntry entry, int dataSetIndex)
	{
		if (Config.BarChartClickAction != null)
		{
			Config.BarChartClickAction(entry, dataSetIndex);
		}
		ShowHideValuePopup(entry, dataSetIndex);
	}

	private void ShowHideValuePopup(BarEntry entry, int dataSetIndex)
	{
		if (currentValuePopup == null)
		{
			currentValuePopup = viewCreator.InstantiateChartPopup(contentView.transform, Config.PopupPrefab);
		}
		if (entry != currentValuePopupEntry)
		{
			UpdateValuePopup(entry, dataSetIndex);
			currentValuePopupEntry = entry;
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

	private void UpdateValuePopup(BarEntry entry, int dataSetIndex)
	{
		currentValuePopup.transform.localPosition = positioner.GetValuePopupPosition(entry, dataSetIndex);
		currentValuePopup.text.text = entry.Value.ToString() ?? "";
		currentValuePopup.gameObject.SetActive(value: true);
	}
}
