using System.Collections.Generic;
using System.Diagnostics;
using Game.Core;

namespace Game.Services;

public sealed class SpecialGoonConfig
{
	public Label id;

	public List<GoonLootTableEntry> loottable;

	public string locintro;

	public string loclootoffer;

	public string loclootready;

	[Conditional("UNITY_EDITOR")]
	public void ValidateLootTable()
	{
		if (loottable == null)
		{
			return;
		}
		foreach (GoonLootTableEntry item in loottable)
		{
			_ = item;
		}
	}
}
