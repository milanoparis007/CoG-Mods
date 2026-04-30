using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AwesomeCharts;

[Serializable]
public class LineDataSet : DataSet<LineEntry>
{
	[SerializeField]
	private float lineThickness = 3f;

	[SerializeField]
	private Color lineColor = Defaults.CHART_LINE_COLOR;

	[SerializeField]
	private Color fillColor = Defaults.CHART_BACKGROUND_COLOR;

	[SerializeField]
	private Texture fillTexture;

	[SerializeField]
	private bool useBezier;

	public float LineThickness
	{
		get
		{
			return lineThickness;
		}
		set
		{
			lineThickness = value;
		}
	}

	public Color LineColor
	{
		get
		{
			return lineColor;
		}
		set
		{
			lineColor = value;
		}
	}

	public Color FillColor
	{
		get
		{
			return fillColor;
		}
		set
		{
			fillColor = value;
		}
	}

	public Texture FillTexture
	{
		get
		{
			return fillTexture;
		}
		set
		{
			fillTexture = value;
		}
	}

	public bool UseBezier
	{
		get
		{
			return useBezier;
		}
		set
		{
			useBezier = value;
		}
	}

	public LineDataSet()
		: this("")
	{
	}

	public LineDataSet(string title)
		: base(title)
	{
	}

	public LineDataSet(string title, List<LineEntry> entries)
		: base(title, entries)
	{
	}

	public float GetMaxPosition()
	{
		if (base.Entries == null || base.Entries.Count == 0)
		{
			return 0f;
		}
		return base.Entries.OrderByDescending((LineEntry a) => a.Position).ToList()[0].Position;
	}

	public float GetMinPosition()
	{
		if (base.Entries == null || base.Entries.Count == 0)
		{
			return 0f;
		}
		return base.Entries.OrderBy((LineEntry a) => a.Position).ToList()[0].Position;
	}

	public float PositionDelta()
	{
		float result = 0f;
		List<LineEntry> sortedEntries = GetSortedEntries();
		if (base.Entries.Count > 1)
		{
			result = sortedEntries[sortedEntries.Count - 1].Position - sortedEntries[0].Position;
		}
		return result;
	}

	public override List<LineEntry> GetSortedEntries()
	{
		return base.Entries.OrderBy((LineEntry entry) => entry.Position).ToList();
	}
}
