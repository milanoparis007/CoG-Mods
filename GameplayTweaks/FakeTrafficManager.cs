using System;
using System.Collections.Generic;
using Game.Core;
using Game.Services.Maps;
using Game.Session.Assets;
using Game.Session.Board;
using Game.Session.Entities;
using UnityEngine;

namespace GameplayTweaks
{
internal sealed class FakeTrafficManager : MonoBehaviour
{
	private const float VisualHeight = 0.05f;

	private const float SpawnRefreshIntervalSeconds = 0.35f;

	private const float SpawnCooldownSeconds = 1.5f;

	private const float MaxDeltaTime = 0.05f;

	private const float MaxPendingDeltaTime = 0.18f;

	private const float ApproxRoadUnitsPerKm = 400f;

	private enum FakeTrafficVehicleClass
	{
		Car,
		Pickup,
		Truck,
		Sports
	}

	private sealed class VehiclePrefabTemplate
	{
		public string Path;

		public FakeTrafficVehicleClass VehicleClass;
	}

	private static readonly VehiclePrefabTemplate[] VehiclePrefabTemplates =
	{
		new VehiclePrefabTemplate
		{
			Path = "Vehicles/Vehicle Car A",
			VehicleClass = FakeTrafficVehicleClass.Car
		},
		new VehiclePrefabTemplate
		{
			Path = "Vehicles/Vehicle Car B",
			VehicleClass = FakeTrafficVehicleClass.Car
		},
		new VehiclePrefabTemplate
		{
			Path = "Vehicles/Vehicle Pickup Truck",
			VehicleClass = FakeTrafficVehicleClass.Pickup
		},
		new VehiclePrefabTemplate
		{
			Path = "Vehicles/Vehicle Truck A",
			VehicleClass = FakeTrafficVehicleClass.Truck
		},
		new VehiclePrefabTemplate
		{
			Path = "Vehicles/Vehicle Sports Car",
			VehicleClass = FakeTrafficVehicleClass.Sports
		}
	};

	private sealed class LoadedVehiclePrefab
	{
		public GameObject Prefab;

		public FakeTrafficVehicleClass VehicleClass;
	};

	private sealed class FakeVehicleView
	{
		public GameObject GameObject;

		public Transform Transform;

		public Vector3 BaseScale;

		public FakeTrafficVehicleClass VehicleClass;
	}

	private sealed class FakeVehicleRecord
	{
		public FakeVehicleView View;

		public int SegmentId;

		public float DistanceAlongSegment;

		public float Speed;

		public int SegmentsRemaining;

		public float Scale;

		public int UpdateBucket;

		public float PendingDeltaTime;

		public WorldPos LastWorldPos;

		public float StopUntilTime;

		public int LastHoldSegmentId = -1;
	}

	private sealed class DirectedRoadSegment
	{
		public int Id;

		public int EdgeId;

		public int StartNodeIndex;

		public int EndNodeIndex;

		public int ReverseSegmentId;

		public WorldPos[] Points;

		public float[] CumulativeLengths;

		public float Length;

		public WorldPos Midpoint;

		public WorldPos StartDirection;

		public WorldPos EndDirection;

		public bool IsDowntown;

		public int StartBranchCount;

		public int EndBranchCount;
	}

	private readonly List<LoadedVehiclePrefab> _loadedPrefabs = new List<LoadedVehiclePrefab>();

	private readonly List<DirectedRoadSegment> _segments = new List<DirectedRoadSegment>();

	private readonly Dictionary<int, List<int>> _segmentsByStartNode = new Dictionary<int, List<int>>();

	private readonly List<FakeVehicleView> _pool = new List<FakeVehicleView>();

	private readonly List<FakeVehicleView> _availableViews = new List<FakeVehicleView>();

	private readonly List<FakeVehicleRecord> _activeVehicles = new List<FakeVehicleRecord>();

	private readonly List<int> _spawnCandidates = new List<int>();

	private readonly HashSet<int> _occupiedSegments = new HashSet<int>();

	private readonly Dictionary<int, float> _segmentCooldownUntil = new Dictionary<int, float>();

	private readonly Dictionary<int, float> _edgeLengthByEdgeId = new Dictionary<int, float>();

	private readonly Dictionary<int, Node> _nodesById = new Dictionary<int, Node>();

	private readonly HashSet<int> _uniqueVisibleEdges = new HashSet<int>();

	private readonly System.Random _rng = new System.Random();

	private Transform _root;

	private int _nextPrefabTemplateIndex;

	private object _nodesRef;

	private object _edgesRef;

	private int _nodeCount = -1;

	private int _edgeCount = -1;

	private float _nextSpawnRefreshTime;

	private int _frameIndex;

	private bool _loggedMissingPrefabs;

	private int _graphRebuildCount;

	private int _spawnedSinceLog;

	private int _releasedSinceLog;

	private int _intersectionHoldsSinceLog;

	private int _lastDesiredVisibleCount;

	private float _lastVisibleRoadKm;

	private float _lastDiagnosticsLogTime;

	private void Awake()
	{
		if ((UnityEngine.Object)(object)_root == (UnityEngine.Object)null)
		{
			GameObject gameObject = new GameObject("GameplayTweaks Fake Traffic");
			gameObject.layer = 2;
			_root = gameObject.transform;
			_root.SetParent(base.transform, false);
		}
	}

	private void Update()
	{
		Tick();
	}

	private void OnDestroy()
	{
		DeactivateAllVehicles();
		for (int i = 0; i < _pool.Count; i++)
		{
			if ((UnityEngine.Object)(object)_pool[i].GameObject != (UnityEngine.Object)null)
			{
				Destroy(_pool[i].GameObject);
			}
		}

		_pool.Clear();
		_availableViews.Clear();
		_loadedPrefabs.Clear();
		if ((UnityEngine.Object)(object)_root != (UnityEngine.Object)null)
		{
			Destroy(_root.gameObject);
			_root = null;
		}
	}

	private void Tick()
	{
		if (!GameplayTweaksPlugin.ShouldRunFakeTraffic())
		{
			DeactivateAllVehicles();
			return;
		}

		EnsurePrefabTemplates();
		if (_loadedPrefabs.Count == 0)
		{
			DeactivateAllVehicles();
			return;
		}

		if (!EnsureRoadGraphCurrent())
		{
			DeactivateAllVehicles();
			return;
		}

		int maxVisible = Mathf.Max(0, GameplayTweaksPlugin.FakeTrafficMaxVisible?.Value ?? 0);
		if (maxVisible <= 0)
		{
			DeactivateAllVehicles();
			return;
		}

		EnsurePoolSize(maxVisible);

		WorldPos cameraCenter = Game.Game.serv.camera.ScreenCenterToWorldPos();
		float deltaTime = Mathf.Min(Time.deltaTime, MaxDeltaTime);
		_frameIndex++;
		UpdateActiveVehicles(cameraCenter, deltaTime);

		if (Time.unscaledTime >= _nextSpawnRefreshTime)
		{
			RefreshSpawnCandidates(cameraCenter);
			_nextSpawnRefreshTime = Time.unscaledTime + SpawnRefreshIntervalSeconds;
		}

		SpawnVehicles(cameraCenter, maxVisible);
	}

