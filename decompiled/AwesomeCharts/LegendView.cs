using System.Collections.Generic;
using UnityEngine;

namespace AwesomeCharts;

[ExecuteInEditMode]
[RequireComponent(typeof(RectTransform))]
public class LegendView : ACMonoBehaviour
{
	public enum LEGEND_ALIGNMENT
	{
		TOP_LEFT,
		TOP_RIGHT,
		BOTTOM_LEFT,
		BOTTOM_RIGHT
	}

	public enum LEGEND_ORIENTATION
	{
		HORIZONTAL,
		VERTICAL
	}

	[SerializeField]
	private int iconSize = 15;

	[SerializeField]
	private int iconSpacing = 10;

	[SerializeField]
	private Sprite iconImage;

	[SerializeField]
	private int textSize = 17;

	[SerializeField]
	private Color textColor = Color.white;

	[SerializeField]
	private Font textFont;

	[SerializeField]
	private int itemWidth = 200;

	[SerializeField]
	private int itemHeight = 30;

	[SerializeField]
	private int itemSpacing = 10;

	[SerializeField]
	private LEGEND_ALIGNMENT alignment = LEGEND_ALIGNMENT.BOTTOM_LEFT;

	[SerializeField]
	private LEGEND_ORIENTATION orientation = LEGEND_ORIENTATION.VERTICAL;

	[SerializeField]
	private List<LegendEntry> entries;

	private ViewCreator viewCreator = new ViewCreator();

	private List<LegendEntryView> legendEntryViews = new List<LegendEntryView>();

	private bool isDirty;

	public int IconSize
	{
		get
		{
			return iconSize;
		}
		set
		{
			iconSize = value;
			SetDirty();
		}
	}

	public int IconSpacing
	{
		get
		{
			return iconSpacing;
		}
		set
		{
			iconSpacing = value;
			SetDirty();
		}
	}

	public int ItemWidth
	{
		get
		{
			return itemWidth;
		}
		set
		{
			itemWidth = value;
			SetDirty();
		}
	}

	public int ItemHeight
	{
		get
		{
			return itemHeight;
		}
		set
		{
			itemHeight = value;
			SetDirty();
		}
	}

	public int ItemSpacing
	{
		get
		{
			return itemSpacing;
		}
		set
		{
			itemSpacing = value;
			SetDirty();
		}
	}

	public Sprite IconImage
	{
		get
		{
			return iconImage;
		}
		set
		{
			iconImage = value;
			SetDirty();
		}
	}

	public int TextSize
	{
		get
		{
			return textSize;
		}
		set
		{
			textSize = value;
			SetDirty();
		}
	}

	public Color TextColor
	{
		get
		{
			return textColor;
		}
		set
		{
			textColor = value;
			SetDirty();
		}
	}

	public Font TextFont
	{
		get
		{
			return textFont;
		}
		set
		{
			textFont = value;
			SetDirty();
		}
	}

	public LEGEND_ALIGNMENT Alignment
	{
		get
		{
			return alignment;
		}
		set
		{
			alignment = value;
			SetDirty();
		}
	}

	public LEGEND_ORIENTATION Orientation
	{
		get
		{
			return orientation;
		}
		set
		{
			orientation = value;
			SetDirty();
		}
	}

	public List<LegendEntry> Entries
	{
		get
		{
			return entries;
		}
		set
		{
			entries = value;
			SetDirty();
		}
	}

	private void SetDirty()
	{
		isDirty = true;
	}

	private void Awake()
	{
		if (entries == null)
		{
			entries = new List<LegendEntry>();
		}
		if (viewCreator == null)
		{
			viewCreator = new ViewCreator();
		}
	}

	private void Start()
	{
		ClearEditModeObjects();
		DrawLegendEntries();
	}

	private void OnValidate()
	{
		isDirty = true;
	}

	protected override void Update()
	{
		if (isDirty)
		{
			DrawLegendEntries();
			isDirty = false;
		}
	}

	private void DrawLegendEntries()
	{
		UpdateLegendEntryViewInstances(entries.Count);
		for (int i = 0; i < entries.Count; i++)
		{
			SetupLegendEntryView(legendEntryViews[i], entries[i], i);
		}
		HideAllChildrenInInspector();
	}

	private void ClearEditModeObjects()
	{
		int childCount = base.transform.childCount;
		for (int i = 0; i < childCount; i++)
		{
			Object.DestroyImmediate(base.transform.GetChild(0).gameObject);
		}
		legendEntryViews.Clear();
	}

