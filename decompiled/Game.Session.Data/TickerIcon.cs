using Game.Core;
using Game.Services;
using Game.Session.Player;

namespace Game.Session.Data;

public struct TickerIcon
{
	public string key;

	public string raw;

	public static readonly TickerIcon DEFAULT = MakeLocGet("ui.tickers.icon.default");

	public static readonly TickerIcon LEVELUP = MakeLocGet("ui.tickers.icon.levelup");

	public static readonly TickerIcon SKILLGAIN = MakeLocGet("ui.tickers.icon.skillgain");

	public static readonly TickerIcon SOCIAL = MakeLocGet("ui.tickers.icon.social");

	public static readonly TickerIcon CREWGAIN = MakeLocGet("ui.tickers.icon.crewgain");

	public static readonly TickerIcon SCOPEOUT = MakeLocGet("ui.tickers.icon.scopeout");

	public static readonly TickerIcon BIZ_UPDATE = MakeLocGet("ui.tickers.icon.bizupdate");

	public static readonly TickerIcon COP_UPDATE = MakeLocGet("ui.tickers.icon.copupdate");

	public static readonly TickerIcon COP_DONO_EXPIRED = MakeLocGet("ui.tickers.icon.dono-expired");

	public static readonly TickerIcon COP_DONO_ANGRY = MakeLocGet("ui.tickers.icon.cop-belligerent");

	public static readonly TickerIcon SAFEHOUSE = MakeLocGet("ui.tickers.icon.safehouse");

	public static readonly TickerIcon SALARY_PROBLEM = MakeLocGet("ui.tickers.icon.salary");

	public static readonly TickerIcon VEHICLE_NEW = MakeLocGet("ui.tickers.icon.car-new");

	public static readonly TickerIcon VEHICLE_JUNK = MakeLocGet("ui.tickers.icon.car-junk");

	public static readonly TickerIcon VEHICLE_CLUNKER = MakeLocGet("ui.tickers.icon.car-clunker");

	public static readonly TickerIcon DEMANDS_GENERIC = MakeLocGet("ui.tickers.icon.demands");

	public static readonly TickerIcon DEMANDS_PROTECTION = MakeLocGet("ui.tickers.icon.demands-protection");

	public static readonly TickerIcon DEMANDS_COMPLIANT = MakeLocGet("ui.tickers.icon.demands-compliant");

	public static readonly TickerIcon DEMANDS_DEFIANT = MakeLocGet("ui.tickers.icon.demands-defiant");

	public static readonly TickerIcon COMBAT_RESULTS = MakeLocGet("ui.tickers.icon.combat-results");

	public static readonly TickerIcon OUTPOST_START = MakeLocGet("ui.tickers.icon.outpost-start");

	public static readonly TickerIcon OUTPOST_URGENT = MakeLocGet("ui.tickers.icon.outpost-urgent");

	public static readonly TickerIcon OUTPOST_STARTED_EXPAND = MakeLocGet("ui.tickers.icon.outpost-exp");

	public static readonly TickerIcon OUTPOST_CAN_EXPAND = MakeLocGet("ui.tickers.icon.outpost-expyes");

	public static readonly TickerIcon OUTPOST_CANNOT_EXPAND = MakeLocGet("ui.tickers.icon.outpost-expno");

	public static readonly TickerIcon OUTPOST_STOLEN = MakeLocGet("ui.tickers.icon.outpost-stolen");

	public static readonly TickerIcon OUTPOST_AI_STOLEN = MakeLocGet("ui.tickers.icon.outpost-ai-stolen");

	public static readonly TickerIcon DELIVERIES = MakeLocGet("ui.tickers.icon.deliveries");

	public static readonly TickerIcon TRIBUTE_NOTICE = MakeLocGet("ui.tickers.icon.tribute-notice");

	public static readonly TickerIcon TRIBUTE_PROBLEM = MakeLocGet("ui.tickers.icon.tribute-problem");

	public static readonly TickerIcon TERR_EXPAND = MakeLocGet("ui.tickers.icon.territory-expand");

	public static readonly TickerIcon TERR_CONTRACT = MakeLocGet("ui.tickers.icon.territory-contract");

	public static readonly TickerIcon STALLED = MakeLocGet("ui.tickers.icon.stall");

	public static readonly TickerIcon BLDG_DAMAGE_TAKEN = MakeLocGet("ui.tickers.icon.building-damage-taken");

	public static readonly TickerIcon BLDG_DAMAGE_FIXED = MakeLocGet("ui.tickers.icon.building-damage-fixed");

	public static readonly TickerIcon BLDG_DAMAGE_DESTROYED = MakeLocGet("ui.tickers.icon.building-damage-destroyed");

	public static readonly TickerIcon QUEST_UPDATE = MakeLocGet("ui.tickers.icon.quests");

	public static readonly TickerIcon EVENT_UPDATE = MakeLocGet("ui.tickers.icon.resevents");

