using System.Linq;
using Game.Core;
using Game.Session;
using SomaSim.Util;
using UnityEngine;

namespace Game.Services.Maps;

public class DebugVisualizationService : AbstractService
{
	private GameObject _container;

	private bool _enabled = true;

	private Heatmap _currentHeatmap;

	private GameObject _currentHeatmapGO;

	public override void OnInitialized()
	{
		_container = new GameObject("DEBUG VIS");
		Game.serv.events.AddListener(ServiceEventType.SessionContextStateChange, OnSessionStateChange);
	}

	public override void OnReleased()
	{
		Game.serv.events.RemoveListener(ServiceEventType.SessionContextStateChange, OnSessionStateChange);
		RemoveAllDebugObjects();
		Object.Destroy(_container);
		_container = null;
	}

	private void OnSessionStateChange(ServiceEvent ev)
	{
		if (Game.ctx.State == SessionState.PreReleased)
		{
			RemoveAllDebugObjects();
		}
	}

	public void RemoveAllDebugObjects()
	{
		foreach (Transform item in _container.transform)
		{
			Object.Destroy(item.gameObject);
		}
	}

	private static string MakeCubeName(Vector3 pos)
	{
		return $"DEBUG CUBE {pos}";
	}

	public void AddCubeIf(bool pred, WorldPos pos, Color color, float scale = 0.5f)
	{
		if (pred)
		{
			AddCube(pos, color, scale);
		}
	}

	public void AddCube(WorldPos pos, Color color, float scale = 0.5f)
	{
		AddCube(pos, color, new Vector3(scale, scale, scale), Vector3.zero);
	}

	public void AddCube(WorldPos pos, Color color, Vector3 offset, float scale = 0.5f)
	{
		AddCube(pos, color, new Vector3(scale, scale, scale), offset);
	}

	public void AddCube(WorldPos pos, Color color, Vector3 scale, Vector3 offset)
	{
		if (_enabled)
		{
			Vector3 vector = pos.AsVector3XZ + offset;
			RemoveCube(vector);
			GameObject gameObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
			gameObject.name = MakeCubeName(vector);
			Transform transform = gameObject.transform;
			transform.SetParent(_container.transform);
			transform.position = vector;
			transform.localScale = scale;
			transform.name = MakeCubeName(vector);
			transform.GetComponent<Renderer>().material.color = color;
		}
	}

	private void RemoveCube(Vector3 pos)
	{
		if (_enabled)
		{
			string name = MakeCubeName(pos);
			_container.transform.DestroyChildByName(name);
		}
	}

	private string MakeLineName(WorldPos from, WorldPos to)
	{
		return $"DEBUG LINE {from} -> {to}";
	}

	public void AddLine(WorldPos[] nodes, Color color, float width = 0.25f, float height = 0.25f)
	{
		if (_enabled)
		{
			WorldPos worldPos = nodes[0];
			WorldPos to = nodes[nodes.Length - 1];
			RemoveLine(worldPos, to);
			GameObject gameObject = ResourceUtil.LoadAndInstantiateGameObject("Maps/Debug Line", _container);
			gameObject.name = MakeLineName(worldPos, to);
			LineRenderer component = gameObject.GetComponent<LineRenderer>();
			component.useWorldSpace = true;
			component.SetPositions(nodes.Select((WorldPos gridpos) => gridpos.AsVector3XZ.SetY(height)).ToArray());
			component.widthMultiplier = width;
			Color startColor = (component.endColor = color);
			component.startColor = startColor;
		}
	}

	public void AddLine(WorldPos from, WorldPos to, Color color, float width = 0.1f, float height = 0.25f)
	{
		if (_enabled)
		{
			AddLine(new WorldPos[2] { from, to }, color, width, height);
		}
	}

	public void RemoveLine(WorldPos from, WorldPos to)
	{
		if (_enabled)
		{
			string name = MakeLineName(from, to);
			_container.transform.DestroyChildByName(name);
		}
	}

	public void ShowHeatmap(HeatmapType type)
	{
		ShowHeatmap(Game.ctx.heatmaps.Find(type));
	}

	public void ShowHeatmap(HeatmapType type, Label tag)
	{
		ShowHeatmap(Game.ctx.heatmaps.Find(type, tag));
	}

	public void ShowHeatmap(Heatmap heatmap)
	{
		if (_enabled)
		{
			bool num = _currentHeatmapGO != null;
			bool flag = _currentHeatmap != null && !_currentHeatmap.cellSize.Equals(heatmap.cellSize);
			if (!num)
			{
				MakeHeatmapGameObject(heatmap);
			}
			if (num && flag)
			{
				RemoveHeatmap();
				MakeHeatmapGameObject(heatmap);
			}
			FillHeatmap(heatmap, heatmap.config.baseColor, heatmap.config.fillColor);
		}
	}

	private void MakeHeatmapGameObject(Heatmap heatmap)
	{
		IntSize intSize = heatmap.HeatmapToMapSize(heatmap.heatmapSize);
		Vector3 asVector3XZ = new WorldPos(intSize.width, intSize.height).AsVector3XZ;
		GameObject gameObject = ResourceUtil.LoadAndInstantiateGameObject("Maps/Debug Heatmap", _container);
		gameObject.name = "DEBUG HEATMAP";
		Transform transform = gameObject.transform;
		transform.SetParent(_container.transform);
		transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
		transform.localScale = new Vector3(asVector3XZ.x, asVector3XZ.z, 1f);
		transform.position = new Vector3(asVector3XZ.x / 2f, 0.2f, asVector3XZ.z / 2f);
		Texture2D texture2D = new Texture2D(heatmap.heatmapSize.width, heatmap.heatmapSize.height, TextureFormat.RGBA32, mipChain: false);
		texture2D.wrapMode = TextureWrapMode.Clamp;
		texture2D.filterMode = FilterMode.Point;
		gameObject.GetComponent<MeshRenderer>().material.mainTexture = texture2D;
		_currentHeatmapGO = gameObject;
	}

	private void FillHeatmap(Heatmap heatmap, Color from, Color to)
	{
		Texture2D texture2D = _currentHeatmapGO.GetComponent<MeshRenderer>().material.mainTexture as Texture2D;
		float[,] data = heatmap.data;
		int i = 0;
		for (int width = heatmap.heatmapSize.width; i < width; i++)
		{
			int j = 0;
			for (int height = heatmap.heatmapSize.height; j < height; j++)
			{
				Color color = Color.Lerp(from, to, data[i, j]);
				color.a = 0.8f;
				texture2D.SetPixel(i, j, color);
			}
		}
		texture2D.Apply();
		_currentHeatmap = heatmap;
	}

	public void RemoveHeatmap()
	{
		if (_enabled)
		{
			Object.Destroy(_currentHeatmapGO);
			_currentHeatmapGO = null;
			_currentHeatmap = null;
		}
	}
}
