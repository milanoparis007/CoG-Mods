using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Game.Core;
using Game.Services;
using Game.Session.Heatmaps;
using SomaSim.SION;
using SomaSim.Util;

namespace Game.Session.Entities;

public class EntityManager : AbstractSessionManager, IAnimatingManager, ISessionManager, ISaveLoadProvider, IEntityLoadProvider, ISaveObserver, ILoadObserver
{
	[DebuggerDisplay("{DebugString}")]
	public struct Handle
	{
		public static readonly Handle EMPTY;

		public Entity entity;

		public int index;

		public int version;

		public bool IsAllocated => entity != null;

		private string DebugString => $"EHANDLE v{version} {entity}";

		public Handle MakeAllocated(Entity entity)
		{
			EntityID id = entity.Id;
			return new Handle
			{
				entity = entity,
				version = id.version,
				index = id.index
			};
		}

		internal Handle MakeLoaded(Entity entity)
		{
			EntityID id = entity.Id;
			return new Handle
			{
				entity = entity,
				version = id.version,
				index = id.index
			};
		}

		public Handle MakeDeallocated(EntityID eid)
		{
			return new Handle
			{
				entity = null,
				version = eid.version,
				index = eid.index
			};
		}
	}

	private class EntityHandleList
	{
		public List<Handle> handles;

		public List<EntityID> freelist;

		public int countAllocated;

		private const int DEFAULT_CAPACITY = 16384;

		public void Initialize()
		{
			handles = new List<Handle>(16384);
			freelist = new List<EntityID>();
			countAllocated = 0;
		}

		public void Release()
		{
		}

		public IEnumerable<Entity> GetAllEntitiesExpensive()
		{
			return from h in handles
				where h.entity != null
				select h.entity;
		}

		internal EntityID AddHandle(Entity entity, bool loaded)
		{
			if (!loaded)
			{
				return AddNewHandle(entity);
			}
			return AddWhileLoading(entity);
		}

		private EntityID AddNewHandle(Entity e)
		{
			if (freelist.Count == 0)
			{
				handles.Add(Handle.EMPTY);
				freelist.Add(EntityID.FromIndex(handles.Count - 1));
			}
			EntityID entityID = freelist.RemoveLast().IncrementVersion();
			Handle handle = handles[entityID.index];
			e.data.ident.id = entityID;
			handles[entityID.index] = handle.MakeAllocated(e);
			countAllocated++;
			return entityID;
		}

		private EntityID AddWhileLoading(Entity e)
		{
			if (e.Id.IsNotValid)
			{
				Logger.Warning("Invalid ID of entity being loaded!");
				return 0uL;
			}
			EntityID id = e.Id;
			if (id.index >= handles.Count)
			{
				Logger.Warning("Invalid handles array during loading, should have been expanded by now");
				return 0uL;
			}
			Handle handle = handles[id.index];
			if (handle.IsAllocated)
			{
				Logger.Warning("Loading an entity that is already allocated");
				return 0uL;
			}
			handles[id.index] = handle.MakeLoaded(e);
			countAllocated++;
			return id;
		}

		public EntityID Remove(Entity e)
		{
			EntityID id = e.Id;
			Handle handle = handles[id.index];
			handles[id.index] = handle.MakeDeallocated(id);
			freelist.Add(id);
			countAllocated--;
			return id;
		}

		public Entity Find(EntityID id)
		{
			if (id.index >= handles.Count)
			{
				return null;
			}
			Handle handle = handles[id.index];
			Entity entity = handle.entity;
			if (entity != null && handle.version != id.version)
			{
				return null;
			}
			return entity;
		}

		public Entity FindByIndex(int index)
		{
			if (index < 0 || index >= handles.Count)
			{
				return null;
			}
			return handles[index].entity;
		}

		public bool Contains(EntityID id)
		{
			Handle handle = ((id.index < handles.Count) ? handles[id.index] : Handle.EMPTY);
			if (handle.IsAllocated)
			{
				return handle.version == id.version;
			}
			return false;
		}
	}

	private struct WaitEntry : IEquatable<WaitEntry>
	{
		public string modid;

		public Hashtable def;

		public bool Equals(WaitEntry other)
		{
			if (def == other.def)
			{
				return modid == other.modid;
			}
			return false;
		}

		public static bool operator ==(WaitEntry a, WaitEntry b)
		{
			return a.Equals(b);
		}