	private void EnsurePrefabTemplates()
	{
		if (_loadedPrefabs.Count > 0)
		{
			return;
		}

		for (int i = 0; i < VehiclePrefabTemplates.Length; i++)
		{
			VehiclePrefabTemplate vehiclePrefabTemplate = VehiclePrefabTemplates[i];
			GameObject gameObject = Resources.Load<GameObject>(vehiclePrefabTemplate.Path);
			if ((UnityEngine.Object)(object)gameObject != (UnityEngine.Object)null)
			{
				_loadedPrefabs.Add(new LoadedVehiclePrefab
				{
					Prefab = gameObject,
					VehicleClass = vehiclePrefabTemplate.VehicleClass
				});
			}
		}

		if (_loadedPrefabs.Count == 0 && !_loggedMissingPrefabs)
		{
			_loggedMissingPrefabs = true;
			GameplayTweaksPlugin.VerificationLog("FakeTraffic", "no vehicle prefabs were found in Resources; fake traffic visuals disabled");
		}
	}

	private bool EnsureRoadGraphCurrent()
	{
		if (Game.Game.ctx?.board?.nodes == null)
		{
			return false;
		}

		List<Node> allNodesUnsafe = Game.Game.ctx.board.nodes.GetAllNodesUnsafe();
		List<NodeEdge> allEdgesUnsafe = Game.Game.ctx.board.nodes.GetAllEdgesUnsafe();
		if (allNodesUnsafe == null || allEdgesUnsafe == null)
		{
			return false;
		}

		if (ReferenceEquals(_nodesRef, allNodesUnsafe) && ReferenceEquals(_edgesRef, allEdgesUnsafe) && _nodeCount == allNodesUnsafe.Count && _edgeCount == allEdgesUnsafe.Count)
		{
			return _segments.Count > 0;
		}

		_nodesRef = allNodesUnsafe;
		_edgesRef = allEdgesUnsafe;
		_nodeCount = allNodesUnsafe.Count;
		_edgeCount = allEdgesUnsafe.Count;

		BuildRoadGraph(allNodesUnsafe, allEdgesUnsafe);
		DeactivateAllVehicles();
		_segmentCooldownUntil.Clear();
		_nextSpawnRefreshTime = 0f;
		_graphRebuildCount++;
		GameplayTweaksPlugin.VerificationLog("FakeTraffic", $"road graph rebuilt nodes={_nodeCount} edges={_edgeCount} directedSegments={_segments.Count}");
		return _segments.Count > 0;
	}

	private void BuildRoadGraph(List<Node> nodes, List<NodeEdge> edges)
	{
		_segments.Clear();
		_segmentsByStartNode.Clear();
		_edgeLengthByEdgeId.Clear();
		_nodesById.Clear();
		for (int i = 0; i < nodes.Count; i++)
		{
			Node node = nodes[i];
			if (node != null && node.IsValid)
			{
				_nodesById[node.id.index] = node;
			}
		}

		for (int i = 0; i < edges.Count; i++)
		{
			NodeEdge nodeEdge = edges[i];
			if (nodeEdge == null || !nodeEdge.IsRoad)
			{
				continue;
			}

			Node node = nodeEdge.a.FindNode();
			Node node2 = nodeEdge.b.FindNode();
			if (node == null || node2 == null || !node.HasRoad || !node2.HasRoad)
			{
				continue;
			}

			List<WorldPos> orientedPoints = BuildOrientedPoints(nodeEdge, node, node2);
			DirectedRoadSegment directedRoadSegment = TryCreateSegment(nodeEdge.neid.index, node.id.index, node2.id.index, orientedPoints);
			if (directedRoadSegment == null)
			{
				continue;
			}

			orientedPoints.Reverse();
			DirectedRoadSegment directedRoadSegment2 = TryCreateSegment(nodeEdge.neid.index, node2.id.index, node.id.index, orientedPoints);
			if (directedRoadSegment2 == null)
			{
				continue;
			}

			directedRoadSegment.Id = _segments.Count;
			directedRoadSegment2.Id = _segments.Count + 1;
			directedRoadSegment.ReverseSegmentId = directedRoadSegment2.Id;
			directedRoadSegment2.ReverseSegmentId = directedRoadSegment.Id;
			_segments.Add(directedRoadSegment);
			_segments.Add(directedRoadSegment2);
			AddSegmentIndex(directedRoadSegment.StartNodeIndex, directedRoadSegment.Id);
			AddSegmentIndex(directedRoadSegment2.StartNodeIndex, directedRoadSegment2.Id);
			if (!_edgeLengthByEdgeId.ContainsKey(nodeEdge.neid.index))
			{
				_edgeLengthByEdgeId[nodeEdge.neid.index] = directedRoadSegment.Length;
			}
		}

		for (int j = 0; j < _segments.Count; j++)
		{
			DirectedRoadSegment directedRoadSegment3 = _segments[j];
			directedRoadSegment3.StartBranchCount = GetBranchCount(directedRoadSegment3.StartNodeIndex);
			directedRoadSegment3.EndBranchCount = GetBranchCount(directedRoadSegment3.EndNodeIndex);
			directedRoadSegment3.IsDowntown = IsDowntownNode(directedRoadSegment3.StartNodeIndex) || IsDowntownNode(directedRoadSegment3.EndNodeIndex);
		}
	}

	private static List<WorldPos> BuildOrientedPoints(NodeEdge edge, Node start, Node end)
	{
		List<WorldPos> list = new List<WorldPos>(edge.roadBeads.Count + 2);
		list.Add(start.pos);
		if (edge.roadBeads != null && edge.roadBeads.Count > 0)
		{
			int num = 0;
			int num2 = edge.roadBeads.Count - 1;
			if ((start.pos - edge.roadBeads[num].pos).MagnitudeSquared <= (start.pos - edge.roadBeads[num2].pos).MagnitudeSquared)
			{
				for (int i = 0; i < edge.roadBeads.Count; i++)
				{
					list.Add(edge.roadBeads[i].pos);
				}
			}
			else
			{
				for (int j = edge.roadBeads.Count - 1; j >= 0; j--)
				{
					list.Add(edge.roadBeads[j].pos);
				}
			}
		}

		list.Add(end.pos);
		return list;
	}

