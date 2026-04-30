using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Game.Core;
using Game.Services.Input;
using Game.Session.Data;
using Game.Session.Player;
using Game.Session.Sim;
using SomaSim.Util;
using UnityEngine;

namespace Game.Services;

public static class Loc
{
	public const string UICHECK = "ui.check";

	public const string UICROSS = "ui.cross";

	public const string UIDOT = "ui.dot";

	public const string UICHECKLINE = "ui.checkline";

	public const string UICROSSLINE = "ui.crossline";

	public const string UIICONLINE = "ui.iconline";

	public const string UICROSSLINE_SUBITEM = "ui.crossline-subitem";

	public const string UICHECKLINE_SUBITEM = "ui.checkline-subitem";

	public const string DEFAULT_LANGUAGE_ID = "en";

	public const string KEY_LANGID = "__id";

	public const string KEY_LANGNAME = "__name";

	public const string LANG_NONLATIN_RU = "ru";

	public const string LANG_NONLATIN_ZH = "zh";

	private static StringBuilder _sb = new StringBuilder();

	private static ItemToNameCache<RelationshipType> _relTypeCache = new ItemToNameCache<RelationshipType>((RelationshipType type) => (int)type, (RelationshipType type) => Enum.GetName(typeof(RelationshipType), type).ToLowerInvariant());

	private static ItemToNameCache<MoneyReason> _moneyReasonCache = new ItemToNameCache<MoneyReason>((MoneyReason type) => (int)type, (MoneyReason type) => Enum.GetName(typeof(MoneyReason), type).ToLowerInvariant());

	private static ItemToNameCache<Demand.Type> _demandTypeCache = new ItemToNameCache<Demand.Type>((Demand.Type type) => (int)type, (Demand.Type type) => Enum.GetName(typeof(Demand.Type), type).ToLowerInvariant());

	private static ItemToNameCache<Demand.State> _demandStateCache = new ItemToNameCache<Demand.State>((Demand.State type) => (int)type, (Demand.State type) => Enum.GetName(typeof(Demand.State), type).ToLowerInvariant());

	private static ItemToNameCache<CrewStats> _crewStatCache = new ItemToNameCache<CrewStats>((CrewStats type) => (int)type, (CrewStats type) => Enum.GetName(typeof(CrewStats), type).ToLowerInvariant());

	private static ItemToNameCache<Election.ElectionStage> _electionStageCache = new ItemToNameCache<Election.ElectionStage>((Election.ElectionStage type) => (int)type, (Election.ElectionStage type) => Enum.GetName(typeof(Election.ElectionStage), type).ToLowerInvariant());

	private static ItemToNameCache<VehicleHealthType> _vehHealthCache = new ItemToNameCache<VehicleHealthType>((VehicleHealthType type) => (int)type, (VehicleHealthType type) => Enum.GetName(typeof(VehicleHealthType), type).ToLowerInvariant());

	public static LocalizationService instance { get; internal set; }

	private static NumberFormatInfo Formatter => Game.serv.loc.GetNumberFormatter();

	public static string Fake(string message)
	{
		Logger.LogAlways("!!! UNLOCALIZED STRING: " + message);
		return message;
	}

	public static string Check()
	{
		return instance.GetValue("ui.check", null, null);
	}

	public static string Cross()
	{
		return instance.GetValue("ui.cross", null, null);
	}

	public static string Dot()
	{
		return instance.GetValue("ui.dot", null, null);
	}

	public static string IconLine(bool valid, string message)
	{
		return instance.GetValue(valid ? "ui.checkline" : "ui.crossline", null, "text", message);
	}

	public static string IconLine(string icon, string message)
	{
		return instance.GetValue("ui.iconline", null, "icon", icon, "text", message);
	}

	public static string IconLineSubItem(bool valid, string message)
	{
		return instance.GetValue(valid ? "ui.checkline-subitem" : "ui.crossline-subitem", null, "text", message);
	}

	public static string Get(string key)
	{
		return instance.GetValue(key, null, null);
	}

	public static string Get(string key, IRandom rng)
	{
		return instance.GetValue(key, rng, null);
	}

	public static string Get(string key, params object[] replacements)
	{
		return instance.GetValue(key, null, replacements);
	}

	public static string Get(string key, IRandom rng, params object[] replacements)
	{
		return instance.GetValue(key, rng, replacements);
	}

	public static string Get(string key, LocReplacementContext ctx)
	{
		return instance.GetValue(key, ctx);
	}