		public static bool operator !=(WaitEntry a, WaitEntry b)
		{
			return !a.Equals(b);
		}

		public override bool Equals(object obj)
		{
			if (obj is WaitEntry waitEntry)
			{
				return this == waitEntry;
			}
			return false;
		}

		public override int GetHashCode()
		{
			return def.GetHashCode() ^ modid.GetHashCode();
		}
	}

	private sealed class UpdateList
	{
		public Dictionary<EntityID, Entity> toUpdate;

		public List<Entity> toAdd;

		public List<EntityID> toRemove;

		public EntityManager manager;

		public void Initialize(EntityManager manager)
		{
			this.manager = manager;
			toAdd = new List<Entity>(2048);
			toRemove = new List<EntityID>(2048);
			toUpdate = new Dictionary<EntityID, Entity>();
		}

		public void Release()
		{
			toUpdate.Clear();
			toRemove.Clear();
			toAdd.Clear();
			manager = null;
		}

		internal void AddToUpdates(Entity e)
		{
			toAdd.Add(e);
		}

		internal void RemoveFromUpdates(Entity e, bool shutdown)
		{
			if (shutdown)
			{
				RemoveImmediately(e);
			}
			else
			{
				toRemove.Add(e.Id);
			}
		}

		internal void UpdateOnFrame(GameAnimUpdate anim)
		{
			AddAllPending();
			RemoveAllPending();
			foreach (KeyValuePair<EntityID, Entity> item in toUpdate)
			{
				Entity value = item.Value;
				foreach (IAnimatedComponent item2 in value.components.updated)
				{
					if (value.IsDestroyed)
					{
						break;
					}
					item2.UpdateOnFrame(anim);
				}
			}
			RemoveAllPending();
		}

		private void AddAllPending()
		{
			while (toAdd.Count > 0)
			{
				Entity entity = toAdd.RemoveLastOrDefault();
				toUpdate.Add(entity.Id, entity);
			}
		}

		private void RemoveAllPending()
		{
			while (toRemove.Count > 0)
			{
				EntityID eid = toRemove.RemoveLastOrDefault();
				RemoveImmediately(eid);
			}
		}

		private void RemoveImmediately(EntityID eid)
		{
			Entity entity = toUpdate.FindOrNull(eid);
			RemoveImmediately(entity);
		}

		private void RemoveImmediately(Entity entity)
		{
			if (entity != null)
			{
				toUpdate.Remove(entity.Id);
				manager.OnRemovedFromUpdate(entity);
			}
		}
	}

	public sealed class DecoAutoplaceConfig
	{
		public List<EntityConfig> templates;

		public List<float> weights;

		public EntityConfig PickElement(IRandom rng)
		{
			return rng.PickElement(templates, weights);
		}
	}

	private class HandleBatching : ParallelBatchingSave.JobCollection<ulong, Handle>
	{
		public override string DebugName => "entities";

		public override ulong GetIndex(Handle entry)
		{
			return (ulong)entry.index;
		}

		public override Hashtable Serialize(Handle entry, Serializer s)
		{
			return SerializeHandle(entry, s);
		}
	}

	private struct LoadEntityResult
	{
		public enum Status
		{
			OK,
			FailMod,
			FailUnknown
		}

		public Status status;

		public string modid;
	}

	private static readonly HashSet<Entity> EMPTY_ENTITY_SET = new HashSet<Entity>();

	private static readonly HashSet<EntityConfig> EMPTY_ENTITY_CONFIG_SET = new HashSet<EntityConfig>();

	private EntityHandleList _entities;

	private bool _allEntitesLoaded;

	private LabelDictionary<EntityConfig> _configs;

	private LabelDictionary<Hashtable> _defprocessed;

	private List<WaitEntry> _defwaiting;

	private UpdateList _updateEntities;

	private LabelDictionary<HashSet<EntityConfig>> _tagToConfigsCache;

	private LabelDictionary<HashSet<EntityConfig>> _prefixToConfigsCache;

	private Dictionary<ZoneType, HashSet<EntityConfig>> _zoneToConfigsCache;

	private Dictionary<Type, HashSet<EntityConfig>> _componentToConfigsCache;

	private Dictionary<int, DecoAutoplaceConfig> _cachedDecoGroups;

