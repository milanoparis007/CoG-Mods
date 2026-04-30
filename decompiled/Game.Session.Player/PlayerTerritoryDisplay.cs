using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Services;
using Game.Session.Board;
using SomaSim.Util;
using UnityEngine;

namespace Game.Session.Player;

public sealed class PlayerTerritoryDisplay
{
	public class MeshGenData
	{
		public List<Vector3> vertices = new List<Vector3>();

		public List<int> triangles = new List<int>();

		public List<Color> colors = new List<Color>();

		public void Clear()
		{
			vertices.Clear();
			triangles.Clear();
			colors.Clear();
		}

		public Mesh MakeMesh()
		{
			Mesh mesh = new Mesh();
			mesh.vertices = vertices.ToArray();
			mesh.triangles = triangles.ToArray();
			mesh.colors = colors.ToArray();
			mesh.RecalculateBounds();
			mesh.RecalculateNormals();
			return mesh;
		}
	}

	private static GameObject _staticContainer;

	private PolygonClipper _clipper;

	private PlayerTerritory _manager;

	private PlayerID _pid;

	private MeshGenData _wallgen;

	private MeshGenData _floorgen;

	private GameObject _wall;

	private RectInt? _maxrect;

	private bool _territoryDataIsReady;

	private volatile bool _isBorderCalcPending;

	private const float LINE_Y_OFFSET = 0.05f;

	public static void ToggleBorders()
	{
		if (_staticContainer != null)
		{
			_staticContainer.SetActive(!_staticContainer.activeSelf);
		}
	}

	public static void ToggleBorders(bool show)
	{
		if (_staticContainer != null)
		{
			_staticContainer.SetActive(show);
		}
	}

	public void Initialize(PlayerTerritory manager)
	{
		_manager = manager;
		_pid = manager.PlayerInfo.PID;
		_clipper = new PolygonClipper();
		_wallgen = new MeshGenData();
		_floorgen = new MeshGenData();
		_maxrect = null;
		InitializeBorderAssets();
		Game.ctx.events.AddListener(SessionEventType.PlayerVizChanged, OnVizChanged);
		Game.ctx.events.AddListener(SessionEventType.PlayerTerritoryChanged, OnTerritoryChanged);
		Game.serv.events.AddListener(ServiceEventType.SessionContextStateChange, OnSessionStateChange);
	}

	public void Release()
	{
		Game.serv.events.RemoveListener(ServiceEventType.SessionContextStateChange, OnSessionStateChange);
		Game.ctx.events.RemoveListener(SessionEventType.PlayerTerritoryChanged, OnTerritoryChanged);
		Game.ctx.events.RemoveListener(SessionEventType.PlayerVizChanged, OnVizChanged);
		RemoveBorder();
		ReleaseBorderAssets();
		_maxrect = null;
		_clipper = null;
		_wallgen = null;
		_floorgen = null;
		_pid = PlayerID.INVALID;
		_manager = null;
	}

	private void OnSessionStateChange(ServiceEvent sev)
	{
		if (Game.ctx.IsPreInteractiveInitDone && Game.ctx.HasSaveFile && !_pid.IsSystem)
		{
			_territoryDataIsReady = true;
			RefreshBorder();
		}
	}

	private void OnTerritoryChanged(SessionEvent sev)
	{
		if (Game.ctx.State == SessionState.Interactive && sev.pid == _pid)
		{
			_territoryDataIsReady = true;
			RefreshBorder();
		}
	}

	private void OnVizChanged(SessionEvent sev)
	{
		if (Game.ctx.State == SessionState.Interactive && sev.pid == _pid && !_isBorderCalcPending)
		{
			RefreshBorder();
		}
	}

