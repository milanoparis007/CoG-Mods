using System.Collections;
using System.Collections.Generic;
using Game.Core;
using Game.Services.Maps;
using Game.Session.Heatmaps;
using Game.Session.Setup;
using SomaSim.Util;
using UnityEngine;

namespace Game.Session.Board;

public sealed class TerrainManager : ISubManager<BoardManager>
{
	private MapConfig _mapdef;

	private GameObject _container;

	private CoroutineTask _task;

	private List<TerrainMeshGenerator> _terrainPieces;

	private Texture2D _baseGroundTexture;

	private Color[] _terrainTexColors;

	private HeightmapData _heigtmap;

	private WaterRegionData _waterData;

	public const float BASE_LAND_HEIGHT = 0.4975f;

	private Mesh _terrainQuad;

	public HeightmapData Heightmap => _heigtmap;

	public WaterRegionData WaterData => _waterData;

	public Mesh TerrainQuad => _terrainQuad;

	public void Initialize(BoardManager mgr)
	{
		_mapdef = Game.ctx.session.mapconfig;
		_terrainPieces = new List<TerrainMeshGenerator>();
	}

	public void Release()
	{
		if (_task != null && !_task.IsFinished)
		{
			_task.Stop();
		}
		Object.Destroy(_container);
		_container = null;
		_task = null;
		_mapdef = null;
		_waterData = null;
		_heigtmap = null;
	}

	internal void SetHeightmap(HeightmapData data)
	{
		_heigtmap = data;
	}

	public void StartTerrainMeshGeneration(TerrainGenData data)
	{
		_waterData = data.waterData;
		_task = Game.serv.sequencer.StartCoroutineTask(MakeTerrainCoroutine(data));
	}

	public bool IsTerrainMeshGenerationDone()
	{
		if (_task != null)
		{
			return _task.IsFinished;
		}
		return false;
	}

	private IEnumerator MakeTerrainCoroutine(TerrainGenData data)
	{
		_container = new GameObject
		{
			name = "Terrain"
		};
		IntSize fullChunkSize = _mapdef.terrain.chunktiles;
		IntSize maprect = _mapdef.map.mapSize;
		IntPos chunkcount = new IntPos(1 + maprect.width / fullChunkSize.width, 1 + maprect.height / fullChunkSize.height);
		GenerateWater(maprect).transform.SetParent(_container.transform);
		_baseGroundTexture = new Texture2D(maprect.width, maprect.height);
		_terrainTexColors = new Color[maprect.width * maprect.height];
		UploadBaseGroundTexture();
		_terrainQuad = MakeSimpleQuad(_mapdef.terrain);
		int count = 0;
		for (int y = 0; y < chunkcount.y; y++)
		{
			for (int x = 0; x < chunkcount.x; x++)
			{
				Rect chunkrect = new Rect(x * fullChunkSize.width, y * fullChunkSize.height, fullChunkSize.width, fullChunkSize.height);
				if (chunkrect.x + chunkrect.width > (float)maprect.width)
				{
					chunkrect.width = (float)maprect.width - chunkrect.x;
				}
				if (chunkrect.y + chunkrect.height > (float)maprect.height)
				{
					chunkrect.height = (float)maprect.height - chunkrect.y;
				}
				GenerateTerrain(ref _terrainQuad, data, maprect, chunkrect).transform.SetParent(_container.transform);
				count++;
				if (count % 5 == 0)
				{
					yield return null;
				}
			}
		}
	}

	private GameObject GenerateWater(IntSize size)
	{
		GameObject gameObject = Resources.Load<GameObject>("Maps/Terrain Water Prefab");
		if (gameObject == null)
		{
			Logger.Error("Failed to load terrain prefab");
			return null;
		}
		MapConfig.TerrainConfig terrain = _mapdef.terrain;
		float num = 0f - terrain.groundheight + terrain.waterheight;
		Shader.SetGlobalFloat("_WaterHeight", num);
		GameObject gameObject2 = Object.Instantiate(gameObject);
		gameObject2.name = "Terrain Water";
		gameObject2.transform.localScale = new Vector3(size.width, 1f, size.height);
		gameObject2.transform.position = new Vector3(0f, num, 0f);
		return gameObject2;
	}