	public static string GetGendered(string key, Gender g, params object[] replacements)
	{
		string text = key + RelSuffix(RelationshipType.None, g);
		string key2 = (instance.HasKey(text) ? text : key);
		return instance.GetValue(key2, null, replacements);
	}

	public static string GetPluralized(string key, Fixnum count, params object[] replacements)
	{
		return GetPluralized(key, (int)count, replacements);
	}

	public static string GetPluralized(string key, int count, params object[] replacements)
	{
		_sb.Append(key);
		_sb.Append(".");
		_sb.Append(count);
		string text = _sb.ToStringAndReset();
		string key2 = (instance.HasKey(text) ? text : key);
		return instance.GetValue(key2, null, replacements);
	}

	public static string GetOrDefault(string key, string @default = "")
	{
		if (key == null)
		{
			return @default;
		}
		return Get(key);
	}

	public static string Price(Price price, bool abs = false, int decimals = 0)
	{
		return Money(price.cash, abs, decimals);
	}

	public static string Money(Money money, int decimals = 0)
	{
		return Money(money.cash, showabs: false, decimals);
	}

	public static string Money(Fixnum amt, bool showabs = false, int decimals = 0)
	{
		string key = (showabs ? "ui.moneypos" : (amt.IsNegative ? "ui.moneyneg" : "ui.moneypos"));
		string text = ((decimals == 0) ? FormatNumber(amt.Abs) : FormatNumber(amt.Abs, decimals));
		return Get(key, "amt", text);
	}

	public static string Volume(Volume volume, bool header = false)
	{
		bool showFeetCubed = Game.serv.saveload.prefs.game.ShowFeetCubed;
		string key = (showFeetCubed ? "ui.volume.ft3" : "ui.volume.m3");
		Fixnum value = (showFeetCubed ? volume.cubicfeet : (volume.cubicfeet / 35));
		string text = Get(key, "num", FormatNumber(value));
		if (!header)
		{
			return text;
		}
		return Get("ui.volume", "vol", text);
	}

	public static string Length(Length length)
	{
		bool showFeetCubed = Game.serv.saveload.prefs.game.ShowFeetCubed;
		string key = (showFeetCubed ? "ui.length.ft" : "ui.length.m");
		float num = (float)length.inches * 2.54f;
		int num2 = (showFeetCubed ? Mathf.FloorToInt(length.inches / 12) : Mathf.FloorToInt(num / 100f));
		int num3 = (showFeetCubed ? Mathf.FloorToInt(length.inches % 12) : Mathf.FloorToInt(num % 100f));
		return Get(key, "big", num2, "small", num3);
	}

	public static string FormatDateShort(SimTime time)
	{
		return FormatDate(time, showyear: false);
	}

	public static string FormatDateLong(SimTime time)
	{
		return FormatDate(time, showyear: true);
	}

	public static string FormatDate(SimTime time, bool showyear)
	{
		if (time.IsMinDate)
		{
			return Get("datetime.min");
		}
		if (time.NeverHappens)
		{
			return Get("datetime.max");
		}
		string obj = (showyear ? "datetime.format.l." : "datetime.format.s.");
		int dateformat = (int)Game.serv.saveload.prefs.game.dateformat;
		string key = obj + dateformat;
		DateTime dateTime = time.ToDate();
		string text = Get("datetime.month." + dateTime.Month);
		string text2 = Get("datetime.day." + dateTime.Day);
		return Get(key, "year", dateTime.Year, "month", text, "dayth", text2, "day", dateTime.Day);
	}

	public static string FormatDateMonthShortened(SimTime time, bool showyear)
	{
		if (time.IsMinDate)
		{
			return Get("datetime.min");
		}
		if (time.NeverHappens)
		{
			return Get("datetime.max");
		}
		string obj = (showyear ? "datetime.format.l." : "datetime.format.s.");
		int dateformat = (int)Game.serv.saveload.prefs.game.dateformat;
		string key = obj + dateformat;
		DateTime dateTime = time.ToDate();
		string text = Get("datetime.month.short." + dateTime.Month);
		return Get(key, "year", dateTime.Year, "month", text, "dayth", dateTime.Day, "day", dateTime.Day);
	}

	public static string Percentage(float fraction)
	{
		int value = (int)Math.Round(fraction * 100f);
		return Get("ui.percentage", "x", FormatNumber(value));
	}

	public static string Percentage(Fixnum fraction)
	{
		int value = (int)(fraction * 100);
		return Get("ui.percentage", "x", FormatNumber(value));
	}