	private void UpdateLegendEntryViewInstances(int requiredCount)
	{
		int count = legendEntryViews.Count;
		for (int num = requiredCount - count; num > 0; num--)
		{
			LegendEntryView item = InstantiateLegendEntryView();
			legendEntryViews.Add(item);
		}
		for (int num2 = count - requiredCount; num2 > 0; num2--)
		{
			LegendEntryView legendEntryView = legendEntryViews[legendEntryViews.Count - 1];
			DestroyDelayed(legendEntryView.gameObject);
			legendEntryViews.Remove(legendEntryView);
		}
	}

	private LegendEntryView InstantiateLegendEntryView()
	{
		return viewCreator.InstantiateLegendEntry(base.transform, PivotValue.BOTTOM_LEFT);
	}

	private void HideAllChildrenInInspector()
	{
		foreach (Transform item in base.transform)
		{
			item.hideFlags = HideFlags.HideInHierarchy | HideFlags.HideInInspector;
		}
	}

	private void SetupLegendEntryView(LegendEntryView view, LegendEntry entry, int index)
	{
		RectTransform component = view.GetComponent<RectTransform>();
		component.sizeDelta = new Vector2(ItemWidth, ItemHeight);
		component.pivot = PivotFromAlignment(alignment);
		component.anchorMin = AnchorsFromAlignment(alignment);
		component.anchorMax = AnchorsFromAlignment(alignment);
		component.anchoredPosition = CalculateLegendEntryPosition(index);
		view.nameText.text = entry.Title;
		view.nameText.fontSize = TextSize;
		view.nameText.color = TextColor;
		if (TextFont != null)
		{
			view.nameText.font = TextFont;
		}
		view.iconImage.color = entry.Color;
		view.iconImage.sprite = IconImage;
		view.IconSize = IconSize;
		view.IconSpacing = IconSpacing;
	}

	private Vector3 CalculateLegendEntryPosition(int index)
	{
		if (orientation == LEGEND_ORIENTATION.VERTICAL)
		{
			return new Vector3(0f, CalculateVerticalEntryPosition(alignment, index), 0f);
		}
		return new Vector3(CalculateHorizontalEntryPosition(alignment, index), 0f, 0f);
	}

	private float CalculateVerticalEntryPosition(LEGEND_ALIGNMENT alignment, int index)
	{
		switch (alignment)
		{
		case LEGEND_ALIGNMENT.BOTTOM_LEFT:
		case LEGEND_ALIGNMENT.BOTTOM_RIGHT:
			return index * (ItemHeight + ItemSpacing);
		case LEGEND_ALIGNMENT.TOP_LEFT:
		case LEGEND_ALIGNMENT.TOP_RIGHT:
			return -(index * (ItemHeight + ItemSpacing));
		default:
			return 0f;
		}
	}

	private float CalculateHorizontalEntryPosition(LEGEND_ALIGNMENT alignment, int index)
	{
		switch (alignment)
		{
		case LEGEND_ALIGNMENT.TOP_LEFT:
		case LEGEND_ALIGNMENT.BOTTOM_LEFT:
			return index * (ItemWidth + ItemSpacing);
		case LEGEND_ALIGNMENT.TOP_RIGHT:
		case LEGEND_ALIGNMENT.BOTTOM_RIGHT:
			return -(index * (ItemWidth + ItemSpacing));
		default:
			return 0f;
		}
	}

	private Vector2 PivotFromAlignment(LEGEND_ALIGNMENT alignment)
	{
		return alignment switch
		{
			LEGEND_ALIGNMENT.TOP_LEFT => PivotValue.TOP_LEFT, 
			LEGEND_ALIGNMENT.TOP_RIGHT => PivotValue.TOP_RIGHT, 
			LEGEND_ALIGNMENT.BOTTOM_LEFT => PivotValue.BOTTOM_LEFT, 
			LEGEND_ALIGNMENT.BOTTOM_RIGHT => PivotValue.BOTTOM_RIGTH, 
			_ => PivotValue.BOTTOM_LEFT, 
		};
	}

	private Vector2 AnchorsFromAlignment(LEGEND_ALIGNMENT alignment)
	{
		return alignment switch
		{
			LEGEND_ALIGNMENT.TOP_LEFT => new Vector2(0f, 1f), 
			LEGEND_ALIGNMENT.TOP_RIGHT => new Vector2(1f, 1f), 
			LEGEND_ALIGNMENT.BOTTOM_LEFT => new Vector2(0f, 0f), 
			LEGEND_ALIGNMENT.BOTTOM_RIGHT => new Vector2(1f, 0f), 
			_ => new Vector2(0f, 0f), 
		};
	}
}
