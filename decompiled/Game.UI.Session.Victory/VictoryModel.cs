using System.Collections.Generic;

namespace Game.UI.Session.Victory;

public class VictoryModel : HUDModel<VictoryModel, VictoryDialog, VictoryController>
{
	private List<VictoryCatPage> _CATEGORIES;

	public VictoryScreenType type;

	private VictoryCatPage _currentReport;

	public bool dead;

	public VictoryCatPage GetReport()
	{
		return _currentReport;
	}

	public void PopulateFromPage(VictoryPage page)
	{
		_currentReport = _CATEGORIES[(int)page];
	}

	public void RefreshModel(VictoryScreenType type)
	{
		_CATEGORIES = new List<VictoryCatPage>
		{
			new VictoryCatPage(VictoryPage.Landing),
			new VictoryCatPage(VictoryPage.Fame),
			new VictoryCatPage(VictoryPage.Connections),
			new VictoryCatPage(VictoryPage.Economy),
			new VictoryCatPage(VictoryPage.Competitors)
		};
		this.type = type;
	}
}
