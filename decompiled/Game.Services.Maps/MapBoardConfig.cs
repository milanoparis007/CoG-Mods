using System.Collections.Generic;
using Game.Core;
using SomaSim.Util;

namespace Game.Services.Maps;

public sealed class MapBoardConfig
{
	public IntSize mapSize;

	public IntSize heatmapSpacing = new IntSize(8, 8);

	public IntSize beadSpacing = new IntSize(1, 1);

	public List<Label> streetNames;

	public List<MapNodesConfig> nodes;

	public List<RailConfig> rails;

	public List<DistrictConfig> districts;

	public MapNodesConfig GetNodesConfigByID(Label id)
	{
		foreach (MapNodesConfig node in nodes)
		{
			if (node.id == id)
			{
				return node;
			}
		}
		return null;
	}

	public DistrictConfig GetFirstDistrictForTag(Label tag)
	{
		foreach (DistrictConfig district in districts)
		{
			if (district.tags.Contains(tag))
			{
				return district;
			}
		}
		return null;
	}

	public DistrictConfig GetDistrictByIndexOrNull(int index)
	{
		return districts.GetOrDefaultFast(index);
	}
}
