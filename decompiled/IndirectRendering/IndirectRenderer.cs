using System.Runtime.InteropServices;
using Game;
using Game.Core;
using Game.Services;
using Game.Session;
using UnityEngine;
using UnityEngine.Rendering;

namespace IndirectRendering;

public class IndirectRenderer : MonoBehaviour
{
	private const int DISPATCH_THREADS_X = 88;

	private static IndirectRenderer _instance;

	private bool _initialized;

	private Camera _camera;

	[Header("Global Overrides")]
	public bool UseGlobalValues;

	public float GlobalLOD1;

	public float GlobalLOD2;

	public float GlobalLOD3;

	public float GlobalCull;

	public float GlobalBoundingRadius;

	[SerializeField]
	private Material _material;

	[Header("Controls")]
	[SerializeField]
	private bool _debugLOD;

	[SerializeField]
	private bool _useDrawCommandBuffer;

	[SerializeField]
	private bool _separateCullingOverFrames;

	[Space(20f)]
	[SerializeField]
	private bool _showLODByID;

	[SerializeField]
	private int _debugLODID;

	private Vector3 _prevCamPos;

	private Quaternion _prevCamRot;

	private Plane[] _cameraPlanes = new Plane[6];

	private Vector4[] _cameraFrustum = new Vector4[6];

	private ComputeBuffer _cmp_ArgsBuffer;

	private ComputeBuffer _cmp_ArgsFinalBuffer;

	private ComputeBuffer _cmp_LODRulesBuffer;

	private ComputeBuffer _cmp_ModelIDBucketBuffer;

	private ComputeBuffer _cmp_ModelIDFinalBuffer;

	private ComputeBuffer _cmp_InstanceDataBuffer;

	private ComputeBuffer _cmp_ComputeData;

	private ComputeBuffer _cmp_ModelInstanceCounts;

	private ComputeBuffer _cmp_ModelIndexOffsets;

	[Header("Shaders")]
	[SerializeField]
	private ComputeShader _shd_Culling;

	[SerializeField]
	private ComputeShader _shd_Sorting;

	private IndirectRendererStats _renderStats;

	private int _CullingKernelID;

	private int _SortingKernelID;

	private int _ModelBufferID = Shader.PropertyToID("_ModelBuffer");

	private int _ModelIDBucketBufferID = Shader.PropertyToID("_ModelIDBucketBuffer");

	private int _ModelIDFinalBufferID = Shader.PropertyToID("_ModelIDFinalBuffer");

	private int _ArgsBufferID = Shader.PropertyToID("_ArgsBuffer");

	private int _ArgsFinalBufferID = Shader.PropertyToID("_ArgsFinalBuffer");

	private int _ComputeDataID = Shader.PropertyToID("_ComputeData");

	private int _CamPositionID = Shader.PropertyToID("_CamPosition");

	private int _CamForwardID = Shader.PropertyToID("_CamForward");

	private int _CamFrustumID = Shader.PropertyToID("_CameraFrustum");

	private int _InstanceCountID = Shader.PropertyToID("_InstanceCount");

	private int _ModelCountID = Shader.PropertyToID("_ModelCount");

	private int _LODRulesID = Shader.PropertyToID("_LODRules");

	private int _ModelInstanceCountsID = Shader.PropertyToID("_ModelInstanceCounts");

	private int _ModelIndexOffsetsID = Shader.PropertyToID("_ModelIndexOffsets");

	public const int NUMBER_OF_ARGS_PER_DRAW = 5;

	public const int ARGS_BYTE_SIZE_PER_DRAW_CALL = 20;

	private DynamicIndirectData _dynamicIndirect;

	public static readonly int MIN_SHADER_LEVEL = 45;

	public static bool? IsAvailable = null;

	private bool _buffersCreated;

	private bool _buffersBound;

	private bool _asyncFlag = true;

	private void Awake()
	{
		if (_instance != null)
		{
			Object.Destroy(base.gameObject);
			return;
		}
		_instance = this;
		Logger.LogAlways($"Shader Level: {SystemInfo.graphicsShaderLevel}");
		Logger.LogAlways($"Supports Compute Shaders: {SystemInfo.supportsComputeShaders}");
		bool flag = SystemInfo.supportsComputeShaders && SystemInfo.graphicsShaderLevel >= MIN_SHADER_LEVEL;
		IsAvailable = flag;
		if (!flag)
		{
			Logger.LogAlways("Shader level does not support compute shaders; falling back on default instancing");
		}
		_camera = GetComponent<Camera>();
		_renderStats = GetComponent<IndirectRendererStats>();
		_CullingKernelID = _shd_Culling.FindKernel("CSMain");
		_SortingKernelID = _shd_Sorting.FindKernel("CSMain");
	}

