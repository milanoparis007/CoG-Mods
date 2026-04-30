using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Session.Assets;
using Game.Session.Setup;
using SomaSim.Util;
using UnityEngine;

namespace Game.Session.Entities;

public sealed class ModelComponent : BaseComponent, IEntityEventObserverComponent
{
	private const float MUL_ACTIVE = 1.5f;

	private const float MUL_FOCUSED = 1.2f;

	private const float MUL_HIGHLIGHT = 1f;

	private const float MUL_NONE = 1f;

	private ModelProxy _proxy;

	private bool _isKnown;

	private VertexPaletteData _paletteData;

	private List<MeshRenderer> _meshRenderers;

	private BoxCollider _collider;

	private static MaterialPropertyBlock s_props = new MaterialPropertyBlock();

	public ModelConfig Config => _baseConfig as ModelConfig;

	public ModelProxy Proxy => _proxy;

	public ModelConfig.SlotOverride CurrentModelOverrideType => _entity.data.model.modeloverride;

	public ModelSlotContainer CurrentModelOverrideConfig => _entity.config.model.GetSlotOverrideOrNull(CurrentModelOverrideType);

	public override void OnAfterEntityCreated(bool loaded)
	{
		base.OnAfterEntityCreated(loaded);
		if (!loaded)
		{
			_entity.data.model.hidden = Config.suppressload && Game.ctx.models.AllowModelLoadSuppression;
		}
		_isKnown = IsBoardComponentKnown();
		CreateProxy();
		CreateCollider();
	}

	public override void OnBeforeEntityDestroyed(bool shutdown)
	{
		DestroyCollider();
		DestroyProxy(shutdown);
		base.OnBeforeEntityDestroyed(shutdown);
	}

	private void CreateProxy()
	{
		Game.ctx.models.CreateModelProxy(_entity, _isKnown, delegate(ModelProxy proxy)
		{
			_proxy = proxy;
		});
	}

	private void DestroyProxy(bool shutdown)
	{
		Game.ctx.models.RemoveModelProxy(_proxy, shutdown);
		_proxy = null;
	}

	public WorldPos GetModelPosition()
	{
		return new WorldPos(_proxy.position);
	}

	public float GetModelRotation()
	{
		return _proxy.rotation.eulerAngles.y;
	}

	public Vector3 GetModelForward()
	{
		return GetMainModel().transform.forward;
	}

	public Vector3 GetModelRight()
	{
		return GetMainModel().transform.right;
	}

	public Mesh GetMeshUnshared()
	{
		return GetMainModel().GetComponentInChildren<MeshFilter>().mesh;
	}

	public Vector3 GetVector3InLocalSpace(Vector3 inVec)
	{
		return GetMainModel().transform.TransformVector(inVec);
	}

	public GameObject GetMainModel()
	{
		return _proxy.GetSubProxy(0).model;
	}

	public void SetHandle(DynamicMeshHandle handle)
	{
		_proxy.dynamicHandle = handle;
	}

	internal Vector3 FindModelOffset()
	{
		if (_entity.config.model.loadtype != ModelConfig.LoadType.Building)
		{
			return Vector3.zero;
		}
		return _entity.config.board.lotsize.AsVector3XZ;
	}

	private float FindVerticalTerrainOffset(WorldPos pos)
	{
		return Game.ctx.board.terrain.Heightmap.GetHeightAtPosition(pos);
	}

	public void MoveModel(WorldPos pos, float deg)
	{
		float y = (Config.hillsokay ? FindVerticalTerrainOffset(pos) : 0f);
		Quaternion rotation = _proxy.rotation;
		rotation.eulerAngles = rotation.eulerAngles.SetY(deg);
		ProxySetPositionAndRotation(new Vector3(pos.x, y, pos.y), rotation);
	}

	public void RotateModel(float rotation)
	{
		Quaternion rotation2 = _proxy.rotation;
		rotation2.eulerAngles = rotation2.eulerAngles.SetY(rotation);
		ProxySetPositionAndRotation(_proxy.position, rotation2);
	}

