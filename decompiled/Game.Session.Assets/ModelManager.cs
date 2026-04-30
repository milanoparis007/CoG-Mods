using System;
using System.Collections.Generic;
using Game.Core;
using Game.Session.Entities;
using IndirectRendering;
using SomaSim.Util;
using UnityEngine;

namespace Game.Session.Assets;

public sealed class ModelManager : AbstractSessionManager, IAnimatingManager, ISessionManager, ICityGenManager
{
	private const int INSTANTIATIONS_PER_FRAME_INTERACTIVE = 50;

	private const int INSTANTIATIONS_PER_FRAME_CITYGEN = 1000;

	private GameObject _container;

	private Dictionary<string, GameObject> _modelTemplates;

	private Dictionary<EntityID, ModelProxy> _modelProxies;

	private PriorityHashSet<ModelProxy> _modelsToLoad;

	private bool _allowModelLoadSuppression;

	private bool _forceImmediateLoading = true;

	private bool _isIndirectEnabled;

	private List<VertexPaletteCategory> _paletteData;

	private Xorshift _rng;

	private Xorshift _entityRng;

	private bool _trimmedOnce;

	private DynamicMesh _combinedRoadsMesh;

	private Transform _colliderRoot;

	private List<ModelProxy> _tmp_proxies = new List<ModelProxy>();

	internal bool IsIndirectEnabled => _isIndirectEnabled;

	public DynamicMesh CombinedRoadsMesh => _combinedRoadsMesh;

	public bool AllowModelLoadSuppression => _allowModelLoadSuppression;

	public Transform EntityColliderRoot => _colliderRoot;

	public ModelManager()
	{
		_allowModelLoadSuppression = !Game.serv.globals.settings.general.debug.showGoonsAtStartup;
		_forceImmediateLoading = true;
		_colliderRoot = new GameObject("Entity Collider Root").transform;
	}

	public override void OnInitializeDone()
	{
		_rng = Game.ctx.scenario.MakeSeededRng<ModelManager>();
		_entityRng = new Xorshift(_rng.y);
		_isIndirectEnabled = Game.serv.camera.IsIndirectInstancingAvailable && Game.serv.saveload.prefs.game.computeShaders;
		_container = new GameObject();
		_container.name = "3D Models";
		_container.transform.position = default(Vector3);
		_modelTemplates = new Dictionary<string, GameObject>();
		_modelProxies = new Dictionary<EntityID, ModelProxy>();
		_modelsToLoad = new PriorityHashSet<ModelProxy>();
		_trimmedOnce = false;
		VertexPaletteData.InitializeGlobals();
		_combinedRoadsMesh = new DynamicMesh("COMBINED ROADS Mesh");
		_combinedRoadsMesh.SetMaterial("Entities");
		VertexPaletteData data = GeneratePaletteValues(PaletteType.PropsTerrain);
		_combinedRoadsMesh.SetData(data, ColorConstants.OVERLAY_DEFAULT);
	}

	public VertexPaletteData GeneratePaletteValues(PaletteType type, Entity entity = null)
	{
		List<VertexPaletteCategoryInfo> list = new List<VertexPaletteCategoryInfo>();
		_entityRng.Init(entity?.components.ident?.GetIdentityHash() ?? _rng.Generate());
		List<VertexPaletteCategory> categories;
		int groupid;
		switch (type)
		{
		default:
			categories = Game.serv.globals.settings.general.palette.buildings.categories;
			groupid = Game.serv.globals.settings.general.palette.buildings.groupid;
			break;
		case PaletteType.PropsTerrain:
			categories = Game.serv.globals.settings.general.palette.environment.categories;
			groupid = Game.serv.globals.settings.general.palette.environment.groupid;
			break;
		case PaletteType.Undiscovered:
			categories = Game.serv.globals.settings.general.palette.undiscovered.categories;
			groupid = Game.serv.globals.settings.general.palette.undiscovered.groupid;
			break;
		case PaletteType.VehiclesPeople:
			categories = Game.serv.globals.settings.general.palette.vehiclespeople.categories;
			groupid = Game.serv.globals.settings.general.palette.vehiclespeople.groupid;
			break;
		}
		foreach (VertexPaletteCategory item2 in categories)
		{
			VertexPaletteCategoryInfo item = new VertexPaletteCategoryInfo
			{
				indices = item2.indices,
				value = _entityRng.Generate(0, item2.numVariants)
			};
			list.Add(item);
		}
		VertexPaletteData vertexPaletteData = new VertexPaletteData();
		vertexPaletteData.ConsumeCategoryList(list);
		vertexPaletteData.groupIndex = groupid;
		return vertexPaletteData;
	}

