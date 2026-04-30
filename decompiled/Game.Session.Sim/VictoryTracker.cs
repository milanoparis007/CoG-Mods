using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Game.Core;
using Game.Services;
using Game.Services.Store;
using Game.Session.Entities;
using Game.Session.Player;
using SomaSim.Util;

namespace Game.Session.Sim;

public class VictoryTracker : ISubManager<SimulationManager>, ILoadObserver
{
	private List<VictoryCategory> _categories;

	private const string PASSENGER_TRAIN = "train-station-passenger";

	private const string FREIGHT_TRAIN = "train-station-freight";

	private const string DOWNTOWN_HOTEL = "biz-downtown-hotel";

	private const string LONGSHOREMAN_HALL = "biz-union-hall";

	private const string WHISKEY_BOTTLED_PROD = "production-makeshift-still-whiskey";

	private const string WHISKEY_BARREL_PROD = "production-small-still-whiskey";

	private const string AGED_WHISKEY_PROD = "production-aged-whiskey";

	private const string BRANDY_BOTTLED_PROD = "production-makeshift-still-brandy";

	private const string BRANDY_BARREL_PROD = "production-small-still-brandy";

	private const string VODKA_BOTTLED_PROD = "production-makeshift-still-vodka";

	private const string VODKA_BARREL_PROD = "production-small-still-vodka";

	private const string GIN_PROD = "production-gin";

	private const string BATHTUB_GIN_PROD = "production-bathtub-gin";

	private const string MOONSHINE_PROD = "production-homebooze-moonshine";

	private const string CORN_MOONSHINE_PROD = "production-homebooze-corn-moonshine";

	private const string ETHNIC_ALCOHOL_PROD = "production-ethnic-alcohol";

	private const string RUM_BOTTLED_PROD = "production-makeshift-still-rum";

	private const string RUM_BARREL_PROD = "production-small-still-rum";

	private const string DARK_RUM_PROD = "production-dark-rum";

	private List<string> bizTrainList = new List<string> { "train-station-passenger", "train-station-freight" };

	private List<string> bizLongshoremanList = new List<string> { "biz-union-hall" };

	private List<string> bizDowntownHotelList = new List<string> { "biz-downtown-hotel" };

	private List<string> spiritsProdsList = new List<string>
	{
		"production-small-still-whiskey", "production-makeshift-still-whiskey", "production-aged-whiskey", "production-small-still-brandy", "production-makeshift-still-brandy", "production-small-still-vodka", "production-makeshift-still-vodka", "production-gin", "production-bathtub-gin", "production-homebooze-moonshine",
		"production-homebooze-corn-moonshine", "production-ethnic-alcohol", "production-small-still-rum", "production-makeshift-still-rum", "production-dark-rum"
	};

	private readonly Label FISHING_TRIP_TROPHY = new Label("fishing-trip");

	private readonly Label CHANDELIER_TROPHY = new Label("chandelier");

	private readonly Label BUSINESS_AWARDS_TROPHY = new Label("business-awards");

	private readonly Label CAESAR_TROPHY = new Label("caesar");

	private readonly Label MAYOR_LAW = new Label("mayor-election");

	private List<Label> buffList = new List<Label>
	{
		BuffConstants.RELBUFF_CARGO_ACTIVE,
		BuffConstants.RELBUFF_CARGO_BRIBE,
		BuffConstants.RELBUFF_PASSENGER_BRIBE
	};

	protected VictorySettings Settings => Game.serv.globals.settings.general.victory;

	public void Initialize(SimulationManager _)
	{
		_categories = InitializeGoals();
		Game.ctx.events.AddListener(SessionEventType.HumanPlayerTurnStarted, OnHumanTurnStarted);
		Game.ctx.console.Add(this, new DebugConsoleEntry("victory", "goalset", CheatGoalSet));
		Game.ctx.console.Add(this, new DebugConsoleEntry("victory", "goalclear", CheatGoalClear));
		Game.ctx.console.Add(this, new DebugConsoleEntry("victory", "forceturn", CheatSetTurn));
	}