	public void DebugRecolorExpensive(Color c)
	{
		GetMainModel().GetComponentInChildren<MeshRenderer>().material.color = c;
	}

	internal void SetHidden(ModelManager _, bool value)
	{
		_entity.data.model.hidden = value;
	}

	private void ProxySetPositionAndRotation(Vector3 position, Quaternion rotation)
	{
		_proxy.SetPositionAndRotation(position, rotation);
		UpdateColliderPositionAndRotation(position, rotation);
	}

	private void CreateCollider()
	{
		if (_entity.config.model.loadtype == ModelConfig.LoadType.Building && _entity.config.selection != null && _entity.config.selection.onfocus != SelectionConfig.OnFocus.None)
		{
			float colliderHeight = _entity.config.board.colliderHeight;
			Vector3 asVector3XZ = _entity.data.board.worldpos.AsVector3XZ;
			Vector3 vec = FindModelOffset();
			Transform transform = GameObject.CreatePrimitive(PrimitiveType.Cube).transform;
			transform.name = $"{_entity.Id} Collider";
			transform.position = asVector3XZ + Vector3.up * colliderHeight / 2f;
			_collider = transform.GetComponent<BoxCollider>();
			transform.localScale = vec.SetY(colliderHeight);
			transform.SetParent(Game.ctx.models.EntityColliderRoot);
			_collider.GetComponent<MeshRenderer>().enabled = false;
			_collider.gameObject.AddComponent<ModelProxyContainerComponent>().eid = _entity.Id;
		}
	}

	private void DestroyCollider()
	{
		if (_collider != null)
		{
			Object.Destroy(_collider.gameObject);
		}
	}

	private void UpdateColliderPositionAndRotation(Vector3 position, Quaternion rotation)
	{
		if (!(_collider == null))
		{
			float colliderHeight = _entity.config.board.colliderHeight;
			_collider.transform.position = position + Vector3.up * colliderHeight / 2f;
			_collider.transform.rotation = rotation;
		}
	}

	internal void OnAfterModelInstantiated(bool async)
	{
		UpdateCachedComponents();
		SetOverlayColor(ColorConstants.OVERLAY_DEFAULT);
		UpdateStretchedRoadMesh();
		RefreshActiveModel();
		RefreshSelectionHighlight();
		if (async)
		{
			_entity.components.SendEvent(EntityEventType.EntityModelLoadedAsync);
		}
	}

	private void UpdateCachedComponents()
	{
		PaletteType type = (PaletteType)Mathf.Clamp(_isKnown ? Config.paletterow : Config.paletterowunknown, 0, 3);
		_paletteData = Game.ctx.models.GeneratePaletteValues(type, _entity);
		_meshRenderers = _proxy.GetAllSubProxiesUnsafe().SelectMany((ModelSubProxy p) => p.model.GetComponentsInChildren<MeshRenderer>()).ToList();
	}

	private void UpdateStretchedRoadMesh()
	{
		if (_entity.data?.board?.bead.HasValidRoadBead == true)
		{
			NodeEdge edge = Game.ctx.board.nodes.GetEdge(_entity.data.board.bead.edgeId);
			RoadBead bead = edge.roadBeads[_entity.data.board.bead.roadBeadIndex];
			CreateTransitTiles.RotateVertsOnEntity(edge, bead, _entity);
			DynamicMeshHandle handle = Game.ctx.models.CombinedRoadsMesh.PushMesh(_proxy.GetSubProxy(0).model);
			SetHandle(handle);
		}
	}

	public bool CanUseModelOverride(ModelConfig.SlotOverride[] types)
	{
		foreach (ModelConfig.SlotOverride type in types)
		{
			if (CanUseModelOverride(type))
			{
				return true;
			}
		}
		return false;
	}

	public bool CanUseModelOverride(ModelConfig.SlotOverride type)
	{
		return _entity.config.model.GetSlotOverrideOrNull(type) != null;
	}