	private GameObject GenerateTerrain(ref Mesh terrainQuad, TerrainGenData data, IntSize mapsize, Rect chunkrect)
	{
		GameObject gameObject = Resources.Load<GameObject>("Maps/Terrain Prefab");
		if (gameObject == null)
		{
			Logger.Error("Failed to load terrain prefab");
			return null;
		}
		GameObject gameObject2 = Object.Instantiate(gameObject);
		gameObject2.name = $"Terrain {chunkrect}";
		MeshFilter component = gameObject2.GetComponent<MeshFilter>();
		TerrainMeshGenerator terrainMeshGenerator = new TerrainMeshGenerator();
		terrainMeshGenerator.Generate(ref terrainQuad, component, data, _mapdef.terrain, mapsize, chunkrect);
		gameObject2.transform.localScale = new Vector3(chunkrect.width, chunkrect.height, 1f);
		gameObject2.transform.position = new Vector3(chunkrect.x, 0f, chunkrect.y);
		_terrainPieces.Add(terrainMeshGenerator);
		return gameObject2;
	}

	public void UpdateColors(bool forceHeatmapUpdate)
	{
		HeatmapManager heatmaps = Game.ctx.heatmaps;
		if (forceHeatmapUpdate)
		{
			heatmaps.ManualUpdate(HeatmapType.GroundBuildings, 2);
			heatmaps.ManualUpdate(HeatmapType.GroundRoadRail, 1);
		}
		Heatmap heatmap = heatmaps.Find(HeatmapType.GroundBuildings);
		Heatmap heatmap2 = heatmaps.Find(HeatmapType.GroundRoadRail);
		Texture2D bilinearTextureFromMap = heatmap.GetBilinearTextureFromMap(Color.white);
		Texture2D bilinearTextureFromMap2 = heatmap2.GetBilinearTextureFromMap(Color.white);
		for (int i = 0; i < _mapdef.map.mapSize.height; i++)
		{
			for (int j = 0; j < _mapdef.map.mapSize.width; j++)
			{
				float u = (float)j / (float)_mapdef.map.mapSize.width;
				float v = (float)i / (float)_mapdef.map.mapSize.height;
				float a = bilinearTextureFromMap.GetPixelBilinear(u, v).a;
				float a2 = bilinearTextureFromMap2.GetPixelBilinear(u, v).a;
				Color color = new Color(a, a2, 0f);
				int num = i * _mapdef.map.mapSize.width + j;
				_terrainTexColors[num] = color;
			}
		}
		UploadBaseGroundTexture();
	}

	private void UploadBaseGroundTexture()
	{
		_baseGroundTexture.SetPixels(_terrainTexColors);
		_baseGroundTexture.Apply();
		Shader.SetGlobalTexture("_TerrainColor", _baseGroundTexture);
	}

	private static Mesh MakeSimpleQuad(MapConfig.TerrainConfig topo)
	{
		Mesh mesh = new Mesh();
		float num = 0.4975f;
		float num2 = 1f - num;
		float z = (num2 - topo.groundheight) * topo.GetTerrainScale(num2 - topo.groundheight < 0f);
		Vector3[] vertices = new Vector3[4]
		{
			new Vector3(0f, 0f, z),
			new Vector3(1f, 0f, z),
			new Vector3(0f, 1f, z),
			new Vector3(1f, 1f, z)
		};
		int[] triangles = new int[6] { 0, 2, 1, 2, 3, 1 };
		Vector2[] uv = new Vector2[4]
		{
			new Vector2(0f, 0f),
			new Vector2(1f, 0f),
			new Vector2(0f, 1f),
			new Vector2(1f, 1f)
		};
		Color[] colors = new Color[4]
		{
			Color.white,
			Color.white,
			Color.white,
			Color.white
		};
		mesh.vertices = vertices;
		mesh.uv = uv;
		mesh.triangles = triangles;
		mesh.colors = colors;
		mesh.RecalculateNormals();
		mesh.RecalculateTangents();
		mesh.RecalculateBounds();
		return mesh;
	}
}
