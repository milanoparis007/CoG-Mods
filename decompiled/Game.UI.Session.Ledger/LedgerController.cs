using System.Collections.Generic;
using System.Linq;

namespace Game.UI.Session.Ledger;

public class LedgerController : HUDController<LedgerModel, LedgerDialog, LedgerController>
{
	private static HashSet<LedgerButtonDef> _seenThisSession = new HashSet<LedgerButtonDef>();

	public void Show()
	{
		ShowCharts(null);
	}

	public void ShowReport(ReportType? reportType)
	{
		if (!base.View.IsShowing)
		{
			base.View.Show();
		}
		base.Model.UpdateReportFrom(null);
		base.View.SwitchToSubview(base.View.reportsview);
		if (reportType.HasValue)
		{
			LedgerButtonDef ledgerButtonDef = LedgerModel.HANDLERS.FirstOrDefault((LedgerButtonDef def) => def.type == reportType.Value);
			if (ledgerButtonDef != null)
			{
				ShowReport(ledgerButtonDef);
				return;
			}
		}
		ShowCharts(null);
	}

	public void Hide()
	{
		base.View.Hide();
	}

	internal void OnLedgerButtonClick(LedgerButtonDef def)
	{
		switch (def.category)
		{
		case PageCategory.Chart:
			ShowCharts(def);
			break;
		case PageCategory.Report:
			ShowReport(def);
			break;
		}
	}

	private void ShowCharts(LedgerButtonDef def)
	{
		if (!base.View.IsShowing)
		{
			base.View.Show();
		}
		base.View.SwitchToSubview(base.View.chartsview);
		RegisterSeen(def);
	}

	private void ShowReport(LedgerButtonDef def)
	{
		if (def != null)
		{
			base.Model.UpdateReportFrom(def);
			base.View.SwitchToSubview(base.View.reportsview);
			base.View.RefreshReportDisplay();
			RegisterSeen(def);
		}
	}

	public void SortCurrentReport(int column)
	{
		ReportDataModel currentReport = base.Model.CurrentReport;
		if (column < currentReport.columns)
		{
			currentReport.Sort(column);
			base.View.RefreshReportDisplay();
		}
	}

	private static void RegisterSeen(LedgerButtonDef def)
	{
		if (def != null)
		{
			_seenThisSession.Add(def);
			if (_seenThisSession.Count == LedgerModel.HANDLERS.Count)
			{
				_seenThisSession.Clear();
			}
		}
	}
}
