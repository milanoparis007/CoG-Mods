using Game.Core;
using Game.Session.Data;

namespace Game.Services;

public sealed class LevelupChain
{
	public Label id;

	public Label hint;

	public string locname;

	public string locdesc;

	public string locicon;

	public int highest;

	public VisitRequirementList visreqs;

	public bool captain;

	public string GetName()
	{
		return Loc.Get(locname);
	}

	public string GetDesc()
	{
		return Loc.Get(locdesc);
	}

	public string GetIcon()
	{
		return Loc.Get(locicon);
	}

	public string Describe(int level)
	{
		return Loc.Get("levelup.asnumber", "name", GetName(), "icon", GetIcon(), "num", Loc.FormatNumber(level));
	}

	internal static bool Matcher(Label id, LevelupChain levelup)
	{
		return levelup.id == id;
	}
}