	private LabelDictionary<List<MemberInfo>> _templateToDeclaredComponents;

	private EntityWithConfigCache<PersonConfig> _personCache;

	private EntityWithConfigCache<BizConfig> _bizCache;

	private EntityWithConfigCache<BuildingConfig> _buildingCache;

	private LabelDictionary<HashSet<Entity>> _tagToEntityCache;

	private LabelDictionary<HashSet<Entity>> _templateToEntityCache;

	private static List<MemberInfo> s_allTemplateConfigs;

	private Hashtable _loadData;

	public override bool IsInitializingDone => _allEntitesLoaded;

	public override void OnInitializeStarted()
	{
		base.OnInitializeStarted();
		_configs = new LabelDictionary<EntityConfig>(1024);
		_defprocessed = new LabelDictionary<Hashtable>(1024);
		_defwaiting = new List<WaitEntry>();
		_entities = new EntityHandleList();
		_entities.Initialize();
		_updateEntities = new UpdateList();
		_updateEntities.Initialize(this);
		_personCache = new EntityWithConfigCache<PersonConfig>();
		_bizCache = new EntityWithConfigCache<BizConfig>();
		_buildingCache = new EntityWithConfigCache<BuildingConfig>();
		_tagToEntityCache = new LabelDictionary<HashSet<Entity>>();
		_templateToEntityCache = new LabelDictionary<HashSet<Entity>>();
		_tagToConfigsCache = new LabelDictionary<HashSet<EntityConfig>>();
		_prefixToConfigsCache = new LabelDictionary<HashSet<EntityConfig>>();
		_zoneToConfigsCache = new Dictionary<ZoneType, HashSet<EntityConfig>>(new ZoneTypeEqualityComparer());
		_componentToConfigsCache = new Dictionary<Type, HashSet<EntityConfig>>();
		_cachedDecoGroups = new Dictionary<int, DecoAutoplaceConfig>();
		_templateToDeclaredComponents = new LabelDictionary<List<MemberInfo>>();
		LoadEverything();
	}

	public override void OnReleased()
	{
		if (_entities.countAllocated > 0)
		{
			Logger.Warning("Entity manager had entities at shutdown, count = " + _entities.countAllocated);
			foreach (Entity item in new List<Entity>(_entities.GetAllEntitiesExpensive()))
			{
				DestroyEntity(item, shutdown: true);
			}
		}
		_entities.Release();
		_updateEntities.Release();
		_tagToConfigsCache.ClearDeep();
		_prefixToConfigsCache.ClearDeep();
		_zoneToConfigsCache.ClearDeep();
		_componentToConfigsCache.ClearDeep();
		_cachedDecoGroups.Clear();
		_templateToDeclaredComponents.ClearDeep();
		_defwaiting.Clear();
		_defprocessed.Clear();
		_configs.Clear();
	}

	private void LoadEverything()
	{
		ArrayList entities = Game.serv.globals.entities;
		LoadArray(entities, null);
		foreach (WaitEntry item in _defwaiting)
		{
			Hashtable hashtable = item.def["ident"] as Hashtable;
			Logger.Warning("Template " + hashtable["template"]?.ToString() + " can't find parent " + hashtable["parent"]);
		}
		VerifyEntityConfigs();
		_allEntitesLoaded = true;
	}

	private void VerifyEntityConfigs()
	{
		Stopwatch stopwatch = Stopwatch.StartNew();
		foreach (EntityConfig value in _configs.Values)
		{
			value.VerityConfigDependencies();
			foreach (BaseConfig item in value.all)
			{
				item.VerifyAfterLoading(value);
			}
		}
		stopwatch.Stop();
	}

	private void LoadArray(ArrayList defs, string modid)
	{
		foreach (object def in defs)
		{
			try
			{
				LoadDefinition(def, modid);
			}
			catch (Exception ex)
			{
				Logger.Error("Failed to load entity definition: " + ((def is Hashtable hashtable && hashtable["ident"] is Hashtable) ? (hashtable["ident"] as Hashtable)["template"] : "(unknown)")?.ToString() + " -- " + ex.ToString());
			}
		}
	}

