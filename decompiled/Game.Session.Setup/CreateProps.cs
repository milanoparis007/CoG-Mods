using System.Collections;
using System.Collections.Generic;
using Game.Core;
using Game.Session.Entities;
using Game.Session.Heatmaps;
using SomaSim.Util;
using UnityEngine;

namespace Game.Session.Setup;

internal class CreateProps
{
	private const int MAX_PROPS_PER_FRAME = 100;

	private SetupOrchestratorContext _ctx;

	private Xorshift _rng;

	public CreateProps(SetupOrchestratorContext ctx)
	{
		_ctx = ctx;
		_rng = Game.ctx.scenario.MakeSeededRng<CreateProps>();
	}

	private IEnumerator SpawnDecosAlongTransit(List<RoadTileInfo> tileInfo, TransitFlags transitType)
	{
		HeatmapManager heatmaps = Game.ctx.heatmaps;
		Heatmap resmap = heatmaps.Find(HeatmapType.LotZone, TagConstants.TAG_RESIDENTIAL);
		Heatmap commap = heatmaps.Find(HeatmapType.LotZone, TagConstants.TAG_COMMERCIAL);
		Heatmap indmap = heatmaps.Find(HeatmapType.LotZone, TagConstants.TAG_INDUSTRIAL);
		int propsSpawned = 0;
		foreach (RoadTileInfo item in tileInfo)
		{
			WorldPos modelPosition = item.entity.components.model.GetModelPosition();
			float modelRotation = item.entity.components.model.GetModelRotation();
			HeatmapPos pos = resmap.WorldToHeatmap(modelPosition);
			float valueFast = resmap.GetValueFast(pos);
			float valueFast2 = commap.GetValueFast(pos);
			float valueFast3 = indmap.GetValueFast(pos);
			Label name = Label.NULL;
			if (valueFast >= valueFast2 && valueFast >= valueFast3)
			{
				name = GetLabelForProps(TagConstants.TAG_RESIDENTIAL, item.tileType, transitType);
			}
			else if (valueFast2 >= valueFast && valueFast2 >= valueFast3)
			{
				name = GetLabelForProps(TagConstants.TAG_COMMERCIAL, item.tileType, transitType);
			}
			else if (valueFast3 >= valueFast && valueFast3 >= valueFast2)
			{
				name = GetLabelForProps(TagConstants.TAG_INDUSTRIAL, item.tileType, transitType);
			}
			if (name.IsSet)
			{
				EntityConfig entityConfig = Game.ctx.entityman.FindTemplate(name);
				if (entityConfig != null)
				{
					GridTransform transform = new GridTransform(modelPosition, modelRotation);
					Game.ctx.board.CreateEntityAtStartup(entityConfig, transform);
				}
			}
			if (propsSpawned % 100 == 0)
			{
				yield return null;
			}
			propsSpawned++;
		}
	}

