using System.Collections.Generic;
using Game.Core;

namespace Game.Session.Setup;

public class MountainRegionData
{
	public List<TerrainBody> _mountainBodies;

	public bool IsPointOnMountain(WorldPos pos)
	{
		foreach (TerrainBody mountainBody in _mountainBodies)
		{
			if (mountainBody.IsPointWithinBody(pos))
			{
				return true;
			}
		}
		return false;
	}

	public bool IsRectOnMountain(WorldRect rect)
	{
		float num = -1f;
		for (float num2 = rect.height + 2f; num <= num2; num += 1f)
		{
			float num3 = -1f;
			for (float num4 = rect.width + 2f; num3 <= num4; num3 += 1f)
			{
				WorldPos pos = new WorldPos(rect.x + num3, rect.y + num);
				if (IsPointOnMountain(pos))
				{
					return true;
				}
			}
		}
		return false;
	}
}
