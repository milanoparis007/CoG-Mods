using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Services;
using Game.Services.Maps;
using Game.Session.Assets;
using Game.Session.Entities;
using Game.Session.Setup;
using SomaSim.SION;
using SomaSim.Util;
using UnityEngine;

namespace Game.Session.Board;

public sealed class BoardManager : AbstractSessionManager, ISaveLoadProvider, ISaveObserver, ILoadObserver
{
	public TerrainManager terrain;

	public NodeManager nodes;

	public SpatialLookup spacecache;

	public BoardManagerData data;

	private MapConfig _mapGenConfig;

	private MapBoardConfig _mapConfig;

	private Pathfinding _gridPather;

	private NodeToCornerMappingCache _corners;

	internal ObjectPool<WorldPosList> cornerListPool;

	private UpgradeData _tmp_upgradeData = new UpgradeData();

	private UpgradeData _tmp_secondaryMergeData = new UpgradeData();

	private RaycastHit[] _hits = new RaycastHit[32];

	public MapBoardConfig MapConfig => _mapConfig;

	public NodeToCornerMappingCache CornerCache => _corners;

	public override void OnInitializeDone()
	{
		data = new BoardManagerData();
		_mapGenConfig = Game.ctx.session.mapconfig;
		_mapConfig = _mapGenConfig.map;
		cornerListPool = new ObjectPool<WorldPosList>();
		cornerListPool.Initialize();
		_corners = new NodeToCornerMappingCache();
		AbstractSessionManager.InitializeSubmanagers(this);
	}

	public override void OnPreInteractive()
	{
		_gridPather = new Pathfinding();
	}

	public override void OnReleased()
	{
		foreach (Entity item in from e in Game.ctx.entityman.GenerateListOfAllEntities()
			where e.config.board != null
			select e)
		{
			DestroyEntity(item, shutdown: true);
		}
		AbstractSessionManager.ReleaseSubmanagers(this);
		_corners = null;
		cornerListPool.Release();
		cornerListPool = null;
		_mapConfig = null;
		_mapGenConfig = null;
		data = null;
	}

	public bool IsValid(WorldPos pos)
	{
		if (pos.x >= 0f && pos.y >= 0f && pos.x < (float)_mapConfig.mapSize.width)
		{
			return pos.y < (float)_mapConfig.mapSize.height;
		}
		return false;
	}

	public List<Entity> DoesPointIntersectAnyEntityWithRule(WorldSize lotSize, GridTransform tr, Predicate<Entity> test, Entity ignore = null, bool showDebug = false)
	{
		WorldPosList worldPosList = cornerListPool.Allocate();
		BoardConfig.GetFootprintTestPoints(lotSize, tr, 0.75f, worldPosList, includeInnerPoints: true);
		List<Entity> list = new List<Entity>();
		for (int i = 0; i < worldPosList.Count; i++)
		{
			WorldPos worldPos = worldPosList[i];
			foreach (Entity item in spacecache.FindEntitiesInCellUnsafe(worldPos))
			{
				if (item == ignore || item.components.board == null || !test(item))
				{
					continue;
				}
				if (item.components.board.IsPointInsideFootprint(worldPos))
				{
					if (showDebug)
					{
						Debug.DrawLine(worldPos.AsVector3XZ, worldPos.AsVector3XZ + Vector3.up * 5f, Color.red, 10000f);
					}
					list.Add(item);
				}
				else if (showDebug)
				{
					Debug.DrawLine(worldPos.AsVector3XZ, worldPos.AsVector3XZ + Vector3.up * 5f, Color.green, 10000f);
				}
			}
		}
		cornerListPool.Free(worldPosList);
		return list;
	}

	public bool DoesPointIntersectAnyEntity(WorldPos pos, Entity ignore = null, bool showDebug = false)
	{
		foreach (Entity item in spacecache.FindEntitiesInCellUnsafe(pos))
		{
			if (item == ignore || item.components.board == null)
			{
				continue;
			}
			if (item.components.board.IsPointInsideFootprint(pos))
			{
				if (showDebug)
				{
					Debug.DrawLine(pos.AsVector3XZ, pos.AsVector3XZ + Vector3.up * 5f, Color.red, 10000f);
				}
				return true;
			}
			if (showDebug)
			{
				Debug.DrawLine(pos.AsVector3XZ, pos.AsVector3XZ + Vector3.up * 5f, Color.green, 10000f);
			}
		}
		return false;
	}