	public void Release()
	{
		Game.ctx.console.Remove(this);
		Game.ctx.events.RemoveListener(SessionEventType.HumanPlayerTurnStarted, OnHumanTurnStarted);
		_categories = null;
	}

	public List<VictoryGoal> GetAllGoals()
	{
		return _categories.SelectMany((VictoryCategory cat) => cat.goals).ToList();
	}

	public List<VictoryCategory> GetVictoryCategories()
	{
		return _categories;
	}

	public int CountPassed()
	{
		return GetAllGoals().Count((VictoryGoal g) => g.result == VictoryResult.Pass);
	}

	public void Show()
	{
		Game.ctx.hud.victory.Controller.ShowFromUIClick();
	}

	public void Hide()
	{
		Game.ctx.hud.victory.Controller.Hide();
	}

	public (int, string) ExplainGameOverPoints()
	{
		int num = MathUtil.Clamp(CountPassed(), 0, 6);
		string key = ((num >= 6) ? "victory.points.d" : ((num >= 3) ? "victory.points.c" : ((num >= 1) ? "victory.points.b" : "victory.points.a")));
		string text = Loc.FormatNumber(num) + " " + Loc.Get("victory.star");
		string item = Loc.Get(key, "icon-and-number", text);
		return (num, item);
	}

	public (int, string) ExplainPointsMidgame()
	{
		int num = MathUtil.Clamp(CountPassed(), 0, 6);
		string text = Loc.FormatNumber(num) + " " + Loc.Get("victory.star");
		string item = Loc.Get("victory.points.neutral", "icon-and-number", text);
		return (num, item);
	}

	public string ExplainGameOverRanking()
	{
		var (flag, text, text2) = GenerateLegacyRankingStrings();
		if (flag)
		{
			text = Loc.Get("victory.star") + " " + text;
		}
		return Loc.Get("victory.ranking-format", "name", text, "desc", text2);
	}

	public (bool, string, string) GenerateLegacyRankingStrings()
	{
		Entity playerPeep = Game.ctx.players.Human.social.GetPlayerPeep();
		string[] array = new string[10]
		{
			"name",
			playerPeep.data.person.FullName,
			"firstname",
			playerPeep.data.person.FirstName,
			"lastname",
			playerPeep.data.person.LastName,
			"groupname",
			Game.ctx.players.Human.social.PlayerGroupName,
			"cityname",
			Game.ctx.session.mapconfig.CityName
		};
		int num = MathUtil.Clamp(CountPassed(), 0, 6);
		string text = num.ToString(CultureInfo.InvariantCulture);
		string key = "victory.ranking." + text + ".name";
		string key2 = "victory.ranking." + text + ".desc";
		Gender g;
		Gender num2 = (g = playerPeep.data.person.g);
		object[] array2 = array;
		object[] replacements = array2;
		string gendered = Loc.GetGendered(key, g, replacements);
		Gender g2 = num2;
		array2 = array;
		replacements = array2;
		string gendered2 = Loc.GetGendered(key2, g2, replacements);
		return (num >= 3, gendered, gendered2);
	}

	private void OnHumanTurnStarted(SessionEvent ev)
	{
		if (ev.pid.IsHumanPlayer)
		{
			RecomputeAllGoals();
		}
	}

	public void RecomputeAllGoals()
	{
		foreach (VictoryCategory category in _categories)
		{
			foreach (VictoryGoal goal in category.goals)
			{
				goal.RecomputeState();
			}
		}
	}

	public void OnAfterManagerLoad()
	{
	}

	public void OnAfterEntityLoad()
	{
		RecomputeAllGoals();
	}

