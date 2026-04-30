using System.Collections.Generic;
using Game.Core;

namespace Game.Session.Heatmaps;

public class HeatmapTypeEqualityComparer : IEqualityComparer<HeatmapType>
{
	public bool Equals(HeatmapType x, HeatmapType y)
	{
		return x == y;
	}

	public int GetHashCode(HeatmapType h)
	{
		return (int)h;
	}
}