	public bool DoesEntityIntersectAnyEntity(Entity e)
	{
		if (e == null || e.components.board == null || e.config.board == null)
		{
			return false;
		}
		WorldSize getLotSizeWithOverride = e.components.board.GetLotSizeWithOverride;
		GridTransform transform = e.data.board.Transform;
		return DoesEntityIntersectAnyEntity(getLotSizeWithOverride, transform, e);
	}

	public bool DoesEntityIntersectAnyEntity(WorldSize lotSize, GridTransform tr, Entity entityToIgnore = null, bool showDebug = false)
	{
		WorldPosList worldPosList = cornerListPool.Allocate();
		BoardConfig.GetFootprintTestPoints(lotSize, tr, 0.75f, worldPosList, includeInnerPoints: true);
		bool flag = false;
		for (int i = 0; i < worldPosList.Count; i++)
		{
			WorldPos pos = worldPosList[i];
			flag = flag || DoesPointIntersectAnyEntity(pos, entityToIgnore, showDebug);
			if (flag)
			{
				break;
			}
		}
		cornerListPool.Free(worldPosList);
		return flag;
	}

	public bool DoesEntityIntersectAnyTerrain(Entity e)
	{
		if (e == null || e.components.board == null)
		{
			return false;
		}
		WorldSize getLotSizeWithOverride = e.components.board.GetLotSizeWithOverride;
		GridTransform transform = e.data.board.Transform;
		return DoesEntityIntersectAnyTerrain(getLotSizeWithOverride, transform);
	}

	public bool DoesEntityIntersectAnyTerrain(WorldSize lotSize, GridTransform tr, bool allowMountains = false)
	{
		if (data == null)
		{
			return false;
		}
		WorldPosList worldPosList = cornerListPool.Allocate();
		BoardConfig.GetFootprintTestPoints(lotSize, tr, 0.75f, worldPosList, includeInnerPoints: true);
		bool flag = false;
		for (int i = 0; i < worldPosList.Count; i++)
		{
			flag = flag || DoesPointIntersectAnyTerrain(worldPosList[i], allowMountains);
		}
		cornerListPool.Free(worldPosList);
		return flag;
	}

	public bool DoesPointIntersectAnyTerrain(WorldPos pos, bool allowMountains)
	{
		HeightmapData heightmap = terrain.Heightmap;
		if (heightmap == null)
		{
			return false;
		}
		TerrainType terrainType = heightmap.GetTerrainType(pos);
		if (!allowMountains)
		{
			return terrainType != TerrainType.Ground;
		}
		if (terrainType != TerrainType.Ground)
		{
			return terrainType != TerrainType.Mountain;
		}
		return false;
	}

	public Entity CreateEntityAtStartup(EntityConfig template, WorldPos pos, float degrees = 0f)
	{
		return CreateEntityImpl(template, new GridTransform(pos, degrees), enabled: true, _: true);
	}

	public Entity CreateEntityAtStartup(EntityConfig template, GridTransform transform)
	{
		return CreateEntityImpl(template, transform, enabled: true, _: true);
	}

	public Entity CreateEntityAtStartup(Label templateName, WorldPos position, float degrees = 0f)
	{
		EntityConfig entityConfig = Game.ctx.entityman.FindTemplate(templateName);
		if (entityConfig == null)
		{
			Logger.Error("Template not found", templateName);
			return null;
		}
		GridTransform transform = new GridTransform
		{
			pos = position,
			deg = degrees
		};
		return CreateEntityImpl(entityConfig, transform, enabled: true, _: true);
	}

	public Entity CreateEntity(Label templateName, WorldPos position, float degrees = 0f)
	{
		return CreateEntity(templateName, new GridTransform(position, degrees));
	}

