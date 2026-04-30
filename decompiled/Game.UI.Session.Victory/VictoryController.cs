using System.Collections.Generic;
using Game.Services;
using Game.Session.Sim;
using Game.UI.Session.Popups;
using SomaSim.Util;

namespace Game.UI.Session.Victory;

public class VictoryController : HUDController<VictoryModel, VictoryDialog, VictoryController>
{
	private int _currentPage;

	private List<VictoryPage> _pages = EnumUtil<VictoryPage>.GetValuesAsList();

	public void ShowFromUIClick()
	{
		Show(VictoryScreenType.FromUIClick);
	}

	public void ShowEndOfYear()
	{
		Game.serv.stats.LogEvent("game_newyear", Game.ctx.clock.Now.YearsInt);
		OkPopup.ShowOk(Loc.Get("victory.yearend"), delegate
		{
			Show(VictoryScreenType.FromEndOfYear);
		}, hides: true);
	}

	public void ShowGameOver(bool isDead = false)
	{
		base.Model.dead = isDead;
		VictorySettings victory = Game.serv.globals.settings.general.victory;
		string header = Loc.Get(victory.endHeadline);
		PhotoConfig endPhoto = victory.endPhoto;
		if (isDead)
		{
			Game.ctx.clock.DisallowPostgame();
			Show(VictoryScreenType.FromGameOver);
			return;
		}
		LogEndGameStats();
		Game.serv.ui.AddPopup(new NewspaperPopup(header, endPhoto, delegate
		{
			Show(VictoryScreenType.FromGameOver);
		}));
	}

	private static void LogEndGameStats()
	{
		VictoryTracker victoryTracker = Game.ctx.simman?.victory;
		if (victoryTracker == null)
		{
			return;
		}
		Game.serv.stats.LogEvent("game_finished_stars", victoryTracker.CountPassed());
		foreach (VictoryGoal allGoal in victoryTracker.GetAllGoals())
		{
			if (allGoal != null && allGoal.result == VictoryResult.Pass)
			{
				Game.serv.stats.LogEvent("game_finished_goalpassed", allGoal.locname);
			}
		}
	}

	internal static void RunFinishGO()
	{
		TimerUtil.RunAfterTime(delegate
		{
			Game.serv.ui.AddPopup(new GameFinishedPopup());
		}, 0.1f);
	}

	internal static void RunDeadGO()
	{
		TimerUtil.RunAfterTime(delegate
		{
			Game.serv.ui.AddPopup(new GameOverPopup());
		}, 0.1f);
	}

	private void Show(VictoryScreenType type)
	{
		Game.ctx.simman.victory.RecomputeAllGoals();
		base.Model.RefreshModel(type);
		if (type == VictoryScreenType.FromGameOver)
		{
			SwitchToEnd();
		}
		else
		{
			SwitchToLanding();
		}
	}

	public void Hide()
	{
		base.View.Hide();
	}

	public void MaybeShowDemoSummary()
	{
	}

	private void SwitchToLanding()
	{
		if (!base.View.IsShowing)
		{
			base.View.Show();
		}
		_currentPage = 0;
		base.View.SwitchToSubview(base.View.landingview);
	}

	private void SwitchToCategory(VictoryPage page)
	{
		if (!base.View.IsShowing)
		{
			base.View.Show();
		}
		base.Model.PopulateFromPage(page);
		base.View.SwitchToSubview(base.View.categoryview);
		base.View.RefreshCategoryDisplay();
	}

	private void SwitchToEnd()
	{
		if (!base.View.IsShowing)
		{
			base.View.Show();
		}
		_currentPage = 0;
		base.View.SwitchToSubview(base.View.endingview, force: true);
	}

	public void Force(int ind)
	{
		_currentPage = ind;
		VictoryPage victoryPage = _pages[_currentPage];
		if (victoryPage == VictoryPage.Landing)
		{
			SwitchToLanding();
		}
		else
		{
			SwitchToCategory(victoryPage);
		}
	}

	public void Switch(int dir)
	{
		_currentPage = MathUtil.Modulus(_currentPage + dir, _pages.Count);
		VictoryPage victoryPage = _pages[_currentPage];
		if (victoryPage == VictoryPage.Landing)
		{
			if (base.Model.type == VictoryScreenType.FromGameOver)
			{
				SwitchToEnd();
			}
			else
			{
				SwitchToLanding();
			}
		}
		else
		{
			SwitchToCategory(victoryPage);
		}
	}

	public void SwitchRight()
	{
		Switch(1);
	}

	public void SwitchLeft()
	{
		Switch(-1);
	}

	public void OpenThrone()
	{
		Game.serv.ui.AddPopup(new ThronePopup());
	}
}
