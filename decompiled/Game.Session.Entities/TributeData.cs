using System.Diagnostics;
using Game.Core;
using Game.Services;
using Game.Session.Player;

namespace Game.Session.Entities;

[DebuggerDisplay("{DebugString}")]
public class TributeData
{
	public PlayerFlags rejected = new PlayerFlags();

	public PlayerSingleFlag accepted = new PlayerSingleFlag();

	public bool humanasked;

	public Price amount;

	public OutpostID outpost;

	private string DebugString => ToString();

	public override string ToString()
	{
		return $"Tribute to {accepted.Get()}: {Loc.Price(amount)}";
	}
}
