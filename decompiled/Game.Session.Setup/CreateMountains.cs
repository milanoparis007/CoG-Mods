using System.Collections;
using System.Collections.Generic;
using Game.Core;
using Game.Services.Maps;
using SomaSim.Util;
using UnityEngine;

namespace Game.Session.Setup;

internal sealed class CreateMountains
{
	private MountainRegionData _mtnRegionData;

	private SetupOrchestratorContext _ctx;

	private IRandom _rng;

	public CreateMountains(SetupOrchestratorContext ctx)
	{
		_ctx = ctx;
		_rng = Game.ctx.scenario.MakeSeededRng<CreateMountains>();
	}

	public IEnumerator Start()
	{
		_mtnRegionData = new MountainRegionData();
		_mtnRegionData._mountainBodies = new List<TerrainBody>();
		foreach (MountainNodesConfig mountainnode in Game.ctx.session.mapconfig.terrain.mountainnodes)
		{
			WorldPos forceStart = mountainnode.forceStart;
			GameObject gameObject = Object.Instantiate(Resources.Load<GameObject>("Maps/TerrainBody"), forceStart.AsVector3XZ, Quaternion.identity);
			gameObject.name = "Mountain Body: " + mountainnode.id.String;
			gameObject.transform.localScale = new Vector3(mountainnode.size, 1f, mountainnode.size);
			TerrainBody component = gameObject.GetComponent<TerrainBody>();
			component.GenerateTerrainBodyVariant(0f, Game.ctx.session.mapconfig.terrain.GetTerrainScale(isMountain: true), mountainnode.size, _rng, new List<WorldPos>());
			component.AllowBridges = false;
			_mtnRegionData._mountainBodies.Add(component);
		}
		_ctx.mountainRegionData = _mtnRegionData;
		yield break;
	}
}