	public override void OnPreInteractive()
	{
	}

	public override void OnReleased()
	{
		_combinedRoadsMesh.Release();
		_combinedRoadsMesh = null;
		_modelsToLoad.Clear();
		foreach (ModelProxy value in _modelProxies.Values)
		{
			RemoveModelProxy(value, shutdown: true);
		}
		_modelProxies.Clear();
		foreach (GameObject value2 in _modelTemplates.Values)
		{
			UnityEngine.Object.Destroy(value2);
		}
		_modelTemplates.Clear();
		UnityEngine.Object.Destroy(_container);
		_container = null;
	}

	public void UpdateAnimations(GameAnimUpdate anim)
	{
		InstantiateQueuedModels(50);
	}

	public void OnCityGenStarted()
	{
	}

	public void OnCityGenTurn()
	{
		InstantiateQueuedModels(1000);
	}

	public void OnCityGenDone()
	{
	}

	private void InstantiateQueuedModels(int count)
	{
		int num = _modelsToLoad.MoveToList(count, _tmp_proxies);
		if (num > 0)
		{
			while (_tmp_proxies.Count > 0)
			{
				ModelProxy proxy = _tmp_proxies.RemoveLast();
				MakeModelForProxy(proxy, wasLoadedAsync: true);
			}
		}
		if (num > 0 && _modelsToLoad.Count == 0 && !_trimmedOnce)
		{
			_trimmedOnce = true;
			_modelsToLoad.TrimExcess();
		}
		if (_tmp_proxies.Capacity > 500)
		{
			_tmp_proxies.TrimExcess();
		}
	}

	private void SpawnModelSlots(ModelProxy proxy, Entity entity, bool isKnown)
	{
		ModelConfig model = entity.config.model;
		PaletteType type = (PaletteType)Mathf.Clamp(isKnown ? model.paletterow : model.paletterowunknown, 0, 3);
		VertexPaletteData paletteData = GeneratePaletteValues(type, entity);
		ModelSlotContainer currentModelOverrideConfig = entity.components.model.CurrentModelOverrideConfig;
		ModelSlotContainer modelSlotContainer = ((isKnown && currentModelOverrideConfig != null) ? currentModelOverrideConfig : (isKnown ? model.slots : model.unknownslots));
		if (modelSlotContainer != null)
		{
			_modelProxies[entity.Id] = proxy;
			modelSlotContainer.DoRoutine(delegate(List<ModelSlot> slots2, bool iscore)
			{
				SpawnModelSlot(proxy, entity, paletteData, iscore, ref slots2);
			});
			return;
		}
		List<string> list = (isKnown ? model.prefabs : model.unknowns);
		if (list == null || list.Count == 0)
		{
			list = model.prefabs;
		}
		entity.data.ident.rng.PickElement(list);
		List<ModelSlot> slots = list.ConvertAll((string str) => new ModelSlot
		{
			model = str
		});
		SpawnModelSlot(proxy, entity, paletteData, _: true, ref slots);
	}

	private void SpawnModelSlot(ModelProxy proxy, Entity entity, VertexPaletteData paletteData, bool _, ref List<ModelSlot> slots)
	{
		if (slots != null && slots.Count != 0)
		{
			ModelConfig model = entity.config.model;
			ModelSlot modelSlot = entity.data.ident.rng.PickElement(slots);
			if (model.indirectInstancing && IsIndirectEnabled)
			{
				float springFoliageHue = Game.ctx.seasons.GetSpringFoliageHue();
				float fallFoliageHue = Game.ctx.seasons.GetFallFoliageHue();
				float fogHideShift = model.FogHideShift;
				float snowThresholdForTrees = Game.ctx.seasons.GetSnowThresholdForTrees();
				int nodeId = entity.components.board?.GetNode()?.id.index ?? (-1);
				PerInstanceRuntimeData data = new PerInstanceRuntimeData
				{
					position = Vector3.zero,
					rotation = 0f,
					overlayColor = ColorConstants.OVERLAY_DEFAULT,
					offset = entity.components.model.FindModelOffset(),
					_paletteData = paletteData,
					springHueShift = springFoliageHue,
					fallHueShift = fallFoliageHue,
					fogHideShift = fogHideShift,
					snowThreshold = snowThresholdForTrees,
					entityId = entity.Id.index,
					nodeId = nodeId
				};
				float x = _rng.Generate(0f - modelSlot.plusminus.x, modelSlot.plusminus.x);
				float y = _rng.Generate(0f - modelSlot.plusminus.y, modelSlot.plusminus.y);
				ModelSlot slotData = new ModelSlot
				{
					model = modelSlot.model,
					offset = modelSlot.offset,
					plusminus = new WorldPos(x, y)
				};
				GameObject orMakeTemplate = GetOrMakeTemplate(modelSlot.model);
				IndirectHandle indirectHandle = IndirectRenderer.AddInstance(entity.Id, orMakeTemplate, data);
				ModelSubProxy subproxy = new ModelSubProxy
				{
					indirectHandle = indirectHandle,
					slotData = slotData
				};
				proxy.PushSubproxy(subproxy);
			}
			else
			{
				proxy.prefabName = modelSlot.model;
				MakeModelForProxy(proxy, wasLoadedAsync: false);
			}
		}
	}

