using System.Collections.Generic;
using System.Diagnostics;
using Game.Core;
using Game.Services;

namespace Game.Session.Data;

[DebuggerDisplay("{DebugString}")]
public sealed class Trait
{
	public Label id;

	public List<Label> not;

	public List<Label> implies;

	public string locname;

	public string locdesc;

	public string locshort;

	public string locicon;

	public string locgambling;

	public string locpolitics;

	private string DebugString => id.String;

	public string GetLocName()
	{
		return Loc.Get(locname);
	}

	public string GetLocDesc()
	{
		return Loc.Get(locdesc);
	}

	public string GetLocShort()
	{
		return Loc.Get(locshort);
	}

	public string GetLocIcon()
	{
		return Loc.Get(locicon);
	}

	public string GetLocGambling()
	{
		if (locgambling != null)
		{
			return Loc.Get(locgambling);
		}
		return Loc.Get(locshort);
	}

	public string GetLocPolitics()
	{
		if (locpolitics != null)
		{
			return Loc.Get(locpolitics);
		}
		return "";
	}
}
