using System.Text;
using SomaSim.Util;

namespace Game.UI.Session.Ledger;

internal class ReportCellList<T> where T : IReportCell
{
	public readonly T[] cells;

	private string _cachedline;

	public ReportCellList(T[] cells)
	{
		this.cells = cells;
	}

	public string GetOrMakeLine()
	{
		string obj = _cachedline ?? MakeLine();
		string result = obj;
		_cachedline = obj;
		return result;
	}

	private string MakeLine()
	{
		StringBuilder stringBuilder = StringBuilderPool.AllocateInstance();
		int i = 0;
		for (int num = cells.Length; i < num; i++)
		{
			if (i > 0)
			{
				stringBuilder.Append(" ");
			}
			stringBuilder.Append(cells[i].GetText());
		}
		return stringBuilder.ToStringAndReturnToPool();
	}
}