	public bool IsUsingModelOverride(ModelConfig.SlotOverride type)
	{
		return CurrentModelOverrideType == type;
	}

	public bool UseModelOverride(ModelConfig.SlotOverride type)
	{
		if (!CanUseModelOverride(type))
		{
			return false;
		}
		if (CurrentModelOverrideType == type)
		{
			return false;
		}
		_entity.data.model.modeloverride = type;
		RefreshActiveModel(forceReloadModel: true);
		return true;
	}

	public void OnEntityEvent(EntityEventType eet)
	{
		switch (eet)
		{
		case EntityEventType.EntityKnownChanged:
			RefreshActiveModel();
			RefreshSelectionHighlight();
			break;
		case EntityEventType.EntityActivationChanged:
		case EntityEventType.EntityFocusChanged:
		case EntityEventType.EntityHighlightChanged:
			RefreshSelectionHighlight();
			break;
		}
	}

	private bool IsBoardComponentKnown()
	{
		PlayerID humanPlayer = PlayerID.HumanPlayer;
		if (_entity.components.board != null)
		{
			return _entity.components.board.IsKnown(humanPlayer);
		}
		return true;
	}

	private void RefreshActiveModel(bool forceReloadModel = false)
	{
		if (_proxy.IsShowing)
		{
			Vector3 position = _proxy.position;
			Quaternion rotation = _proxy.rotation;
			bool flag = IsBoardComponentKnown();
			if (_isKnown != flag || forceReloadModel)
			{
				DestroyProxy(shutdown: false);
				_isKnown = flag;
				CreateProxy();
				UpdateInstancedColors();
			}
			ProxySetPositionAndRotation(position, rotation);
		}
	}

	private void RefreshSelectionHighlight()
	{
		if (_proxy != null && _proxy.IsShowing && _entity.IsEnabled)
		{
			SelectionComponent.VizStack.State? state = _entity.components.selection?.vizstate.GetMax();
			float amount = ((state == SelectionComponent.VizStack.State.Active) ? 1.5f : ((state == SelectionComponent.VizStack.State.Focus) ? 1.2f : ((state == SelectionComponent.VizStack.State.Highlight) ? 1f : 1f)));
			UpdateInstancedColors(amount);
		}
	}

	private void UpdateInstancedColors()
	{
		UpdateInstancedColors(1f);
	}

	private void UpdateInstancedColors(float amount)
	{
		if (_meshRenderers == null)
		{
			return;
		}
		float springFoliageHue = Game.ctx.seasons.GetSpringFoliageHue();
		float fallFoliageHue = Game.ctx.seasons.GetFallFoliageHue();
		float snowThresholdForTrees = Game.ctx.seasons.GetSnowThresholdForTrees();
		int value = base.entity.components.board?.GetNode()?.id.index ?? (-1);
		int index = _entity.Id.index;
		foreach (MeshRenderer meshRenderer in _meshRenderers)
		{
			meshRenderer.GetPropertyBlock(s_props);
			_paletteData.SetPropertyBlock(ref s_props);
			s_props.SetFloat("_SpringHueShift", springFoliageHue);
			s_props.SetFloat("_FallHueShift", fallFoliageHue);
			s_props.SetFloat("_FogHideShift", Config.FogHideShift);
			s_props.SetFloat("_Multiplier", amount);
			s_props.SetFloat("_SnowThreshold", snowThresholdForTrees);
			s_props.SetInt("_EntityID", index);
			s_props.SetInt("_NodeID", value);
			meshRenderer.SetPropertyBlock(s_props);
		}
	}

	public void SetOverlayColor(Color col)
	{
		_proxy.SetColor(col);
		if (_meshRenderers == null)
		{
			return;
		}
		foreach (MeshRenderer meshRenderer in _meshRenderers)
		{
			meshRenderer.GetPropertyBlock(s_props);
			s_props.SetColor("_OverlayColor", col);
			meshRenderer.SetPropertyBlock(s_props);
		}
	}
}