	private void LoadDefinition(object defobj, string modid)
	{
		if (!(defobj is Hashtable hashtable) || hashtable["ident"] == null || !(hashtable["ident"] is Hashtable))
		{
			Logger.Error("INVALID DEFINITION: " + defobj);
			return;
		}
		Hashtable hashtable2 = hashtable["ident"] as Hashtable;
		if (!(hashtable2["template"] is string value))
		{
			Logger.Error("Template needs a name: " + defobj);
			return;
		}
		Hashtable value2 = null;
		if (hashtable2["parent"] is string text && !_defprocessed.TryGetValue((Label)text, out value2))
		{
			_defwaiting.Add(new WaitEntry
			{
				def = hashtable,
				modid = modid
			});
			return;
		}
		Hashtable hashtable3 = ((value2 == null) ? hashtable : (HashtableMerger.Merge(value2, hashtable) as Hashtable));
		Label label = new Label(value);
		_defprocessed.Add(label, hashtable3);
		if (_configs.ContainsKey(label))
		{
			Logger.Error("Duplicate template:", label);
			return;
		}
		EntityConfig entityConfig = Game.serv.serializer.instance.Deserialize<EntityConfig>(hashtable3);
		if (entityConfig == null)
		{
			Logger.Error("Failed to deserialize entity template with name ", label);
			return;
		}
		entityConfig.all = TypeUtils.GetMemberInstances<BaseConfig>(entityConfig).ToList();
		_configs.Add(entityConfig.Template, entityConfig);
		entityConfig.ident.modid = modid;
		_templateToDeclaredComponents[entityConfig.Template] = GetTemplateDeclaredComponentMembers(entityConfig);
		if (entityConfig.ident.tags != null && entityConfig.ident.tags.Count > 0)
		{
			foreach (Label tag in entityConfig.ident.tags)
			{
				_tagToConfigsCache.AddToSet(tag, entityConfig);
			}
		}
		if (entityConfig.building != null && entityConfig.building.type != ZoneType.Unknown)
		{
			_zoneToConfigsCache.AddToSet(entityConfig.building.type, entityConfig);
		}
		foreach (BaseConfig item in entityConfig.all)
		{
			_componentToConfigsCache.AddToSet(item.GetType(), entityConfig);
		}
		ProcessWaitingOn(entityConfig.Template);
	}

	private void ProcessWaitingOn(Label tmplname)
	{
		foreach (WaitEntry item in _defwaiting.FindAll((WaitEntry entry) => (entry.def["ident"] as Hashtable)["parent"] as string == tmplname.String))
		{
			_defwaiting.Remove(item);
			LoadDefinition(item.def, item.modid);
		}
	}

	private List<MemberInfo> GetTemplateDeclaredComponentMembers(EntityConfig instance)
	{
		if (s_allTemplateConfigs == null)
		{
			s_allTemplateConfigs = TypeUtils.GetMembersByType(typeof(BaseConfig), typeof(EntityConfig));
		}
		return s_allTemplateConfigs.Where((MemberInfo member) => TypeUtils.GetValue(member, instance) != null).ToList();
	}

	public EntityConfig FindTemplate(Label name)
	{
		return _configs.FindOrNull(name);
	}

	public LabelDictionary<EntityConfig> GetAllTemplatesUnsafe()
	{
		return _configs;
	}

	public List<EntityConfig> FindTemplatesByTest(Predicate<EntityConfig> pred)
	{
		List<EntityConfig> list = new List<EntityConfig>();
		foreach (KeyValuePair<Label, EntityConfig> config in _configs)
		{
			EntityConfig value = config.Value;
			if (pred(value))
			{
				list.Add(value);
			}
		}
		return list;
	}

	public DecoAutoplaceConfig FindTemplatesForDecos(AppealLevel appeal, ZoneTypeFlags zone, GroundFlags ground)
	{
		int key = (int)((uint)appeal | (uint)((int)zone << 8)) | ((int)ground << 16);
		DecoAutoplaceConfig decoAutoplaceConfig = _cachedDecoGroups.FindOrNull(key);
		if (decoAutoplaceConfig != null)
		{
			return decoAutoplaceConfig;
		}
		List<EntityConfig> list = FindTemplatesByTest((EntityConfig cfg) => cfg.model != null && cfg.model.autoplace != null && cfg.model.autoplace.DoesMatch(appeal, zone, ground) && cfg.model.prefabs != null && cfg.model.prefabs.Count > 0);
		List<float> weights = list.SelectIntoNewList((EntityConfig cfg) => cfg.model.autoplace.weight);
		DecoAutoplaceConfig decoAutoplaceConfig2 = new DecoAutoplaceConfig
		{
			templates = list,
			weights = weights
		};
		_cachedDecoGroups.Add(key, decoAutoplaceConfig2);
		return decoAutoplaceConfig2;
	}

