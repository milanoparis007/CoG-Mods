using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Services;
using Game.Session.Board;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Player;
using Game.Session.Quests;
using Game.Session.Sim;
using Game.Session.Sim.Modules;
using Game.UI.Session.Ledger;
using SomaSim.Util;

namespace Game.Session.Achievements;

public static class AchievementTests
{
	private const string THOMPSON = "weapon-thompson";

	private const string COLT = "weapon-colt";

	private const string WINCHESTER = "weapon-winchester";

	private const string PISTOL = "weapon-pistol";

	private const string FISTS = "weapon-fists";

	private const string BRICK_WINE_1 = "production-homebooze-brick-wine";

	private const string BRICK_WINE_2 = "production-homebooze-brick-wine-improved";

	private const string BRICK_WINE_3 = "production-homebooze-brick-wine-improved-upgrade";

	private const string BRICK_WINE_4 = "production-homebooze-brick-wine-dock";

	private const string CIDER_1 = "production-homebooze-cider";

	private const string CIDER_2 = "production-homebooze-cider-improved";

	private const string CIDER_3 = "production-homebooze-cider-improved-upgrade";

	private const string CIDER_4 = "production-homebooze-cider-dock";

	private const string HOME_BREW_1 = "production-homebooze-home-brew";

	private const string HOME_BREW_2 = "production-homebooze-home-brew-improved";

	private const string HOME_BREW_3 = "production-homebooze-home-brew-improved-upgrade";

	private const string HOME_BREW_4 = "production-homebooze-home-brew-dock";

	private const string MOONSHINE_1 = "production-homebooze-moonshine";

	private const string MOONSHINE_2 = "production-homebooze-moonshine-improved";

	private const string MOONSHINE_3 = "production-homebooze-moonshine-improved-upgrade";

	private const string MOONSHINE_4 = "production-homebooze-moonshine-dock";

	private const string BOOTLEGGER_BRICK_WINE = "bootlegger-homebooze-brick-wine";

	private const string BOOTLEGGER_FRUIT_WINE = "bootlegger-homebooze-fruit-wine";

	private const string BOOTLEGGER_CIDER = "bootlegger-homebooze-cider";

	private const string BOOTLEGGER_APPLE_JACK = "bootlegger-homebooze-apple-jack";

	private const string BOOTLEGGER_HOME_BREW = "bootlegger-homebooze-home-brew";

	private const string BOOTLEGGER_MOONSHINE = "bootlegger-homebooze-moonshine";

	private const string SUPPER = "supper-club-player";

	private const string SUPPER_A = "supper-club-player-a";

	private const string SUPPER_B = "supper-club-player-b";

	private const string SUPPER_C = "supper-club-player-c";

	private const string SUPPER_D = "supper-club-player-d";

	private const string SUPPER_A_PITTS = "supper-club-player-a-pittsburgh";

	private const string SUPPER_B_PITTS = "supper-club-player-b-pittsburgh";

	private const string SUPPER_C_PITTS = "supper-club-player-c-pittsburgh";

	private const string SUPPER_D_PITTS = "supper-club-player-d-pittsburgh";

	private const string SUPPER_A_CINCI = "supper-club-player-a-cincinnati";

	private const string SUPPER_B_CINCI = "supper-club-player-b-cincinnati";

	private const string SUPPER_C_CINCI = "supper-club-player-c-cincinnati";

	private const string SUPPER_D_CINCI = "supper-club-player-d-cincinnati";

	private const string SUPPER_A_DETROIT = "supper-club-player-a-detroit";

	private const string SUPPER_B_DETROIT = "supper-club-player-b-detroit";

	private const string SUPPER_C_DETROIT = "supper-club-player-c-detroit";

	private const string SUPPER_D_DETROIT = "supper-club-player-d-detroit";

	private const string DANCE = "dance-hall-player";

	private const string DANCE_A = "dance-hall-player-a";

	private const string DANCE_B = "dance-hall-player-b";

	private const string DANCE_C = "dance-hall-player-c";

	private const string DANCE_D = "dance-hall-player-d";

	private const string DANCE_A_PITTS = "dance-hall-player-a-pittsburgh";

	private const string DANCE_B_PITTS = "dance-hall-player-b-pittsburgh";

	private const string DANCE_C_PITTS = "dance-hall-player-c-pittsburgh";

	private const string DANCE_D_PITTS = "dance-hall-player-d-pittsburgh";

	private const string DANCE_A_CINCI = "dance-hall-player-a-cincinnati";

	private const string DANCE_B_CINCI = "dance-hall-player-b-cincinnati";

	private const string DANCE_C_CINCI = "dance-hall-player-c-cincinnati";

	private const string DANCE_D_CINCI = "dance-hall-player-d-cincinnati";

	private const string DANCE_A_DETROIT = "dance-hall-player-a-detroit";

	private const string DANCE_B_DETROIT = "dance-hall-player-b-detroit";

	private const string DANCE_C_DETROIT = "dance-hall-player-c-detroit";

	private const string DANCE_D_DETROIT = "dance-hall-player-d-detroit";

	private const string JAZZ = "jazz-club-player";

	private const string JAZZ_A = "jazz-club-player-a";

	private const string JAZZ_B = "jazz-club-player-b";

	private const string JAZZ_C = "jazz-club-player-c";

	private const string JAZZ_D = "jazz-club-player-d";

	private const string JAZZ_A_PITTS = "jazz-club-player-a-pittsburgh";

	private const string JAZZ_B_PITTS = "jazz-club-player-b-pittsburgh";

	private const string JAZZ_C_PITTS = "jazz-club-player-c-pittsburgh";

	private const string JAZZ_D_PITTS = "jazz-club-player-d-pittsburgh";

	private const string JAZZ_A_CINCI = "jazz-club-player-a-cincinnati";

	private const string JAZZ_B_CINCI = "jazz-club-player-b-cincinnati";

	private const string JAZZ_C_CINCI = "jazz-club-player-c-cincinnati";