	private List<VictoryCategory> InitializeGoals()
	{
		List<VictoryCategory> list = new List<VictoryCategory>();
		list.Add(new VictoryCategory
		{
			locname = "victory.category.1.name",
			locicon = "victory.category.1.icon",
			locdesc = "victory.category.1.desc",
			photo = "Victory 1",
			goals = new List<VictoryGoal>
			{
				new VictoryGoal("victory.net-worth.goal.name", "victory.net-worth.goal.desc", new VictoryNetWorthSubgoal("victory.net-worth.1sub.desc")),
				new VictoryGoal("victory.controls-city.goal.name", "victory.controls-city.goal.desc", Loc.Percentage(Settings.territoryControl), new VictoryControlsCitySubgoal("victory.controls-city.1sub.desc")),
				new VictoryGoal("victory.captains.goal.name", "victory.captains.goal.desc", Loc.FormatNumber(Settings.numberOfCaptains), visDefault, new List<Label> { CAESAR_TROPHY }, new VictoryCaptains("victory.captains.1sub.desc")),
				new VictoryGoal("victory.gambling.goal.name", "victory.gambling.goal.desc", null, visIsAC, new VictoryLuxuryCasinoSubgoal("victory.gambling.1sub.desc")),
				new VictoryGoal("victory.specialists.goal.name", "victory.specialists.goal.desc", Loc.FormatNumber(Settings.numberOfSchemes), visIsPhilly, new VictorySchemesSubgoal("victory.specialists.1sub.desc"))
			}
		});
		list.Add(new VictoryCategory
		{
			locname = "victory.category.2.name",
			locicon = "victory.category.2.icon",
			locdesc = "victory.category.2.desc",
			photo = "Victory 2",
			goals = new List<VictoryGoal>
			{
				new VictoryGoal("victory.affiliates.goal.name", "victory.affiliates.goal.desc", Loc.FormatNumber(Settings.numberOfAffiliates), visDefault, new List<Label> { BUSINESS_AWARDS_TROPHY }, new VictoryAffiliates("victory.affiliates.1sub.desc")),
				new VictoryGoal("victory.trade-train.goal.name", "victory.trade-train.goal.desc", new VictorySpecificBuffSubgoal("victory.trade-train.1sub.desc", bizTrainList, buffList)),
				new VictoryGoal("victory.trade-boats.goal.name", "victory.trade-boats.goal.desc", null, visDefault, new List<Label> { FISHING_TRIP_TROPHY }, new VictoryCountBizSubgoal("victory.trade-boats.1sub.desc", mustBeTiedHouse: false, bizLongshoremanList)),
				new VictoryGoal("victory.mayor.goal.name", "victory.mayor.goal.desc", null, visPackShadowGovernment, new VictoryLawEnactedSubgoal("victory.mayor.1sub.desc", MAYOR_LAW))
			}
		});
		list.Add(new VictoryCategory
		{
			locname = "victory.category.3.name",
			locicon = "victory.category.3.icon",
			locdesc = "victory.category.3.desc",
			photo = "Victory 3",
			goals = new List<VictoryGoal>
			{
				new VictoryGoal("victory.trade-cincinnati.goal.name", "victory.trade-cincinnati.goal.desc", null, visIsCinci, new VictorySpecificSaleSubgoal("victory.trade-cincinnati.1sub.desc", "bourbon", (int)Settings.amtBourbon)),
				new VictoryGoal("victory.producer.goal.name", "victory.producer.goal.desc", new VictoryModuleWorth(manufacture: true, consume: false, "victory.producer.1sub.desc")),
				new VictoryGoal("victory.consumer.goal.name", "victory.consumer.goal.desc", new VictoryModuleWorth(manufacture: false, consume: true, "victory.consumer.1sub.desc")),
				new VictoryGoal("victory.trade-hotel.goal.name", "victory.trade-hotel.goal.desc", null, visDefault, new List<Label> { CHANDELIER_TROPHY }, new VictoryCountBizSubgoal("victory.trade-hotel.1sub.desc", mustBeTiedHouse: true, bizDowntownHotelList)),
				new VictoryGoal("victory.german-sudhaus.goal.name", "victory.german-sudhaus.goal.desc", Loc.FormatNumber(Settings.amtEthnicAlcohol), visEthPackGerman, new VictorySpecificSaleSubgoal("victory.german-sudhaus.1sub.desc", "ethnic-alcohol", (int)Settings.amtEthnicAlcohol)),
				new VictoryGoal("victory.polish-prods.goal.name", "victory.polish-prods.goal.desc", Loc.FormatNumber(Settings.amtSpiritProds), visEthPackPolish, new VictoryHaveModulesInstalledSubgoal("victory.polish-prods.1sub.desc", spiritsProdsList)),
				new VictoryGoal("victory.irish-whiskey.goal.name", "victory.irish-whiskey.goal.desc", Loc.FormatNumber(Settings.amtWhiskeys), visEthPackIrish, new VictorySpecificSaleSubgoal("victory.irish-whiskey.1sub.desc", "ethnic-alcohol", (int)Settings.amtWhiskeys), new VictorySpecificSaleSubgoal("victory.irish-whiskey.2sub.desc", "whiskey-bottled", (int)Settings.amtWhiskeys), new VictorySpecificSaleSubgoal("victory.irish-whiskey.3sub.desc", "aged-whiskey", (int)Settings.amtWhiskeys)),
				new VictoryGoal("victory.english-cigs.goal.name", "victory.english-cigs.goal.desc", Loc.FormatNumber(Settings.amtCigs), visEthPackEnglish, new VictorySpecificSaleSubgoal("victory.english-cigs.1sub.desc", "cigarettes", (int)Settings.amtCigs)),
				new VictoryGoal("victory.dirtycash.goal.name", "victory.dirtycash.goal.desc", Loc.FormatNumber(Settings.amtDirtyCash), visDefault, new VictorySpecificSaleSubgoal("victory.dirtycash.1sub.desc", "counterfeitcash", (int)Settings.amtDirtyCash), new VictorySpecificSaleSubgoal("victory.dirtycash.2sub.desc", "dirty-cash", (int)Settings.amtDirtyCash)),
				new VictoryGoal("victory.drugs.goal.name", "victory.drugs.goal.desc", Loc.FormatNumber(Settings.amtCounterfeit), visDefault, new VictorySpecificSaleSubgoal("victory.drugs.1sub.desc", "cannabis-pound", (int)Settings.amtCounterfeit), new VictorySpecificSaleSubgoal("victory.drugs.2sub.desc", "heroin-packs", (int)Settings.amtCounterfeit)),
				new VictoryGoal("victory.homes.goal.name", "victory.homes.goal.desc", Loc.FormatNumber(Settings.amtStolenGoods), visDefault, new VictorySpecificSaleSubgoal("victory.homes.1sub.desc", "bandlow", (int)Settings.amtStolenGoods), new VictorySpecificSaleSubgoal("victory.homes.2sub.desc", "homemid", (int)Settings.amtStolenGoods))
			}
		});
		list.Add(new VictoryCategory
		{
			locname = "victory.category.4.name",
			locicon = "victory.category.4.icon",
			locdesc = "victory.category.4.desc",
			photo = "Victory 4",
			goals = new List<VictoryGoal>
			{
				new VictoryGoal("victory.gangs.goal.name", "victory.gangs.goal.desc", Loc.Percentage(Settings.gangsEliminated), new VictoryPlayersEliminated("victory.gangs.1sub.desc", PlayerType.GangPlayer)),
				new VictoryGoal("victory.goons.goal.name", "victory.goons.goal.desc", Loc.Percentage(Settings.goonsNeutralized), new VictoryPlayersEliminated("victory.goons.1sub.desc", PlayerType.GoonPlayer)),
				new VictoryGoal("victory.no-vendetta.goal.name", "victory.no-vendetta.goal.desc", new VictoryNoVendetta("victory.no-vendetta.1sub.desc")),
				new VictoryGoal("victory.italian-stolen-booze.goal.name", "victory.italian-stolen-booze.goal.desc", Loc.FormatNumber(Settings.amtBoozeStolen), visEthPackItalian, new VictoryBoozeStolen("victory.italian-stolen-booze.1sub.desc"))
			}
		});
		List<VictoryCategory> list2 = list;
		foreach (VictoryCategory item in list2)
		{
			item.goals = item.goals.Where((VictoryGoal goal) => goal.visFunc()).ToList();
		}
		return list2;
	}

