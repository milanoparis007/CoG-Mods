using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace AwesomeCharts;

[ExecuteInEditMode]
public class PieChart : BaseChart<PieData>, IPointerClickHandler, IEventSystemHandler
{
	public delegate void EntryClickDelegate(int index, PieEntry entry);

	[SerializeField]
	private PieData data;

	[SerializeField]
	private PieChartConfig config;

	private const float ENTRY_INDICATOR_LINE_SPACING = 5f;

	private const float SELECTED_ENTRY_OFFSET = 8f;

	private GameObject maskContainer;

	private List<PieChartEntryView> entryViews = new List<PieChartEntryView>();

	private List<PieChartValueIndicator> valueIndicatorViews = new List<PieChartValueIndicator>();

	private List<PieEntry> selectedEntries = new List<PieEntry>();

	private List<EntryClickDelegate> clickDelegates = new List<EntryClickDelegate>();

	public PieChartConfig Config
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

	public override PieData GetChartData()
	{
		return data;
	}

	private void Reset()
	{
		data = new PieData(new PieDataSet());
		config = new PieChartConfig();
	}

	protected override void Awake()
	{
		base.Awake();
		if (data == null)
		{
			data = new PieData(new PieDataSet());
		}
		if (config == null)
		{
			config = new PieChartConfig();
		}
	}

	protected override void Start()
	{
		entryViews = new List<PieChartEntryView>();
		valueIndicatorViews = new List<PieChartValueIndicator>();
		base.Start();
	}

	private void OnConfigChanged()
	{
		SetDirty();
	}

	protected override List<LegendEntry> CreateLegendViewEntries()
	{
		List<LegendEntry> list = new List<LegendEntry>();
		if (data == null || data.DataSet == null)
		{
			return list;
		}
		foreach (PieEntry entry in data.DataSet.Entries)
		{
			list.Add(new LegendEntry(entry.Label, entry.Color));
		}
		return list;
	}

	protected override void OnDrawChartContent()
	{
		base.OnDrawChartContent();
		data.DataSet.RecalculateValues();
		UpdateMaskContainer();
		UpdateEntryViewsInstances(data.DataSet.GetEntriesCount());
		UpdateValueIndicatorInstances(data.DataSet.GetEntriesCount());
		FillEntryViews();
		UpdateValueIndicators();
	}

	private void UpdateMaskContainer()
	{
		if (maskContainer == null)
		{
			maskContainer = viewCreator.InstantiateMaskablePieChartObject("mask", chartDataContainerView.transform, PivotValue.CENTER);
		}
		float num = Mathf.Max(Config.InnerPadding * 2, 0f);
		maskContainer.GetComponent<RectTransform>().sizeDelta = new Vector2(num, num);
	}

	private void UpdateEntryViewsInstances(int requiredCount)
	{
		int count = entryViews.Count;
		for (int num = requiredCount - count; num > 0; num--)
		{
			entryViews.Add(CreateEntryView());
		}
		for (int num2 = count - requiredCount; num2 > 0; num2--)
		{
			PieChartEntryView pieChartEntryView = entryViews[entryViews.Count - 1];
			DestroyDelayed(pieChartEntryView.gameObject);
			entryViews.Remove(pieChartEntryView);
		}
	}

	private PieChartEntryView CreateEntryView()
	{
		return viewCreator.InstantiatePieChartEntryView("Pie entry", maskContainer.transform, PivotValue.CENTER);
	}

	private void UpdateValueIndicatorInstances(int requiredCount)
	{
		int count = valueIndicatorViews.Count;
		for (int num = requiredCount - count; num > 0; num--)
		{
			valueIndicatorViews.Add(CreateValueIndicatorView());
		}
		for (int num2 = count - requiredCount; num2 > 0; num2--)
		{
			PieChartValueIndicator pieChartValueIndicator = valueIndicatorViews[valueIndicatorViews.Count - 1];
			DestroyDelayed(pieChartValueIndicator.gameObject);
			valueIndicatorViews.Remove(pieChartValueIndicator);
		}
	}

	private PieChartValueIndicator CreateValueIndicatorView()
	{
		return viewCreator.InstantiatePieEntryValueIndicator("Pie entry value indicator", contentView.transform, PivotValue.CENTER);
	}

	private void FillEntryViews()
	{
		PieDataSet dataSet = data.DataSet;
		for (int i = 0; i < dataSet.GetEntriesCount(); i++)
		{
			FillEntryView(entryViews[i], dataSet.Entries[i], dataSet.GetTotalValue(), dataSet.GetPercentValue(i), dataSet.GetRotationValue(i));
		}
	}

	private void FillEntryView(PieChartEntryView view, PieEntry entry, float totalValue, float percentValue, float rotation)
	{
		view.TotalValue = totalValue;
		view.Entry = entry;
		view.transform.rotation = Quaternion.Euler(new Vector3(0f, 0f, 0f - rotation));
		float angle = rotation + percentValue / 2f * 360f;
		Vector2 anchoredPosition = (selectedEntries.Contains(entry) ? MathUtils.GetPositionOnCircle(angle, 8f) : Vector2.zero);
		float num = GetChartRadius() * 2f;
		view.GetComponent<RectTransform>().sizeDelta = new Vector2(num, num);
		view.GetComponent<RectTransform>().anchoredPosition = anchoredPosition;
	}