	public static void WireUpDebug()
	{
		global::Game.Game.ctx.console.Add(_instance, new DebugConsoleEntry("camera", "renderdebugview", _instance.ResetDebugView));
	}

	private string ResetDebugView(string[] arg)
	{
		_camera.transform.parent.parent.position = new Vector3(123f, 0f, 130.5f);
		_camera.transform.parent.rotation = Quaternion.Euler(44.353f, 114.026f, 0f);
		_camera.transform.parent.localScale = Vector3.forward * 10f;
		return "Set to render debug position!";
	}

	private void OnApplicationFocus(bool focus)
	{
		if (focus)
		{
			ForceDirty();
		}
	}

	private void OnPreCull()
	{
		if (!_initialized || _dynamicIndirect == null)
		{
			return;
		}
		Shader.SetGlobalFloat("_EnableLODDebug", _debugLOD ? 1f : 0f);
		if (_useDrawCommandBuffer)
		{
			return;
		}
		Bounds bounds = new Bounds
		{
			center = _camera.transform.position,
			extents = Vector3.one * 10000f
		};
		int num = 0;
		for (int i = 0; i < _dynamicIndirect._IndirectMeshData.Count; i++)
		{
			IndirectMeshData indirectMeshData = _dynamicIndirect._IndirectMeshData[i];
			int num2 = indirectMeshData.lodData.Length;
			for (int j = num; j < num2; j++)
			{
				IndirectLODData indirectLODData = indirectMeshData.lodData[j];
				int argsOffset = indirectMeshData.argsOffset * 4 + 20 * j;
				ShadowCastingMode castShadows = ((j != num2 - 1) ? ShadowCastingMode.On : ShadowCastingMode.Off);
				Graphics.DrawMeshInstancedIndirect(indirectMeshData.mesh, 0, _material, bounds, _cmp_ArgsFinalBuffer, argsOffset, indirectLODData.propertyBlock, castShadows, receiveShadows: true, 0, null, LightProbeUsage.Off, null);
			}
		}
	}

	private void OnDestroy()
	{
		Shutdown();
	}

	private IndirectHandle privAddInstance(EntityID entityID, GameObject prefab, PerInstanceRuntimeData data)
	{
		return _dynamicIndirect?.InsertData(entityID, prefab, data);
	}

	public static IndirectHandle AddInstance(EntityID entityID, GameObject prefab, PerInstanceRuntimeData data)
	{
		return _instance.privAddInstance(entityID, prefab, data);
	}

	private void privRemoveInstance(IndirectHandle handle)
	{
		_dynamicIndirect?.RemoveData(handle);
	}

	public static void RemoveInstance(IndirectHandle handle)
	{
		_instance.privRemoveInstance(handle);
	}

	private void privUpdateTransform(IndirectHandle handle, Vector3 pos, float rot)
	{
		_dynamicIndirect?.UpdateTransform(handle, pos, rot);
	}

	public static void UpdateTransform(IndirectHandle handle, Vector3 pos, float rot)
	{
		_instance.privUpdateTransform(handle, pos, rot);
	}

	private void privUpdateOverlayColor(IndirectHandle handle, Color color)
	{
		_dynamicIndirect?.UpdateOverlayColor(handle, color);
	}

	public static void UpdateOverlayColor(IndirectHandle handle, Color color)
	{
		_instance.privUpdateOverlayColor(handle, color);
	}

	private void privForceDirty()
	{
		if (_dynamicIndirect != null)
		{
			_dynamicIndirect.ForceDirty();
		}
	}

	public static void ForceDirty()
	{
		if (_instance != null)
		{
			_instance.privForceDirty();
		}
	}

	private void BindMaterialData()
	{
		foreach (IndirectMeshData indirectMeshDatum in _dynamicIndirect._IndirectMeshData)
		{
			indirectMeshDatum.SetupPropertyBlocks();
		}
		_material.SetBuffer(_ArgsFinalBufferID, _cmp_ArgsFinalBuffer);
		_material.SetBuffer(_ModelIDFinalBufferID, _cmp_ModelIDFinalBuffer);
		_material.SetBuffer(_ModelBufferID, _cmp_InstanceDataBuffer);
	}

