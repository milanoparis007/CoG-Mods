using System.Collections;
using System.Collections.Generic;
using Game.Core;
using Game.Services.Maps;

namespace Game.Session.Setup;

internal class CreateDistricts
{
	private MapBoardConfig _config;

	internal IEnumerator Start()
	{
		_config = Game.ctx.board.MapConfig;
		if (_config.districts == null)
		{
			yield break;
		}
		for (int i = 0; i < _config.districts.Count; i++)
		{
			DistrictConfig districtConfig = _config.districts[i];
			if (districtConfig.tags == null || districtConfig.tags.Count == 0)
			{
				continue;
			}
			foreach (Node item in Game.ctx.board.nodes.GetAllNodesUnsafe())
			{
				if (IsNodeValid(item) && IsInsideDistrict(districtConfig, item))
				{
					AddDistrict(i, districtConfig, item);
				}
			}
		}
		yield return null;
	}

	private bool IsNodeValid(Node node)
	{
		if (node.IsValid)
		{
			return node.HasRoad;
		}
		return false;
	}

	private bool IsInsideDistrict(DistrictConfig dis, Node node)
	{
		return (node.pos - dis.start).Magnitude <= (float)dis.radius;
	}

	private void AddDistrict(int disindex, DistrictConfig _, Node node)
	{
		if (node.districts == null)
		{
			node.districts = new List<NodeDistrictData>();
		}
		node.districts.Add(new NodeDistrictData
		{
			index = disindex
		});
	}
}
