using System.Collections.Generic;
using Game.Services.Maps;

namespace Game.Services;

public sealed class MapSettings
{
	public List<MapConfig> maps = new List<MapConfig>();

	public MapConfig FindBuiltInMapConfigByID(string id)
	{
		string text = id.ToLowerInvariant();
		foreach (MapConfig map in maps)
		{
			if (map.id == text)
			{
				return map;
			}
		}
		return null;
	}

	public MapConfig FindBuiltInMapConfigByCityType(string citytype)
	{
		string text = citytype.ToLowerInvariant();
		foreach (MapConfig map in maps)
		{
			if (map.citytype == text)
			{
				return map;
			}
		}
		return null;
	}
}
