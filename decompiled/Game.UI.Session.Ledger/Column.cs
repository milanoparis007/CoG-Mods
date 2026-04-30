using System.Text;
using SomaSim.Util;

namespace Game.UI.Session.Ledger;

internal class Column : IReportCell
{
	private static StringBuilder _sb = new StringBuilder(128);

	public readonly int width;

	public readonly string rawtext;

	public readonly string mo;

	public readonly Cell.Alignment align;

	public int index;

	public string text;

	public Column(int width, string text, string mo, Cell.Alignment align = Cell.Alignment.Right)
	{
		this.width = width;
		rawtext = text;
		this.mo = mo;
		this.align = align;
	}

	public void UpdateIndex(int index)
	{
		this.index = index;
		string value = Cell.TrimOrPad(rawtext, width, align, ".");
		_sb.Append("<link=\"");
		_sb.Append(index);
		_sb.Append(">");
		_sb.Append(value);
		_sb.Append("</link>");
		text = _sb.ToStringAndReset();
	}

	public string GetText()
	{
		return text;
	}
}