	public Entity Find(EntityID id)
	{
		return _entities.Find(id);
	}

	public Entity FindByIndex(int index)
	{
		return _entities.FindByIndex(index);
	}

	public bool Contains(EntityID id)
	{
		return _entities.Contains(id);
	}

	public Entity CreateByName(Label tmplname)
	{
		EntityConfig entityConfig = FindTemplate(tmplname);
		if (entityConfig != null)
		{
			return CreateByTemplate(entityConfig);
		}
		return null;
	}

	public Entity CreateByTemplate(EntityConfig configs, EntityData savedata = null)
	{
		bool loaded = savedata != null;
		Entity entity = GenerateNewEntity(configs, savedata);
		if (entity == null)
		{
			Logger.Error("Failed to create entity type", configs.Template);
			return null;
		}
		EntityID entityID = _entities.AddHandle(entity, loaded);
		if (entityID.IsNotValid)
		{
			Logger.Warning("Can't create entity with id ", entityID, " - manager already contains " + _entities.Find(entityID).Name);
			return null;
		}
		if (entity.components.HasUpdatedComponents)
		{
			_updateEntities.AddToUpdates(entity);
		}
		AddToCaches(entity);
		List<BaseComponent> all = entity.components.all;
		int count = all.Count;
		for (int i = 0; i < count; i++)
		{
			all[i].OnAfterEntityCreated(loaded);
		}
		return entity;
	}

	private Entity GenerateNewEntity(EntityConfig configs, EntityData savedata)
	{
		Entity entity = new Entity();
		entity.config = configs;
		try
		{
			entity.components = CreateComponentsFromConfigs(entity);
			entity.data = MoveOrCreateData(entity, savedata);
			return entity;
		}
		catch (Exception ex)
		{
			Logger.Error(ex);
			return null;
		}
	}

	public void DestroyEntity(Entity entity, bool shutdown)
	{
		_ = (ulong)entity.Id;
		List<BaseComponent> all = entity.components.all;
		int i = 0;
		for (int count = all.Count; i < count; i++)
		{
			all[i].OnBeforeEntityDestroyed(shutdown);
		}
		RemoveFromCaches(entity, shutdown);
		if (entity.components.HasUpdatedComponents)
		{
			_updateEntities.RemoveFromUpdates(entity, shutdown);
		}
		else
		{
			FinalizeDestroyingEntity(entity);
		}
	}

	internal void OnRemovedFromUpdate(Entity entity)
	{
		FinalizeDestroyingEntity(entity);
	}

	private void FinalizeDestroyingEntity(Entity entity)
	{
		_entities.Remove(entity);
		entity.data = null;
		entity.components = DestroyComponents(entity);
		entity.config = null;
	}

	private void AddToCaches(Entity e)
	{
		_templateToEntityCache.AddToSet(e.config.Template, e);
		_personCache.AddIfConfigPresent(e, e.config.person);
		_bizCache.AddIfConfigPresent(e, e.config.biz);
		_buildingCache.AddIfConfigPresent(e, e.config.building);
		TagList tags = e.config.ident.tags;
		if (tags == null)
		{
			return;
		}
		foreach (Label item in tags)
		{
			_tagToEntityCache.AddToSet(item, e);
		}
	}

	private void RemoveFromCaches(Entity e, bool shutdown)
	{
		TagList tags = e.config.ident.tags;
		if (tags != null)
		{
			foreach (Label item in tags)
			{
				_tagToEntityCache.RemoveFromSet(item, e, removeEmptySet: true);
			}
		}
		_buildingCache.RemoveIfConfigPresent(e, e.config.building);
		_bizCache.RemoveIfConfigPresent(e, e.config.biz);
		_personCache.RemoveIfConfigPresent(e, e.config.person);
		_templateToEntityCache.RemoveFromSet(e.config.Template, e, removeEmptySet: true);
	}

	public HashSet<Entity> GetCachedEntitiesPersonsUnsafe()
	{
		return _personCache;
	}

	public HashSet<Entity> GetCachedEntitiesBizUnsafe()
	{
		return _bizCache;
	}