	private void BindComputeBufferData()
	{
		if (_dynamicIndirect._args.Count != 0 && _dynamicIndirect._modelData.Count != 0)
		{
			int count = _dynamicIndirect._args.Count / 5;
			_cmp_ArgsBuffer = new ComputeBuffer(count, 20, ComputeBufferType.DrawIndirect);
			_cmp_ArgsBuffer.SetData(_dynamicIndirect._args);
			_cmp_ArgsFinalBuffer = new ComputeBuffer(count, 20, ComputeBufferType.DrawIndirect);
			_cmp_ArgsFinalBuffer.SetData(_dynamicIndirect._args);
			_cmp_LODRulesBuffer = new ComputeBuffer(_dynamicIndirect._LODRules.Count, Marshal.SizeOf(typeof(LODRule)), ComputeBufferType.Default);
			_cmp_LODRulesBuffer.SetData(_dynamicIndirect._LODRules);
			_cmp_InstanceDataBuffer = new ComputeBuffer(_dynamicIndirect._modelData.Count, Marshal.SizeOf(typeof(ModelInstanceData)), ComputeBufferType.Default);
			_cmp_InstanceDataBuffer.SetData(_dynamicIndirect._modelData);
			_cmp_ComputeData = new ComputeBuffer(_dynamicIndirect._computeData.Count, Marshal.SizeOf(typeof(InstanceComputeData)), ComputeBufferType.Default);
			_cmp_ComputeData.SetData(_dynamicIndirect._computeData);
			_cmp_ModelIDBucketBuffer = new ComputeBuffer(_dynamicIndirect._modelData.Count * 4, 4, ComputeBufferType.Default);
			_cmp_ModelIDFinalBuffer = new ComputeBuffer(_dynamicIndirect._modelData.Count, 4, ComputeBufferType.Default);
			_cmp_ModelInstanceCounts = new ComputeBuffer(_dynamicIndirect._ModelInstanceCount.Count, 4, ComputeBufferType.Default);
			_cmp_ModelInstanceCounts.SetData(_dynamicIndirect._ModelInstanceCount);
			_cmp_ModelIndexOffsets = new ComputeBuffer(_dynamicIndirect._ModelIndexOffsets.Count, 4, ComputeBufferType.Default);
			_cmp_ModelIndexOffsets.SetData(_dynamicIndirect._ModelIndexOffsets);
			_buffersCreated = true;
		}
	}

	private void BindComputeShaderData()
	{
		if (_buffersCreated)
		{
			_shd_Culling.SetInt(_InstanceCountID, _dynamicIndirect._modelData.Count);
			_shd_Culling.SetInt(_ModelCountID, _dynamicIndirect._IndirectMeshData.Count);
			_shd_Culling.SetBuffer(_CullingKernelID, _ArgsBufferID, _cmp_ArgsBuffer);
			_shd_Culling.SetBuffer(_CullingKernelID, _LODRulesID, _cmp_LODRulesBuffer);
			_shd_Culling.SetBuffer(_CullingKernelID, _ComputeDataID, _cmp_ComputeData);
			_shd_Culling.SetBuffer(_CullingKernelID, _ModelInstanceCountsID, _cmp_ModelInstanceCounts);
			_shd_Culling.SetBuffer(_CullingKernelID, _ModelIndexOffsetsID, _cmp_ModelIndexOffsets);
			_shd_Culling.SetBuffer(_CullingKernelID, _ModelIDBucketBufferID, _cmp_ModelIDBucketBuffer);
			_shd_Sorting.SetInt(_InstanceCountID, _dynamicIndirect._modelData.Count);
			_shd_Sorting.SetInt(_ModelCountID, _dynamicIndirect._IndirectMeshData.Count);
			_shd_Sorting.SetBuffer(_SortingKernelID, _ArgsBufferID, _cmp_ArgsBuffer);
			_shd_Sorting.SetBuffer(_SortingKernelID, _ArgsFinalBufferID, _cmp_ArgsFinalBuffer);
			_shd_Sorting.SetBuffer(_SortingKernelID, _ModelIndexOffsetsID, _cmp_ModelIndexOffsets);
			_shd_Sorting.SetBuffer(_SortingKernelID, _ModelIDBucketBufferID, _cmp_ModelIDBucketBuffer);
			_shd_Sorting.SetBuffer(_SortingKernelID, _ModelIDFinalBufferID, _cmp_ModelIDFinalBuffer);
			_buffersBound = true;
		}
	}

	private void DoCulling()
	{
		Vector3 position = _camera.transform.position;
		Vector3 forward = _camera.transform.forward;
		GeometryUtility.CalculateFrustumPlanes(_camera, _cameraPlanes);
		for (int i = 0; i < 6; i++)
		{
			Plane plane = _cameraPlanes[i];
			Vector3 normal = plane.normal;
			float distance = plane.distance;
			_cameraFrustum[i] = new Vector4(normal.x, normal.y, normal.z, distance);
		}
		_shd_Culling.SetVector(_CamPositionID, position);
		_shd_Culling.SetVector(_CamForwardID, forward);
		_shd_Culling.SetVectorArray(_CamFrustumID, _cameraFrustum);
		_cmp_ArgsBuffer.SetData(_dynamicIndirect._args);
		_shd_Culling.Dispatch(_CullingKernelID, 88, 1, 1);
	}

