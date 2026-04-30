using System.Diagnostics;
using Game.Core;
using SomaSim.Util;

namespace Game.Services;

[DebuggerDisplay("{DebugString}")]
public sealed class UnitDef
{
	public string locstring;

	public Volume volume;

	public Label unitid;

	private string DebugString => $"UNIT {unitid} @ {volume} each";

	public string GetQtyAndUnits(Fixnum qty)
	{
		return Loc.GetPluralized(locstring, qty, "qty", qty);
	}

	public string GetBareUnitNoun(Fixnum qty)
	{
		return Loc.GetPluralized(locstring, qty, "qty", "").Trim();
	}

	public string GetVolume()
	{
		return Loc.Volume(volume);
	}

	public override string ToString()
	{
		return DebugString;
	}
}
