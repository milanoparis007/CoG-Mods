using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Session.Entities;
using SomaSim.Util;
using UnityEngine;

namespace Game.Session.Setup;

internal sealed class CreateWaterDecos
{
	private const bool DEBUG = false;

	private WaterDecoTemplate _templateRes;

	private WaterDecoTemplate _templateComInd;

	private SetupOrchestratorContext _ctx;

	private Xorshift _rng;

	private List<EntityConfig> _decosRes;

	private List<EntityConfig> _decosInd;

	private static readonly List<string> DECOS_RES = new List<string> { "river-inwater-highend-xl", "river-inwater-highend-l", "river-inwater-highend-m" };

	private static readonly List<string> DECOS_IND = new List<string> { "river-inwater-lowend-xl", "river-inwater-lowend-l", "river-inwater-lowend-m" };

	private static readonly string DECO_RES_WATERSIDE = "river-waterside-highend-xl";

	private static readonly string DECO_IND_WATERSIDE = "river-waterside-lowend-xl";

	public CreateWaterDecos(SetupOrchestratorContext ctx)
	{
		_ctx = ctx;
		_rng = Game.ctx.scenario.MakeSeededRng<CreateWaterDecos>();
		_decosRes = DECOS_RES.Select((string name) => Game.ctx.entityman.FindTemplate((Label)name)).WhereNotNull().ToList();
		_decosInd = DECOS_IND.Select((string name) => Game.ctx.entityman.FindTemplate((Label)name)).WhereNotNull().ToList();
	}

	public IEnumerator Start()
	{
		if (!Game.serv.globals.settings.general.debug.skipDecos)
		{
			yield return DressWaterEdges();
		}
	}

	private IEnumerator DressWaterEdges()
	{
		_templateRes = new WaterDecoTemplate
		{
			leftDock = Game.ctx.entityman.FindTemplate((Label)"river-rock-endleft"),
			midDock = Game.ctx.entityman.FindTemplate((Label)"river-rock-mid"),
			rightDock = Game.ctx.entityman.FindTemplate((Label)"river-rock-endright")
		};
		_templateComInd = new WaterDecoTemplate
		{
			leftDock = Game.ctx.entityman.FindTemplate((Label)"river-dock-endleft"),
			midDock = Game.ctx.entityman.FindTemplate((Label)"river-dock-mid"),
			rightDock = Game.ctx.entityman.FindTemplate((Label)"river-dock-endright")
		};
		Transform transform = new GameObject("Water Deco Root").transform;
		for (int i = 0; i < _ctx.waterRegionData.waterRegions.Count; i++)
		{
			WaterRegion waterRegion = _ctx.waterRegionData.waterRegions[i];
			float num = waterRegion.length - 8f;
			new GameObject($"Water {i} Length {num}").transform.SetParent(transform);
			PlaceLiningsAlongRiver(waterRegion, waterRegion.leftCenter, waterRegion.left.normal, num);
			PlaceLiningsAlongRiver(waterRegion, waterRegion.rightCenter, waterRegion.right.normal, num);
		}
		yield return null;
	}

	private void PlaceLiningsAlongRiver(WaterRegion region, Vector3 center, Vector3 normal, float length)
	{
		Heatmap resmap = Game.ctx.heatmaps.Find(HeatmapType.LotZone, TagConstants.TAG_RESIDENTIAL);
		List<WaterBead> beads = CollectWaterBeads(region, center, normal, length, resmap);
		PlaceDocks(beads);
		PlaceDockDecos(beads, normal);
	}

