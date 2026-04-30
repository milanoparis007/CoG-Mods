using System.Collections.Generic;
using System.Diagnostics;
using Game.Core;
using Game.Services;

namespace Game.Session.Entities;

[DebuggerDisplay("{DebugString}")]
public sealed class PersonData : BaseData
{
	public string first;

	public string last;

	public string nickname;

	public string title;

	public Skin s;

	public Gender g;

	public Label eth;

	public SimTime born = SimTime.MAX_DATE;

	public SimTime died = SimTime.MAX_DATE;

	public int famId;

	public PortraitInfo portrait;

	public TagList traitIds = new TagList();

	private string _fullname;

	private string _shortname;

	public EntityID business = 0uL;

	public EntityID resassigned = EntityID.INVALID;

	public SimTime futuremarriage = SimTime.MAX_DATE;

	public SimTime futuredeath = SimTime.MAX_DATE;

	public List<SimTime> futurekids = new List<SimTime>();

	public SimTime futurefriends = SimTime.MAX_DATE;

	public string FirstName => first;

	public string LastName => last;

	public string FullName => _fullname ?? (_fullname = MakeFullName(first, last, nickname, title));

	public string ShortName => _shortname ?? (_shortname = MakeShortName(first, last, nickname, title));

	public string Initials => first.Substring(0, 1) + last.Substring(0, 1);

	public Gender Gender => g;

	public Skin Skin => s;

	public Label Ethnicity => eth;

	public bool IsAlive => died.NeverHappens;

	public bool IsEmployed => business.IsValid;

	public bool HasResAssigned => resassigned.IsValid;

	private string DebugString => $"{FullName} ({eth})";

	public EthnicityDef GetEthDef()
	{
		return Game.serv.globals.settings.ethnicities.FindEthnicityDef(eth);
	}

	public SimTimeSpan GetAge(SimTime onThisDate)
	{
		return new SimTimeSpan(onThisDate.days - born.days);
	}

	public void SetAge(float yearsOld, SimTime onThisDate)
	{
		SimTimeSpan delta = SimTimeSpan.FromYears(0f - yearsOld, 0f);
		SimTime simTime = onThisDate.Increment(delta);
		born = simTime;
	}

	public void SetNickname(string nickname)
	{
		this.nickname = nickname;
		_shortname = (_fullname = null);
	}

	public void SetTitle(string title)
	{
		this.title = title;
		_shortname = (_fullname = null);
	}

	public string InfoString()
	{
		string text = (IsAlive ? "" : "+");
		return Loc.Get("ui.personinfo.infostring", "name", FullName, "ifDead", text, "eth", eth, "born", born.YearsInt);
	}

	public static string MakeFullName(string first, string last, string nickname, string title)
	{
		return MakeName((nickname != null) ? "ui.name.nicklong" : "ui.name.simple", first, last, nickname, title);
	}

	public static string MakeShortName(string first, string last, string nickname, string title)
	{
		return MakeName((nickname != null) ? "ui.name.nickshort" : "ui.name.simple", first, last, nickname, title);
	}

	private static string MakeName(string key, string first, string last, string nickname, string title)
	{
		return Loc.Get(key, "first", first, "last", last, "nickname", nickname ?? "", "title", title ?? "").Trim();
	}
}
