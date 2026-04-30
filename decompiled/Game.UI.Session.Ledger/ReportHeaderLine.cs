namespace Game.UI.Session.Ledger;

internal class ReportHeaderLine : ReportCellList<Column>
{
	public ReportHeaderLine(Column[] headers)
		: base(headers)
	{
		int i = 0;
		for (int num = headers.Length; i < num; i++)
		{
			headers[i].UpdateIndex(i);
		}
	}
}
