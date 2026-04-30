using System.Collections;
using Game.Core;
using UnityEngine;

namespace Game.Session.Setup;

internal sealed class CreateHeightmap
{
	private readonly IntSize RESOLUTION = new IntSize(512, 512);

	private SetupOrchestratorContext _ctx;

	public CreateHeightmap(SetupOrchestratorContext ctx)
	{
		_ctx = ctx;
	}

	public IEnumerator Start()
	{
		_ctx.heightmapData = new HeightmapData();
		Texture2D texture2D = new Texture2D(RESOLUTION.width, RESOLUTION.height)
		{
			filterMode = FilterMode.Point
		};
		IntSize mapSize = Game.ctx.session.mapconfig.map.mapSize;
		Vector2 vector = new Vector2((float)mapSize.width / (float)RESOLUTION.width, (float)mapSize.height / (float)RESOLUTION.height);
		Color[] array = new Color[RESOLUTION.width * RESOLUTION.height];
		for (int i = 0; i < RESOLUTION.height; i++)
		{
			for (int j = 0; j < RESOLUTION.width; j++)
			{
				Color color = new Color(0f, 0f, 0f, 1f);
				float x = (float)j * vector.x;
				float y = (float)i * vector.y;
				WorldPos pos = new WorldPos(x, y);
				if (_ctx.mountainRegionData != null)
				{
					foreach (TerrainBody mountainBody in _ctx.mountainRegionData._mountainBodies)
					{
						if (mountainBody.IsPointWithinBody(pos, out var info))
						{
							color.g = info.height;
						}
					}
				}
				if (_ctx.waterRegionData != null)
				{
					foreach (WaterRegion waterRegion in _ctx.waterRegionData.waterRegions)
					{
						if (waterRegion.IsPointWithin(pos))
						{
							color.r = 0.2f;
						}
					}
					foreach (TerrainBody waterBody in _ctx.waterRegionData.waterBodies)
					{
						if (waterBody.IsPointWithinBody(pos, out var _))
						{
							color.r = 0.2f;
						}
					}
				}
				int num = i * RESOLUTION.width + j;
				array[num] = color;
			}
		}
		texture2D.SetPixels(array);
		texture2D.Apply();
		_ctx.heightmapData.heightMap = texture2D;
		Game.ctx.board.terrain.SetHeightmap(_ctx.heightmapData);
		GameObject gameObject = GameObject.CreatePrimitive(PrimitiveType.Plane);
		gameObject.transform.position = new Vector3(-5f, 0f, -5f);
		gameObject.transform.eulerAngles = new Vector3(0f, 180f, 0f);
		gameObject.name = "TEST PLANE";
		gameObject.GetComponent<MeshRenderer>().material.mainTexture = texture2D;
		yield break;
	}
}
