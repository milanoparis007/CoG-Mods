using System;

namespace Game.UI.Session.Ledger;

public class LedgerButtonDef
{
	public string icon;

	public PageCategory category;

	public ReportType type;

	public Func<ReportDataModel> fn;

	public LedgerButtonDef(ReportType type, Func<ReportDataModel> fn, string icon)
	{
		category = PageCategory.Report;
		this.type = type;
		this.fn = fn;
		this.icon = icon;
	}

	public LedgerButtonDef(PageCategory category, string icon)
	{
		this.category = category;
		this.icon = icon;
	}
}
