using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Session.Board;
using Game.Session.Entities;
using Game.Session.Overlays;
using SomaSim.Util;
using UnityEngine;

namespace Game.Session.Assets;

public sealed class VFXManager : AbstractSessionManager, IAnimatingManager, ISessionManager
{
	internal struct MarkerObject
	{
		public Entity entity;

		public GameObject marker;
	}

	internal struct BorderObject
	{
		public WorldPos wpos;

		public WorldSize wsize;

		public GameObject marker;
	}

	internal struct NavLineObject
	{
		public NavLineHandle handle;

		public bool useTravelData;

		public GameObject line;

		public GameObject source;

		public GameObject target;

		public Color color;

		public bool UpdateInteractive => useTravelData;
	}

	public struct NavLineHandle : IEquatable<NavLineHandle>
	{
		public ulong id;

		public bool attached;

		public bool IsValid => id != 0;

		public bool IsNotValid => id == 0;

		public bool IsAttachedToEntity => attached;

		public Entity FindEntity()
		{
			if (!attached)
			{
				return null;
			}
			return EntityID.FromID(id).FindEntity();
		}

		public static NavLineHandle Make(EntityID eid)
		{
			return new NavLineHandle
			{
				id = eid.id,
				attached = true
			};
		}

		public static NavLineHandle MakeUnique(IRandom rng)
		{
			return new NavLineHandle
			{
				id = rng.Generate(),
				attached = false
			};
		}

		public bool Equals(NavLineHandle other)
		{
			if (id == other.id)
			{
				return attached == other.attached;
			}
			return false;
		}

		public override int GetHashCode()
		{
			return id.GetHashCode();
		}

		public override bool Equals(object obj)
		{
			if (obj is NavLineHandle navLineHandle)
			{
				return navLineHandle.Equals(this);
			}
			return false;
		}
	}

	private class NavLineEqualityComparer : IEqualityComparer<NavLineHandle>
	{
		public bool Equals(NavLineHandle x, NavLineHandle y)
		{
			return x.Equals(y);
		}

		public int GetHashCode(NavLineHandle obj)
		{
			return obj.GetHashCode();
		}
	}

	private struct WorldArrowObject
	{
		public WorldArrowHandle handle;

		public GameObject line;

		public object extras;
	}

	public struct WorldArrowHandle : IEquatable<WorldArrowHandle>
	{
		public EntityID from;

		public EntityID to;

		public EntityID ctx;

		public Color color;

		public ArrowType arrowType;

		public static WorldArrowHandle Make(EntityID from, EntityID to, Color color, EntityID ctx, ArrowType type)
		{
			return new WorldArrowHandle
			{
				from = from,
				to = to,
				color = color,
				ctx = ctx,
				arrowType = type
			};
		}

		public (WorldPos from, WorldPos to, WorldPos midpoint) MakeArrowPoints()
		{
			WorldPos positionForFX = GetPositionForFX(from);
			WorldPos positionForFX2 = GetPositionForFX(to);
			WorldPos item = WorldPos.Lerp(positionForFX, positionForFX2, 0.5f);
			return (from: positionForFX, to: positionForFX2, midpoint: item);
		}

		public Entity GetPickTarget()
		{
			return ctx.FindEntity();
		}

		public bool Equals(WorldArrowHandle other)
		{
			if (from == other.from && to == other.to && color.Equals(other.color))
			{
				return ctx == other.ctx;
			}
			return false;
		}

		public override bool Equals(object obj)
		{
			if (obj is WorldArrowHandle worldArrowHandle)
			{
				return worldArrowHandle.Equals(this);
			}
			return false;
		}

		public override int GetHashCode()
		{
			return from.index ^ to.index ^ color.GetHashCode();
		}
	}

	private class WorldArrowEqualityComparer : IEqualityComparer<WorldArrowHandle>
	{
		public bool Equals(WorldArrowHandle x, WorldArrowHandle y)
		{
			return x.Equals(y);
		}

		public int GetHashCode(WorldArrowHandle obj)
		{
			return obj.GetHashCode();
		}
	}

	private const string VFX_LINE_ENDING_PREFAB = "VFX/NavLine_Endcap";