	private const string JAZZ_D_CINCI = "jazz-club-player-d-cincinnati";

	private const string JAZZ_A_DETROIT = "jazz-club-player-a-detroit";

	private const string JAZZ_B_DETROIT = "jazz-club-player-b-detroit";

	private const string JAZZ_C_DETROIT = "jazz-club-player-c-detroit";

	private const string JAZZ_D_DETROIT = "jazz-club-player-d-detroit";

	private const string HOTEL = "hotel-player";

	private const string HOTEL_A = "hotel-player-a";

	private const string HOTEL_B = "hotel-player-b";

	private const string HOTEL_C = "hotel-player-c";

	private const string HOTEL_D = "hotel-player-d";

	private const string HOTEL_A_PITTS = "hotel-player-a-pittsburgh";

	private const string HOTEL_B_PITTS = "hotel-player-b-pittsburgh";

	private const string HOTEL_C_PITTS = "hotel-player-c-pittsburgh";

	private const string HOTEL_D_PITTS = "hotel-player-d-pittsburgh";

	private const string HOTEL_A_CINCI = "hotel-player-a-cincinnati";

	private const string HOTEL_B_CINCI = "hotel-player-b-cincinnati";

	private const string HOTEL_C_CINCI = "hotel-player-c-cincinnati";

	private const string HOTEL_D_CINCI = "hotel-player-d-cincinnati";

	private const string HOTEL_A_DETROIT = "hotel-player-a-detroit";

	private const string HOTEL_B_DETROIT = "hotel-player-b-detroit";

	private const string HOTEL_C_DETROIT = "hotel-player-c-detroit";

	private const string HOTEL_D_DETROIT = "hotel-player-d-detroit";

	private const string BOTTLER = "production-bottled-booze-small";

	private const string WHISKEY_PROD = "production-makeshift-still-whiskey";

	private const string BRANDY_PROD = "production-makeshift-still-brandy";

	private const string VODKA_PROD = "production-makeshift-still-vodka";

	private const string RUM_PROD = "production-makeshift-still-rum";

	private const string WHISKEY_SMALL = "production-small-still-whiskey";

	private const string BRANDY_SMALL = "production-small-still-brandy";

	private const string VODKA_SMALL = "production-small-still-vodka";

	private const string RUM_SMALL = "production-small-still-rum";

	private const string WHISKEY_HIGH = "production-aged-whiskey";

	private const string WHISKEY_ALPHA_HIGH = "production-aged-whiskey-alpha";

	private const string CORDIALS_HIGH = "production-cordials";

	private const string GIN_HIGH = "production-gin";

	private const string DARK_RUM_HIGH = "production-dark-rum";

	private const string DARK_RUM_ALPHA_HIGH = "production-dark-rum-alpha";

	private const string BEER_KEGS_HIGH = "production-beer-kegs";

	private const string BEER_KEGS_ALPHA = "production-beer-kegs-alpha";

	private const string CHAMPAGNE = "production-champagne";

	private const string CHAMPAGNE_ALPHA = "production-champagne-alpha";

	private const string FRUIT_BRANDY_PROD = "production-fruit-brandy";

	private const string FRUIT_BRANDY_PROD_2 = "production-fruit-brandy-upgraded";

	private const string FRUIT_BRANDY_PROD_3 = "production-fruit-brandy-upgraded-upgrade";

	private const string GARAGE_SMALL = "player-garage-module-small";

	private const string GARAGE_MEDIUM = "player-garage-module-medium";

	private const string TRUCK_GARAGE_SMALL = "player-truck-garage-small";

	private const string TRUCK_GARAGE_IMPROVED = "player-truck-garage-improved";

	private const string MOONSHINE_GARAGE = "production-homebooze-moonshine-dock";

	private const string CIDER_GARAGE = "production-homebooze-cider-dock";

	private const string HOME_BREW_GARAGE = "production-homebooze-home-brew-dock";

	private const string BRICK_GARAGE = "production-homebooze-brick-wine-dock";

	private const string FAKE_WINE = "fake-wine";

	private const string FAKE_BEER = "fake-beer";

	private const string SPARKLING_CIDER = "sparkling-cider";

	private const string BATHTUB_GINWORKS = "bathtub-ginworks";

	private const string IMPROVED_MOONSHINE = "improved-homebooze-moonshine";

	private const string IMPROVED_BRICK_WINE = "improved-homebooze-brick-wine";

	private const string IMPROVED_CIDER = "improved-homebooze-cider";

	private const string IMPROVED_HOME_BREW = "improved-homebooze-home-brew";

	private const string ACTIONS_LEVEL = "levelup-actions";

	private const string COMBAT_LEVEL = "levelup-combat";

	private const string DRIVING_LEVEL = "levelup-driving";

	private const string CITY_DRIVER = "levelup-city-driver";

	private const string ETH_BUILDING_TAKEOVER = "ethnic-building-takeover";

	private const string ETH_QUEST_TWO = "player-same-ethnicity-two";

	private const string ETH_QUEST_THREE_A = "player-same-ethnicity-three-a";

	private const string ETH_QUEST_THREE_B = "player-same-ethnicity-three-b";

	private const string ETH_QUEST_THREE_C = "player-same-ethnicity-three-c";

	private const string ETH_QUEST_THREE_D = "player-same-ethnicity-three-d";

	private const string ETH_QUEST_THREE_E = "player-same-ethnicity-three-e";

	private const string ETH_QUEST_FOUR = "player-same-ethnicity-four";

	private const string FAMILY_QUEST_BOURBON = "bourbon-dump-one";

	private const string FAMILY_QUEST_CIDER = "crew-family-one-cider";

	private const string FAMILY_QUEST_APPLEJACK = "crew-family-one-apple-jack";

	private const string FAMILY_QUEST_BRICK_WINE = "crew-family-one-brick-wine";

	private const string FAMILY_QUEST_FRUIT_WINE = "crew-family-one-fruit-wine";

