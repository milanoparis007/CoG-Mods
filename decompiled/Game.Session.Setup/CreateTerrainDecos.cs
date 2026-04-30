using System.Collections;
using System.Collections.Generic;
using Game.Core;
using Game.Session.Board;
using Game.Session.Entities;
using Game.Session.Heatmaps;
using SomaSim.Util;
using UnityEngine;

namespace Game.Session.Setup;

internal sealed class CreateTerrainDecos
{
	private enum DataState
	{
		Open,
		InQueue,
		Complete
	}

	private class TerrDecoGroup
	{
		public List<TerrDecoData> items;

		public float resScore;

		public float comScore;

		public float indScore;

		public Vector2 min;

		public Vector2 max;

		public ZoneType GetZoneType()
		{
			if (resScore >= comScore)
			{
				if (resScore >= indScore)
				{
					return ZoneType.Res;
				}
				if (!(comScore > indScore))
				{
					return ZoneType.Ind;
				}
				return ZoneType.Com;
			}
			if (comScore > indScore)
			{
				return ZoneType.Com;
			}
			return ZoneType.Ind;
		}
	}

	private class TerrDecoData
	{
		public Vector2Int pos;

		public bool hit;

		public int id = -1;

		public bool processed;

		public int breadth;
	}

	private const bool DEBUG = false;

	private Vector2Int RESOLUTION = new Vector2Int(512, 512);

	private int MAX_DECOS_PER_FRAME = 500;

	private Color[] _colorBuffer;

	private Xorshift _rng;

	private Label TMPL_OUTLIER_CORNER = (Label)"deco-outlier-corner-M";

	private SetupOrchestratorContext _ctx;

	private const int POS_CACHE_LENGTH = 4;

	private Vector2Int[] POS_CACHE = new Vector2Int[4];

	public CreateTerrainDecos(SetupOrchestratorContext ctx)
	{
		_ctx = ctx;
		_rng = Game.ctx.scenario.MakeSeededRng<CreateTerrainDecos>();
	}

	private bool IsPositionValid(Vector2Int pos)
	{
		if (pos.x >= 0 && pos.x < RESOLUTION.x && pos.y >= 0)
		{
			return pos.y < RESOLUTION.y;
		}
		return false;
	}

	public IEnumerator Start()
	{
		if (!Game.serv.globals.settings.general.debug.skipDecos)
		{
			yield return FillInEmptySpace();
			yield return DressOutlierCorners();
		}
	}

	private IEnumerator DressOutlierCorners()
	{
		foreach (NodeEdge specialEdge in _ctx.specialEdgeData.specialEdges)
		{
			if (!specialEdge.transitTilesCreated)
			{
				continue;
			}
			Node node = Game.ctx.board.nodes.GetNode(specialEdge.a);
			Vector3 asVector3XZ = (Game.ctx.board.nodes.GetNode(specialEdge.b).pos - node.pos).Normalized.AsVector3XZ;
			Vector3 vector = Vector3.Cross(Vector3.up, asVector3XZ);
			Vector3 vector2 = Vector3.Cross(Vector3.down, asVector3XZ);
			bool flag = false;
			bool flag2 = false;
			foreach (LotBead lotBead in specialEdge.lotBeads)
			{
				Vector3 pos = lotBead.pos.AsVector3XZ + vector * 1.5f;
				Vector3 pos2 = lotBead.pos.AsVector3XZ + vector2 * 1.5f;
				GridTransform gridTransform = new GridTransform(new WorldPos(pos), _rng.GenerateFloat() * 360f);
				GridTransform gridTransform2 = new GridTransform(new WorldPos(pos2), _rng.GenerateFloat() * 360f);
				bool num = !Game.ctx.board.DoesEntityIntersectAnyEntity(new WorldSize(0.5f, 0.5f), gridTransform);
				bool flag3 = !Game.ctx.board.DoesEntityIntersectAnyEntity(new WorldSize(0.5f, 0.5f), gridTransform2);
				if (num)
				{
					if (!flag)
					{
						Game.ctx.board.CreateEntity(TMPL_OUTLIER_CORNER, gridTransform);
						flag = true;
					}
					else
					{
						flag = false;
					}
				}
				else
				{
					flag = false;
				}
				if (flag3)
				{
					if (!flag2)
					{
						Game.ctx.board.CreateEntity(TMPL_OUTLIER_CORNER, gridTransform2);
						flag2 = true;
					}
					else
					{
						flag2 = false;
					}
				}
				else
				{
					flag2 = false;
				}
			}
		}
		yield return null;
	}