	public Entity CreateEntity(Label templateName, GridTransform transform)
	{
		EntityConfig entityConfig = Game.ctx.entityman.FindTemplate(templateName);
		if (entityConfig == null)
		{
			Logger.Error("Template not found", templateName);
			return null;
		}
		return CreateEntityImpl(entityConfig, transform, enabled: true, _: false);
	}

	private Entity CreateEntityImpl(EntityConfig template, GridTransform transform, bool enabled, bool _)
	{
		Entity entity = Game.ctx.entityman.CreateByTemplate(template);
		entity.components.board?.Insert(transform);
		entity.components.mobile?.Move(transform.pos, transform.deg);
		entity.SetEnabled(enabled);
		return entity;
	}

	public void DestroyEntity(Entity entity, bool shutdown)
	{
		if (entity == null || entity.config == null)
		{
			string arg = ((entity == null) ? "null" : "destroyed");
			Logger.Warning($"Tried to destroy an entity that is {arg}, shutdown = {shutdown}");
		}
		else
		{
			ClearInterestingAndPotential(entity, shutdown);
			entity.SetEnabled(value: false);
			Game.ctx.entityman.DestroyEntity(entity, shutdown);
		}
	}

	public Entity ReplaceEntityInPlace(Entity toBeReplaced, EntityConfig newEntityConfig)
	{
		EntityID id = toBeReplaced.Id;
		BoardData board = toBeReplaced.data.board;
		WorldPos worldpos = board.worldpos;
		float deg = board.deg;
		bool isEnabled = toBeReplaced.IsEnabled;
		toBeReplaced.components.GenerateUpgradeData(id, _tmp_upgradeData);
		DestroyEntity(toBeReplaced, shutdown: false);
		GridTransform transform = new GridTransform
		{
			pos = worldpos,
			deg = deg
		};
		Entity entity = CreateEntityImpl(newEntityConfig, transform, isEnabled, _: false);
		entity.components.ConsumeUpgradeData(_tmp_upgradeData);
		return entity;
	}

	public Entity MergeEntitiesInPlace(Entity toBeReplaced, Entity toBeMergedIn, EntityConfig newEntityConfig)
	{
		EntityID id = toBeMergedIn.Id;
		toBeMergedIn.components.GenerateUpgradeData(id, _tmp_secondaryMergeData);
		DestroyEntity(toBeMergedIn, shutdown: false);
		Entity entity = ReplaceEntityInPlace(toBeReplaced, newEntityConfig);
		entity.components.ConsumeUpgradeData(_tmp_secondaryMergeData);
		return entity;
	}

	public void SetInteresting(Entity building, Node node)
	{
		building.components.building.SetInteresting(interesting: true);
		node.interesting.Add(building.Id);
	}

	public void SetPotential(Entity building, Node node)
	{
		building.components.building.SetPotential(potential: true);
		node.potential = building.Id;
	}

	public void ClearInterestingAndPotential(Entity entity, bool shutdown)
	{
		if (shutdown || entity.components.building == null || entity.components.board == null)
		{
			return;
		}
		Node node = entity.components.board.GetNode();
		if (node != null)
		{
			entity.components.building.SetInteresting(interesting: false);
			entity.components.building.SetPotential(potential: false);
			if (node.interesting.Contains(entity.Id))
			{
				node.interesting.Remove(entity.Id);
			}
			if (node.potential == entity.Id)
			{
				node.potential = EntityID.INVALID;
			}
		}
	}

	public (Node target, List<WorldPos> result) NodeToWorldPath(List<NodeID> nodepath)
	{
		List<WorldPos> list = new List<WorldPos>();
		for (int i = 0; i < nodepath.Count - 1; i++)
		{
			Node node = nodepath[i].FindNode();
			Node target = nodepath[i + 1].FindNode();
			NodeEdge edgeToTarget = nodes.GetEdgeToTarget(node, target);
			list.Add(node.pos);
			List<RoadBead> roadBeads = edgeToTarget.roadBeads;
			if (roadBeads == null || roadBeads.Count <= 0)
			{
				continue;
			}
			float magnitudeSquared = (node.pos - roadBeads.FirstOrDefaultFast().pos).MagnitudeSquared;
			float magnitudeSquared2 = (node.pos - roadBeads.LastOrDefaultFast().pos).MagnitudeSquared;
			if (magnitudeSquared <= magnitudeSquared2)
			{
				int j = 0;
				for (int count = roadBeads.Count; j < count; j++)
				{
					list.Add(roadBeads[j].pos);
				}
			}
			else
			{
				for (int num = roadBeads.Count - 1; num >= 0; num--)
				{
					list.Add(roadBeads[num].pos);
				}
			}
		}
		Node node2 = nodepath.LastOrDefaultFast().FindNode();
		list.Add(node2.pos);
		return (target: node2, result: list);
	}

