using System.Diagnostics;
using Game.Core;

namespace Game.Services;

[DebuggerDisplay("{DebugString}")]
public sealed class ResourceCategory
{
	public Label id;

	public string locicon;

	public string locname;

	public int sortorder;

	private string DebugString => id.String;

	public string GetIcon()
	{
		return Loc.Get(locicon);
	}

	public string GetName()
	{
		return Loc.Get(locname);
	}

	public override string ToString()
	{
		return DebugString;
	}
}
