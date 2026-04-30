using Game.Core;
using Game.Session.Setup;

namespace Game.Session.Board;

public sealed class TerrainGenData
{
	public WaterRegionData waterData;

	public MountainRegionData mountainData;

	public HeightmapData heightmapData;

	public TerrainGenData(WaterRegionData waterData, MountainRegionData mountainData, HeightmapData heightmapData)
	{
		this.waterData = waterData;
		this.mountainData = mountainData;
		this.heightmapData = heightmapData;
	}

	internal bool CheckIntersection(WorldRect terrRect)
	{
		if (!mountainData.IsRectOnMountain(terrRect))
		{
			return waterData.IsRectInWater(terrRect);
		}
		return true;
	}
}