	public static readonly TickerIcon TRADE_BUY = MakeLocGet("ui.tickers.icon.buytrade");

	public static readonly TickerIcon TRADE_SELL = MakeLocGet("ui.tickers.icon.selltrade");

	public static readonly TickerIcon NEW_PLAYER = MakeLocGet("ui.tickers.icon.newplayer");

	public static readonly TickerIcon GANG_REQUEST = MakeLocGet("ui.tickers.icon.gangrequest");

	public static readonly TickerIcon GANG_ATTACK = MakeLocGet("ui.tickers.icon.gangattack");

	public static readonly TickerIcon GANG_WAR_START = MakeLocGet("ui.tickers.icon.gangwar-start");

	public static readonly TickerIcon GANG_WAR_END = MakeLocGet("ui.tickers.icon.gangwar-end");

	public static readonly TickerIcon GANG_AGREEMENT_START = MakeLocGet("ui.tickers.icon.gangtruce-start");

	public static readonly TickerIcon GANG_TRUCE_END = MakeLocGet("ui.tickers.icon.gangtruce-end");

	public static readonly TickerIcon FORCECLOSE_HUMAN = MakeLocGet("ui.tickers.icon.forceclose-human");

	public static readonly TickerIcon FORCECLOSE_GANG = MakeLocGet("ui.tickers.icon.forceclose-gang");

	public static readonly TickerIcon BUYOUT_HIREASCREW = MakeLocGet("ui.tickers.icon.buyout-hireascrew");

	public static readonly TickerIcon BUYOUT_PAYTOMOVE = MakeLocGet("ui.tickers.icon.buyout-paytomove");

	public static readonly TickerIcon LOOT_AVAILABLE = MakeLocGet("ui.tickers.icon.loot-available");

	public static readonly TickerIcon LOOT_RETRACTED = MakeLocGet("ui.tickers.icon.loot-retracted");

	public static readonly TickerIcon LOOT_READY = MakeLocGet("ui.tickers.icon.loot-ready");

	public static readonly TickerIcon BOTTLE_RETURN = MakeLocGet("ui.tickers.icon.deliveries.bottle-rack");

	public static readonly TickerIcon REPAIR_SUCCESS = MakeLocGet("ui.tickers.icon.deliveries.repair-success");

	public static readonly TickerIcon REPAIR_FAIL = MakeLocGet("ui.tickers.icon.deliveries.repair-fail");

	public static readonly TickerIcon WILL_STEAL = MakeLocGet("ui.tickers.icon.will-steal");

	public static readonly TickerIcon DEBTOR_NEW = MakeLocGet("ui.tickers.icon.debtor-new");

	public static readonly TickerIcon DEBTOR_FORGIVE = MakeLocGet("ui.tickers.icon.debtor-forgive");

	public static readonly TickerIcon DEBTOR_PAID = MakeLocGet("ui.tickers.icon.debtor-paid");

	public static readonly TickerIcon DEBTOR_ESCAPED = MakeLocGet("ui.tickers.icon.debtor-escape");

	public static readonly TickerIcon CASINO_AVAILABLE = MakeLocGet("ui.tickers.icon.casino-available");

	public static readonly TickerIcon CASINO_NEEDS_MONEY = MakeLocGet("ui.tickers.icon.casino-in-debt");

	public static readonly TickerIcon CASINO_AMENITY_DONE = MakeLocGet("ui.tickers.icon.casino-amenity-done");

	public static readonly TickerIcon GAMBLING_UPDATE = MakeLocGet("ui.tickers.icon.gambling-update");

	public static readonly TickerIcon SCHEME_AVAILABLE = MakeLocGet("ui.tickers.icon.scheme-available");

	public static readonly TickerIcon SCHEME_UPDATE = MakeLocGet("ui.tickers.icon.scheme-update");

	public static readonly TickerIcon THRONE = MakeLocGet("ui.tickers.icon.throne");

	public static readonly TickerIcon THRONE_UNLOCK = MakeLocGet("ui.tickers.icon.throne-unlock");

	public static readonly TickerIcon PROMOTE = MakeLocGet("ui.tickers.icon.promote");

	public static readonly TickerIcon POLITICS = MakeLocGet("ui.tickers.icon.politics");

	public static readonly TickerIcon LAW = MakeLocGet("ui.tickers.icon.law");

	public string GetIcon()
	{
		if (key == null)
		{
			return raw;
		}
		return Loc.Get(key);
	}

	public TickerIcon WrapInPlayerColor(PlayerID pid)
	{
		return MakeRaw(pid.FindPlayer().social.WrapInPlayerColor(GetIcon(), 0f));
	}

	public static TickerIcon MakeRaw(string raw)
	{
		return new TickerIcon
		{
			raw = raw
		};
	}

	private static TickerIcon MakeLocGet(string key)
	{
		return new TickerIcon
		{
			key = key
		};
	}
}
