using System.Collections.Generic;
using Game.Services;
using Game.Session.Sim;

namespace Game.UI.Session.Victory;

public class VictoryCatPage
{
	public readonly string catName;

	public readonly string catIcon;

	public readonly string catDesc;

	public readonly List<VictoryGoal> catGoals;

	public readonly PhotoConfig catPhoto;

	public VictoryCatPage(VictoryPage page)
	{
		List<VictoryCategory> victoryCategories = Game.ctx.simman.victory.GetVictoryCategories();
		if (page != VictoryPage.Landing)
		{
			VictoryCategory victoryCategory = victoryCategories[(int)(page - 1)];
			catName = victoryCategory.locname;
			catIcon = victoryCategory.locicon;
			catDesc = victoryCategory.locdesc;
			catGoals = victoryCategory.goals;
			catPhoto = victoryCategory.photo;
		}
	}
}
