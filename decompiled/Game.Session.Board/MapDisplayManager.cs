using System.Collections;
using System.Collections.Generic;
using Game.Core;
using Game.Services.Maps;
using Game.Session.Player;
using SomaSim.Util;
using UnityEngine;

namespace Game.Session.Board;

public class MapDisplayManager : AbstractSessionManager, IAnimatingManager, ISessionManager
{
	public bool DEBUG_PLANE_RENDERER;

	private const int MAX_SAMPLES_PER_FRAME = 4000;

	private Vector2Int RESOLUTION = new Vector2Int(1024, 1024);

	private const int COLLIDER_CACHE_SIZE = 100;

	private Texture2D _texture;

	private MeshRenderer debugPlaneRenderer;

	private PolygonCollider2D[] _tempColliders = new PolygonCollider2D[100];

	private RectInt? _updateRegion;

	private Coroutine _currentUpdateRoutine;

	private GameObject _territoryParent;

	private int _layer;

	private ContactFilter2D _contactFilter;

	private Color[] _colorBuffer;

	private Dictionary<PlayerID, List<TerritoryCollider>> _colliders;

	private Dictionary<PlayerID, Color> _colors;

	private Dictionary<PlayerID, TerritoryLabel> _labels;

	private List<TerritoryLabel> _districts;

	private Canvas _mapNamesCanvas;

	public override void OnInitializeStarted()
	{
		Game.serv.camera.OnCameraZoomPercentage.Add(OnCameraZoom);
		_territoryParent = new GameObject("Territory Parent");
		_layer = LayerMask.NameToLayer("Territory");
		_contactFilter = new ContactFilter2D
		{
			layerMask = 1 << _layer
		};
		Color clear = Color.clear;
		_texture = new Texture2D(RESOLUTION.x, RESOLUTION.y);
		_colorBuffer = new Color[RESOLUTION.x * RESOLUTION.y];
		for (int i = 0; i < RESOLUTION.y; i++)
		{
			for (int j = 0; j < RESOLUTION.x; j++)
			{
				int num = i * RESOLUTION.x + j;
				_colorBuffer[num] = clear;
			}
		}
		_texture.SetPixels(_colorBuffer);
		_texture.Apply();
		IntSize mapSize = Game.ctx.session.mapconfig.map.mapSize;
		Shader.SetGlobalVector("_WorldSize", new Vector4(mapSize.width, mapSize.height, 0f, 0f));
		Shader.SetGlobalTexture("_TerritoryTex", _texture);
		Shader.SetGlobalFloat("_TerritoryBlend", 1f);
		if (DEBUG_PLANE_RENDERER)
		{
			GameObject gameObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
			gameObject.transform.position = new Vector3(150f, 0.05f, 200f);
			gameObject.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
			gameObject.transform.localScale = new Vector3(300f, 400f, 1f);
			gameObject.name = "Territory Plane";
			gameObject.layer = _layer;
			debugPlaneRenderer = gameObject.GetComponent<MeshRenderer>();
			debugPlaneRenderer.material = new Material(Shader.Find("Unlit/Transparent"))
			{
				mainTexture = _texture
			};
		}
		_colliders = new Dictionary<PlayerID, List<TerritoryCollider>>(new PlayerIDEqualityComparer());
		_colors = new Dictionary<PlayerID, Color>(new PlayerIDEqualityComparer());
		_labels = new Dictionary<PlayerID, TerritoryLabel>(new PlayerIDEqualityComparer());
		_districts = new List<TerritoryLabel>();
		GameObject gameObject2 = new GameObject("Map Canvas");
		_mapNamesCanvas = gameObject2.AddComponent<Canvas>();
		_mapNamesCanvas.gameObject.layer = _layer;
		RectTransform obj = _mapNamesCanvas.transform as RectTransform;
		obj.position = Vector3.zero;
		obj.anchorMin = Vector2.zero;
		obj.anchorMax = Vector2.zero;
		obj.pivot = Vector2.zero;
		obj.sizeDelta = new Vector2(0f, 0f);
		obj.rotation = Quaternion.Euler(90f, 0f, 0f);
	}

	public override void OnPreInteractiveAIGen()
	{
		base.OnPreInteractiveAIGen();
		CreateDistricts();
	}

	public override void OnReleased()
	{
		Object.Destroy(_territoryParent);
		if (debugPlaneRenderer != null)
		{
			Object.Destroy(debugPlaneRenderer.gameObject);
			debugPlaneRenderer = null;
		}
		Object.Destroy(_mapNamesCanvas.gameObject);
		_territoryParent = null;
		_mapNamesCanvas = null;
		_colorBuffer = null;
		_colliders.Clear();
		_colliders = null;
		_colors = null;
		_labels = null;
		_districts = null;
		_layer = 0;
		Game.serv.camera.OnCameraZoomPercentage.Remove(OnCameraZoom);
	}