	private void DoSorting()
	{
		_shd_Sorting.Dispatch(_SortingKernelID, 88, 1, 1);
	}

	private void Update()
	{
		if (_dynamicIndirect == null)
		{
			return;
		}
		bool flag = false;
		if (_dynamicIndirect.IsDirty)
		{
			_dynamicIndirect.Push();
			Shutdown();
			BindComputeBufferData();
			BindMaterialData();
			BindComputeShaderData();
			flag = true;
			if (_buffersBound && _buffersCreated)
			{
				_initialized = true;
			}
			_shd_Culling.GetKernelThreadGroupSizes(_CullingKernelID, out var _, out var _, out var _);
		}
		if (!_initialized)
		{
			return;
		}
		Shader.SetGlobalFloat("_EnableLODIDGroups", _showLODByID ? 1f : 0f);
		Shader.SetGlobalInt("_DebugLODID", _debugLODID);
		Vector3 position = _camera.transform.position;
		Quaternion rotation = _camera.transform.rotation;
		if (position != _prevCamPos || rotation != _prevCamRot || flag)
		{
			if (_separateCullingOverFrames && !flag)
			{
				if (_asyncFlag)
				{
					DoCulling();
				}
				else
				{
					DoSorting();
				}
				_asyncFlag = !_asyncFlag;
			}
			else
			{
				DoCulling();
				DoSorting();
			}
		}
		_prevCamPos = position;
		_prevCamRot = rotation;
	}

	public static bool TestBuffers()
	{
		return _instance.TestArgsHaveLODS();
	}

	private bool TestArgsHaveLODS()
	{
		uint[] array = new uint[_dynamicIndirect._args.Count];
		_cmp_ArgsFinalBuffer.GetData(array);
		uint num = 0u;
		for (int i = 1; i < array.Length; i += 5)
		{
			num += array[i];
		}
		return num != 0;
	}

	public static LODDefinitionValues GetLODDefinitionValues()
	{
		return new LODDefinitionValues
		{
			lod1 = _instance._dynamicIndirect.GlobalLOD1,
			lod2 = _instance._dynamicIndirect.GlobalLOD2,
			lod3 = _instance._dynamicIndirect.GlobalLOD3,
			cull = _instance._dynamicIndirect.GlobalCull
		};
	}

	private void Shutdown()
	{
		_cmp_ArgsBuffer = IndirectUtils.ReleaseBuffer(ref _cmp_ArgsBuffer);
		_cmp_ArgsFinalBuffer = IndirectUtils.ReleaseBuffer(ref _cmp_ArgsFinalBuffer);
		_cmp_InstanceDataBuffer = IndirectUtils.ReleaseBuffer(ref _cmp_InstanceDataBuffer);
		_cmp_ComputeData = IndirectUtils.ReleaseBuffer(ref _cmp_ComputeData);
		_cmp_LODRulesBuffer = IndirectUtils.ReleaseBuffer(ref _cmp_LODRulesBuffer);
		_cmp_ModelIDBucketBuffer = IndirectUtils.ReleaseBuffer(ref _cmp_ModelIDBucketBuffer);
		_cmp_ModelIDFinalBuffer = IndirectUtils.ReleaseBuffer(ref _cmp_ModelIDFinalBuffer);
		_cmp_ModelInstanceCounts = IndirectUtils.ReleaseBuffer(ref _cmp_ModelInstanceCounts);
		_cmp_ModelIndexOffsets = IndirectUtils.ReleaseBuffer(ref _cmp_ModelIndexOffsets);
		_buffersCreated = false;
		_buffersBound = false;
	}

	private void privPreGameSetup()
	{
		_dynamicIndirect = new DynamicIndirectData
		{
			GlobalCull = GlobalCull,
			GlobalLOD1 = GlobalLOD1,
			GlobalLOD2 = GlobalLOD2,
			GlobalLOD3 = GlobalLOD3,
			GlobalBoundingRadius = GlobalBoundingRadius
		};
		_initialized = false;
	}

	public static void PreGameSetup()
	{
		_instance.privPreGameSetup();
	}

	private void privPostGameDestroy()
	{
		Shutdown();
		_dynamicIndirect.CleanUpData();
		_dynamicIndirect = null;
		_initialized = false;
	}

	public static void PostGameDestroy()
	{
		_instance.privPostGameDestroy();
	}
}