	public HashSet<Entity> GetCachedEntitiesBuildingsUnsafe()
	{
		return _buildingCache;
	}

	public HashSet<Entity> GetCachedEntitiesByTagUnsafe(Label tag)
	{
		return _tagToEntityCache.FindOrNull(tag) ?? EMPTY_ENTITY_SET;
	}

	public HashSet<Entity> GetCachedEntitiesByTemplateUnsafe(Label template)
	{
		return _templateToEntityCache.FindOrNull(template) ?? EMPTY_ENTITY_SET;
	}

	public HashSet<EntityConfig> GetCachedConfigsByTagUnsafe(Label tag)
	{
		return _tagToConfigsCache.FindOrNull(tag) ?? EMPTY_ENTITY_CONFIG_SET;
	}

	public HashSet<EntityConfig> GetCachedConfigsByZoneTypeUnsafe(ZoneType type)
	{
		return _zoneToConfigsCache.FindOrNull(type) ?? EMPTY_ENTITY_CONFIG_SET;
	}

	public HashSet<EntityConfig> GetCachedConfigsByComponentUnsafe<T>() where T : BaseConfig
	{
		return GetCachedConfigsByComponentUnsafe(typeof(T));
	}

	public HashSet<EntityConfig> GetCachedConfigsByComponentUnsafe(Type type)
	{
		return _componentToConfigsCache.FindOrNull(type);
	}

	public HashSet<EntityConfig> FindCachedTemplatesByPrefix(Label prefix)
	{
		HashSet<EntityConfig> hashSet = _prefixToConfigsCache.FindOrNull(prefix);
		if (hashSet == null)
		{
			string trimmed = prefix.String.TrimEnd('*');
			hashSet = new HashSet<EntityConfig>(from template in _configs
				where template.Key.String.StartsWith(trimmed)
				select template.Value);
			_prefixToConfigsCache[prefix] = hashSet;
		}
		return hashSet;
	}

	public List<Entity> GenerateListOfAllEntities()
	{
		return (from handle in _entities.handles
			where handle.IsAllocated
			select handle.entity).ToList();
	}

	public void UpdateAnimations(GameAnimUpdate anim)
	{
		_updateEntities.UpdateOnFrame(anim);
	}

	public void OnBeforeSave()
	{
	}

	public void Save(Serializer s, ConcurrentSaveTable results)
	{
		List<Handle> handles = _entities.handles;
		HandleBatching handleBatching = Game.serv.saveload.batcher.MakeParallelJobs<HandleBatching, ulong, Handle>(handles, 1000);
		handleBatching.RunAllJobs();
		results.Set("entities", handleBatching.collector.ValuesToArrayList());
		results.Set("freelist", s.Serialize(_entities.freelist));
		results.Set("count", s.Serialize(_entities.countAllocated));
	}

	public IEnumerator Load(Hashtable data)
	{
		_loadData = data;
		yield break;
	}

	public IEnumerator DelayedEntityLoad()
	{
		if (_loadData != null)
		{
			Hashtable data = _loadData;
			_loadData = null;
			List<string> mods = new List<string>();
			List<string> failed = new List<string>();
			ArrayList handles = data["entities"] as ArrayList;
			yield return Game.serv.sequencer.StartCoroutine(DeserializeHandles(handles, mods, failed));
			SaveLoadUtils.DeserializeSingleKey(data, "freelist", delegate(List<EntityID> result)
			{
				_entities.freelist = result;
			});
			SaveLoadUtils.DeserializeSingleKey(data, "count", delegate(int result)
			{
				_entities.countAllocated = result;
			});
		}
	}

	public void OnAfterManagerLoad()
	{
	}

	public void OnAfterEntityLoad()
	{
	}

	private static Hashtable SerializeHandle(Handle h, Serializer s)
	{
		Hashtable hashtable = new Hashtable();
		hashtable["v"] = s.Serialize(h.version);
		hashtable["i"] = s.Serialize(h.index);
		if (h.entity != null)
		{
			Entity entity = h.entity;
			OnComponentSave(entity, before: true);
			hashtable["tmpl"] = s.Serialize(entity.config.Template);
			hashtable["data"] = s.Serialize(entity.data, specifyValueTypes: false);
			OnComponentSave(entity, before: false);
		}
		return hashtable;
	}