	private DirectedRoadSegment TryCreateSegment(int edgeId, int startNodeIndex, int endNodeIndex, List<WorldPos> points)
	{
		if (points == null || points.Count < 2)
		{
			return null;
		}

		List<WorldPos> list = new List<WorldPos>(points.Count);
		for (int i = 0; i < points.Count; i++)
		{
			if (list.Count == 0 || !list[list.Count - 1].EqualsEpsilon(points[i], 0.01f))
			{
				list.Add(points[i]);
			}
		}

		if (list.Count < 2)
		{
			return null;
		}

		float[] array = new float[list.Count];
		float num = 0f;
		for (int j = 1; j < list.Count; j++)
		{
			float magnitude = (list[j] - list[j - 1]).Magnitude;
			if (magnitude <= 0.01f)
			{
				continue;
			}

			num += magnitude;
			array[j] = num;
		}

		if (num <= 0.25f)
		{
			return null;
		}

		DirectedRoadSegment directedRoadSegment = new DirectedRoadSegment
		{
			EdgeId = edgeId,
			StartNodeIndex = startNodeIndex,
			EndNodeIndex = endNodeIndex,
			Points = list.ToArray(),
			CumulativeLengths = array,
			Length = num
		};
		directedRoadSegment.Midpoint = EvaluatePoint(directedRoadSegment, num * 0.5f);
		directedRoadSegment.StartDirection = FindDirection(directedRoadSegment.Points, 0, 1);
		directedRoadSegment.EndDirection = FindDirection(directedRoadSegment.Points, directedRoadSegment.Points.Length - 2, directedRoadSegment.Points.Length - 1);
		return directedRoadSegment;
	}

	private static WorldPos FindDirection(WorldPos[] points, int fromIndex, int toIndex)
	{
		if (points == null || points.Length == 0)
		{
			return WorldPos.NORTH;
		}

		fromIndex = Mathf.Clamp(fromIndex, 0, points.Length - 1);
		toIndex = Mathf.Clamp(toIndex, 0, points.Length - 1);
		WorldPos worldPos = points[toIndex] - points[fromIndex];
		if (worldPos.MagnitudeSquared <= 0.0001f)
		{
			return WorldPos.NORTH;
		}

		return worldPos.Normalized;
	}

	private void AddSegmentIndex(int startNodeIndex, int segmentId)
	{
		List<int> value;
		if (!_segmentsByStartNode.TryGetValue(startNodeIndex, out value))
		{
			value = new List<int>();
			_segmentsByStartNode.Add(startNodeIndex, value);
		}

		value.Add(segmentId);
	}

	private int GetBranchCount(int nodeIndex)
	{
		List<int> value;
		if (_segmentsByStartNode.TryGetValue(nodeIndex, out value) && value != null)
		{
			return value.Count;
		}

		return 0;
	}

	private bool IsDowntownNode(int nodeIndex)
	{
		Node value;
		if (_nodesById.TryGetValue(nodeIndex, out value) && value != null)
		{
			return value.IsInMatchingDistrict(DistrictConfig.TAG_DOWNTOWN);
		}

		return false;
	}

	private void EnsurePoolSize(int maxVisible)
	{
		while (_pool.Count < maxVisible)
		{
			FakeVehicleView fakeVehicleView = CreateView();
			if (fakeVehicleView == null)
			{
				return;
			}

			_pool.Add(fakeVehicleView);
			_availableViews.Add(fakeVehicleView);
		}
	}

	private FakeVehicleView CreateView()
	{
		if (_loadedPrefabs.Count == 0 || (UnityEngine.Object)(object)_root == (UnityEngine.Object)null)
		{
			return null;
		}

		LoadedVehiclePrefab loadedVehiclePrefab = _loadedPrefabs[_nextPrefabTemplateIndex % _loadedPrefabs.Count];
		_nextPrefabTemplateIndex++;
		GameObject gameObject = Instantiate(loadedVehiclePrefab.Prefab, _root);
		gameObject.name = "FakeTrafficVehicle";
		PrepareVisual(gameObject);
		gameObject.SetActive(false);
		return new FakeVehicleView
		{
			GameObject = gameObject,
			Transform = gameObject.transform,
			BaseScale = gameObject.transform.localScale,
			VehicleClass = loadedVehiclePrefab.VehicleClass
		};
	}

	private static void PrepareVisual(GameObject gameObject)
	{
		Transform[] componentsInChildren = gameObject.GetComponentsInChildren<Transform>(true);
		for (int i = 0; i < componentsInChildren.Length; i++)
		{
			componentsInChildren[i].gameObject.layer = 2;
		}

		Component[] componentsInChildren2 = gameObject.GetComponentsInChildren<Component>(true);
		for (int j = 0; j < componentsInChildren2.Length; j++)
		{
			Component component = componentsInChildren2[j];
			if ((UnityEngine.Object)(object)component == (UnityEngine.Object)null)
			{
				continue;
			}

			string name = component.GetType().Name;
			if (component is ModelProxyContainerComponent || string.Equals(name, "Collider", StringComparison.Ordinal) || string.Equals(name, "MeshCollider", StringComparison.Ordinal) || string.Equals(name, "BoxCollider", StringComparison.Ordinal) || string.Equals(name, "SphereCollider", StringComparison.Ordinal) || string.Equals(name, "CapsuleCollider", StringComparison.Ordinal) || string.Equals(name, "Rigidbody", StringComparison.Ordinal))
			{
				UnityEngine.Object.Destroy(component);
			}
			else if (string.Equals(name, "AudioSource", StringComparison.Ordinal))
			{
				Behaviour behaviour = component as Behaviour;
				if ((UnityEngine.Object)(object)behaviour != (UnityEngine.Object)null)
				{
					behaviour.enabled = false;
				}
			}
		}
	}

	private FakeVehicleView TakeAvailableViewForSegment(DirectedRoadSegment segment)
	{
		if (_availableViews.Count <= 0)
		{
			return null;
		}

		if (segment == null)
		{
			FakeVehicleView fakeVehicleView = _availableViews[_availableViews.Count - 1];
			_availableViews.RemoveAt(_availableViews.Count - 1);
			return fakeVehicleView;
		}

		float num = 0f;
		for (int i = 0; i < _availableViews.Count; i++)
		{
			num += GetVehicleClassWeight(_availableViews[i].VehicleClass, segment);
		}

		if (num <= 0.001f)
		{
			FakeVehicleView fakeVehicleView2 = _availableViews[_availableViews.Count - 1];
			_availableViews.RemoveAt(_availableViews.Count - 1);
			return fakeVehicleView2;
		}

		float num2 = (float)_rng.NextDouble() * num;
		for (int j = 0; j < _availableViews.Count; j++)
		{
			FakeVehicleView fakeVehicleView3 = _availableViews[j];
			num2 -= GetVehicleClassWeight(fakeVehicleView3.VehicleClass, segment);
			if (num2 <= 0f)
			{
				_availableViews.RemoveAt(j);
				return fakeVehicleView3;
			}
		}

		FakeVehicleView fakeVehicleView4 = _availableViews[_availableViews.Count - 1];
		_availableViews.RemoveAt(_availableViews.Count - 1);
		return fakeVehicleView4;
	}