	public static bool visIsPhilly()
	{
		return visIsCity("philadelphia");
	}

	public static bool visIsCinci()
	{
		return visIsCity("cincinnati");
	}

	public static bool visIsAC()
	{
		return visIsCity("atlantic-city");
	}

	public static bool visIsCity(string cityName)
	{
		return Game.ctx.session.mapconfig.id == cityName;
	}

	public static bool visDefault()
	{
		return true;
	}

	public static bool visEthPackGerman()
	{
		return visEthPack(new Label("de"));
	}

	public static bool visEthPackIrish()
	{
		return visEthPack(new Label("ir"));
	}

	public static bool visEthPackItalian()
	{
		return visEthPack(new Label("it"));
	}

	public static bool visEthPackEnglish()
	{
		return visEthPack(new Label("en"));
	}

	public static bool visEthPackPolish()
	{
		return visEthPack(new Label("pl"));
	}

	public static bool visEthPack(Label eth)
	{
		return PlayerCrew.HasEthPackAndIsEth(eth);
	}

	public static bool visPackShadowGovernment()
	{
		return Game.serv.store.IsPackInstalled(PackID.ShadowGovernment);
	}

	private string CheatGoalSet(string[] args)
	{
		List<VictoryGoal> allGoals = GetAllGoals();
		if (args.Length != 4)
		{
			return DebugConsoleEntry.InvalidParam("Invalid parameters", args, 2, "<index> <new-value>");
		}
		if (!int.TryParse(args[2], out var result) || result < 0 || result >= allGoals.Count)
		{
			return DebugConsoleEntry.InvalidParam("Invalid index", args, 2, "<index> <new-value>");
		}
		if (!int.TryParse(args[3], out var result2))
		{
			return DebugConsoleEntry.InvalidParam("Invalid value", args, 3, "<new-value>");
		}
		VictoryResult? victoryResult = EnumUtil<VictoryResult>.TryCastToEnum(result2);
		if (!victoryResult.HasValue)
		{
			return DebugConsoleEntry.InvalidParam("Invalid value", args, 3, "<new-value>");
		}
		VictoryGoal victoryGoal = allGoals[result];
		VictorySubgoal victorySubgoal = victoryGoal.subgoals.FirstOrDefaultFast();
		victorySubgoal.state.result = victoryResult.Value;
		victorySubgoal.state.forced = true;
		victoryGoal.RecomputeState();
		return $"Goal {victoryGoal.locname} state set to {victoryResult.Value}";
	}