	public static string RelSuffix(RelationshipType type, Gender gender)
	{
		string text = ((gender == Gender.M) ? ".m" : ".f");
		if (type == RelationshipType.None)
		{
			return text;
		}
		string text2 = _relTypeCache.ToCachedString(type);
		return "." + text2 + text;
	}

	public static string Relationship(RelationshipType type, Gender gender)
	{
		return Get("reltype" + RelSuffix(type, gender));
	}

	public static string RelationshipToYou(RelationshipType type, Gender gender)
	{
		return Get("reltype.toyou" + RelSuffix(type, gender));
	}

	public static string GetMoneyReason(MoneyReason reason)
	{
		return Get("moneyreason." + _moneyReasonCache.ToCachedString(reason));
	}

	public static string GetDemandType(Demand.Type type)
	{
		return Get("demand.type." + _demandTypeCache.ToCachedString(type));
	}

	public static string GetDemandState(Demand.State state)
	{
		return Get("demand.state." + _demandStateCache.ToCachedString(state));
	}

	public static string GetCampaignStage(Election.ElectionStage stage)
	{
		return Get("ui.politics.election-stage." + _electionStageCache.ToCachedString(stage));
	}

	public static string GetCrewStatName(CrewStats stat)
	{
		return Get("ui.crewinfo.stat." + _crewStatCache.ToCachedString(stat), "value", "");
	}

	public static string GetCrewStatWithValueSpace(CrewStats stat, string value)
	{
		return Get("ui.crewinfo.stat." + _crewStatCache.ToCachedString(stat), "value", value);
	}

	public static string GetCrewStatWithValueColon(CrewStats stat, string value)
	{
		return Get("ui.crewinfo.stat." + _crewStatCache.ToCachedString(stat), "value", ": " + value);
	}

	public static string GetCrewStatUnit(CrewStats stat, int value)
	{
		switch (stat)
		{
		case CrewStats.BoozeSold:
		case CrewStats.BoozeProduced:
			return GetPluralized("ui.crewinfo.stat.unit.units", value, "value", value);
		case CrewStats.DriveDistance:
		case CrewStats.NodeScouted:
			return GetPluralized("ui.crewinfo.stat.unit.corners", value, "value", value);
		case CrewStats.PeepsKilled:
		case CrewStats.GamblersExtorted:
		case CrewStats.GamblersForgiven:
		case CrewStats.GamblersKicked:
		case CrewStats.PeepsThreatened:
			return GetPluralized("ui.crewinfo.stat.unit.people", value, "value", value);
		default:
			return GetPluralized("ui.crewinfo.stat.unit.none", value, "value", value);
		}
	}

	public static string GetWithPolUnit(string key, params object[] replacements)
	{
		List<object> list = replacements.ToList();
		list.Add("polUnit");
		list.Add(Get(Game.ctx.session.mapconfig.citypolunitkey));
		return Get(key, list.ToArray());
	}

	public static string GetAction(KeyAction action)
	{
		return Get("ui.options.keyaction." + action.ToString().ToLowerInvariant());
	}

	public static string GetKey(KeyCode key)
	{
		if (key == KeyCode.None)
		{
			return "";
		}
		return Get("ui.keycode." + key.ToString().ToLowerInvariant());
	}

	public static string GetVehicleCondition(VehicleHealthType type, bool shortinfo)
	{
		return Get((shortinfo ? "ui.vehicle-cond." : "ui.vehicle-cexp.") + _vehHealthCache.ToCachedString(type));
	}

	public static string FormatNumber(int value)
	{
		return value.ToString("d", Formatter);
	}

	public static string FormatNumber(Fixnum value)
	{
		if (value == (int)value)
		{
			return FormatNumber((int)value);
		}
		return ((float)value).ToString("n", Formatter);
	}

	public static string FormatNumber(Fixnum value, int decimals)
	{
		return ((float)value).ToString($"n{decimals}", Formatter);
	}

	public static string FormatNumberPlusMinus(Fixnum value, int? decimals = null)
	{
		string text = (decimals.HasValue ? FormatNumber(value.Abs, decimals.Value) : FormatNumber(value.Abs));
		return Get((value >= 0) ? "ui.numpos" : "ui.numneg", "num", text);
	}

	public static string PercentagePlusMinus(Fixnum fraction)
	{
		string text = Percentage(fraction.Abs);
		return Get((fraction >= 0) ? "ui.numpos" : "ui.numneg", "num", text);
	}

	public static string LanguageName(string id)
	{
		return Get("$$" + id);
	}
}
