using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AwesomeCharts;

[Serializable]
public class BarDataSet : DataSet<BarEntry>
{
	[SerializeField]
	private List<Color> barColors = new List<Color>();

	public List<Color> BarColors
	{
		get
		{
			return barColors;
		}
		set
		{
			barColors = value;
		}
	}

	public BarDataSet()
		: this("")
	{
	}

	public BarDataSet(string title)
		: base(title)
	{
	}

	public BarDataSet(string title, List<BarEntry> entries)
		: base(title, entries)
	{
	}

	public long GetMaxPosition()
	{
		if (base.Entries == null || base.Entries.Count == 0)
		{
			return 0L;
		}
		return base.Entries.OrderByDescending((BarEntry a) => a.Position).ToList()[0].Position;
	}

	public long GetMinPosition()
	{
		if (base.Entries == null || base.Entries.Count == 0)
		{
			return 0L;
		}
		return base.Entries.OrderBy((BarEntry a) => a.Position).ToList()[0].Position;
	}

	public long PositionDelta()
	{
		long result = 0L;
		List<BarEntry> sortedEntries = GetSortedEntries();
		if (base.Entries.Count > 1)
		{
			result = sortedEntries[sortedEntries.Count - 1].Position - sortedEntries[0].Position;
		}
		return result;
	}

	public Color GetColorForIndex(int index)
	{
		if (BarColors == null || BarColors.Count == 0)
		{
			return Defaults.BAR_LINE_COLOR;
		}
		return BarColors[index % BarColors.Count];
	}

	public override List<BarEntry> GetSortedEntries()
	{
		return base.Entries.OrderBy((BarEntry entry) => entry.Position).ToList();
	}
}