	private static float GetVehicleClassWeight(FakeTrafficVehicleClass vehicleClass, DirectedRoadSegment segment)
	{
		float num = segment.IsDowntown ? 1.25f : 0.9f;
		switch (vehicleClass)
		{
		case FakeTrafficVehicleClass.Car:
			return (segment.EndBranchCount >= 3) ? (1.2f * num) : (1.05f * num);
		case FakeTrafficVehicleClass.Pickup:
			return segment.IsDowntown ? 0.7f : 1.15f;
		case FakeTrafficVehicleClass.Truck:
			return segment.IsDowntown ? 0.45f : 1.3f;
		case FakeTrafficVehicleClass.Sports:
			return segment.IsDowntown ? 1.65f : 0.65f;
		default:
			return 1f;
		}
	}

	private void RefreshSpawnCandidates(WorldPos cameraCenter)
	{
		_spawnCandidates.Clear();
		_occupiedSegments.Clear();
		float suppressRadius = Mathf.Max(0f, GameplayTweaksPlugin.FakeTrafficSelectionSuppressRadius?.Value ?? 28f);
		float suppressRadiusSquared = suppressRadius * suppressRadius;
		bool hasActiveSuppressPos = TryResolveSelectionSuppressPos(Game.Game.ctx?.selection?.CurrentActive, out WorldPos activeSuppressPos);
		bool hasFocusSuppressPos = TryResolveSelectionSuppressPos(Game.Game.ctx?.selection?.CurrentFocus, out WorldPos focusSuppressPos);
		for (int i = 0; i < _activeVehicles.Count; i++)
		{
			_occupiedSegments.Add(_activeVehicles[i].SegmentId);
		}

		float num = Mathf.Max(10f, GameplayTweaksPlugin.FakeTrafficInnerRingRadius?.Value ?? 95f);
		float num2 = Mathf.Max(num + 20f, GameplayTweaksPlugin.FakeTrafficMiddleRingRadius?.Value ?? 155f);
		float num3 = num * num;
		float num4 = num2 * num2;
		float unscaledTime = Time.unscaledTime;
		for (int j = 0; j < _segments.Count; j++)
		{
			DirectedRoadSegment directedRoadSegment = _segments[j];
			if (_occupiedSegments.Contains(directedRoadSegment.Id))
			{
				continue;
			}

			float value;
			if (_segmentCooldownUntil.TryGetValue(directedRoadSegment.Id, out value) && value > unscaledTime)
			{
				continue;
			}

			float magnitudeSquared = (directedRoadSegment.Midpoint - cameraCenter).MagnitudeSquared;
			if (magnitudeSquared > num4)
			{
				continue;
			}

			if (IsSuppressedBySelection(directedRoadSegment.Midpoint, hasActiveSuppressPos, activeSuppressPos, hasFocusSuppressPos, focusSuppressPos, suppressRadiusSquared))
			{
				continue;
			}

			bool flag = magnitudeSquared <= num3;
			if (flag || IsSpawnScreenSafe(directedRoadSegment.Midpoint))
			{
				_spawnCandidates.Add(directedRoadSegment.Id);
			}
		}

		if (_spawnCandidates.Count > 0)
		{
			return;
		}

		for (int k = 0; k < _segments.Count; k++)
		{
			DirectedRoadSegment directedRoadSegment2 = _segments[k];
			if (_occupiedSegments.Contains(directedRoadSegment2.Id))
			{
				continue;
			}

			if ((directedRoadSegment2.Midpoint - cameraCenter).MagnitudeSquared > num4)
			{
				continue;
			}

			if (IsSuppressedBySelection(directedRoadSegment2.Midpoint, hasActiveSuppressPos, activeSuppressPos, hasFocusSuppressPos, focusSuppressPos, suppressRadiusSquared))
			{
				continue;
			}

			if ((directedRoadSegment2.Midpoint - cameraCenter).MagnitudeSquared <= num4)
			{
				_spawnCandidates.Add(directedRoadSegment2.Id);
			}
		}
	}

	private bool IsSpawnScreenSafe(WorldPos worldPos)
	{
		Vector2 vector = Game.Game.serv.camera.WorldToScreenPos(worldPos, VisualHeight);
		float width = Screen.width;
		float height = Screen.height;
		const float num = 96f;
		if (vector.x < 0f - num || vector.x > width + num || vector.y < 0f - num || vector.y > height + num)
		{
			return true;
		}

		float num2 = width * 0.15f;
		float num3 = width * 0.85f;
		float num4 = height * 0.15f;
		float num5 = height * 0.85f;
		return vector.x <= num2 || vector.x >= num3 || vector.y <= num4 || vector.y >= num5;
	}

	private void SpawnVehicles(WorldPos cameraCenter, int maxVisible)
	{
		int num = DesiredVisibleCount(cameraCenter, maxVisible);
		while (_activeVehicles.Count < num && _availableViews.Count > 0 && _spawnCandidates.Count > 0)
		{
			int num2 = TakeWeightedSpawnCandidate(cameraCenter);
			if (num2 < 0)
			{
				break;
			}

			int index = _spawnCandidates.IndexOf(num2);
			if (index >= 0)
			{
				_spawnCandidates[index] = _spawnCandidates[_spawnCandidates.Count - 1];
				_spawnCandidates.RemoveAt(_spawnCandidates.Count - 1);
			}
			if (num2 >= 0 && num2 < _segments.Count)
			{
				FakeVehicleRecord fakeVehicleRecord = SpawnVehicleOnSegment(_segments[num2]);
				if (fakeVehicleRecord != null)
				{
					MaybeSpawnPlatoon(_segments[num2], fakeVehicleRecord, num);
				}
			}
		}

		MaybeLogDiagnostics(cameraCenter, maxVisible);
	}