	public Color GetColorForPlayerText(PlayerInfo player, float brightness = 0.5f)
	{
		return Color.Lerp(GetColorForPlayer(player), Color.white, brightness);
	}

	public Color GetColorForPlayer(PlayerInfo player)
	{
		PlayerID pID = player.PID;
		if (_colors.ContainsKey(pID))
		{
			return _colors[pID];
		}
		Color playerColor = player.territory.colorInfo.GetPlayerColor();
		_colors.Add(pID, playerColor);
		return playerColor;
	}

	private void CreateDistricts()
	{
		List<DistrictConfig> districts = Game.ctx.session.mapconfig.map.districts;
		if (districts == null)
		{
			return;
		}
		foreach (DistrictConfig item in districts)
		{
			AddDistrict(item);
		}
	}

	public void AddDistrict(DistrictConfig dist)
	{
		if (CanAddDistrictLabel(dist))
		{
			TerritoryLabel territoryLabel = MakeNewTerritoryLabel($"District {dist.customname ?? dist.locname} {dist.start}");
			territoryLabel.Initialize(dist);
			territoryLabel.Show(showText: true);
		}
	}

	private bool CanAddDistrictLabel(DistrictConfig dist)
	{
		using (ListPool<Node>.PooledBlockList pooledBlockList = ListPool<Node>.Allocate())
		{
			Game.ctx.board.nodes.FindAndSortNodesInRadius(dist.start, 20f, sort: false, pooledBlockList);
			foreach (Node item in pooledBlockList)
			{
				if (item.HasAnyTransit)
				{
					return true;
				}
			}
		}
		return false;
	}

	private TerritoryLabel MakeNewTerritoryLabel(string debug)
	{
		GameObject gameObject = new GameObject("TerritoryLabel " + debug);
		gameObject.transform.SetParent(_territoryParent.transform);
		gameObject.layer = _layer;
		TerritoryLabel territoryLabel = gameObject.AddComponent<TerritoryLabel>();
		territoryLabel.Create(_mapNamesCanvas);
		return territoryLabel;
	}

	public void AddOrUpdateTerritory(PlayerID pid, List<List<WorldPos>> lines)
	{
		List<TerritoryCollider> list = _colliders.FindOrAddNew(pid);
		while (list.Count > lines.Count)
		{
			Object.Destroy(list.RemoveLast().gameObject);
		}
		while (list.Count < lines.Count)
		{
			GameObject gameObject = new GameObject($"TerritoryCollider {pid}")
			{
				layer = _layer
			};
			gameObject.transform.SetParent(_territoryParent.transform);
			list.Add(gameObject.AddComponent<TerritoryCollider>());
		}
		for (int i = 0; i < lines.Count; i++)
		{
			list[i].Set(pid, lines[i]);
		}
		RefreshTerritoryLabel(pid);
	}

	public void RefreshTerritoryLabel(PlayerID pid, bool updateName = false)
	{
		int num = _colliders.FindOrNull(pid)?.Count ?? 0;
		UpdateTerritoryLabel(pid, num > 0, updateName);
	}

	private void UpdateTerritoryLabel(PlayerID pid, bool hasTerritory, bool updateName)
	{
		TerritoryLabel territoryLabel = _labels.FindOrNull(pid);
		if (territoryLabel == null)
		{
			TerritoryLabel territoryLabel2 = (_labels[pid] = MakeNewTerritoryLabel(pid.ToString()));
			territoryLabel = territoryLabel2;
			territoryLabel.Initialize(pid);
		}
		if (updateName && territoryLabel != null)
		{
			territoryLabel.Initialize(pid);
		}
		PlayerInfo playerInfo = pid.FindPlayer();
		bool showText = playerInfo.OfInterest && !playerInfo.IsJustGoon && hasTerritory && !playerInfo.territory.IsSafehouseVanquished;
		territoryLabel.Show(showText);
	}

	private void ExpandCurrentRectToContain(RectInt desiredUpdate)
	{
		if (_updateRegion.HasValue)
		{
			RectInt value = _updateRegion.Value;
			int num = Mathf.Min(desiredUpdate.xMin, value.xMin);
			int num2 = Mathf.Max(desiredUpdate.xMax, value.xMax);
			int num3 = Mathf.Min(desiredUpdate.yMin, value.yMin);
			int num4 = Mathf.Max(desiredUpdate.yMax, value.yMax);
			_updateRegion = new RectInt(num, num3, num2 - num, num4 - num3);
		}
		else
		{
			_updateRegion = desiredUpdate;
		}
	}