	public void GetGridPath(PlayerID pid, EntityID agentId, WorldPos source, WorldPos target, IPathContext ctx, Action<Pathfinding.Result> callback)
	{
		Node source2 = nodes.FindNearestNodeAround(source, 1f);
		Node target2 = nodes.FindNearestNodeAround(target, 1f);
		_gridPather.Run(pid, agentId, source2, target2, ctx, callback);
	}

	public Label GetMainEthnicityAtNode(Node node)
	{
		if (node.maineth.IsNotSet)
		{
			node.maineth = FindMainEthnicity(node.pos);
		}
		return node.maineth;
	}

	public Label FindMainEthnicity(WorldPos pos)
	{
		List<Label> ethnicitiesUniqueSorted = Game.ctx.session.mapconfig.GetEthnicitiesUniqueSorted();
		Label result = ethnicitiesUniqueSorted.FirstOrDefaultFast();
		float num = float.NegativeInfinity;
		foreach (Label item in ethnicitiesUniqueSorted)
		{
			float valueSafe = Game.ctx.heatmaps.FindEthnicityMap(item).GetValueSafe(pos);
			if (valueSafe > num)
			{
				num = valueSafe;
				result = item;
			}
		}
		return result;
	}

	public Entity PickEntityUnderCursor(Vector2 screenpos)
	{
		int count = Physics.RaycastNonAlloc(Game.serv.camera.ScreenToRay(screenpos), _hits);
		return PickClosestEntity(_hits, count);
	}

	private Entity PickClosestEntity(RaycastHit[] hits, int count)
	{
		float num = float.MaxValue;
		EntityID id = 0uL;
		for (int i = 0; i < count; i++)
		{
			RaycastHit raycastHit = hits[i];
			if (!(raycastHit.distance > num))
			{
				EntityID entityID = ModelProxyContainerComponent.FindEntityId(raycastHit.transform.gameObject);
				if (!entityID.IsNotValid)
				{
					num = raycastHit.distance;
					id = entityID;
				}
			}
		}
		return id.FindEntity();
	}

	public void OnBeforeSave()
	{
		SaveLoadUtils.GetEachOfType<ISaveObserver>(this).ForEach(delegate(SaveLoadUtils.MemberProvider<ISaveObserver> entry)
		{
			entry.provider.OnBeforeSave();
		});
	}

	public void OnAfterManagerLoad()
	{
		SaveLoadUtils.GetEachOfType<ILoadObserver>(this).ForEach(delegate(SaveLoadUtils.MemberProvider<ILoadObserver> entry)
		{
			entry.provider.OnAfterManagerLoad();
		});
	}

	public void OnAfterEntityLoad()
	{
		SaveLoadUtils.GetEachOfType<ILoadObserver>(this).ForEach(delegate(SaveLoadUtils.MemberProvider<ILoadObserver> entry)
		{
			entry.provider.OnAfterEntityLoad();
		});
	}

	public void Save(Serializer s, ConcurrentSaveTable results)
	{
		Hashtable result = new Hashtable();
		SaveLoadUtils.SaveMembersByNameInParallel(this, result);
		results.Set("board", s.Serialize(data));
	}

	public IEnumerator Load(Hashtable data)
	{
		SaveLoadUtils.DeserializeSingleKey(data, "board", delegate(BoardManagerData result)
		{
			this.data = result;
		});
		yield return Game.serv.sequencer.StartCoroutine(SaveLoadUtils.LoadMembersByNameCoroutine(this, data));
	}
}