	private void InitializeBorderAssets()
	{
		if (_staticContainer == null)
		{
			_staticContainer = new GameObject
			{
				name = "Territory Container"
			};
		}
		GameObject gameObject = Resources.Load<GameObject>("Maps/Territory Border Prefab");
		if (!(gameObject == null))
		{
			_wall = UnityEngine.Object.Instantiate(gameObject, _staticContainer.transform);
			GameObject wall = _wall;
			PlayerID pid = _pid;
			wall.name = "Territory Wall " + pid.ToString();
		}
	}

	private void ReleaseBorderAssets()
	{
		_wall.transform.SetParent(null);
		UnityEngine.Object.Destroy(_wall);
		_wall = null;
		if (_staticContainer.transform.childCount == 0)
		{
			UnityEngine.Object.Destroy(_staticContainer);
			_staticContainer = null;
		}
	}

	internal bool RunningOnUnityThread()
	{
		return Game.serv.sequencer.IsRunningOnUnityThread();
	}

	private void RefreshBorder()
	{
		if (_territoryDataIsReady && Game.ctx.players.Human.meetings.IsPlayerMet(_pid) && !_isBorderCalcPending)
		{
			_isBorderCalcPending = true;
			List<Node> ownedNodes = _manager.OwnedNodeIds.Select((NodeID id) => id.FindNode()).ToList();
			ActionSequencerService sequencer = Game.serv.sequencer;
			Func<Lines> producer = delegate
			{
				Paths paths = _clipper.GatherIndividualPolygons(ownedNodes);
				return _clipper.GenerateSinglePolygon(paths);
			};
			Action<Lines> consumer = delegate(Lines lines)
			{
				SetBorderMeshes(lines);
				_isBorderCalcPending = false;
			};
			PlayerID pid = _pid;
			sequencer.StartThreadedProducerConsumer(producer, consumer, "territory lines producer for pid " + pid.ToString());
		}
	}

	private void SetBorderMeshes(Lines lines)
	{
		Color colorForPlayer = Game.ctx.mapdisplay.GetColorForPlayer(_manager.PlayerInfo);
		Game.ctx.mapdisplay.AddOrUpdateTerritory(_pid, lines);
		SetWalls(lines, colorForPlayer);
		SetFloors(lines, colorForPlayer);
		UpdateTerritoryMaxRect(lines);
		if (_maxrect.HasValue)
		{
			Game.ctx.mapdisplay.FlagAsDirty(_maxrect.Value);
		}
		if (lines.debug == null)
		{
			return;
		}
		foreach (WorldPos item in lines.debug)
		{
			Game.serv.debugvis.AddCube(item, Color.green);
		}
	}

	private void UpdateTerritoryMaxRect(List<List<WorldPos>> lines)
	{
		RectInt? boundRectFromPositions = GetBoundRectFromPositions(lines);
		if (boundRectFromPositions.HasValue)
		{
			RectInt value = boundRectFromPositions.Value;
			_maxrect = (_maxrect.HasValue ? GetBoundRectFromTwoRectangles(_maxrect.Value, value) : value);
		}
	}

	private RectInt? GetBoundRectFromPositions(List<List<WorldPos>> lines)
	{
		bool flag = false;
		int num = int.MaxValue;
		int num2 = int.MinValue;
		int num3 = int.MaxValue;
		int num4 = int.MinValue;
		foreach (List<WorldPos> line in lines)
		{
			foreach (WorldPos item in line)
			{
				int b = Mathf.RoundToInt(item.x);
				int b2 = Mathf.RoundToInt(item.y);
				num = Mathf.Min(num, b);
				num2 = Mathf.Max(num2, b);
				num3 = Mathf.Min(num3, b2);
				num4 = Mathf.Max(num4, b2);
				flag = true;
			}
		}
		if (!flag)
		{
			return null;
		}
		return new RectInt(num - 1, num3 - 1, num2 - num + 2, num4 - num3 + 2);
	}