	private const string VFX_LINE_PREFAB = "VFX/NavLine";

	private const string VFX_BORDER_PREFAB = "VFX/Block_Border";

	private const string VFX_AVATAR_PREFAB = "VFX/Avatar_Marker";

	private const string VFX_WORLD_ARROW_ORDER = "VFX/World Arrow";

	private const string VFX_WORLD_ARROW_REL = "VFX/World Arrow Rel";

	private const string VFX_CLOUDS = "VFX/CloudGate";

	private const float NAVLINE_Y_OFFSET = 0.2f;

	private const float BORDER_Y_OFFSET = 0.05f;

	public const float ARROW_HEIGHT = 2f;

	public const float ARROW_APEX = 3f;

	public const float ARROW_TRIM = 2f;

	private ObjectPoolResettable<GameObject> _navlinePool;

	private ObjectPoolResettable<GameObject> _navlineEndingPool;

	private List<ObjectPoolResettable<GameObject>> _arrowPools;

	private Dictionary<NavLineHandle, NavLineObject> _navlines;

	private Dictionary<WorldArrowHandle, WorldArrowObject> _arrows;

	private MarkerObject _avatarMarker;

	private BorderObject _cornerMarker;

	private Xorshift _rng;

	private CloudGate _clouds;

	private GameObject _pfxContainer;

	public override void OnInitializeDone()
	{
		_pfxContainer = new GameObject
		{
			name = "PFX Container"
		};
		_rng = Game.ctx.scenario.MakeSeededRng<VFXManager>();
		_navlines = new Dictionary<NavLineHandle, NavLineObject>(new NavLineEqualityComparer());
		_navlinePool = new ObjectPoolResettable<GameObject>();
		_navlinePool.Initialize(MakeNavLine, ActivateVFX, ResetVFX, DestroyVFX);
		_navlineEndingPool = new ObjectPoolResettable<GameObject>();
		_navlineEndingPool.Initialize(MakeNavLineEnding, ActivateVFX, ResetVFX, DestroyVFX);
		_arrows = new Dictionary<WorldArrowHandle, WorldArrowObject>(new WorldArrowEqualityComparer());
		_arrowPools = new List<ObjectPoolResettable<GameObject>>();
		_arrowPools.AddTimes(() => new ObjectPoolResettable<GameObject>(), ArrowHandles.ARROW_TYPES.Length);
		GetArrowPool(ArrowType.Orders).Initialize(() => MakePrefab("VFX/World Arrow"), ActivateVFX, ResetVFX, DestroyVFX);
		GetArrowPool(ArrowType.Rel).Initialize(() => MakePrefab("VFX/World Arrow Rel"), ActivateVFX, ResetVFX, DestroyVFX);
		_avatarMarker = new MarkerObject
		{
			marker = MakePrefab("VFX/Avatar_Marker")
		};
		_avatarMarker.marker.SetActive(value: false);
		_cornerMarker = new BorderObject
		{
			marker = MakePrefab("VFX/Block_Border")
		};
		_cornerMarker.marker.SetActive(value: false);
		_clouds = new CloudGate();
		_clouds.Initialize(MakePrefab("VFX/CloudGate"));
	}

	public override void OnReleased()
	{
		UnityEngine.Object.Destroy(_clouds.container);
		_clouds.Release();
		_clouds = null;
		UnityEngine.Object.Destroy(_cornerMarker.marker);
		_cornerMarker = default(BorderObject);
		UnityEngine.Object.Destroy(_avatarMarker.marker);
		_avatarMarker = default(MarkerObject);
		foreach (ObjectPoolResettable<GameObject> arrowPool in _arrowPools)
		{
			arrowPool.Release();
		}
		_arrowPools.Clear();
		foreach (WorldArrowObject value in _arrows.Values)
		{
			DestroyVFX(value.line);
		}
		_arrows.Clear();
		_navlineEndingPool.Release();
		_navlineEndingPool = null;
		_navlinePool.Release();
		_navlinePool = null;
		foreach (NavLineObject value2 in _navlines.Values)
		{
			DestroyVFX(value2.line);
			DestroyVFX(value2.source);
			DestroyVFX(value2.target);
		}
		_navlines.Clear();
		UnityEngine.Object.Destroy(_pfxContainer);
		_pfxContainer = null;
	}

