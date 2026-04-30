using System;
using System.Collections.Generic;
using System.Text;
using SomaSim.Util;

namespace Game.UI.Session.Ledger;

public class ReportDataModel
{
	public enum SortType
	{
		InitialSortNone,
		InitialSortAscending,
		InitialSortDescending
	}

	private static StringBuilder _sb = new StringBuilder(32768);

	private readonly int LedgerWidthChars = 75;

	internal readonly string title;

	internal readonly SortType sorttype;

	internal readonly ReportHeaderLine header;

	internal readonly List<ReportLine> lines;

	internal int lastSortColumn { get; private set; }

	internal bool lastSortAscending { get; private set; }

	internal int columns => header.cells.Length;

	internal bool CanBeResorted => sorttype != SortType.InitialSortNone;

	internal ReportDataModel(string title, SortType options, params Column[] headers)
	{
		this.title = title;
		sorttype = options;
		header = new ReportHeaderLine(headers);
		lines = new List<ReportLine>();
		lastSortColumn = -1;
	}

	internal bool HasSortType(SortType type)
	{
		return type == sorttype;
	}

	internal void AddRow(params Cell[] cells)
	{
		ReportLine item = new ReportLine(header.cells, cells);
		lines.Add(item);
	}

	internal string Build()
	{
		int i = 0;
		for (int count = lines.Count; i < count; i++)
		{
			_sb.AppendLine(lines[i].GetOrMakeLine());
		}
		return _sb.ToStringAndReset();
	}

	internal void Sort(int column)
	{
		bool flag = true;
		if (lastSortColumn == column)
		{
			flag = !lastSortAscending;
		}
		Sort(column, flag);
	}

	internal void Sort(int column, bool ascending)
	{
		if (CanBeResorted)
		{
			lines.StableSort(MakeComparer(column, ascending));
			lastSortColumn = column;
			lastSortAscending = ascending;
		}
	}

	internal Comparison<ReportLine> MakeComparer(int column, bool ascending)
	{
		return delegate(ReportLine aline, ReportLine bline)
		{
			Cell cell = aline.cells[column];
			Cell cell2 = bline.cells[column];
			int num = 0;
			if (cell.sortvalue is int num2)
			{
				num = num2.CompareTo((int)cell2.sortvalue);
			}
			else if (cell.sortvalue is string text)
			{
				num = text.CompareTo(cell2.sortvalue as string);
			}
			if (!ascending)
			{
				num = -num;
			}
			return num;
		};
	}
}
