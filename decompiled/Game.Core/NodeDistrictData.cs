using Game.Services.Maps;

namespace Game.Core;

public struct NodeDistrictData
{
	public int index;

	public DistrictConfig GetDistrict()
	{
		return Game.ctx.session.mapconfig.map.GetDistrictByIndexOrNull(index);
	}

	public TagList GetDistrictTags()
	{
		return GetDistrict()?.tags;
	}
}