	private const string FAMILY_QUEST_HOME_BREW = "crew-family-one-home-brew";

	private const string FAMILY_QUEST_MOONSHINE = "crew-family-one-moonshine";

	private const string FRIENDLY_GESTURE = "outpost-building-takeover";

	private const string CROSSING_OVER = "canada-access";

	private const string RAILROAD_BRICK_WINE = "train-station-mission-three-brick-wine";

	private const string RAILROAD_CIDER = "train-station-mission-three-cider";

	private const string RAILROAD_HOME_BREW = "train-station-mission-three-home-brew";

	private const string RAILROAD_MOONSHINE = "train-station-mission-three-moonshine";

	private const string RAILROAD_APPLE_JACK = "train-station-mission-three-brick-apple-jack";

	private const string RAILROAD_FAKE_WINE = "train-station-mission-three-fake-wine";

	private const string RAILROAD_SPARKLING_CIDER = "train-station-mission-three-sparkling-cider";

	private const string RAILROAD_FAKE_BEER = "train-station-mission-three-fake-beer";

	private const string RAILROAD_BATHTUB_GIN = "train-station-mission-three-bathtub-gin";

	private const string RAILROAD_FRUIT_BRANDY = "train-station-mission-three-fruit-brandy";

	private const string RAILROAD_CORN_WHISKEY = "train-station-mission-three-corn-whiskey";

	private const string PROTECTION_QUEST = "protection-broken-burglary";

	private const string MOONSHINE_LEARNER = "moonshine-three";

	private const string BRICK_WINE_LEARNER = "brick-wine-learner";

	private const string CIDER_LEARNER = "cider-three";

	private const string HOME_BREW_LEARNER = "home-brew-three";

	private const string FANCY_HOTEL = "biz-downtown-hotel";

	private const string FANCY_CANADA_HOTEL = "biz-downtown-hotel-canada";

	private const string TRAIN_STATION_PASSENGER = "train-station-passenger";

	private const string TRAIN_STATION_FREIGHT = "train-station-freight";

	private const string UNION_HALL = "biz-union-hall";

	private const string DINNER_PARTY = "dinner-party";

	private const string POKER_NIGHT = "poker-night";

	private const string BOURBON = "bourbon";

	private const string WHISKEY_AGED = "aged-whiskey";

	private const string WHISKEY_BARREL = "whiskey-barrel";

	private const string WHISKEY_BOTTLED = "whiskey-bottled";

	private const string VODKA_BARREL = "vodka-barrel";

	private const string VODKA_BOTTLED = "vodka-bottled";

	private const string BRANDY_BOTTLED = "brandy-bottled";

	private const string BRANDY_BARREL = "brandy-barrel";

	private const string BRANDY_AGED = "aged-brandy";

	private const string RUM_BOTTLED = "rum-bottled";

	private const string RUM_BARREL = "rum-barrel";

	private const string RUM_CARIBBEAN = "caribbean-rum";

	private const string RUM_DARK = "dark-rum";

	private const string DELIVERY_TRUCK = "vehicle-small-delivery-truck";

	private static readonly List<Label> wine = new List<Label>
	{
		new Label("production-homebooze-brick-wine"),
		new Label("production-homebooze-brick-wine-improved"),
		new Label("production-homebooze-brick-wine-improved-upgrade"),
		new Label("production-homebooze-brick-wine-dock")
	};

	private static readonly List<Label> cider = new List<Label>
	{
		new Label("production-homebooze-cider"),
		new Label("production-homebooze-cider-improved"),
		new Label("production-homebooze-cider-improved-upgrade"),
		new Label("production-homebooze-cider-dock")
	};

	private static readonly List<Label> homebrew = new List<Label>
	{
		new Label("production-homebooze-home-brew"),
		new Label("production-homebooze-home-brew-improved"),
		new Label("production-homebooze-home-brew-improved-upgrade"),
		new Label("production-homebooze-home-brew-dock")
	};

	private static readonly List<Label> moonshine = new List<Label>
	{
		new Label("production-homebooze-moonshine"),
		new Label("production-homebooze-moonshine-improved"),
		new Label("production-homebooze-moonshine-improved-upgrade"),
		new Label("production-homebooze-moonshine-dock")
	};

	private static readonly (string, List<Label>)[] HOMEMADE_HOOCH_CATEGORIES = new(string, List<Label>)[4]
	{
		("brick-wine", wine),
		("cider", cider),
		("homebrew", homebrew),
		("moonshine", moonshine)
	};

	private static readonly List<Label> bootleggers = new List<Label>
	{
		new Label("bootlegger-homebooze-apple-jack"),
		new Label("bootlegger-homebooze-brick-wine"),
		new Label("bootlegger-homebooze-cider"),
		new Label("bootlegger-homebooze-fruit-wine"),
		new Label("bootlegger-homebooze-home-brew"),
		new Label("bootlegger-homebooze-moonshine")
	};

	private static readonly (string, List<Label>)[] BOOTLEGGER_CATEGORIES = new(string, List<Label>)[1] { ("bootlegger", bootleggers) };

	private static readonly List<Label> supper = new List<Label>
	{
		new Label("supper-club-player"),
		new Label("supper-club-player-a"),
		new Label("supper-club-player-b"),
		new Label("supper-club-player-c"),
		new Label("supper-club-player-d"),
		new Label("supper-club-player-a-cincinnati"),
		new Label("supper-club-player-b-cincinnati"),
		new Label("supper-club-player-c-cincinnati"),
		new Label("supper-club-player-d-cincinnati"),
		new Label("supper-club-player-a-pittsburgh"),
		new Label("supper-club-player-b-pittsburgh"),
		new Label("supper-club-player-c-pittsburgh"),
		new Label("supper-club-player-d-pittsburgh"),
		new Label("supper-club-player-a-detroit"),
		new Label("supper-club-player-b-detroit"),
		new Label("supper-club-player-c-detroit"),
		new Label("supper-club-player-d-detroit")
	};