	private int DesiredVisibleCount(WorldPos cameraCenter, int maxVisible)
	{
		_uniqueVisibleEdges.Clear();
		float num = Mathf.Max(10f, GameplayTweaksPlugin.FakeTrafficMiddleRingRadius?.Value ?? GameplayTweaksPlugin.FakeTrafficInnerRingRadius?.Value ?? 95f);
		float num2 = num * num;
		float num3 = 0f;
		float num4 = Mathf.Max(1f, GameplayTweaksPlugin.FakeTrafficDowntownBias?.Value ?? 2.2f);
		for (int i = 0; i < _segments.Count; i++)
		{
			DirectedRoadSegment directedRoadSegment = _segments[i];
			if ((directedRoadSegment.Midpoint - cameraCenter).MagnitudeSquared > num2 || !_uniqueVisibleEdges.Add(directedRoadSegment.EdgeId))
			{
				continue;
			}

			if (_edgeLengthByEdgeId.TryGetValue(directedRoadSegment.EdgeId, out float value))
			{
				float num5 = directedRoadSegment.IsDowntown ? Mathf.Lerp(1f, num4, 0.55f) : 1f;
				num3 += value * num5;
			}
		}

		if (_uniqueVisibleEdges.Count <= 0 || num3 <= 0.25f)
		{
			_lastVisibleRoadKm = 0f;
			_lastDesiredVisibleCount = 0;
			return 0;
		}

		float num6 = Mathf.Max(0.25f, GameplayTweaksPlugin.FakeTrafficSpawnPerRoadKm?.Value ?? 4.5f);
		float num7 = num3 / ApproxRoadUnitsPerKm;
		int num8 = Mathf.CeilToInt(num7 * num6);
		int minVisible = Mathf.Min(_uniqueVisibleEdges.Count >= 6 ? 4 : 2, maxVisible);
		_lastVisibleRoadKm = num7;
		_lastDesiredVisibleCount = Mathf.Clamp(Mathf.Max(num8, minVisible), 0, maxVisible);
		return _lastDesiredVisibleCount;
	}

	private FakeVehicleRecord SpawnVehicleOnSegment(DirectedRoadSegment segment, float? forcedDistance = null, float? forcedSpeed = null, int? forcedSegmentsRemaining = null)
	{
		FakeVehicleView fakeVehicleView = TakeAvailableViewForSegment(segment);
		if (fakeVehicleView == null)
		{
			return null;
		}

		float num = Mathf.Min(GameplayTweaksPlugin.FakeTrafficSpeedMin?.Value ?? 4.5f, GameplayTweaksPlugin.FakeTrafficSpeedMax?.Value ?? 7.5f);
		float num2 = Mathf.Max(GameplayTweaksPlugin.FakeTrafficSpeedMin?.Value ?? 4.5f, GameplayTweaksPlugin.FakeTrafficSpeedMax?.Value ?? 7.5f);
		FakeVehicleRecord fakeVehicleRecord = new FakeVehicleRecord
		{
			View = fakeVehicleView,
			SegmentId = segment.Id,
			DistanceAlongSegment = Mathf.Clamp(forcedDistance ?? ((float)_rng.NextDouble() * segment.Length), 0f, Mathf.Max(0.25f, segment.Length - 0.1f)),
			Speed = Mathf.Clamp(forcedSpeed ?? Mathf.Lerp(num, num2, (float)_rng.NextDouble()), num, num2),
			SegmentsRemaining = forcedSegmentsRemaining ?? _rng.Next(5, 13),
			Scale = Mathf.Lerp(0.92f, 1.08f, (float)_rng.NextDouble()),
			UpdateBucket = _rng.Next(0, Mathf.Max(1, GameplayTweaksPlugin.FakeTrafficUpdateBuckets?.Value ?? 3))
		};
		fakeVehicleView.GameObject.SetActive(true);
		fakeVehicleRecord.LastWorldPos = ApplyPose(fakeVehicleRecord, segment, fakeVehicleRecord.DistanceAlongSegment);
		_activeVehicles.Add(fakeVehicleRecord);
		_segmentCooldownUntil[segment.Id] = Time.unscaledTime + SpawnCooldownSeconds;
		_spawnedSinceLog++;
		return fakeVehicleRecord;
	}

	private void UpdateActiveVehicles(WorldPos cameraCenter, float deltaTime)
	{
		float num = Mathf.Max(10f, GameplayTweaksPlugin.FakeTrafficInnerRingRadius?.Value ?? 95f);
		float num2 = Mathf.Max(num + 20f, GameplayTweaksPlugin.FakeTrafficMiddleRingRadius?.Value ?? 155f);
		float num3 = Mathf.Max(GameplayTweaksPlugin.FakeTrafficDespawnRadius?.Value ?? 130f, num2 + 12f);
		float num4 = num * num;
		float num5 = num2 * num2;
		float num6 = num3 * num3;
		int num7 = Mathf.Max(1, GameplayTweaksPlugin.FakeTrafficUpdateBuckets?.Value ?? 3);
		for (int i = _activeVehicles.Count - 1; i >= 0; i--)
		{
			FakeVehicleRecord fakeVehicleRecord = _activeVehicles[i];
			if (fakeVehicleRecord.View == null || (UnityEngine.Object)(object)fakeVehicleRecord.View.GameObject == (UnityEngine.Object)null || fakeVehicleRecord.SegmentId < 0 || fakeVehicleRecord.SegmentId >= _segments.Count)
			{
				ReleaseVehicleAt(i);
				continue;
			}

			WorldPos worldPos = fakeVehicleRecord.LastWorldPos;
			if (worldPos.IsZero)
			{
				worldPos = _segments[fakeVehicleRecord.SegmentId].Midpoint;
			}

			float magnitudeSquared = (worldPos - cameraCenter).MagnitudeSquared;
			if (magnitudeSquared > num6)
			{
				ReleaseVehicleAt(i);
				continue;
			}

			fakeVehicleRecord.PendingDeltaTime = Mathf.Min(MaxPendingDeltaTime, fakeVehicleRecord.PendingDeltaTime + deltaTime);
			int num8 = magnitudeSquared <= num4 ? 1 : ((magnitudeSquared <= num5) ? num7 : (num7 + 1));
			if (!ShouldUpdateThisFrame(fakeVehicleRecord.UpdateBucket, num8))
			{
				continue;
			}

			if (!AdvanceVehicle(fakeVehicleRecord, fakeVehicleRecord.PendingDeltaTime))
			{
				ReleaseVehicleAt(i);
				continue;
			}

			fakeVehicleRecord.PendingDeltaTime = 0f;
			if ((fakeVehicleRecord.LastWorldPos - cameraCenter).MagnitudeSquared > num6)
			{
				ReleaseVehicleAt(i);
			}
		}
	}

