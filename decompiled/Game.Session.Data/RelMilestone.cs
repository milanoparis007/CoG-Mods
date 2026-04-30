using System.Diagnostics;

namespace Game.Session.Data;

[DebuggerDisplay("{DebugString}")]
public sealed class RelMilestone
{
	public int spent;

	public int avail;

	public int GrantedTotal => spent + avail;

	private string DebugString => ToString();

	public override string ToString()
	{
		return $"Total tix: {GrantedTotal} ({avail} available, {spent} spent)";
	}
}