	private static readonly List<Label> fancy = new List<Label>
	{
		new Label("dance-hall-player"),
		new Label("dance-hall-player-a"),
		new Label("dance-hall-player-b"),
		new Label("dance-hall-player-c"),
		new Label("dance-hall-player-d"),
		new Label("dance-hall-player-a-cincinnati"),
		new Label("dance-hall-player-b-cincinnati"),
		new Label("dance-hall-player-c-cincinnati"),
		new Label("dance-hall-player-d-cincinnati"),
		new Label("dance-hall-player-a-pittsburgh"),
		new Label("dance-hall-player-b-pittsburgh"),
		new Label("dance-hall-player-c-pittsburgh"),
		new Label("dance-hall-player-d-pittsburgh"),
		new Label("dance-hall-player-a-detroit"),
		new Label("dance-hall-player-b-detroit"),
		new Label("dance-hall-player-c-detroit"),
		new Label("dance-hall-player-d-detroit"),
		new Label("jazz-club-player"),
		new Label("jazz-club-player-a"),
		new Label("jazz-club-player-b"),
		new Label("jazz-club-player-c"),
		new Label("jazz-club-player-d"),
		new Label("jazz-club-player-a-cincinnati"),
		new Label("jazz-club-player-b-cincinnati"),
		new Label("jazz-club-player-c-cincinnati"),
		new Label("jazz-club-player-d-cincinnati"),
		new Label("jazz-club-player-a-pittsburgh"),
		new Label("jazz-club-player-b-pittsburgh"),
		new Label("jazz-club-player-c-pittsburgh"),
		new Label("jazz-club-player-d-pittsburgh"),
		new Label("jazz-club-player-a-detroit"),
		new Label("jazz-club-player-b-detroit"),
		new Label("jazz-club-player-c-detroit"),
		new Label("jazz-club-player-d-detroit"),
		new Label("hotel-player"),
		new Label("hotel-player-a"),
		new Label("hotel-player-b"),
		new Label("hotel-player-c"),
		new Label("hotel-player-d"),
		new Label("hotel-player-a-cincinnati"),
		new Label("hotel-player-b-cincinnati"),
		new Label("hotel-player-c-cincinnati"),
		new Label("hotel-player-d-cincinnati"),
		new Label("hotel-player-a-pittsburgh"),
		new Label("hotel-player-b-pittsburgh"),
		new Label("hotel-player-c-pittsburgh"),
		new Label("hotel-player-d-pittsburgh"),
		new Label("hotel-player-a-detroit"),
		new Label("hotel-player-b-detroit"),
		new Label("hotel-player-c-detroit"),
		new Label("hotel-player-d-detroit")
	};

	private static readonly (string, List<Label>)[] RITZ_CATEGORIES = new(string, List<Label>)[2]
	{
		("supper", supper),
		("fancy", fancy)
	};

	private static readonly List<Label> bottler = new List<Label>
	{
		new Label("production-bottled-booze-small")
	};

	private static readonly (string, List<Label>)[] BOTTLER_CATEGORIES = new(string, List<Label>)[1] { ("bottler", bottler) };

	private static readonly List<Label> stills = new List<Label>
	{
		new Label("production-makeshift-still-whiskey"),
		new Label("production-small-still-whiskey"),
		new Label("production-makeshift-still-brandy"),
		new Label("production-small-still-brandy"),
		new Label("production-makeshift-still-rum"),
		new Label("production-small-still-rum"),
		new Label("production-makeshift-still-vodka"),
		new Label("production-small-still-vodka")
	};

	private static readonly (string, List<Label>)[] STILLS_CATEGORIES = new(string, List<Label>)[1] { ("stills", stills) };

	private static readonly List<Label> high_booze = new List<Label>
	{
		new Label("production-beer-kegs-alpha"),
		new Label("production-beer-kegs"),
		new Label("production-aged-whiskey"),
		new Label("production-aged-whiskey-alpha"),
		new Label("production-cordials"),
		new Label("production-gin"),
		new Label("production-dark-rum-alpha"),
		new Label("production-dark-rum"),
		new Label("production-champagne"),
		new Label("production-champagne-alpha")
	};

	private static readonly (string, List<Label>)[] HIGH_BOOZE_CATEGORIES = new(string, List<Label>)[1] { ("high_booze", high_booze) };

	private static readonly List<string> ethQuests = new List<string> { "ethnic-building-takeover", "player-same-ethnicity-two", "player-same-ethnicity-three-a", "player-same-ethnicity-three-b", "player-same-ethnicity-three-c", "player-same-ethnicity-three-d", "player-same-ethnicity-three-e", "player-same-ethnicity-four" };

	private static readonly (string, List<string>)[] ETH_QUESTS_CATEGORIES = new(string, List<string>)[1] { ("ethQuests", ethQuests) };

	private static readonly List<string> famQuests = new List<string> { "crew-family-one-apple-jack", "bourbon-dump-one", "crew-family-one-brick-wine", "crew-family-one-cider", "crew-family-one-fruit-wine", "crew-family-one-home-brew", "crew-family-one-moonshine" };

	private static readonly (string, List<string>)[] FAME_QUEST_CATEGORIES = new(string, List<string>)[1] { ("famQuests", famQuests) };

	private static readonly List<string> gestures = new List<string> { "outpost-building-takeover" };

	private static readonly (string, List<string>)[] GESTURE_QUEST_CATEGORIES = new(string, List<string>)[1] { ("gesture", gestures) };

	private static readonly List<string> canada = new List<string> { "canada-access" };

	private static readonly (string, List<string>)[] CANADA_INTRO_QUEST_CATEGORIES = new(string, List<string>)[1] { ("canada", canada) };

	private static readonly List<Label> fruit_booze = new List<Label>
	{
		new Label("production-fruit-brandy"),
		new Label("production-fruit-brandy-upgraded"),
		new Label("production-fruit-brandy-upgraded-upgrade")
	};

