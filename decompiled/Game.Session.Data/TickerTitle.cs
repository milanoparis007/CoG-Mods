using Game.Services;

namespace Game.Session.Data;

public struct TickerTitle
{
	public string key;

	public string raw;

	public static readonly TickerTitle DEFAULT = MakeLocGet("ui.tickers.category.default");

	public static readonly TickerTitle LEVELUP = MakeLocGet("ui.tickers.category.levelup");

	public static readonly TickerTitle SKILLGAIN = MakeLocGet("ui.tickers.category.skillgain");

	public static readonly TickerTitle SCOPEOUT = MakeLocGet("ui.tickers.category.scopeout");

	public static readonly TickerTitle BIZ_UPDATE = MakeLocGet("ui.tickers.category.bizupdate");

	public static readonly TickerTitle COP_UPDATE = MakeLocGet("ui.tickers.category.copupdate");

	public static readonly TickerTitle CREWGAIN = MakeLocGet("ui.tickers.category.crewgain");

	public static readonly TickerTitle DEMANDS = MakeLocGet("ui.tickers.category.demands");

	public static readonly TickerTitle RESOURCE = MakeLocGet("ui.tickers.category.resource");

	public static readonly TickerTitle SALARY_PROBLEM = MakeLocGet("ui.tickers.category.salary");

	public static readonly TickerTitle COMBAT_RESULTS = MakeLocGet("ui.tickers.category.combat-results");

	public static readonly TickerTitle STALLED_PROD = MakeLocGet("ui.tickers.category.stallprod");

	public static readonly TickerTitle STALLED_MODULE = MakeLocGet("ui.tickers.category.stallmodule");

	public static readonly TickerTitle BLDG_DAMAGE_TAKEN = MakeLocGet("ui.tickers.category.building-damage-taken");

	public static readonly TickerTitle BLDG_DAMAGE_FIXED = MakeLocGet("ui.tickers.category.building-damage-fixed");

	public static readonly TickerTitle BLDG_DAMAGE_DESTROYED = MakeLocGet("ui.tickers.category.building-damage-destroyed");

	public static readonly TickerTitle OUTPOST_START = MakeLocGet("ui.tickers.category.outpost-start");

	public static readonly TickerTitle OUTPOST_STARTED_EXPAND = MakeLocGet("ui.tickers.category.outpost-exp");

	public static readonly TickerTitle OUTPOST_CAN_EXPAND = MakeLocGet("ui.tickers.category.outpost-expyes");

	public static readonly TickerTitle OUTPOST_CANNOT_EXPAND = MakeLocGet("ui.tickers.category.outpost-expno");

	public static readonly TickerTitle OUTPOST_NOTICE = MakeLocGet("ui.tickers.category.outpost-notice");

	public static readonly TickerTitle OUTPOST_SHUTDOWN = MakeLocGet("ui.tickers.category.outpost-shutdown");

	public static readonly TickerTitle OUTPOST_STOLEN = MakeLocGet("ui.tickers.category.outpost-stolen");

	public static readonly TickerTitle OUTPOST_AI_STOLEN = MakeLocGet("ui.tickers.category.outpost-ai-stolen");

	public static readonly TickerTitle TRIBUTE_PROBLEM = MakeLocGet("ui.tickers.category.tribute-problem");

	public static readonly TickerTitle TERR_EXPAND = MakeLocGet("ui.tickers.category.territory-expand");

	public static readonly TickerTitle TERR_CONTRACT = MakeLocGet("ui.tickers.category.territory-contract");

	public static readonly TickerTitle VEHICLE_NEW = MakeLocGet("ui.tickers.category.car");

	public static readonly TickerTitle VEHICLE_JUNK = MakeLocGet("ui.tickers.category.car");

	public static readonly TickerTitle VEHICLE_CLUNKER = MakeLocGet("ui.tickers.category.car");

	public static readonly TickerTitle DELIVERIES = MakeLocGet("ui.tickers.category.deliveries");

	public static readonly TickerTitle QUEST_UPDATE = MakeLocGet("ui.tickers.category.quests");

	public static readonly TickerTitle EVENT_UPDATE = MakeLocGet("ui.tickers.category.resevents");

	public static readonly TickerTitle NEW_PLAYER = MakeLocGet("ui.tickers.category.newplayer");

	public static readonly TickerTitle GANG_ATTACK = MakeLocGet("ui.tickers.category.gangattack");

	public static readonly TickerTitle GANG_REQUEST = MakeLocGet("ui.tickers.category.gangrequest");

	public static readonly TickerTitle GANG_WAR_START = MakeLocGet("ui.tickers.category.gangwar-start");

	public static readonly TickerTitle GANG_WAR_END = MakeLocGet("ui.tickers.category.gangwar-end");

	public static readonly TickerTitle GANG_AGREEMENT_START = MakeLocGet("ui.tickers.category.gangtruce-start");

	public static readonly TickerTitle GANG_TRUCE_END = MakeLocGet("ui.tickers.category.gangtruce-end");

	public static readonly TickerTitle FORCECLOSE_HUMAN = MakeLocGet("ui.tickers.category.forceclose-human");

	public static readonly TickerTitle FORCECLOSE_GANG = MakeLocGet("ui.tickers.category.forceclose-gang");

	public static readonly TickerTitle BUYOUT = MakeLocGet("ui.tickers.category.buyout");

	public static readonly TickerTitle LOOT_AVAILABLE = MakeLocGet("ui.tickers.category.loot-available");

	public static readonly TickerTitle LOOT_RETRACTED = MakeLocGet("ui.tickers.category.loot-retracted");

	public static readonly TickerTitle LOOT_READY = MakeLocGet("ui.tickers.category.loot-ready");

	public static readonly TickerTitle CASINO_AVAILABLE = MakeLocGet("ui.tickers.category.casino-available");

	public static readonly TickerTitle CASINO_NEEDS_MONEY = MakeLocGet("ui.tickers.category.casino-in-debt");

	public static readonly TickerTitle CASINO_RAIDED = MakeLocGet("ui.tickers.category.casino-raided");

	public static readonly TickerTitle CASINO_ATTACKED = MakeLocGet("ui.tickers.category.casino-attacked");

	public static readonly TickerTitle CASINO_UPDATE = MakeLocGet("ui.tickers.category.casino-update");

	public static readonly TickerTitle SCHEME_UPDATE = MakeLocGet("ui.tickers.category.scheme-update");

	public static readonly TickerTitle THRONE = MakeLocGet("ui.tickers.category.throne");

	public static readonly TickerTitle PROMOTE = MakeLocGet("ui.tickers.category.promote");

	public static readonly TickerTitle POLITICS = MakeLocGet("ui.tickers.category.politics");

	public static readonly TickerTitle LAW_ENACTED = MakeLocGet("ui.tickers.category.law-enacted");

	public static readonly TickerTitle LAW_REVOKED = MakeLocGet("ui.tickers.category.law-revoked");

	public string GetText()
	{
		if (key == null)
		{
			return raw;
		}
		return Loc.Get(key);
	}

	public static TickerTitle MakeRaw(string raw)
	{
		return new TickerTitle
		{
			raw = raw
		};
	}

	private static TickerTitle MakeLocGet(string key)
	{
		return new TickerTitle
		{
			key = key
		};
	}
}