	private IEnumerator FillInEmptySpace()
	{
		IntSize mapSize = Game.ctx.session.mapconfig.map.mapSize;
		_colorBuffer = new Color[RESOLUTION.x * RESOLUTION.y];
		Texture2D tex = new Texture2D(RESOLUTION.x, RESOLUTION.y);
		BoardManager board = Game.ctx.board;
		Vector2 increment = new Vector2((float)mapSize.width / (float)RESOLUTION.x, (float)mapSize.height / (float)RESOLUTION.y);
		TerrDecoData[,] decoData = new TerrDecoData[RESOLUTION.x, RESOLUTION.y];
		using (new BlockStopwatch("procgen", "terrain deco"))
		{
			for (int i = 0; i < RESOLUTION.y; i++)
			{
				for (int j = 0; j < RESOLUTION.x; j++)
				{
					WorldPos pos = new WorldPos((float)j * increment.x, (float)i * increment.y);
					Color clear = Color.clear;
					TerrDecoData terrDecoData = new TerrDecoData
					{
						pos = new Vector2Int(j, i)
					};
					if (board.DoesPointIntersectAnyEntity(pos) || board.DoesPointIntersectAnyTerrain(pos, allowMountains: true))
					{
						terrDecoData.hit = true;
						terrDecoData.processed = true;
					}
					int num = i * RESOLUTION.x + j;
					_colorBuffer[num] = clear;
					decoData[j, i] = terrDecoData;
				}
			}
			HeatmapManager heatmaps = Game.ctx.heatmaps;
			Heatmap resmap = heatmaps.Find(HeatmapType.LotZone, TagConstants.TAG_RESIDENTIAL);
			Heatmap commap = heatmaps.Find(HeatmapType.LotZone, TagConstants.TAG_COMMERCIAL);
			Heatmap indmap = heatmaps.Find(HeatmapType.LotZone, TagConstants.TAG_INDUSTRIAL);
			AppealHeatmap appealMap = heatmaps.Find(HeatmapType.Appeal) as AppealHeatmap;
			GroundBuildingHeatmap groundBuildings = heatmaps.Find(HeatmapType.GroundBuildings) as GroundBuildingHeatmap;
			GroundRailHeatmap groundRoadRail = heatmaps.Find(HeatmapType.GroundRoadRail) as GroundRailHeatmap;
			yield return null;
			List<TerrDecoGroup> groups = new List<TerrDecoGroup>();
			Vector2 vector = new Vector2(-1f, -1f);
			int num2 = 0;
			for (int k = 0; k < RESOLUTION.y; k++)
			{
				for (int l = 0; l < RESOLUTION.x; l++)
				{
					TerrDecoData terrDecoData2 = decoData[l, k];
					if (!terrDecoData2.hit && !terrDecoData2.processed)
					{
						TerrDecoGroup group = new TerrDecoGroup
						{
							min = vector,
							max = vector,
							items = new List<TerrDecoData>()
						};
						Queue<TerrDecoData> queue = new Queue<TerrDecoData>();
						queue.Enqueue(terrDecoData2);
						SpinOnQueue(ref queue, ref decoData, num2, ref group, ref resmap, ref commap, ref indmap, increment);
						groups.Add(group);
						num2++;
					}
				}
			}
			yield return null;
			int count = 0;
			foreach (TerrDecoGroup item in groups)
			{
				ZoneType zoneType = item.GetZoneType();
				int num3 = 0;
				int num4 = 8;
				_rng.Shuffle(item.items);
				foreach (TerrDecoData item2 in item.items)
				{
					int num5 = item2.pos.y * RESOLUTION.x + item2.pos.x;
					Color color = zoneType switch
					{
						ZoneType.Com => Color.blue, 
						ZoneType.Res => Color.green, 
						_ => Color.yellow, 
					};
					_colorBuffer[num5] = color;
					num3++;
					if (num3 != num4)
					{
						continue;
					}
					WorldPos pos2 = new WorldPos((float)item2.pos.x * increment.x, (float)item2.pos.y * increment.y);
					pos2 += new WorldPos(0.5f * _rng.Generate(-1f, 1f), 0.5f * _rng.Generate(-1f, 1f));
					AppealLevel appealLevel = appealMap.GetAppealLevel(pos2);
					ZoneTypeFlags zone = ZoneTypeHelper.ToFlags(zoneType);
					GroundFlags ground = groundBuildings.GetGroundFlag(pos2) | groundRoadRail.GetGroundFlag(pos2);
					EntityManager.DecoAutoplaceConfig decoAutoplaceConfig = Game.ctx.entityman.FindTemplatesForDecos(appealLevel, zone, ground);
					if (decoAutoplaceConfig == null)
					{
						continue;
					}
					EntityConfig entityConfig = decoAutoplaceConfig.PickElement(_rng);
					float deg = _rng.GenerateFloat() * 360f;
					WorldSize lotsize = entityConfig.board.lotsize;
					GridTransform gridTransform = new GridTransform
					{
						pos = pos2,
						deg = deg
					};
					if (!Game.ctx.board.DoesEntityIntersectAnyEntity(lotsize, gridTransform) && !Game.ctx.board.DoesEntityIntersectAnyTerrain(lotsize, gridTransform, allowMountains: true))
					{
						Game.ctx.board.CreateEntityAtStartup(entityConfig, gridTransform);
						count++;
						if (count % MAX_DECOS_PER_FRAME == 0)
						{
							yield return null;
						}
					}
					num3 = 0;
					num4 = 8;
				}
			}
		}
		tex.SetPixels(_colorBuffer);
		tex.Apply();
	}

