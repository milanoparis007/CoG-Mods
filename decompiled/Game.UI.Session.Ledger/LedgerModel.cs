using System.Collections.Generic;

namespace Game.UI.Session.Ledger;

public class LedgerModel : HUDModel<LedgerModel, LedgerDialog, LedgerController>
{
	internal static readonly List<LedgerButtonDef> HANDLERS = new List<LedgerButtonDef>
	{
		new LedgerButtonDef(PageCategory.Chart, "ledger.charts.button"),
		new LedgerButtonDef(ReportType.Crew, LedgerReportGenerator.GetCrew, "ledger.crewlist.button"),
		new LedgerButtonDef(ReportType.Inventory, LedgerReportGenerator.GetInventory, "ledger.inventory.button"),
		new LedgerButtonDef(ReportType.NetWorth, LedgerReportGenerator.GetNetWorth, "ledger.networth.button"),
		new LedgerButtonDef(ReportType.Fronts, LedgerReportGenerator.GetFronts, "ledger.fronts.button"),
		new LedgerButtonDef(ReportType.Gambling, LedgerReportGenerator.GetGambling, "ledger.gambling.button")
	};

	public ReportDataModel CurrentReport { get; private set; }

	internal void UpdateReportFrom(LedgerButtonDef def)
	{
		CurrentReport = def?.fn();
		if (CurrentReport != null)
		{
			if (CurrentReport.HasSortType(ReportDataModel.SortType.InitialSortAscending))
			{
				CurrentReport.Sort(0, ascending: true);
			}
			if (CurrentReport.HasSortType(ReportDataModel.SortType.InitialSortDescending))
			{
				CurrentReport.Sort(0, ascending: false);
			}
		}
	}
}
