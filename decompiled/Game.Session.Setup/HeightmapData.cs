using Game.Core;
using UnityEngine;

namespace Game.Session.Setup;

public sealed class HeightmapData
{
	public Texture2D heightMap;

	private (float water, float mountain) GetTerrainHelper(WorldPos pos)
	{
		float num = 0f;
		float num2 = 0f;
		IntSize mapSize = Game.ctx.session.mapconfig.map.mapSize;
		for (int i = -2; i < 3; i += 2)
		{
			for (int j = -2; j < 3; j += 2)
			{
				float num3 = pos.x + (float)i;
				float num4 = pos.y + (float)j;
				float u = num3 / (float)mapSize.width;
				float v = num4 / (float)mapSize.height;
				Color pixelBilinear = heightMap.GetPixelBilinear(u, v);
				num += pixelBilinear.r;
				num2 += pixelBilinear.g;
			}
		}
		return (water: num, mountain: num2);
	}

	public TerrainType GetTerrainType(WorldPos pos)
	{
		TerrainType result = TerrainType.Ground;
		var (num, num2) = GetTerrainHelper(pos);
		if (num == 0f && num2 == 0f)
		{
			result = TerrainType.Ground;
		}
		else if (num > 0f)
		{
			result = TerrainType.Water;
		}
		else if (num2 > 0f)
		{
			result = TerrainType.Mountain;
		}
		return result;
	}

	private float GetMinMountainHeight(WorldPos pos)
	{
		float num = 10f;
		IntSize mapSize = Game.ctx.session.mapconfig.map.mapSize;
		for (int i = -2; i < 3; i += 2)
		{
			for (int j = -2; j < 3; j += 2)
			{
				float num2 = pos.x + (float)i;
				float num3 = pos.y + (float)j;
				float u = num2 / (float)mapSize.width;
				float v = num3 / (float)mapSize.height;
				Color pixelBilinear = heightMap.GetPixelBilinear(u, v);
				if (pixelBilinear.g < num)
				{
					num = pixelBilinear.g;
				}
			}
		}
		return num;
	}

	public float GetHeightAtPosition(WorldPos pos, bool normalized = false)
	{
		float num = GetMinMountainHeight(pos);
		if (!normalized)
		{
			num *= Game.ctx.session.mapconfig.terrain.GetTerrainScale(isMountain: true) / 2f;
		}
		return num;
	}
}