	private void ActivateVFX(GameObject obj)
	{
		obj.SetActive(value: true);
	}

	private void ResetVFX(GameObject obj)
	{
		obj.SetActive(value: false);
	}

	private void DestroyVFX(GameObject obj)
	{
		UnityEngine.Object.Destroy(obj);
	}

	private GameObject MakePrefab(string name)
	{
		return UnityEngine.Object.Instantiate(Resources.Load(name), _pfxContainer.transform) as GameObject;
	}

	private ObjectPoolResettable<GameObject> GetArrowPool(ArrowType type)
	{
		return _arrowPools[(int)type];
	}

	public void PlayOneShotPFXOverBuilding(PFXType type, Entity building, PlayerID pid, float height = 0f)
	{
		PlayOneShotPFX(type, building.data.board.worldpos, pid, height);
	}

	public void PlayOneShotPFX(PFXType type, WorldPos pos, PlayerID pid, float height = 0f)
	{
		if (pid.IsHumanPlayer)
		{
			PFXOneShot pFXOneShot = PFXDefinitions.Instance.FindOrNull(type);
			if (pFXOneShot != null)
			{
				Vector3 position = Game.serv.camera.WorldToSceneVector(pos, height);
				UnityEngine.Object.Instantiate(pFXOneShot.template, position, Quaternion.identity, _pfxContainer.transform);
			}
		}
	}

	public static WorldPos GetPositionForFX(EntityID target)
	{
		return BoardUtil.FindBoardPositionFor(target.FindEntity()).GetValueOrDefault();
	}

	private GameObject MakeNavLineEnding()
	{
		return MakePrefab("VFX/NavLine_Endcap");
	}

	private GameObject MakeNavLine()
	{
		return MakePrefab("VFX/NavLine");
	}

	public NavLineHandle ShowNavLineTraveling(Entity e)
	{
		return ShowNavLine(e);
	}

	public NavLineHandle ShowNavLineFixed(Entity e, List<WorldPos> nodes)
	{
		return ShowNavLine(e, nodes);
	}

	public NavLineHandle ShowNavLineDetached(Color color, List<WorldPos> nodes)
	{
		return ShowNavLine(NavLineHandle.MakeUnique(_rng), color, nodes, useTravelData: false).handle;
	}

	private NavLineHandle ShowNavLine(Entity e, List<WorldPos> fixedNodes = null)
	{
		bool flag = fixedNodes == null;
		List<WorldPos> nodes = (flag ? e.data.mobile.travelContext : fixedNodes);
		NavLineHandle navLineHandle = NavLineHandle.Make(e.Id);
		Color navColor = Game.ctx.players.Human.territory.colorInfo.GetNavColor();
		NavLineObject obj = ShowNavLine(navLineHandle, navColor, nodes, flag);
		if (flag)
		{
			UpdateNavLineInTravel(e, obj);
		}
		return navLineHandle;
	}

	private NavLineObject ShowNavLine(NavLineHandle handle, Color color, List<WorldPos> nodes, bool useTravelData)
	{
		if (!_navlines.TryGetValue(handle, out var value))
		{
			value = new NavLineObject
			{
				handle = handle,
				line = _navlinePool.Allocate(),
				source = _navlineEndingPool.Allocate(),
				target = _navlineEndingPool.Allocate(),
				useTravelData = useTravelData,
				color = color
			};
			_navlines.Add(handle, value);
		}
		if (!useTravelData)
		{
			UpdateNavLine(value, nodes, nodes.LastOrDefaultFast());
		}
		return value;
	}

	public void HideNavLine(Entity e)
	{
		HideNavLine(NavLineHandle.Make(e.Id));
	}

	public NavLineHandle HideNavLine(NavLineHandle handle)
	{
		if (handle.IsNotValid)
		{
			return default(NavLineHandle);
		}
		NavLineObject? navLineObject = _navlines.FindOrNullable(handle);
		if (!navLineObject.HasValue)
		{
			return default(NavLineHandle);
		}
		NavLineObject value = navLineObject.Value;
		_navlines.Remove(handle);
		_navlinePool.Free(value.line);
		_navlineEndingPool.Free(value.source);
		_navlineEndingPool.Free(value.target);
		return default(NavLineHandle);
	}