	public void CreateModelProxy(Entity entity, bool isKnown, Action<ModelProxy> onProxyCreationFn)
	{
		ModelConfig model = entity.config.model;
		ModelProxy modelProxy = new ModelProxy
		{
			entity = entity,
			status = ModelProxy.Status.Uninitialized
		};
		_modelProxies[entity.Id] = modelProxy;
		onProxyCreationFn(modelProxy);
		if (entity.data.model.hidden && _allowModelLoadSuppression)
		{
			SetStatusHidden(entity, model, modelProxy);
		}
		else if (_forceImmediateLoading || model.ShouldLoadImmediately)
		{
			SpawnModelSlots(modelProxy, entity, isKnown);
		}
		else
		{
			EnqueueForLoading(modelProxy, entity);
		}
	}

	private static void SetStatusHidden(Entity entity, ModelConfig config, ModelProxy proxy)
	{
		proxy.prefabName = entity.data.ident.rng.PickElement(config.prefabs);
		proxy.status = ModelProxy.Status.Hidden;
	}

	private void EnqueueForLoading(ModelProxy proxy, Entity entity)
	{
		int loadingPriority = entity.config.model.LoadingPriority;
		_modelsToLoad.Add(loadingPriority, proxy);
		proxy.status = ModelProxy.Status.Queued;
	}

	public void RemoveModelProxy(ModelProxy proxy, bool shutdown)
	{
		if (proxy.IsShowing)
		{
			proxy.Teardown();
		}
		if (!shutdown)
		{
			if (proxy.IsQueued)
			{
				int loadingPriority = proxy.entity.config.model.LoadingPriority;
				_modelsToLoad.Remove(loadingPriority, proxy);
				proxy.status = ModelProxy.Status.Uninitialized;
			}
			_modelProxies.Remove(proxy.entity.Id);
		}
	}

	public bool RevealModelIfHidden(Entity entity)
	{
		ModelProxy modelProxy = _modelProxies.FindOrNull(entity.Id);
		if (modelProxy != null && modelProxy.IsHidden)
		{
			entity.components.model.SetHidden(this, value: false);
			EnqueueForLoading(modelProxy, entity);
			return true;
		}
		return false;
	}

	private void MakeModelForProxy(ModelProxy proxy, bool wasLoadedAsync)
	{
		GameObject gameObject = UnityEngine.Object.Instantiate(GetOrMakeTemplate(proxy.prefabName));
		gameObject.name = $"{proxy.prefabName}/{proxy.entity.Id}";
		gameObject.SetActive(value: true);
		gameObject.GetComponent<ModelProxyContainerComponent>().Set(proxy.entity.Id);
		LODGroup componentInChildren = gameObject.GetComponentInChildren<LODGroup>();
		if (componentInChildren != null)
		{
			componentInChildren.size = 5f;
			componentInChildren.localReferencePoint = Vector3.zero;
		}
		ModelSubProxy subproxy = new ModelSubProxy
		{
			model = gameObject
		};
		proxy.PushSubproxy(subproxy);
		proxy.status = ModelProxy.Status.Ready;
		proxy.entity.components.model.OnAfterModelInstantiated(wasLoadedAsync);
	}

	private GameObject GetOrMakeTemplate(string name)
	{
		GameObject gameObject = _modelTemplates.FindOrNull(name);
		if (gameObject == null)
		{
			GameObject gameObject2 = Resources.Load(name) as GameObject;
			_ = gameObject2 == null;
			try
			{
				gameObject = UnityEngine.Object.Instantiate(gameObject2);
				gameObject.AddComponent<ModelProxyContainerComponent>();
				gameObject.name = "TMPL " + name;
				gameObject.SetActive(value: false);
				_modelTemplates.Add(name, gameObject);
			}
			catch (Exception ex)
			{
				Logger.Error("Failed to make model", name, "\n", ex.Message, ex.StackTrace);
			}
		}
		return gameObject;
	}
}