	private void PlaceDockDecos(List<WaterBead> beads, Vector3 normal)
	{
		EntityConfig entityConfig = Game.ctx.entityman.FindTemplate((Label)DECO_RES_WATERSIDE);
		EntityConfig entityConfig2 = Game.ctx.entityman.FindTemplate((Label)DECO_IND_WATERSIDE);
		int num = 0;
		foreach (WaterBead bead in beads)
		{
			bool flag = num-- <= 0 && _rng.CheckProbability(0.75f);
			List<EntityConfig> list = (bead.IsResidential ? _decosRes : (bead.IsIndustrial ? _decosInd : null));
			if (flag && list != null)
			{
				EntityConfig template = _rng.PickElement(list);
				num = Mathf.CeilToInt(Game.ctx.board.CreateEntityAtStartup(template, bead.pos, bead.rot).config.board.lotsize.width);
			}
			else if (!flag && list != null && _rng.CheckProbability(0.6f))
			{
				EntityConfig entityConfig3 = (bead.IsResidential ? entityConfig : entityConfig2);
				WorldSize lotsize = entityConfig3.board.lotsize;
				WorldPos pos = bead.pos + new WorldPos(normal.normalized * -1f * (lotsize.height + 0.5f));
				if (!Game.ctx.board.DoesEntityIntersectAnyEntity(lotsize, new GridTransform(pos, bead.rot)))
				{
					Game.ctx.board.CreateEntityAtStartup(entityConfig3, pos, bead.rot);
				}
			}
		}
	}

	private void PlaceDocks(List<WaterBead> beads)
	{
		EntityConfig entityConfig = Game.ctx.entityman.FindTemplate((Label)"river-dock-blendleft");
		EntityConfig entityConfig2 = Game.ctx.entityman.FindTemplate((Label)"river-dock-blendright");
		WaterBead waterBead = null;
		foreach (WaterBead bead in beads)
		{
			EntityConfig entityConfig3 = null;
			switch (bead.decoType)
			{
			case WaterDecoType.Transition:
				if (waterBead != null)
				{
					entityConfig3 = (waterBead.IsResidential ? entityConfig : entityConfig2);
				}
				break;
			case WaterDecoType.ResStart:
				entityConfig3 = _templateRes.leftDock;
				break;
			case WaterDecoType.ResMid:
				entityConfig3 = _templateRes.midDock;
				break;
			case WaterDecoType.ResEnd:
				entityConfig3 = _templateRes.rightDock;
				break;
			case WaterDecoType.IndStart:
				entityConfig3 = _templateComInd.leftDock;
				break;
			case WaterDecoType.IndMid:
				entityConfig3 = _templateComInd.midDock;
				break;
			case WaterDecoType.IndEnd:
				entityConfig3 = _templateComInd.rightDock;
				break;
			}
			if (entityConfig3 != null)
			{
				Game.ctx.board.CreateEntityAtStartup(entityConfig3, bead.pos, bead.rot);
			}
			waterBead = bead;
		}
	}

	private List<WaterBead> CollectWaterBeads(WaterRegion region, Vector3 center, Vector3 normal, float length, Heatmap resmap)
	{
		Quaternion quaternion = Quaternion.LookRotation(normal, Vector3.up);
		Vector3 vector = Quaternion.Euler(0f, 90f, 0f) * normal;
		Vector3 vector2 = center + vector * length / 2f;
		float y = (quaternion * Quaternion.Euler(0f, 180f, 0f)).eulerAngles.y;
		List<WaterBead> list = new List<WaterBead>();
		int currentLengthCount = 0;
		WaterDecoType prev = WaterDecoType.None;
		for (int i = 0; (float)i < length; i++)
		{
			Vector3 pos = vector2 - vector * i;
			Vector3 pos2 = vector2 - vector * (i + 1);
			WorldPos worldPos = new WorldPos(pos);
			WorldPos pos3 = new WorldPos(pos2);
			bool flag = (float)i > length - 3f;
			List<WaterRegion> hitRegions = new List<WaterRegion>();
			List<TerrainBody> hitBodies = new List<TerrainBody>();
			if (!flag)
			{
				if (_ctx.waterRegionData.IsPointInWaterDetailed(worldPos, 2f, ref hitRegions, ref hitBodies))
				{
					hitRegions.Remove(region);
					if (hitRegions.Count > 0 || hitBodies.Count > 0)
					{
						flag = true;
					}
				}
				if (!flag && _ctx.waterRegionData.IsPointInWaterDetailed(pos3, 2f, ref hitRegions, ref hitBodies))
				{
					hitRegions.Remove(region);
					if (hitRegions.Count > 0 || hitBodies.Count > 0)
					{
						flag = true;
					}
				}
			}
			HeatmapPos pos4 = resmap.WorldToHeatmap(worldPos);
			bool nextIsRes = resmap.GetValueSafe(pos4) > 0.4f;
			WaterDecoType nextWaterDecoType = GetNextWaterDecoType(prev, nextIsRes, flag, _rng, ref currentLengthCount);
			WaterBead item = new WaterBead
			{
				pos = worldPos,
				rot = y,
				decoType = nextWaterDecoType
			};
			list.Add(item);
			prev = nextWaterDecoType;
		}
		return list;
	}

