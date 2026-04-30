using System.Collections;
using System.Collections.Generic;
using Game.Core;
using Game.Services.Maps;
using SomaSim.Util;
using UnityEngine;

namespace Game.Session.Setup;

internal sealed class CreateWater
{
	private List<WaterNode> _waterNodes = new List<WaterNode>();

	private List<TerrainBody> _waterBodies = new List<TerrainBody>();

	private List<WaterRegion> _waterRegions = new List<WaterRegion>();

	private SetupOrchestratorContext _ctx;

	private IRandom _rng;

	public CreateWater(SetupOrchestratorContext ctx)
	{
		_ctx = ctx;
		_rng = Game.ctx.scenario.MakeSeededRng<CreateWater>();
	}

	public IEnumerator Start()
	{
		foreach (WaterNodesConfig waternode in Game.ctx.session.mapconfig.terrain.waternodes)
		{
			GenerateWaterNode(waternode);
		}
		foreach (WaterNode waterNode in _waterNodes)
		{
			ConnectNodes(waterNode);
		}
		_ctx.waterRegionData = GetWaterRegionData();
		yield break;
	}

	private void GenerateWaterNode(WaterNodesConfig cfg)
	{
		WorldPos forceStart = cfg.forceStart;
		WaterNode waterNode = new WaterNode(cfg);
		_waterNodes.Add(waterNode);
		if (waterNode.body)
		{
			GameObject gameObject = Object.Instantiate(Resources.Load<GameObject>("Maps/TerrainBody"), forceStart.AsVector3XZ, Quaternion.identity);
			gameObject.name = "Water Body: " + cfg.id.String;
			gameObject.transform.localScale = new Vector3(cfg.width, 1f, cfg.width);
			TerrainBody component = gameObject.GetComponent<TerrainBody>();
			component.AllowBridges = waterNode.allowBridges;
			component.GenerateTerrainBodyVariant(0f, 0f, cfg.width, _rng, waterNode.deformers);
			_waterBodies.Add(component);
		}
	}

	private void ConnectNodes(WaterNode node)
	{
		foreach (Label child in node.connections)
		{
			WaterNode b = _waterNodes.Find((WaterNode test) => test.id == child);
			WaterRegion item = new WaterRegion(node, b);
			_waterRegions.Add(item);
		}
	}

	private WaterRegionData GetWaterRegionData()
	{
		return new WaterRegionData
		{
			waterRegions = _waterRegions,
			waterBodies = _waterBodies
		};
	}
}