	private string CheatGoalClear(string[] args)
	{
		List<VictoryGoal> allGoals = GetAllGoals();
		if (args.Length != 3)
		{
			return DebugConsoleEntry.InvalidParam("Invalid parameters", args, 2, "<index>");
		}
		if (!int.TryParse(args[2], out var result) || result < 0 || result >= allGoals.Count)
		{
			return DebugConsoleEntry.InvalidParam("Invalid index", args, 2, "<index>");
		}
		VictoryGoal victoryGoal = allGoals[result];
		victoryGoal.subgoals.FirstOrDefaultFast().state.forced = false;
		victoryGoal.RecomputeState();
		return "Goal " + victoryGoal.locname + " state reset and recomputed";
	}

	private string CheatSetTurn(string[] args)
	{
		if (args.Length != 3)
		{
			return DebugConsoleEntry.InvalidParam("Invalid parameters", args, 2, "<turn>");
		}
		if (!int.TryParse(args[2], out var result) || result < 0)
		{
			return DebugConsoleEntry.InvalidParam("Invalid turn number", args, 2, "<turn>");
		}
		if (!Game.ctx.clock.CheatForceTurn(result))
		{
			return $"Time cannot go backwards! Current time set to {Game.ctx.clock.Now}";
		}
		return $"Current game time set to {Game.ctx.clock.Now}, press next turn to update";
	}
}