	private WaterDecoType GetNextWaterDecoType(WaterDecoType prev, bool nextIsRes, bool forceEnd, Xorshift rng, ref int currentLengthCount)
	{
		WaterDecoType waterDecoType = WaterDecoType.None;
		switch (prev)
		{
		case WaterDecoType.None:
			if (rng.GenerateFloat() < 0.5f || currentLengthCount >= 5)
			{
				waterDecoType = (nextIsRes ? WaterDecoType.ResStart : WaterDecoType.IndStart);
				currentLengthCount = 0;
			}
			else
			{
				waterDecoType = WaterDecoType.None;
				currentLengthCount++;
			}
			if (forceEnd)
			{
				waterDecoType = WaterDecoType.None;
			}
			break;
		case WaterDecoType.Transition:
			waterDecoType = (nextIsRes ? WaterDecoType.ResMid : WaterDecoType.IndMid);
			break;
		case WaterDecoType.ResStart:
			waterDecoType = WaterDecoType.ResMid;
			break;
		case WaterDecoType.ResMid:
			if (!nextIsRes)
			{
				waterDecoType = WaterDecoType.Transition;
			}
			else if ((rng.GenerateFloat() > 0.05f && currentLengthCount < 25) || currentLengthCount < 5)
			{
				waterDecoType = WaterDecoType.ResMid;
				currentLengthCount++;
			}
			else
			{
				waterDecoType = WaterDecoType.ResEnd;
			}
			break;
		case WaterDecoType.ResEnd:
			waterDecoType = WaterDecoType.None;
			currentLengthCount = 0;
			break;
		case WaterDecoType.IndStart:
			waterDecoType = WaterDecoType.IndMid;
			break;
		case WaterDecoType.IndMid:
			if (nextIsRes)
			{
				waterDecoType = WaterDecoType.Transition;
			}
			else if ((rng.GenerateFloat() > 0.05f && currentLengthCount < 50) || currentLengthCount < 5)
			{
				waterDecoType = WaterDecoType.IndMid;
				currentLengthCount++;
			}
			else
			{
				waterDecoType = WaterDecoType.IndEnd;
			}
			break;
		case WaterDecoType.IndEnd:
			waterDecoType = WaterDecoType.None;
			currentLengthCount = 0;
			break;
		}
		if (forceEnd)
		{
			switch (waterDecoType)
			{
			case WaterDecoType.IndStart:
			case WaterDecoType.IndMid:
				waterDecoType = WaterDecoType.IndEnd;
				break;
			case WaterDecoType.ResStart:
			case WaterDecoType.ResMid:
				waterDecoType = WaterDecoType.ResEnd;
				break;
			case WaterDecoType.Transition:
				waterDecoType = (nextIsRes ? WaterDecoType.IndEnd : WaterDecoType.ResEnd);
				break;
			}
		}
		return waterDecoType;
	}
}