	private RectInt GetBoundRectFromTwoRectangles(RectInt a, RectInt b)
	{
		int num = Mathf.Min(a.xMin, b.xMin);
		int num2 = Mathf.Max(a.xMax, b.xMax);
		int num3 = Mathf.Min(a.yMin, b.yMin);
		int num4 = Mathf.Max(a.yMax, b.yMax);
		return new RectInt(num, num3, num2 - num, num4 - num3);
	}

	private void RemoveBorder()
	{
		RemoveWalls();
		RemoveFloors();
	}

	private MeshFilter GetBorderWallMeshFilter()
	{
		return _wall.GetChild("Wall").GetComponent<MeshFilter>();
	}

	private MeshFilter GetBorderFloorMeshFilter()
	{
		return _wall.GetChild("Floor").GetComponent<MeshFilter>();
	}

	private void RemoveWalls()
	{
		GetBorderWallMeshFilter().mesh = null;
	}

	private void SetWalls(List<List<WorldPos>> lines, Color color)
	{
		_wallgen.Clear();
		foreach (List<WorldPos> line in lines)
		{
			AddWall(_wallgen, line, color);
		}
		GetBorderWallMeshFilter().mesh = _wallgen.MakeMesh();
	}

	private void AddWall(MeshGenData data, List<WorldPos> line, Color color)
	{
		CameraService camera = Game.serv.camera;
		int i = 0;
		for (int count = line.Count; i < count; i++)
		{
			Vector3 thispos = camera.WorldToSceneVector(line[i]);
			Vector3 nextpos = camera.WorldToSceneVector(line[(i + 1) % count]);
			AddWallSegment(data, thispos, nextpos, color);
		}
	}

	private void AddWallSegment(MeshGenData data, Vector3 thispos, Vector3 nextpos, Color color)
	{
		int count = data.vertices.Count;
		Vector3 vector = new Vector3(0f, 1.5f, 0f);
		data.vertices.Add(thispos);
		data.vertices.Add(thispos + vector);
		data.vertices.Add(nextpos);
		data.vertices.Add(nextpos + vector);
		Color item = color;
		item.a = 0f;
		data.colors.Add(color);
		data.colors.Add(item);
		data.colors.Add(color);
		data.colors.Add(item);
		data.triangles.Add(count);
		data.triangles.Add(count + 1);
		data.triangles.Add(count + 2);
		data.triangles.Add(count + 1);
		data.triangles.Add(count + 2);
		data.triangles.Add(count + 3);
	}

	private void RemoveFloors()
	{
		GetBorderFloorMeshFilter().mesh = null;
	}

	private void SetFloors(List<List<WorldPos>> lines, Color color)
	{
		_floorgen.Clear();
		foreach (List<WorldPos> line in lines)
		{
			AddFloorSegments(_floorgen, line, color);
		}
		GetBorderFloorMeshFilter().mesh = _floorgen.MakeMesh();
	}

	private void AddFloorSegments(MeshGenData data, List<WorldPos> line, Color color)
	{
		CameraService camera = Game.serv.camera;
		ExtrudeLine(line, 0.45f, out var main, out var side);
		Color right = color;
		right.a = 0.1f;
		int i = 0;
		for (int count = main.Count; i < count; i++)
		{
			Vector3 aleft = camera.WorldToSceneVector(main[i], 0.1f);
			Vector3 bleft = camera.WorldToSceneVector(main[(i + 1) % count], 0.1f);
			Vector3 aright = camera.WorldToSceneVector(side[i], 0.1f);
			Vector3 bright = camera.WorldToSceneVector(side[(i + 1) % count], 0.1f);
			AddFloorSegment(data, aleft, aright, bleft, bright, color, right);
		}
	}

