using System.Diagnostics;
using Game.Core;

namespace Game.Session.Data;

[DebuggerDisplay("{DebugString}")]
public sealed class AutomationStep
{
	public EntityID target;

	public AutoAction action;

	public MovedItems items;

	public bool enabled = true;

	public bool skipIfFullOnBuy = true;

	public bool skipIfEmptyOnSell = true;

	private string DebugString => $"{target}: {action}, {items}, en:{enabled}";
}