	private bool AdvanceVehicle(FakeVehicleRecord vehicle, float deltaTime)
	{
		if (vehicle == null || deltaTime <= 0f || vehicle.SegmentId < 0 || vehicle.SegmentId >= _segments.Count)
		{
			return false;
		}

		DirectedRoadSegment directedRoadSegment = _segments[vehicle.SegmentId];
		vehicle.LastWorldPos = ApplyPose(vehicle, directedRoadSegment, vehicle.DistanceAlongSegment);
		float unscaledTime = Time.unscaledTime;
		if (vehicle.StopUntilTime > unscaledTime)
		{
			return true;
		}

		float num = Mathf.Max(3.5f, GameplayTweaksPlugin.FakeTrafficQueueSpacing?.Value ?? 6.5f);
		float num2 = FindLeadGapOnSegment(vehicle);
		float num3 = GetStopLineDistance(directedRoadSegment, num);
		float unscaledTime2 = Time.unscaledTime;
		bool flag = vehicle.StopUntilTime > unscaledTime2;
		bool flag2 = flag || ShouldStartIntersectionHold(vehicle, directedRoadSegment, num2, num3);
		if (num2 < num * 0.85f && !flag2)
		{
			return true;
		}

		float num4 = vehicle.Speed * deltaTime;
		if (num2 < float.MaxValue)
		{
			num4 = Mathf.Min(num4, Mathf.Max(0f, num2 - num));
		}

		if (flag2)
		{
			float num5 = Mathf.Max(0f, num3 - vehicle.DistanceAlongSegment);
			if (num5 <= 0.05f)
			{
				vehicle.DistanceAlongSegment = Mathf.Min(vehicle.DistanceAlongSegment, num3);
				if (!flag)
				{
					BeginIntersectionHold(vehicle, directedRoadSegment, unscaledTime2);
				}

				vehicle.LastWorldPos = ApplyPose(vehicle, directedRoadSegment, vehicle.DistanceAlongSegment);
				return true;
			}

			num4 = Mathf.Min(num4, num5);
		}

		if (num4 <= 0.02f)
		{
			vehicle.LastWorldPos = ApplyPose(vehicle, directedRoadSegment, vehicle.DistanceAlongSegment);
			return true;
		}

		vehicle.DistanceAlongSegment += num4;
		if (flag2 && vehicle.DistanceAlongSegment >= num3 - 0.05f)
		{
			vehicle.DistanceAlongSegment = Mathf.Min(vehicle.DistanceAlongSegment, num3);
			if (!flag)
			{
				BeginIntersectionHold(vehicle, directedRoadSegment, unscaledTime2);
			}

			vehicle.LastWorldPos = ApplyPose(vehicle, directedRoadSegment, vehicle.DistanceAlongSegment);
			return true;
		}

		while (vehicle.DistanceAlongSegment >= directedRoadSegment.Length)
		{
			vehicle.DistanceAlongSegment -= directedRoadSegment.Length;
			vehicle.SegmentsRemaining--;
			if (vehicle.SegmentsRemaining <= 0 || !TryAdvanceSegment(vehicle, directedRoadSegment))
			{
				return false;
			}

			directedRoadSegment = _segments[vehicle.SegmentId];
		}

		vehicle.LastWorldPos = ApplyPose(vehicle, directedRoadSegment, vehicle.DistanceAlongSegment);
		return true;
	}

	private bool ShouldUpdateThisFrame(int bucket, int stride)
	{
		if (stride <= 1)
		{
			return true;
		}

		return (_frameIndex + bucket) % stride == 0;
	}

	private bool TryAdvanceSegment(FakeVehicleRecord vehicle, DirectedRoadSegment previousSegment)
	{
		List<int> value;
		if (!_segmentsByStartNode.TryGetValue(previousSegment.EndNodeIndex, out value) || value == null || value.Count == 0)
		{
			return false;
		}

		int num = -1;
		float num2 = 0f;
		for (int i = 0; i < value.Count; i++)
		{
			int num3 = value[i];
			if (num3 < 0 || num3 >= _segments.Count)
			{
				continue;
			}

			DirectedRoadSegment directedRoadSegment = _segments[num3];
			float num4 = 0.2f + (WorldPos.Dot(previousSegment.EndDirection, directedRoadSegment.StartDirection) + 1f) * 0.5f;
			if (directedRoadSegment.Id == previousSegment.ReverseSegmentId && value.Count > 1)
			{
				num4 *= 0.15f;
			}

			float num5 = (float)_rng.NextDouble() * num4;
			if (!(num5 > num2))
			{
				continue;
			}

			num2 = num5;
			num = num3;
		}

		if (num < 0)
		{
			return false;
		}

		vehicle.SegmentId = num;
		return true;
	}

	private WorldPos ApplyPose(FakeVehicleRecord vehicle, DirectedRoadSegment segment, float distance)
	{
		WorldPos worldPos = EvaluatePoint(segment, distance);
		WorldPos directionAt = EvaluateDirectionAt(segment, distance);
		if (directionAt.MagnitudeSquared <= 0.0001f)
		{
			directionAt = segment.EndDirection;
		}

		float num = WorldPos.GetFacingDegrees(worldPos, worldPos + directionAt) ?? 0f;
		vehicle.View.Transform.position = Game.Game.serv.camera.WorldToSceneVector(worldPos, VisualHeight);
		vehicle.View.Transform.rotation = Quaternion.Euler(0f, num, 0f);
		vehicle.View.Transform.localScale = vehicle.View.BaseScale * vehicle.Scale;
		return worldPos;
	}

	private static WorldPos EvaluatePoint(DirectedRoadSegment segment, float distance)
	{
		if (segment.Points.Length == 0)
		{
			return default(WorldPos);
		}

		if (distance <= 0f)
		{
			return segment.Points[0];
		}

		if (distance >= segment.Length)
		{
			return segment.Points[segment.Points.Length - 1];
		}

		for (int i = 1; i < segment.CumulativeLengths.Length; i++)
		{
			float num = segment.CumulativeLengths[i];
			if (distance > num)
			{
				continue;
			}

			float num2 = segment.CumulativeLengths[i - 1];
			float num3 = Mathf.Max(0.0001f, num - num2);
			float t = Mathf.Clamp01((distance - num2) / num3);
			return WorldPos.Lerp(segment.Points[i - 1], segment.Points[i], t);
		}

		return segment.Points[segment.Points.Length - 1];
	}

	private static WorldPos EvaluateDirectionAt(DirectedRoadSegment segment, float distance)
	{
		if (segment.Points.Length < 2)
		{
			return WorldPos.NORTH;
		}

		if (distance <= 0f)
		{
			return segment.StartDirection;
		}

		if (distance >= segment.Length)
		{
			return segment.EndDirection;
		}

		for (int i = 1; i < segment.CumulativeLengths.Length; i++)
		{
			if (distance <= segment.CumulativeLengths[i])
			{
				WorldPos worldPos = segment.Points[i] - segment.Points[i - 1];
				if (worldPos.MagnitudeSquared > 0.0001f)
				{
					return worldPos.Normalized;
				}

				break;
			}
		}

		return segment.EndDirection;
	}

	private void ReleaseVehicleAt(int index)
	{
		FakeVehicleRecord fakeVehicleRecord = _activeVehicles[index];
		if (fakeVehicleRecord?.View != null && (UnityEngine.Object)(object)fakeVehicleRecord.View.GameObject != (UnityEngine.Object)null)
		{
			fakeVehicleRecord.View.GameObject.SetActive(false);
			_availableViews.Add(fakeVehicleRecord.View);
		}

		_activeVehicles.RemoveAt(index);
		_releasedSinceLog++;
	}

