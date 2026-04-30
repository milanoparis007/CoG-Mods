using System.Diagnostics;

namespace Game.Core;

[DebuggerDisplay("{DebugString}")]
public struct HeatmapPos
{
	public int x;

	public int y;

	private string DebugString => ToString();

	public HeatmapPos(int x, int y)
	{
		this.x = x;
		this.y = y;
	}

	public override string ToString()
	{
		return $"[HPOS {x},{y}]";
	}
}