	private IEnumerator TrySpawningRailSetPieces()
	{
		Label HIGHEND_XL = (Label)"deco-railside-highend-XL";
		Label LOWEND_XL = (Label)"deco-railside-lowend-XL";
		List<Label> HIGHENDS = new List<Label>
		{
			(Label)"deco-railside-highend-L",
			(Label)"deco-railside-highend-M"
		};
		List<Label> LOWENDS = new List<Label>
		{
			(Label)"deco-railside-lowend-L",
			(Label)"deco-railside-lowend-M"
		};
		AppealHeatmap appealMap = Game.ctx.heatmaps.Find(HeatmapType.Appeal) as AppealHeatmap;
		List<Entity> placedSetPieces = new List<Entity>();
		foreach (NodeEdge railEdge in _ctx.transitTileData.railEdges)
		{
			Node node = Game.ctx.board.nodes.GetNode(railEdge.a);
			Node node2 = Game.ctx.board.nodes.GetNode(railEdge.b);
			bool flag = false;
			if ((node.HasRoad && node.HasRail) || (node2.HasRoad && node2.HasRail))
			{
				flag = true;
			}
			if (flag)
			{
				EntityConfig entityConfig = Game.ctx.entityman.FindTemplate(HIGHEND_XL);
				EntityConfig entityConfig2 = Game.ctx.entityman.FindTemplate(LOWEND_XL);
				for (int i = 2; i < railEdge.roadBeads.Count; i++)
				{
					RoadBead bead = railEdge.roadBeads[i];
					if (!placedSetPieces.TrueForAll((Entity e) => (e.data.board.worldpos - bead.pos).Magnitude > 15f))
					{
						continue;
					}
					int num = (DirectionUtil.IsHorizontal(railEdge.abDir) ? 90 : 0);
					float deg = bead.deg + (float)num;
					GridTransform gridTransform = new GridTransform(bead.pos, deg);
					EntityConfig entityConfig3 = ((appealMap.GetAppealLevel(bead.pos) == AppealLevel.Low) ? entityConfig2 : entityConfig);
					WorldSize lotsize = entityConfig3.board.lotsize;
					GridTransform tr = gridTransform;
					tr.pos += entityConfig3.board.collisionOffset.Rotate(deg);
					List<Entity> list = Game.ctx.board.DoesPointIntersectAnyEntityWithRule(lotsize, tr, (Entity e) => e.config.building != null);
					if (Game.ctx.board.DoesEntityIntersectAnyTerrain(lotsize, tr))
					{
						continue;
					}
					foreach (Entity item3 in list)
					{
						if (item3.config != null && !item3.config.board.ignoredBySetPieces)
						{
							Game.ctx.board.DestroyEntity(item3, shutdown: false);
						}
					}
					Entity item = Game.ctx.board.CreateEntityAtStartup(entityConfig3, gridTransform);
					placedSetPieces.Add(item);
					break;
				}
				yield return null;
				continue;
			}
			for (int num2 = 2; num2 < railEdge.roadBeads.Count; num2++)
			{
				RoadBead bead2 = railEdge.roadBeads[num2];
				if (placedSetPieces.TrueForAll((Entity e) => (e.data.board.worldpos - bead2.pos).Magnitude > 15f))
				{
					int num3 = (DirectionUtil.IsHorizontal(railEdge.abDir) ? 90 : 0);
					float deg2 = bead2.deg + (float)num3;
					GridTransform gridTransform2 = new GridTransform(bead2.pos, deg2);
					List<Label> list2 = ((appealMap.GetAppealLevel(bead2.pos) == AppealLevel.Low) ? LOWENDS : HIGHENDS);
					Label name = _rng.PickElement(list2);
					EntityConfig entityConfig4 = Game.ctx.entityman.FindTemplate(name);
					WorldSize lotsize2 = entityConfig4.board.lotsize;
					GridTransform tr2 = gridTransform2;
					tr2.pos += entityConfig4.board.collisionOffset.Rotate(deg2);
					List<Entity> list3 = Game.ctx.board.DoesPointIntersectAnyEntityWithRule(lotsize2, tr2, (Entity e) => e.config.building != null);
					if (!Game.ctx.board.DoesEntityIntersectAnyTerrain(lotsize2, tr2) && list3.Count == 0)
					{
						Entity item2 = Game.ctx.board.CreateEntityAtStartup(entityConfig4, gridTransform2);
						placedSetPieces.Add(item2);
						break;
					}
				}
			}
		}
	}

	internal IEnumerator Start()
	{
		if (!Game.serv.globals.settings.general.debug.skipDecos && _ctx.transitTileData != null)
		{
			yield return SpawnDecosAlongTransit(_ctx.transitTileData.roads, TransitFlags.Road);
			yield return SpawnDecosAlongTransit(_ctx.transitTileData.rails, TransitFlags.Rail);
			yield return TrySpawningRailSetPieces();
		}
	}

	private Label GetLabelForProps(Label lotLabel, RoadTileType tileType, TransitFlags transitType)
	{
		string text = string.Empty;
		switch (transitType)
		{
		case TransitFlags.Road:
			text += "road";
			if (lotLabel == TagConstants.TAG_RESIDENTIAL)
			{
				text += "-deco-resd";
			}
			else if (lotLabel == TagConstants.TAG_COMMERCIAL)
			{
				text += "-deco-comm";
			}
			else if (lotLabel == TagConstants.TAG_INDUSTRIAL)
			{
				text += "-deco-indst";
			}
			break;
		case TransitFlags.Rail:
			text += "rail-deco";
			break;
		}
		switch (tileType)
		{
		case RoadTileType.End:
			text += "-end";
			break;
		case RoadTileType.TwoStraight:
			text += "-mid";
			break;
		case RoadTileType.Corner:
			text += "-corner";
			break;
		case RoadTileType.ThreeWay:
			text += "-T";
			break;
		case RoadTileType.FourWay:
			text += "-intersection";
			break;
		case RoadTileType.Crossing:
			text += "-crossing";
			break;
		default:
		{
			Label label = lotLabel;
			Debug.LogError("Don't have a road tile type for spawning props: " + label.ToString() + " | " + tileType);
			break;
		}
		}
		Label result = Label.NULL;
		if (!string.IsNullOrEmpty(text))
		{
			result = (Label)text;
		}
		return result;
	}
}