	public bool IsNavLineShowing(NavLineHandle handle)
	{
		return _navlines.ContainsKey(handle);
	}

	public bool IsNavLineShowing(Entity e)
	{
		return IsNavLineShowing(NavLineHandle.Make(e.Id));
	}

	public void UpdateAnimations(GameAnimUpdate anim)
	{
		foreach (KeyValuePair<NavLineHandle, NavLineObject> navline in _navlines)
		{
			if (navline.Value.UpdateInteractive && navline.Key.IsValid && navline.Key.IsAttachedToEntity)
			{
				UpdateNavLineInTravel(navline.Key.FindEntity(), navline.Value);
			}
		}
		if (_avatarMarker.entity != null)
		{
			UpdateMarkerPosition();
		}
	}

	private void UpdateNavLineInTravel(Entity entity, NavLineObject obj)
	{
		List<WorldPos> travelContext = entity.data.mobile.travelContext;
		WorldPos worldpos = entity.data.mobile.worldpos;
		UpdateNavLine(obj, travelContext, worldpos);
	}

	private void UpdateNavLine(NavLineObject obj, List<WorldPos> nodes, WorldPos source)
	{
		LineRenderer component = obj.line.GetComponent<LineRenderer>();
		component.positionCount = nodes.Count + 1;
		WorldPos pos = nodes[0];
		int i = 0;
		for (int count = nodes.Count; i < count; i++)
		{
			WorldPos pos2 = nodes[i];
			SetNavLinePosition(component, i, pos2);
		}
		SetNavLinePosition(component, nodes.Count, source);
		obj.line.transform.position = Vector3.zero.SetY(0.2f);
		obj.source.transform.position = Game.serv.camera.WorldToSceneVector(source).SetY(0.2f);
		obj.target.transform.position = Game.serv.camera.WorldToSceneVector(pos).SetY(0.2f);
		SetNavLineColor(obj.line, obj.color);
		SetNavEndcapColor(obj.source, obj.color);
		SetNavEndcapColor(obj.target, obj.color);
	}

	private void SetNavLineColor(GameObject go, Color c)
	{
		go.GetComponent<LineRenderer>().material.SetColor("_Color", c);
	}

	private void SetNavEndcapColor(GameObject go, Color c)
	{
		go.GetComponent<LineRenderer>().material.color = c;
	}

	internal static void SetNavLinePosition(LineRenderer line, int index, WorldPos pos)
	{
		Vector3 vector = Game.serv.camera.WorldToSceneVector(pos);
		line.SetPosition(index, new Vector3(vector.x, vector.z, vector.y));
	}

	public bool IsArrowShowing(WorldArrowHandle handle)
	{
		return _arrows.ContainsKey(handle);
	}

	public object FindArrowExtras(WorldArrowHandle handle)
	{
		return _arrows.FindOrNullable(handle)?.extras;
	}

	public WorldArrowHandle ShowWorldArrow(EntityID from, EntityID to, Color color, EntityID ctx, ArrowType type, object extras = null)
	{
		WorldArrowHandle worldArrowHandle = WorldArrowHandle.Make(from, to, color, ctx, type);
		if (!_arrows.TryGetValue(worldArrowHandle, out var value))
		{
			value = new WorldArrowObject
			{
				handle = worldArrowHandle,
				extras = extras,
				line = GetArrowPool(worldArrowHandle.arrowType).Allocate()
			};
			_arrows.Add(worldArrowHandle, value);
		}
		RefreshArrow(value);
		return worldArrowHandle;
	}

	public void HideWorldArrow(WorldArrowHandle handle)
	{
		WorldArrowObject? worldArrowObject = _arrows.FindOrNullable(handle);
		if (worldArrowObject.HasValue)
		{
			WorldArrowObject value = worldArrowObject.Value;
			_arrows.Remove(handle);
			GetArrowPool(handle.arrowType).Free(value.line);
		}
	}

	public void HideAllWorldArrows()
	{
		foreach (WorldArrowHandle item in _arrows.Keys.ToList())
		{
			HideWorldArrow(item);
		}
	}