	private IEnumerator DeserializeHandles(ArrayList handles, List<string> mods, List<string> failed)
	{
		_entities.handles = ListExtensions.MakeFilled(handles.Count, Handle.EMPTY);
		Serializer s = Game.serv.serializer.instance;
		int i = 0;
		for (int count = handles.Count; i < count; i++)
		{
			Hashtable data = handles[i] as Hashtable;
			LoadEntityResult loadEntityResult = LoadAndCreateEntity(s, data, mods);
			if (loadEntityResult.status == LoadEntityResult.Status.FailMod)
			{
				failed.Add(loadEntityResult.modid);
			}
			if (i > 0 && i % 500 == 0)
			{
				yield return null;
			}
		}
		int j = 0;
		for (int count2 = _entities.handles.Count; j < count2; j++)
		{
			_ = _entities.handles[j].entity;
		}
	}

	private LoadEntityResult LoadAndCreateEntity(Serializer s, Hashtable data, List<string> mods)
	{
		if (!data.ContainsKey("v") || !data.ContainsKey("i"))
		{
			Logger.Warning("Invalid saved handle: " + data);
			return new LoadEntityResult
			{
				status = LoadEntityResult.Status.FailUnknown
			};
		}
		int version = s.Deserialize<int>(data["v"]);
		int index = s.Deserialize<int>(data["i"]);
		_entities.handles[index] = new Handle
		{
			version = version,
			index = index
		};
		if (!(data["tmpl"] is string text))
		{
			return new LoadEntityResult
			{
				status = LoadEntityResult.Status.OK
			};
		}
		EntityData entityData = s.Deserialize<EntityData>(data["data"]);
		if (entityData == null)
		{
			Logger.Warning("Failed to deserialize entity data for " + text);
			return new LoadEntityResult
			{
				status = LoadEntityResult.Status.FailUnknown
			};
		}
		string modid = entityData.ident.modid;
		if (modid != null && !mods.Contains(modid))
		{
			return new LoadEntityResult
			{
				status = LoadEntityResult.Status.FailMod,
				modid = modid
			};
		}
		EntityConfig entityConfig = FindTemplate((Label)text);
		if (entityConfig == null)
		{
			Logger.Warning("Unknown saved template: " + text);
			return new LoadEntityResult
			{
				status = LoadEntityResult.Status.FailUnknown,
				modid = modid
			};
		}
		OnComponentLoad(CreateByTemplate(entityConfig, entityData));
		return new LoadEntityResult
		{
			status = LoadEntityResult.Status.OK,
			modid = modid
		};
	}

	private static void OnComponentLoad(Entity e)
	{
		List<BaseComponent> all = e.components.all;
		int i = 0;
		for (int count = all.Count; i < count; i++)
		{
			if (all[i] is ILoadObserverComponent loadObserverComponent)
			{
				loadObserverComponent.OnAfterLoading();
			}
		}
	}

	private static void OnComponentSave(Entity e, bool before)
	{
		List<BaseComponent> all = e.components.all;
		int i = 0;
		for (int count = all.Count; i < count; i++)
		{
			if (all[i] is ISaveObserverComponent saveObserverComponent)
			{
				if (before)
				{
					saveObserverComponent.OnBeforeSaving();
				}
				else
				{
					saveObserverComponent.OnAfterSaving();
				}
			}
		}
	}

	private EntityComponents CreateComponentsFromConfigs(Entity entity)
	{
		EntityComponents entityComponents = new EntityComponents();
		entityComponents.AddComponents(entity);
		return entityComponents;
	}

	private EntityComponents DestroyComponents(Entity entity)
	{
		entity.components.RemoveComponents();
		return null;
	}

	private EntityData MoveOrCreateData(Entity entity, EntityData source = null)
	{
		EntityData entityData = new EntityData();
		foreach (BaseConfig item in entity.config.all)
		{
			item.MoveOrCreateData(entityData, source);
		}
		return entityData;
	}

	private bool IsMissingThisField(EntityData data, string name)
	{
		MemberInfo[] member = typeof(EntityData).GetMember(name);
		if (member == null || member.Length == 0)
		{
			return false;
		}
		MemberInfo[] array = member;
		for (int i = 0; i < array.Length; i++)
		{
			if (TypeUtils.GetValue(array[i], data) != null)
			{
				return false;
			}
		}
		return true;
	}
}