	private static readonly (string, List<Label>)[] FRUIT_BOOZE_CATEGORIES = new(string, List<Label>)[1] { ("fruit_booze", fruit_booze) };

	private static readonly List<string> railroadQuests = new List<string>
	{
		"train-station-mission-three-brick-apple-jack", "train-station-mission-three-bathtub-gin", "train-station-mission-three-brick-wine", "train-station-mission-three-cider", "train-station-mission-three-corn-whiskey", "train-station-mission-three-fake-beer", "train-station-mission-three-fake-wine", "train-station-mission-three-fruit-brandy", "train-station-mission-three-home-brew", "train-station-mission-three-moonshine",
		"train-station-mission-three-sparkling-cider"
	};

	private static readonly (string, List<string>)[] RAILROAD_INTRO_QUEST_CATEGORIES = new(string, List<string>)[1] { ("railroadQuests", railroadQuests) };

	private static readonly List<string> hooliganQuest = new List<string> { "protection-broken-burglary" };

	private static readonly (string, List<string>)[] HOOLIGAN_QUEST_CATEGORIES = new(string, List<string>)[1] { ("hooliganQuest", hooliganQuest) };

	private static readonly List<Label> fancy_hotel = new List<Label>
	{
		new Label("biz-downtown-hotel"),
		new Label("biz-downtown-hotel-canada")
	};

	private static readonly List<Label> train_station = new List<Label>
	{
		new Label("train-station-freight"),
		new Label("train-station-passenger")
	};

	private static readonly List<Label> union = new List<Label>
	{
		new Label("biz-union-hall")
	};

	private static readonly (string, List<Label>)[] HIGH_PILLOW_CATEGORIES = new(string, List<Label>)[3]
	{
		("fancy_hotel", fancy_hotel),
		("train_station", train_station),
		("union", union)
	};

	private static readonly List<Label> passenger = new List<Label>
	{
		new Label("train-station-passenger")
	};

	private static readonly List<Label> freight = new List<Label>
	{
		new Label("train-station-freight")
	};

	private static readonly (string, List<Label>)[] STATION_MASTER_CATEGORIES = new(string, List<Label>)[2]
	{
		("passenger", passenger),
		("freight", freight)
	};

	private static readonly List<Label> trucks = new List<Label>
	{
		new Label("player-garage-module-medium"),
		new Label("player-garage-module-small"),
		new Label("production-homebooze-brick-wine-dock"),
		new Label("production-homebooze-cider-dock"),
		new Label("production-homebooze-home-brew-dock"),
		new Label("production-homebooze-moonshine-dock"),
		new Label("player-truck-garage-improved"),
		new Label("player-truck-garage-small")
	};

	private static readonly (string, List<Label>)[] REPAIR_CATEGORIES = new(string, List<Label>)[1] { ("trucks", trucks) };

	public static bool AchievementScavengeCheck(SessionEvent sessionEvent)
	{
		EntityID eid = sessionEvent.eid;
		bool isJustGoon = sessionEvent.pid.FindPlayer().IsJustGoon;
		_ = eid.FindEntity().data.building.safehouse;
		bool flag = true;
		return isJustGoon && flag;
	}

	public static bool AchievementCheckThompsonKill(SessionEvent sessionEvent)
	{
		CombatResults obj = sessionEvent.ctx as CombatResults;
		bool flag = obj.attacker.weapon.resid == new Label("weapon-thompson");
		bool isHumanCrew = obj.attacker.IsHumanCrew;
		bool isDead = obj.target.IsDead;
		return flag && isHumanCrew && isDead;
	}

	public static bool AchievementCheckSoftKill(SessionEvent sessionEvent)
	{
		CombatResults obj = sessionEvent.ctx as CombatResults;
		bool flag = !obj.attacker.weapon.firearm;
		bool isHumanCrew = obj.attacker.IsHumanCrew;
		bool isDead = obj.target.IsDead;
		return flag && isHumanCrew && isDead;
	}

	public static bool AchievementHomemadeHooch(SessionEvent sessionEvent)
	{
		return HasModuleInCategories(Game.ctx.players.Human, HOMEMADE_HOOCH_CATEGORIES);
	}

	public static bool AchievementCaptainGain(SessionEvent sessionEvent)
	{
		bool isHumanPlayer = sessionEvent.pid.IsHumanPlayer;
		bool flag = sessionEvent.eid.FindEntity().components.agent.IsCaptain();
		return isHumanPlayer && flag;
	}

	public static bool AchievementTenBuildings(SessionEvent sessionEvent)
	{
		return Game.ctx.players.GetHumanPlayerData().territory.controlledBuildings.Count >= 10;
	}

	public static bool AchievementFiveCrew(SessionEvent sessionEvent)
	{
		return AchieveCrewCount(5);
	}

	public static bool AchievementThirteenCrew(SessionEvent sessionEvent)
	{
		return AchieveCrewCount(13);
	}

	public static bool AchievementThreeFronts(SessionEvent sessionEvent)
	{
		return AchieveFrontCount(3);
	}

	public static bool AchievementTwentyCorners(SessionEvent sessionEvent)
	{
		return AchieveNodeCount(20);
	}

	public static bool AchievementFiftyCorners(SessionEvent sessionEvent)
	{
		return AchieveNodeCount(50);
	}

	public static bool AchievementMoneyDirty(SessionEvent sessionEvent)
	{
		return AchieveMoney(2000);
	}

	public static bool AchievementMoneySizeable(SessionEvent sessionEvent)
	{
		return AchieveMoney(5000);
	}

	public static bool AchievementMoneyConsiderable(SessionEvent sessionEvent)
	{
		return AchieveMoney(15000);
	}

	public static bool AchievementMoneyCopious(SessionEvent sessionEvent)
	{
		return AchieveMoney(25000);
	}

	public static bool AchievementChicagoWin(SessionEvent sessionEvent)
	{
		return AchieveWin("chicago");
	}

	public static bool AchievementPittsburghWin(SessionEvent sessionEvent)
	{
		return AchieveWin("pittsburgh");
	}