	private static bool IsSuppressedBySelection(WorldPos midpoint, bool hasActiveSuppressPos, WorldPos activeSuppressPos, bool hasFocusSuppressPos, WorldPos focusSuppressPos, float suppressRadiusSquared)
	{
		if (suppressRadiusSquared <= 0f)
		{
			return false;
		}

		if (hasActiveSuppressPos && (midpoint - activeSuppressPos).MagnitudeSquared <= suppressRadiusSquared)
		{
			return true;
		}

		if (hasFocusSuppressPos && (midpoint - focusSuppressPos).MagnitudeSquared <= suppressRadiusSquared)
		{
			return true;
		}

		return false;
	}

	private int TakeWeightedSpawnCandidate(WorldPos cameraCenter)
	{
		if (_spawnCandidates.Count <= 0)
		{
			return -1;
		}

		float num = 0f;
		for (int i = 0; i < _spawnCandidates.Count; i++)
		{
			int num2 = _spawnCandidates[i];
			if (num2 >= 0 && num2 < _segments.Count)
			{
				num += GetSpawnWeight(_segments[num2], cameraCenter);
			}
		}

		if (num <= 0.001f)
		{
			return _spawnCandidates[_rng.Next(_spawnCandidates.Count)];
		}

		float num3 = (float)_rng.NextDouble() * num;
		for (int j = 0; j < _spawnCandidates.Count; j++)
		{
			int num4 = _spawnCandidates[j];
			if (num4 < 0 || num4 >= _segments.Count)
			{
				continue;
			}

			num3 -= GetSpawnWeight(_segments[num4], cameraCenter);
			if (num3 <= 0f)
			{
				return num4;
			}
		}

		return _spawnCandidates[_spawnCandidates.Count - 1];
	}

	private float GetSpawnWeight(DirectedRoadSegment segment, WorldPos cameraCenter)
	{
		float num = Mathf.Max(10f, GameplayTweaksPlugin.FakeTrafficInnerRingRadius?.Value ?? 95f);
		float num2 = Mathf.Max(num + 20f, GameplayTweaksPlugin.FakeTrafficMiddleRingRadius?.Value ?? 155f);
		float magnitude = (segment.Midpoint - cameraCenter).Magnitude;
		float num3 = 1f - Mathf.Clamp01((magnitude - num) / Mathf.Max(1f, num2 - num));
		float num4 = 0.75f + num3 * 0.55f;
		float num5 = Mathf.Max(1f, GameplayTweaksPlugin.FakeTrafficDowntownBias?.Value ?? 2.2f);
		if (segment.IsDowntown)
		{
			num4 *= num5;
		}

		int num6 = Mathf.Max(segment.StartBranchCount, segment.EndBranchCount);
		if (num6 >= 3)
		{
			num4 *= 1f + Mathf.Min(0.55f, 0.12f * (num6 - 2));
		}

		return Mathf.Max(0.05f, num4);
	}

	private float FindLeadGapOnSegment(FakeVehicleRecord vehicle)
	{
		float num = float.MaxValue;
		for (int i = 0; i < _activeVehicles.Count; i++)
		{
			FakeVehicleRecord fakeVehicleRecord = _activeVehicles[i];
			if (fakeVehicleRecord == null || ReferenceEquals(fakeVehicleRecord, vehicle) || fakeVehicleRecord.SegmentId != vehicle.SegmentId)
			{
				continue;
			}

			float num2 = fakeVehicleRecord.DistanceAlongSegment - vehicle.DistanceAlongSegment;
			if (num2 > 0.01f && num2 < num)
			{
				num = num2;
			}
		}

		return num;
	}

	private bool ShouldStartIntersectionHold(FakeVehicleRecord vehicle, DirectedRoadSegment segment, float leadGap, float stopLineDistance)
	{
		if (vehicle.LastHoldSegmentId == segment.Id || segment.EndBranchCount < 3)
		{
			return false;
		}

		float num = stopLineDistance - vehicle.DistanceAlongSegment;
		float num2 = Mathf.Max(3.5f, GameplayTweaksPlugin.FakeTrafficQueueSpacing?.Value ?? 6.5f);
		if (num < -0.05f || num > Mathf.Max(3.5f, num2 * 1.15f))
		{
			return false;
		}

		if (leadGap < num2 * 1.2f)
		{
			return false;
		}

		float num3 = Mathf.Clamp01(GameplayTweaksPlugin.FakeTrafficIntersectionStopChance?.Value ?? 0.24f);
		float num4 = 1f + Mathf.Min(0.5f, 0.15f * (segment.EndBranchCount - 2));
		if (segment.IsDowntown)
		{
			num4 *= 1.35f;
		}

		return (float)_rng.NextDouble() < Mathf.Clamp01(num3 * num4);
	}

	private void BeginIntersectionHold(FakeVehicleRecord vehicle, DirectedRoadSegment segment, float unscaledTime)
	{
		float num = Mathf.Min(GameplayTweaksPlugin.FakeTrafficStopSecondsMin?.Value ?? 0.7f, GameplayTweaksPlugin.FakeTrafficStopSecondsMax?.Value ?? 1.9f);
		float num2 = Mathf.Max(GameplayTweaksPlugin.FakeTrafficStopSecondsMin?.Value ?? 0.7f, GameplayTweaksPlugin.FakeTrafficStopSecondsMax?.Value ?? 1.9f);
		vehicle.StopUntilTime = unscaledTime + Mathf.Lerp(num, num2, (float)_rng.NextDouble());
		vehicle.LastHoldSegmentId = segment.Id;
		_intersectionHoldsSinceLog++;
	}

	private float GetStopLineDistance(DirectedRoadSegment segment, float queueSpacing)
	{
		if (segment.EndBranchCount < 3)
		{
			return segment.Length;
		}

		float num = Mathf.Max(0.9f, GameplayTweaksPlugin.FakeTrafficStopLineOffset?.Value ?? 1.9f);
		num = Mathf.Min(num, Mathf.Max(1.1f, queueSpacing * 0.7f));
		return Mathf.Clamp(segment.Length - num, 0.25f, Mathf.Max(0.25f, segment.Length - 0.1f));
	}

