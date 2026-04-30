using System.Collections.Generic;
using Game.Core;

namespace Game.Session.Setup;

public sealed class WaterRegionData
{
	public List<WaterRegion> waterRegions = new List<WaterRegion>();

	public List<TerrainBody> waterBodies = new List<TerrainBody>();

	public bool IsPointInWaterDetailed(WorldPos pos, float cautionRadius, ref List<WaterRegion> hitRegions, ref List<TerrainBody> hitBodies)
	{
		hitRegions.Clear();
		hitBodies.Clear();
		foreach (WaterRegion waterRegion in waterRegions)
		{
			if (waterRegion.IsPointWithin(pos))
			{
				hitRegions.Add(waterRegion);
			}
		}
		foreach (TerrainBody waterBody in waterBodies)
		{
			if (waterBody.IsPointWithinBodyCautious(pos, cautionRadius))
			{
				hitBodies.Add(waterBody);
			}
		}
		if (hitRegions.Count <= 0)
		{
			return hitBodies.Count > 0;
		}
		return true;
	}

	public bool IsPointInWater(WorldPos pos, out bool allowBridges)
	{
		allowBridges = true;
		foreach (WaterRegion waterRegion in waterRegions)
		{
			if (waterRegion.IsPointWithin(pos))
			{
				allowBridges = waterRegion.AllowBridges;
				return true;
			}
		}
		foreach (TerrainBody waterBody in waterBodies)
		{
			if (waterBody.IsPointWithinBody(pos, out var _))
			{
				allowBridges = waterBody.AllowBridges;
				return true;
			}
		}
		return false;
	}

	public bool IsPointInWater(WorldPos pos)
	{
		bool allowBridges;
		return IsPointInWater(pos, out allowBridges);
	}

	public bool IsRectInWater(WorldRect rect)
	{
		float num = -1f;
		for (float num2 = rect.height + 2f; num <= num2; num += 1f)
		{
			float num3 = -1f;
			for (float num4 = rect.width + 2f; num3 <= num4; num3 += 1f)
			{
				WorldPos pos = new WorldPos(rect.x + num3, rect.y + num);
				if (IsPointInWater(pos))
				{
					return true;
				}
			}
		}
		return false;
	}
}