	public void FlagAsDirty(RectInt desiredUpdate)
	{
		ExpandCurrentRectToContain(desiredUpdate);
	}

	private bool RefreshMap(RectInt region)
	{
		bool result = false;
		if (_currentUpdateRoutine == null)
		{
			_currentUpdateRoutine = Game.instance.StartCoroutine(RefreshRegionAsync(region));
			result = true;
		}
		return result;
	}

	public void UpdateAnimations(GameAnimUpdate anim)
	{
		if (_updateRegion.HasValue && RefreshMap(_updateRegion.Value))
		{
			_updateRegion = null;
		}
		if (UnityEngine.Input.GetKeyDown(KeyCode.R))
		{
			IntSize mapSize = Game.ctx.session.mapconfig.map.mapSize;
			RefreshMap(new RectInt(0, 0, mapSize.width, mapSize.height));
		}
	}

	private void OnCameraZoom(float zoomPercentage)
	{
		FloatRange territoryDisplayZoomRange = Game.serv.globals.settings.general.territory.territoryDisplayZoomRange;
		float num = territoryDisplayZoomRange.to - territoryDisplayZoomRange.from;
		float num2 = Mathf.Clamp01((zoomPercentage - territoryDisplayZoomRange.from) / num);
		Shader.SetGlobalFloat("_TerritoryBlend", num2);
		SetMapNamesActive(num2 > 0f);
	}

	private void SetMapNamesActive(bool activateNames)
	{
		_mapNamesCanvas.gameObject.SetActive(activateNames);
	}

	private IEnumerator RefreshRegionAsync(RectInt region)
	{
		while (!Game.ctx.IsInteractive)
		{
			yield return null;
		}
		IntSize size = Game.ctx.session.mapconfig.map.mapSize;
		region.ClampToBounds(new RectInt(0, 0, size.width, size.height));
		Vector2 increment = new Vector2((float)size.width / (float)RESOLUTION.x, (float)size.height / (float)RESOLUTION.y);
		Color defaultColor = Color.clear;
		int xStart = (int)((float)region.x / (float)size.width * (float)RESOLUTION.x);
		int num = (int)((float)region.y / (float)size.height * (float)RESOLUTION.y);
		int xMax = xStart + (int)((float)region.width / (float)size.width * (float)RESOLUTION.x);
		int yMax = num + (int)((float)region.height / (float)size.height * (float)RESOLUTION.y);
		_ = Time.realtimeSinceStartup;
		List<PlayerID> foundIds = new List<PlayerID>();
		int currentCount = 0;
		for (int y = num; y < yMax; y++)
		{
			for (int x = xStart; x < xMax; x++)
			{
				foundIds.Clear();
				Color color = defaultColor;
				float x2 = (float)x * increment.x;
				float y2 = (float)y * increment.y;
				Vector2 point = new Vector2(x2, y2);
				ContactFilter2D contactFilter = _contactFilter;
				Collider2D[] tempColliders = _tempColliders;
				int num2 = Physics2D.OverlapPoint(point, contactFilter, tempColliders);
				if (num2 != 0)
				{
					for (int i = 0; i < num2; i++)
					{
						PlayerInfo player = _tempColliders[i].GetComponent<TerritoryCollider>().GetPlayer();
						if (foundIds.Contains(player.PID))
						{
							foundIds.Remove(player.PID);
						}
						else
						{
							foundIds.Add(player.PID);
						}
					}
					if (foundIds.Count == 1)
					{
						PlayerID key = foundIds[0];
						color = _colors[key];
						color.a = 0.5f;
					}
				}
				int num3 = y * RESOLUTION.x + x;
				_colorBuffer[num3] = color;
				currentCount++;
				if (currentCount % 4000 == 0)
				{
					yield return null;
					if (_colorBuffer == null)
					{
						yield break;
					}
				}
			}
		}
		yield return null;
		_ = Time.realtimeSinceStartup;
		_texture.SetPixels(_colorBuffer);
		_texture.Apply();
		Shader.SetGlobalVector("_WorldSize", new Vector4(size.width, size.height, 0f, 0f));
		Shader.SetGlobalTexture("_TerritoryTex", _texture);
		_currentUpdateRoutine = null;
	}

	public void SetTerritoryTextAlpha(float alpha)
	{
		foreach (TerritoryLabel value in _labels.Values)
		{
			value.SetAlpha(alpha);
		}
		foreach (TerritoryLabel district in _districts)
		{
			district.SetAlpha(alpha);
		}
	}
}