	private void MaybeSpawnPlatoon(DirectedRoadSegment segment, FakeVehicleRecord leadVehicle, int desiredVisible)
	{
		if (leadVehicle == null || _availableViews.Count <= 0 || _activeVehicles.Count >= desiredVisible)
		{
			return;
		}

		int num = Mathf.Clamp(GameplayTweaksPlugin.FakeTrafficMaxPlatoonFollowers?.Value ?? 2, 0, 3);
		if (num <= 0)
		{
			return;
		}

		float num2 = Mathf.Clamp01(GameplayTweaksPlugin.FakeTrafficPlatoonChance?.Value ?? 0.42f);
		if (segment.IsDowntown)
		{
			num2 *= 1.35f;
		}

		if (segment.EndBranchCount >= 3)
		{
			num2 *= 1.15f;
		}

		if ((float)_rng.NextDouble() > Mathf.Clamp01(num2))
		{
			return;
		}

		int num3 = _rng.Next(1, num + 1);
		float num4 = Mathf.Max(4.5f, GameplayTweaksPlugin.FakeTrafficQueueSpacing?.Value ?? 6.5f);
		float num5 = leadVehicle.DistanceAlongSegment;
		for (int i = 0; i < num3 && _availableViews.Count > 0 && _activeVehicles.Count < desiredVisible; i++)
		{
			float num6 = num4 * Mathf.Lerp(0.95f, 1.12f, (float)_rng.NextDouble());
			num5 -= num6;
			if (num5 <= 0.35f || HasVehicleNearDistance(segment.Id, num5, num4 * 0.75f))
			{
				break;
			}

			_ = SpawnVehicleOnSegment(segment, num5, leadVehicle.Speed * Mathf.Lerp(0.94f, 1.01f, (float)_rng.NextDouble()), Mathf.Max(4, leadVehicle.SegmentsRemaining + _rng.Next(-1, 2)));
		}
	}

	private bool HasVehicleNearDistance(int segmentId, float distanceAlongSegment, float minGap)
	{
		for (int i = 0; i < _activeVehicles.Count; i++)
		{
			FakeVehicleRecord fakeVehicleRecord = _activeVehicles[i];
			if (fakeVehicleRecord != null && fakeVehicleRecord.SegmentId == segmentId && Mathf.Abs(fakeVehicleRecord.DistanceAlongSegment - distanceAlongSegment) < minGap)
			{
				return true;
			}
		}

		return false;
	}

	private static bool TryResolveSelectionSuppressPos(Entity entity, out WorldPos worldPos)
	{
		worldPos = default(WorldPos);
		if (entity == null)
		{
			return false;
		}

		if (entity.components?.mobile != null)
		{
			worldPos = entity.data.mobile.worldpos;
			return !worldPos.IsZero;
		}

		if (entity.components?.board != null)
		{
			worldPos = entity.data.board.worldpos;
			if (!worldPos.IsZero)
			{
				return true;
			}

			if (entity.components.model != null)
			{
				worldPos = entity.components.model.GetModelPosition();
				if (!worldPos.IsZero)
				{
					return true;
				}
			}
		}

		if (entity.components?.biz != null)
		{
			Entity building = BuildingUtil.FindBuildingForBiz(entity);
			if (building != null)
			{
				if (building.components?.board != null)
				{
					worldPos = building.data.board.worldpos;
					if (!worldPos.IsZero)
					{
						return true;
					}
				}

				if (building.components?.model != null)
				{
					worldPos = building.components.model.GetModelPosition();
					return !worldPos.IsZero;
				}
			}
		}

		return false;
	}

	private void DeactivateAllVehicles()
	{
		for (int i = _activeVehicles.Count - 1; i >= 0; i--)
		{
			ReleaseVehicleAt(i);
		}

		_spawnCandidates.Clear();
		_occupiedSegments.Clear();
	}

	internal string GetDiagnosticsSummary()
	{
		if (!GameplayTweaksPlugin.ShouldRunFakeTraffic())
		{
			return $"state=idle enabled={GameplayTweaksPlugin.EnableFakeTraffic?.Value ?? false} interactive={Game.Game.ctx?.IsInteractive ?? false}";
		}

		WorldPos worldPos = Game.Game.serv?.camera?.ScreenCenterToWorldPos() ?? default(WorldPos);
		return BuildDiagnosticsSummary(worldPos, Mathf.Max(0, GameplayTweaksPlugin.FakeTrafficMaxVisible?.Value ?? 0), includeSinceLog: false);
	}

	private void MaybeLogDiagnostics(WorldPos cameraCenter, int maxVisible)
	{
		if (!(GameplayTweaksPlugin.FakeTrafficDiagnosticsEnabled?.Value ?? false))
		{
			return;
		}

		float num = Mathf.Max(1f, GameplayTweaksPlugin.FakeTrafficDiagnosticsLogInterval?.Value ?? 8f);
		if (Time.unscaledTime < _lastDiagnosticsLogTime + num)
		{
			return;
		}

		_lastDiagnosticsLogTime = Time.unscaledTime;
		GameplayTweaksPlugin.VerificationLog("FakeTraffic", BuildDiagnosticsSummary(cameraCenter, maxVisible, includeSinceLog: true));
		_spawnedSinceLog = 0;
		_releasedSinceLog = 0;
		_intersectionHoldsSinceLog = 0;
	}

	private string BuildDiagnosticsSummary(WorldPos cameraCenter, int maxVisible, bool includeSinceLog)
	{
		float num = Mathf.Max(10f, GameplayTweaksPlugin.FakeTrafficInnerRingRadius?.Value ?? 95f);
		float num2 = Mathf.Max(num + 20f, GameplayTweaksPlugin.FakeTrafficMiddleRingRadius?.Value ?? 155f);
		float num3 = num * num;
		float num4 = num2 * num2;
		int num5 = 0;
		int num6 = 0;
		for (int i = 0; i < _activeVehicles.Count; i++)
		{
			FakeVehicleRecord fakeVehicleRecord = _activeVehicles[i];
			if (fakeVehicleRecord == null || fakeVehicleRecord.SegmentId < 0 || fakeVehicleRecord.SegmentId >= _segments.Count)
			{
				continue;
			}

			WorldPos worldPos = fakeVehicleRecord.LastWorldPos;
			if (worldPos.IsZero)
			{
				worldPos = _segments[fakeVehicleRecord.SegmentId].Midpoint;
			}

			float magnitudeSquared = (worldPos - cameraCenter).MagnitudeSquared;
			if (magnitudeSquared <= num3)
			{
				num5++;
			}
			else if (magnitudeSquared <= num4)
			{
				num6++;
			}
		}

		string text = $"active={_activeVehicles.Count}/{maxVisible} desired={_lastDesiredVisibleCount} pool={_pool.Count} free={_availableViews.Count} candidates={_spawnCandidates.Count} inner={num5} middle={num6} roadKm={_lastVisibleRoadKm:0.00} segments={_segments.Count} prefabs={_loadedPrefabs.Count} rebuilds={_graphRebuildCount}";
		if (includeSinceLog)
		{
			text += $" spawned={_spawnedSinceLog} released={_releasedSinceLog} holds={_intersectionHoldsSinceLog}";
		}

		return text;
	}
}
}