	private void SpinOnQueue(ref Queue<TerrDecoData> queue, ref TerrDecoData[,] buffer, int currentID, ref TerrDecoGroup group, ref Heatmap resmap, ref Heatmap commap, ref Heatmap indmap, Vector2 increment)
	{
		while (queue.Count > 0 && group.items.Count < 1000)
		{
			TerrDecoData terrDecoData = queue.Dequeue();
			if (terrDecoData.breadth < 15)
			{
				terrDecoData.processed = true;
				terrDecoData.id = currentID;
				Vector2 vector = terrDecoData.pos * increment;
				HeatmapPos pos = resmap.WorldToHeatmap(new WorldPos(vector));
				float valueSafe = resmap.GetValueSafe(pos);
				float valueSafe2 = commap.GetValueSafe(pos);
				float valueSafe3 = indmap.GetValueSafe(pos);
				group.resScore += valueSafe;
				group.comScore += valueSafe2;
				group.indScore += valueSafe3;
				group.items.Add(terrDecoData);
				POS_CACHE[0] = new Vector2Int(terrDecoData.pos.x - 1, terrDecoData.pos.y);
				POS_CACHE[1] = new Vector2Int(terrDecoData.pos.x + 1, terrDecoData.pos.y);
				POS_CACHE[2] = new Vector2Int(terrDecoData.pos.x, terrDecoData.pos.y + 1);
				POS_CACHE[3] = new Vector2Int(terrDecoData.pos.x, terrDecoData.pos.y - 1);
				if (group.min.x == -1f || group.min.y == -1f)
				{
					group.min = vector;
					group.max = vector;
				}
				else
				{
					if (vector.x < group.min.x)
					{
						group.min.x = vector.x;
					}
					if (vector.x > group.max.x)
					{
						group.max.x = vector.x;
					}
					if (vector.y < group.min.y)
					{
						group.min.y = vector.y;
					}
					if (vector.y > group.max.y)
					{
						group.max.y = vector.y;
					}
				}
				Vector2Int[] pOS_CACHE = POS_CACHE;
				for (int i = 0; i < pOS_CACHE.Length; i++)
				{
					Vector2Int pos2 = pOS_CACHE[i];
					if (IsPositionValid(pos2))
					{
						TerrDecoData terrDecoData2 = buffer[pos2.x, pos2.y];
						if (!terrDecoData2.hit && !terrDecoData2.processed)
						{
							terrDecoData2.breadth = terrDecoData.breadth + 1;
							queue.Enqueue(terrDecoData2);
						}
					}
				}
			}
			else
			{
				terrDecoData.breadth = 0;
			}
		}
	}
}
