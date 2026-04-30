namespace Game.UI.Session.Ledger;

internal class ReportLine : ReportCellList<Cell>
{
	public ReportLine(Column[] headers, Cell[] cells)
		: base(cells)
	{
		int i = 0;
		for (int num = headers.Length; i < num; i++)
		{
			int width = headers[i].width;
			cells[i].UpdateWidth(width);
		}
	}
}
