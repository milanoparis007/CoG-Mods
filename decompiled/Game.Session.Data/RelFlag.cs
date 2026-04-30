using System.Diagnostics;
using Game.Core;

namespace Game.Session.Data;

[DebuggerDisplay("{DebugString}")]
public struct RelFlag
{
	public Label id;

	public SimTime expires;

	private string DebugString => $"{id}, exp: {expires}";

	public RelFlag(Label id, SimTime? expires = null)
	{
		this = default(RelFlag);
		this.id = id;
		this.expires = expires ?? SimTime.MAX_DATE;
	}

	public bool IsExpired(SimTime now)
	{
		return now.days > expires.days;
	}

	public override string ToString()
	{
		return DebugString;
	}
}