	public static bool AchievementCincinnatiWin(SessionEvent sessionEvent)
	{
		return AchieveWin("cincinnati");
	}

	public static bool AchievementDetroitWin(SessionEvent sessionEvent)
	{
		return AchieveWin("detroit");
	}

	public static bool AchievementDinnerParty(SessionEvent sessionEvent)
	{
		return ReseventCheck(Game.ctx.simman.resevents.GetData(sessionEvent.eid.FindEntity()), new Label("dinner-party"));
	}

	public static bool AchievementPokerParty(SessionEvent sessionEvent)
	{
		return ReseventCheck(Game.ctx.simman.resevents.GetData(sessionEvent.eid.FindEntity()), new Label("poker-night"));
	}

	public static bool AchievementGinJoint(SessionEvent sessionEvent)
	{
		return HasModuleInCategories(Game.ctx.players.Human, BOOTLEGGER_CATEGORIES);
	}

	public static bool AchievementGettingFancy(SessionEvent sessionEvent)
	{
		PlayerInfo human = Game.ctx.players.Human;
		bool flag = human.skills.HasSkill(new Label("fake-wine"));
		bool flag2 = human.skills.HasSkill(new Label("fake-beer"));
		bool flag3 = human.skills.HasSkill(new Label("sparkling-cider"));
		bool flag4 = human.skills.HasSkill(new Label("bathtub-ginworks"));
		return flag && flag2 && flag3 && flag4;
	}

	public static bool AchievementPuttingOnRitz(SessionEvent sessionEvent)
	{
		return HasModuleInCategories(Game.ctx.players.Human, RITZ_CATEGORIES);
	}

	public static bool AchievementBottledUp(SessionEvent sessionEvent)
	{
		return HasModuleInCategories(Game.ctx.players.Human, BOTTLER_CATEGORIES);
	}

	public static bool AchievementStillDreaming(SessionEvent sessionEvent)
	{
		return HasModuleInCategories(Game.ctx.players.Human, STILLS_CATEGORIES);
	}

	public static bool AchievementHighEndBooze(SessionEvent sessionEvent)
	{
		return HasModuleInCategories(Game.ctx.players.Human, HIGH_BOOZE_CATEGORIES);
	}

	public static bool AchievementVehiclesNum(SessionEvent sessionEvent)
	{
		return Game.ctx.players.Human.crew.CountVehicles >= 12;
	}

	public static bool AchievementConnectToCop(SessionEvent sessionEvent)
	{
		return sessionEvent.eid.FindEntity().data.agent.pid.FindPlayer().IsCopOrFed;
	}

	public static bool AchievementLevelupDriver(SessionEvent sessionEvent)
	{
		return AchieveLevel(new Label("levelup-driving"), 9, sessionEvent.eid, sessionEvent.pid);
	}

	public static bool AchievementLevelupAction(SessionEvent sessionEvent)
	{
		return AchieveLevel(new Label("levelup-actions"), 3, sessionEvent.eid, sessionEvent.pid);
	}

	public static bool AchievementLevelupFighter(SessionEvent sessionEvent)
	{
		return AchieveLevel(new Label("levelup-combat"), 3, sessionEvent.eid, sessionEvent.pid);
	}

	public static bool AchievementEthnicityQuests(SessionEvent sessionEvent)
	{
		return HasQuestInCategories(ETH_QUESTS_CATEGORIES);
	}

	public static bool AchievementFamQuests(SessionEvent sessionEvent)
	{
		return HasQuestInCategories(FAME_QUEST_CATEGORIES);
	}

	public static bool AchievementGestureQuest(SessionEvent sessionEvent)
	{
		return HasQuestInCategories(GESTURE_QUEST_CATEGORIES);
	}

	public static bool AchievementCanadaQuest(SessionEvent sessionEvent)
	{
		return HasQuestInCategories(CANADA_INTRO_QUEST_CATEGORIES);
	}

	public static bool AchievementFirewater(SessionEvent sessionEvent)
	{
		return HasModuleInCategories(Game.ctx.players.Human, FRUIT_BOOZE_CATEGORIES);
	}

	public static bool AchievementBourbonBarrels(SessionEvent sessionEvent)
	{
		bool num = GetBoozeInfo(new Label("bourbon")) >= 200;
		bool flag = Game.ctx.session.mapconfig.id == "cincinnati";
		return num && flag;
	}

	public static bool AchievementBigShoulders(SessionEvent sessionEvent)
	{
		int num = GetBoozeInfo(new Label("aged-whiskey")) + GetBoozeInfo(new Label("whiskey-barrel")) + GetBoozeInfo(new Label("whiskey-bottled"));
		int num2 = GetBoozeInfo(new Label("vodka-bottled")) + GetBoozeInfo(new Label("vodka-barrel"));
		int num3 = GetBoozeInfo(new Label("brandy-bottled")) + GetBoozeInfo(new Label("brandy-barrel")) + GetBoozeInfo(new Label("aged-brandy"));
		int num4 = GetBoozeInfo(new Label("rum-bottled")) + GetBoozeInfo(new Label("rum-barrel")) + GetBoozeInfo(new Label("caribbean-rum")) + GetBoozeInfo(new Label("dark-rum"));
		bool flag = Game.ctx.session.mapconfig.id == "chicago";
		return num + num2 + num3 + num4 >= 1 && flag;
	}

	public static bool AchievementBoozeBaron(SessionEvent sessionEvent)
	{
		return CompareBoozeTotal(500);
	}

	public static bool AchievementLiquorLord(SessionEvent sessionEvent)
	{
		return CompareBoozeTotal(1500);
	}

	public static bool AchievementStalwart(SessionEvent sessionEvent)
	{
		return CompareBoozeTotal(3000);
	}

	public static bool AchievementBootlegger(SessionEvent sessionEvent)
	{
		return CompareBootlegTotal(500);
	}