	private void AddFloorSegment(MeshGenData data, Vector3 aleft, Vector3 aright, Vector3 bleft, Vector3 bright, Color left, Color right)
	{
		int count = data.vertices.Count;
		data.vertices.Add(aleft);
		data.vertices.Add(aright);
		data.vertices.Add(bleft);
		data.vertices.Add(bright);
		data.colors.Add(left);
		data.colors.Add(right);
		data.colors.Add(left);
		data.colors.Add(right);
		data.triangles.Add(count);
		data.triangles.Add(count + 1);
		data.triangles.Add(count + 2);
		data.triangles.Add(count + 1);
		data.triangles.Add(count + 2);
		data.triangles.Add(count + 3);
	}

	private void ExtrudeLine(List<WorldPos> points, float dist, out List<WorldPos> main, out List<WorldPos> side)
	{
		main = new List<WorldPos>();
		side = new List<WorldPos>();
		int i = 0;
		for (int count = points.Count; i < count; i++)
		{
			WorldPos prev = ((i == 0) ? points[count - 1] : points[i - 1]);
			WorldPos cur = points[i];
			WorldPos next = points[(i + 1) % count];
			FindExtrudedPoints(prev, cur, next, dist, main, side);
		}
	}

	private void FindExtrudedPoints(WorldPos prev, WorldPos cur, WorldPos next, float dist, List<WorldPos> main, List<WorldPos> side)
	{
		WorldPos worldPos = cur - prev;
		WorldPos worldPos2 = next - cur;
		WorldPos worldPos3 = worldPos.Perpendicular().Normalized * dist;
		WorldPos worldPos4 = worldPos2.Perpendicular().Normalized * dist;
		WorldPos worldPos5 = prev + worldPos3;
		WorldPos worldPos6 = cur + worldPos4;
		WorldPos worldPos7 = FindLineLineIntersection(worldPos5, worldPos5 + worldPos, worldPos6, worldPos6 + worldPos2) ?? (cur + worldPos3);
		float magnitude = (cur - worldPos7).Magnitude;
		float num = dist * 2f;
		if (!(magnitude > num) || !MakeChamferedEdge(cur, worldPos7, worldPos, worldPos2, num, main, side))
		{
			main.Add(cur);
			side.Add(worldPos7);
		}
	}

	private bool MakeChamferedEdge(WorldPos cur, WorldPos point, WorldPos aVec, WorldPos bVec, float maxlen, List<WorldPos> main, List<WorldPos> side)
	{
		WorldPos worldPos = (point - cur).Normalized * maxlen;
		WorldPos a = cur + worldPos;
		WorldPos normalized = worldPos.Perpendicular().Normalized;
		WorldPos? worldPos2 = FindVecVecIntersection(a, normalized, point, aVec);
		WorldPos? worldPos3 = FindVecVecIntersection(a, normalized, point, bVec);
		if (!worldPos2.HasValue || !worldPos3.HasValue)
		{
			return false;
		}
		if ((cur - worldPos2.Value).MagnitudeSquared >= 400f)
		{
			worldPos2 = cur;
		}
		if ((cur - worldPos3.Value).MagnitudeSquared >= 400f)
		{
			worldPos3 = cur;
		}
		main.Add(cur);
		side.Add(worldPos2.Value);
		main.Add(cur);
		side.Add(worldPos3.Value);
		return true;
	}

	private static WorldPos? FindVecVecIntersection(WorldPos a, WorldPos aVec, WorldPos b, WorldPos bVec)
	{
		return FindLineLineIntersection(a, a + aVec, b, b + bVec);
	}

	private static WorldPos? FindLineLineIntersection(WorldPos a, WorldPos b, WorldPos c, WorldPos d)
	{
		float num = b.y - a.y;
		float num2 = a.x - b.x;
		float num3 = num * a.x + num2 * a.y;
		float num4 = d.y - c.y;
		float num5 = c.x - d.x;
		float num6 = num4 * c.x + num5 * c.y;
		float num7 = num * num5 - num4 * num2;
		if (num7 == 0f)
		{
			return null;
		}
		float x = (num5 * num3 - num2 * num6) / num7;
		float y = (num * num6 - num4 * num3) / num7;
		return new WorldPos(x, y);
	}
}