	private float GetChartRadius()
	{
		Vector2 size = GetSize();
		return Mathf.Min(size.x / 2f, size.y / 2f);
	}

	private void UpdateValueIndicators()
	{
		PieDataSet dataSet = data.DataSet;
		for (int i = 0; i < dataSet.GetEntriesCount(); i++)
		{
			if (ShouldValueIndicatorBeEnabled(dataSet.Entries[i]))
			{
				valueIndicatorViews[i].gameObject.SetActive(value: true);
				UpdateValueIndicatorView(valueIndicatorViews[i], dataSet.Entries[i], dataSet.GetRotationValue(i), dataSet.GetPercentValue(i));
			}
			else
			{
				valueIndicatorViews[i].gameObject.SetActive(value: false);
			}
		}
	}

	private bool ShouldValueIndicatorBeEnabled(PieEntry entry)
	{
		if (Config.ValueIndicatorVisibility != PieChartConfig.ValueIndicatorVisibilityMode.ALWAYS)
		{
			if (Config.ValueIndicatorVisibility == PieChartConfig.ValueIndicatorVisibilityMode.ONLY_SELECTED)
			{
				return selectedEntries.Contains(entry);
			}
			return false;
		}
		return true;
	}

	private void UpdateValueIndicatorView(PieChartValueIndicator view, PieEntry entry, float rotation, float percentValue)
	{
		float num = rotation + percentValue / 2f * 360f;
		bool flag = num % 360f > 180f;
		int num2 = ((!flag) ? 1 : (-1));
		Vector2 positionOnCircle = MathUtils.GetPositionOnCircle(num, GetChartRadius() + 5f + (selectedEntries.Contains(entry) ? 8f : 0f));
		Vector2 positionOnCircle2 = MathUtils.GetPositionOnCircle(num, GetChartRadius() + (float)Config.ValueIndicatorLineLength + 5f + (selectedEntries.Contains(entry) ? 8f : 0f));
		Vector2 vector = positionOnCircle2 + new Vector2(Config.ValueIndicatorLineLength * num2, 0f);
		Vector2 vector2 = vector + new Vector2(5f * (float)num2, 0f);
		List<Vector2> linePoints = new List<Vector2>
		{
			positionOnCircle - vector2,
			positionOnCircle2 - vector2,
			vector - vector2
		};
		view.GetComponent<RectTransform>().anchoredPosition = vector2;
		view.Label = CreateValueIndicatorLabel(entry, percentValue);
		view.FontSize = Config.ValueIndicatorFontSize;
		view.IndicatorColor = Config.ValueIndicatorColor;
		view.LinePoints = linePoints;
		view.ReversedLabel = flag;
	}

	private string CreateValueIndicatorLabel(PieEntry entry, float percentValue)
	{
		return string.Format("{0}: {1}%", entry.Label, (percentValue * 100f).ToString("0.00"));
	}

	public void OnPointerClick(PointerEventData eventData)
	{
		if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(GetComponent<RectTransform>(), eventData.position, eventData.pressEventCamera, out var localPoint))
		{
			return;
		}
		float num = Vector2.Distance(localPoint, Vector2.zero);
		if (num <= GetChartRadius() && num > (float)Config.InnerPadding)
		{
			double angle = MathUtils.GetAngle(Vector2.zero, localPoint);
			angle = MathUtils.AngleToCircleAngle(angle);
			int num2 = data.DataSet.EntryIndexForAngle(angle);
			if (num2 > -1)
			{
				OnEntryClick(num2, data.DataSet.Entries[num2]);
			}
		}
	}

	private void OnEntryClick(int index, PieEntry entry)
	{
		for (int i = 0; i < clickDelegates.Count; i++)
		{
			clickDelegates[i](index, entry);
		}
	}

	public bool IsEntryAtPositionSelected(int position)
	{
		return selectedEntries.Contains(data.DataSet.GetEntryAt(position));
	}

	public void SelectEntryAtPosition(int position)
	{
		PieEntry entryAt = data.DataSet.GetEntryAt(position);
		if (entryAt != null && !selectedEntries.Contains(entryAt))
		{
			selectedEntries.Add(entryAt);
			SetDirty();
		}
	}

	public void DeselectEntryAtPosition(int position)
	{
		PieEntry entryAt = data.DataSet.GetEntryAt(position);
		if (entryAt != null && selectedEntries.Contains(entryAt))
		{
			selectedEntries.Remove(entryAt);
			SetDirty();
		}
	}

	public void AddEntryClickDelegate(EntryClickDelegate clickDelegate)
	{
		if (!clickDelegates.Contains(clickDelegate))
		{
			clickDelegates.Add(clickDelegate);
		}
	}

	public void RemoveEntryClickDelegate(EntryClickDelegate clickDelegate)
	{
		if (clickDelegates.Contains(clickDelegate))
		{
			clickDelegates.Remove(clickDelegate);
		}
	}
}