	public static bool AchievementSpeakeasy(SessionEvent sessionEvent)
	{
		return CompareBootlegTotal(1500);
	}

	public static bool AchievementClubs(SessionEvent sessionEvent)
	{
		return CompareBootlegTotal(3000);
	}

	public static bool AchievementRailroads(SessionEvent sessionEvent)
	{
		return HasQuestInCategories(RAILROAD_INTRO_QUEST_CATEGORIES);
	}

	public static bool AchievementExtortEight(SessionEvent sessionEvent)
	{
		return CompareExtortTotal(8);
	}

	public static bool AchievementExtortSixteen(SessionEvent sessionEvent)
	{
		return CompareExtortTotal(16);
	}

	public static bool AchievementKeepWord(SessionEvent sessionEvent)
	{
		return HasQuestInCategories(HOOLIGAN_QUEST_CATEGORIES);
	}

	public static bool AchievementTiedHouse(SessionEvent sessionEvent)
	{
		return Game.ctx.achievements.tiedTotal >= 9;
	}

	public static bool AchievementSucceedIfHuman(SessionEvent sessionEvent)
	{
		return sessionEvent.pid.IsHumanPlayer;
	}

	public static bool AchievementMovingExperiences(SessionEvent sessionEvent)
	{
		foreach (EntityID allVehicle in Game.ctx.players.Human.crew.AllVehicles)
		{
			if (allVehicle.FindEntity().config.Template == new Label("vehicle-small-delivery-truck"))
			{
				return true;
			}
		}
		return false;
	}

	public static bool AchievementSteppingItUp(SessionEvent sessionEvent)
	{
		foreach (AutomationSequence allSequence in Game.ctx.players.Human.automation.GetAllSequences())
		{
			if (allSequence.steps.Count >= 25)
			{
				return true;
			}
		}
		return false;
	}

	public static bool AchievementComplete40Quests(SessionEvent sessionEvent)
	{
		return Game.ctx.quests.FindCompleteQuestCount() >= 40;
	}

	public static bool AchievementKnow15Skills(SessionEvent sessionEvent)
	{
		return Game.ctx.players.Human.skills.CurrentSkillCount >= 15;
	}

	public static bool AchievementNetWorthSugar(SessionEvent sessionEvent)
	{
		return LedgerReportGenerator.GetNetWorthOfPlayer(Game.ctx.players.Human).cash >= 25000;
	}

	public static bool AchievementNetWorthCentury(SessionEvent sessionEvent)
	{
		return LedgerReportGenerator.GetNetWorthOfPlayer(Game.ctx.players.Human).cash >= 50000;
	}

	public static bool AchievementHighPillow(SessionEvent sessionEvent)
	{
		return HasBuildingInCategories(Game.ctx.players.Human, HIGH_PILLOW_CATEGORIES);
	}

	public static bool AchievementStationMaster(SessionEvent sessionEvent)
	{
		return HasBuildingInCategories(Game.ctx.players.Human, STATION_MASTER_CATEGORIES);
	}

	public static bool AchievementCopaceticCoordination(SessionEvent sessionEvent)
	{
		return CheckTimeRequirementOnStall(365);
	}

	public static bool AchievementExtraordinaryLogistics(SessionEvent sessionEvent)
	{
		return CheckTimeRequirementOnStall(730);
	}

	public static bool AchievementPeoplePerson(SessionEvent sessionEvent)
	{
		return AchieveTickets(50);
	}

	public static bool AchievementSocialButterfly(SessionEvent sessionEvent)
	{
		if (AchieveTickets(100))
		{
			return AchieveNumRes(10);
		}
		return false;
	}

	public static bool AchievementImprover(SessionEvent sessionEvent)
	{
		bool flag = Game.ctx.players.Human.skills.HasSkill(new Label("improved-homebooze-moonshine"));
		bool num = Game.ctx.quests.IsQuestCompletedByID("moonshine-three", EntityID.INVALID);
		bool flag2 = Game.ctx.players.Human.skills.HasSkill(new Label("improved-homebooze-brick-wine"));
		bool flag3 = Game.ctx.quests.IsQuestCompletedByID("brick-wine-learner", EntityID.INVALID);
		bool flag4 = Game.ctx.players.Human.skills.HasSkill(new Label("improved-homebooze-cider"));
		bool flag5 = Game.ctx.quests.IsQuestCompletedByID("cider-three", EntityID.INVALID);
		bool flag6 = Game.ctx.players.Human.skills.HasSkill(new Label("improved-homebooze-home-brew"));
		bool flag7 = Game.ctx.quests.IsQuestCompletedByID("home-brew-three", EntityID.INVALID);
		if (!(num && flag) && !(flag3 && flag2) && !(flag5 && flag4))
		{
			return flag7 && flag6;
		}
		return true;
	}

	public static bool AchievementLayLow(SessionEvent sessionEvent)
	{
		return !Game.ctx.achievements.WasArrested;
	}

	public static bool AchievementApplesauce(SessionEvent sessionEvent)
	{
		return Game.ctx.players.Human.crew.DeadCrewCount == 0;
	}

	public static bool AchievementMaintenance(SessionEvent sessionEvent)
	{
		bool flag = HasModuleInCategories(Game.ctx.players.Human, REPAIR_CATEGORIES);
		bool flag2 = false;
		foreach (EntityID item in Game.ctx.players.Human.territory.GetAllControlledBuildingsUnsafe())
		{
			foreach (IBizModule bizModule in ModulesUtil.GetBizModules(item))
			{
				if (bizModule is VehicleModule vehicleModule && vehicleModule.data.numRepairs >= 10)
				{
					flag2 = true;
					break;
				}
			}
		}
		return flag2 && flag;
	}

	public static bool AchievementDeterminedDeliveries(SessionEvent sessionEvent)
	{
		return AchieveLevel(new Label("levelup-city-driver"), 3, sessionEvent.eid, sessionEvent.pid);
	}

	public static bool AchievementAutoSucceed(SessionEvent sessionEvent)
	{
		return true;
	}