	public void RefreshArrowColor(WorldArrowHandle handle, Color? overrideColor)
	{
		if (_arrows.TryGetValue(handle, out var value))
		{
			Color c = overrideColor ?? handle.color;
			value.line.GetComponent<WorldArrow>().Recolor(c);
		}
	}

	private void RefreshArrow(WorldArrowObject obj)
	{
		(WorldPos from, WorldPos to, WorldPos midpoint) tuple = obj.handle.MakeArrowPoints();
		WorldPos item = tuple.from;
		WorldPos item2 = tuple.to;
		WorldPos item3 = tuple.midpoint;
		Vector3 center = item3.AsVector3XZ.SetY(2f);
		float magnitude = (item2 - item).Magnitude;
		magnitude = MathUtil.ClampMin(magnitude - 2f, 2f);
		float valueOrDefault = WorldPos.GetFacingDegrees(item, item2).GetValueOrDefault();
		valueOrDefault -= 90f;
		WorldArrow component = obj.line.GetComponent<WorldArrow>();
		component.Move(center, valueOrDefault, magnitude);
		component.Recolor(obj.handle.color);
	}

	internal bool IsMarkerShowing(Entity entity)
	{
		return _avatarMarker.entity == entity;
	}

	internal void ShowMarkerFor(Entity entity)
	{
		bool num = entity != null;
		bool flag = _avatarMarker.entity != null;
		bool flag2 = _avatarMarker.entity == entity && entity != null;
		if (!num && flag)
		{
			_avatarMarker.marker.SetActive(value: false);
			_avatarMarker.entity = null;
		}
		if (num)
		{
			if (!flag)
			{
				_avatarMarker.marker.SetActive(value: true);
			}
			if (!flag2)
			{
				_avatarMarker.entity = entity;
			}
		}
	}

	private void UpdateMarkerPosition()
	{
		if (_avatarMarker.entity != null)
		{
			WorldPos entityMarkerPos = GetEntityMarkerPos(_avatarMarker.entity);
			Vector3 position = Game.serv.camera.WorldToSceneVector(entityMarkerPos);
			_avatarMarker.marker.transform.position = position;
		}
	}

	private WorldPos GetEntityMarkerPos(Entity entity)
	{
		if (entity?.data == null)
		{
			return WorldPos.Zero;
		}
		if (entity.components.mobile != null)
		{
			return entity.data.mobile.worldpos;
		}
		if (entity.components.board != null)
		{
			return entity.data.board.worldpos;
		}
		return WorldPos.Zero;
	}

	internal bool IsCornerShowing()
	{
		return _cornerMarker.marker.activeSelf;
	}

	internal void ShowCorner(bool show)
	{
		bool activeSelf = _cornerMarker.marker.activeSelf;
		if (!show && activeSelf)
		{
			_cornerMarker.marker.SetActive(value: false);
		}
		if (show && !activeSelf)
		{
			_cornerMarker.marker.SetActive(value: true);
		}
	}

	internal void UpdateCornerPosition(Node node)
	{
		_cornerMarker.wpos = node.pos;
		_cornerMarker.wsize = new WorldSize(1f, 1f);
		WorldSize worldSize = _cornerMarker.wsize;
		LineRenderer component = _cornerMarker.marker.GetComponent<LineRenderer>();
		component.transform.position = _cornerMarker.wpos.AsVector3XZ.SetY(0.05f);
		component.loop = true;
		SetNavLinePosition(component, 0, OffsetFromCenter(-0.5f, -0.5f));
		SetNavLinePosition(component, 1, OffsetFromCenter(0.5f, -0.5f));
		SetNavLinePosition(component, 2, OffsetFromCenter(0.5f, 0.5f));
		SetNavLinePosition(component, 3, OffsetFromCenter(-0.5f, 0.5f));
		Color navColor = Game.ctx.players.Human.territory.colorInfo.GetNavColor();
		component.material.color = navColor;
		WorldPos OffsetFromCenter(float mx, float my)
		{
			float x = worldSize.width * mx;
			float y = worldSize.height * my;
			return new WorldPos(x, y).Rotate(node.deg);
		}
	}
}