	public static int GetBoozeInfo(Label res)
	{
		return Game.ctx.achievements.boozeInfo.FindOrDefault(Resource.Find(res), 0);
	}

	public static bool CompareBoozeTotal(int req)
	{
		return Game.ctx.achievements.boozeTotal >= req;
	}

	public static bool CompareBootlegTotal(int req)
	{
		return Game.ctx.achievements.bootlegTotal >= req;
	}

	public static bool CompareExtortTotal(int req)
	{
		return Game.ctx.achievements.extortTotal >= req;
	}

	public static bool AchieveCrewCount(int numCrew)
	{
		return Game.ctx.players.Human.crew.TotalCrewCount >= numCrew;
	}

	public static bool AchieveFrontCount(int numFronts)
	{
		return Game.ctx.players.Human.outposts.GetOutpostEntriesUnsafe().Count >= numFronts;
	}

	public static bool AchieveNodeCount(int numNodes)
	{
		return Game.ctx.players.Human.territory.OwnedNodeCount >= numNodes;
	}

	public static bool AchieveMoney(int amount)
	{
		return Game.ctx.players.Human.finances.GetMoneyThisTurn().endMoney.cash >= amount;
	}

	public static bool AchieveWin(string city)
	{
		if (Game.ctx.simman.victory.CountPassed() >= 3)
		{
			return Game.ctx.session.mapconfig.id == city;
		}
		return false;
	}

	public static bool AchieveTickets(int tickets)
	{
		return Game.ctx.achievements.UsedTickets >= tickets;
	}

	public static bool AchieveNumRes(int res)
	{
		return Game.ctx.achievements.AttendedNo >= res;
	}

	public static bool AchieveLevel(Label skill, int level, EntityID crew, PlayerID player)
	{
		if (crew.IsNotValid)
		{
			return false;
		}
		bool isHumanPlayer = player.IsHumanPlayer;
		XP xp = crew.FindEntity().data.agent.xp;
		bool flag = xp != null && xp.GetLevelupLevel(skill) >= level;
		return isHumanPlayer && flag;
	}

	public static bool HasQuestInCategories(params (string category, List<string> quests)[] toCheck)
	{
		QuestManager quests = Game.ctx.quests;
		Dictionary<string, bool> dictionary = new Dictionary<string, bool>();
		(string, List<string>)[] array = toCheck;
		for (int i = 0; i < array.Length; i++)
		{
			(string, List<string>) tuple = array[i];
			dictionary.Add(tuple.Item1, value: false);
		}
		array = toCheck;
		for (int i = 0; i < array.Length; i++)
		{
			(string, List<string>) tuple2 = array[i];
			foreach (string item in tuple2.Item2)
			{
				if (quests.IsQuestCompletedByID(item, EntityID.INVALID))
				{
					dictionary[tuple2.Item1] = true;
					break;
				}
			}
		}
		return dictionary.Values.All((bool value) => value);
	}

	public static bool HasModuleInCategories(PlayerInfo player, params (string category, List<Label> modules)[] toCheck)
	{
		List<EntityID> allControlledBuildingsUnsafe = player.territory.GetAllControlledBuildingsUnsafe();
		Dictionary<string, bool> dictionary = new Dictionary<string, bool>();
		(string, List<Label>)[] array = toCheck;
		for (int i = 0; i < array.Length; i++)
		{
			(string, List<Label>) tuple = array[i];
			dictionary.Add(tuple.Item1, value: false);
		}
		foreach (EntityID item in allControlledBuildingsUnsafe)
		{
			array = toCheck;
			for (int i = 0; i < array.Length; i++)
			{
				(string, List<Label>) tuple2 = array[i];
				if (dictionary[tuple2.Item1])
				{
					continue;
				}
				foreach (Label item2 in tuple2.Item2)
				{
					if (ModulesUtil.HasModule(item.FindEntity(), item2))
					{
						dictionary[tuple2.Item1] = true;
						break;
					}
				}
			}
		}
		return dictionary.Values.All((bool value) => value);
	}

	public static bool HasBuildingInCategories(PlayerInfo player, params (string category, List<Label> identity)[] toCheck)
	{
		List<NodeID> allOwnedNodesUnsafe = player.territory.GetAllOwnedNodesUnsafe();
		Dictionary<string, bool> dictionary = new Dictionary<string, bool>();
		(string, List<Label>)[] array = toCheck;
		for (int i = 0; i < array.Length; i++)
		{
			(string, List<Label>) tuple = array[i];
			dictionary.Add(tuple.Item1, value: false);
		}
		foreach (NodeID item in allOwnedNodesUnsafe)
		{
			foreach (EntityID item2 in item.FindNode().interesting)
			{
				array = toCheck;
				for (int i = 0; i < array.Length; i++)
				{
					(string, List<Label>) tuple2 = array[i];
					foreach (Label item3 in tuple2.Item2)
					{
						if (BuildingUtil.FindBizForBuilding(item2).config.Template == item3)
						{
							dictionary[tuple2.Item1] = true;
							break;
						}
					}
				}
			}
		}
		return dictionary.Values.All((bool value) => value);
	}

	public static bool CheckTimeRequirementOnStall(int days)
	{
		foreach (EntityID item in Game.ctx.players.Human.territory.GetAllControlledBuildingsUnsafe())
		{
			List<IBizModule> bizModules = ModulesUtil.GetBizModules(item);
			if (bizModules.Count == 0)
			{
				continue;
			}
			foreach (IBizModule item2 in bizModules)
			{
				if (item2 != null && item2 is ManufactureModule manufactureModule && (Game.ctx.clock.Now - manufactureModule.data.lastStall).deltadays >= days)
				{
					return true;
				}
			}
		}
		return false;
	}

	public static bool ReseventCheck(ResEventData data, Label resevent)
	{
		bool isThisTurn = data.IsThisTurn;
		bool flag = data.GetConfig().id == resevent;
		return isThisTurn && flag;
	}
}
