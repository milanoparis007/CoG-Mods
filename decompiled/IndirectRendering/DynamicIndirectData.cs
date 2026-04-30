using System.Collections.Generic;
using Game;
using Game.Core;
using Game.Services;
using SomaSim.Util;
using UnityEngine;

namespace IndirectRendering;

public class DynamicIndirectData
{
	public float GlobalLOD1;

	public float GlobalLOD2;

	public float GlobalLOD3;

	public float GlobalCull;

	public float GlobalBoundingRadius;

	public List<ModelInstanceData> _modelData;

	private List<List<ModelInstanceData>> _modelDataNested;

	public List<InstanceComputeData> _computeData;

	private List<List<InstanceComputeData>> _computeDataNested;

	private List<List<IndirectHandle>> _handles;

	private int _typeCount;

	private Dictionary<GameObject, int> _objectMap;

	private GameObject _spawnedGobjRoot;

	public List<int> _ModelInstanceCount;

	public List<int> _ModelIndexOffsets;

	public List<IndirectMeshData> _IndirectMeshData;

	public List<uint> _args;

	public List<LODRule> _LODRules;

	private bool _areDataResized;

	private bool _areDataModified;

	public int TypeCount => _typeCount;

	public bool IsDirty
	{
		get
		{
			if (!_areDataModified)
			{
				return _areDataResized;
			}
			return true;
		}
	}

	public void Push()
	{
		_areDataModified = false;
		if (_areDataResized)
		{
			_areDataResized = false;
			RebuildFlatLists();
		}
	}

	public DynamicIndirectData()
	{
		_modelData = new List<ModelInstanceData>();
		_computeData = new List<InstanceComputeData>();
		_modelDataNested = new List<List<ModelInstanceData>>();
		_computeDataNested = new List<List<InstanceComputeData>>();
		_objectMap = new Dictionary<GameObject, int>();
		_handles = new List<List<IndirectHandle>>();
		_ModelInstanceCount = new List<int>();
		_ModelIndexOffsets = new List<int>();
		_IndirectMeshData = new List<IndirectMeshData>();
		_args = new List<uint>();
		_LODRules = new List<LODRule>();
		_areDataModified = false;
		_areDataResized = false;
		_typeCount = 0;
		_spawnedGobjRoot = new GameObject("__SPAWNED GOBJ ROOT__");
	}

	public void CleanUpData()
	{
		if (_spawnedGobjRoot != null)
		{
			Object.Destroy(_spawnedGobjRoot);
		}
		_spawnedGobjRoot = null;
	}

	private string GetModelName(string prefabName)
	{
		int num = prefabName.LastIndexOf('/') + 1;
		if (num >= 0)
		{
			prefabName = prefabName.Substring(num);
		}
		prefabName = prefabName.Remove(prefabName.Length - "(Clone)".Length);
		return prefabName;
	}

	private int GameObjectToInt(GameObject gobj)
	{
		if (!_objectMap.ContainsKey(gobj))
		{
			_objectMap.Add(gobj, _typeCount);
			_handles.Add(new List<IndirectHandle>());
			_modelDataNested.Add(new List<ModelInstanceData>());
			_computeDataNested.Add(new List<InstanceComputeData>());
			_ModelInstanceCount.Add(0);
			_ModelIndexOffsets.Add(0);
			GameObject gameObject = Object.Instantiate(gobj, _spawnedGobjRoot.transform);
			string modelName = GetModelName(gameObject.name);
			LODDefinition lODDefinition = null;
			if (global::Game.Game.serv != null)
			{
				lODDefinition = global::Game.Game.serv.globals.settings.modelimport.modelconfigs.Find((LODDefinition cfg) => cfg.model == modelName);
			}
			if (lODDefinition == null)
			{
				lODDefinition = new LODDefinition
				{
					lodoverride = new LODDefinitionValues
					{
						lod1 = GlobalLOD1,
						lod2 = GlobalLOD2,
						lod3 = GlobalLOD3,
						cull = GlobalCull
					}
				};
			}
			gameObject.SetActive(value: false);
			MeshFilter[] componentsInChildren = gameObject.GetComponentsInChildren<MeshFilter>(includeInactive: true);
			IndirectMeshData indirectMeshData = new IndirectMeshData(gameObject.name, null, componentsInChildren, lODDefinition.GetLods());
			indirectMeshData.FillBufferArgs(ref _args, ref _LODRules);
			_IndirectMeshData.Add(indirectMeshData);
			_typeCount++;
		}
		return _objectMap[gobj];
	}

	public void UpdateTransform(IndirectHandle handle, Vector3 position, float rotation)
	{
		handle.runtimeData.position = position;
		handle.runtimeData.rotation = rotation;
		Quaternion quaternion = Quaternion.Euler(0f, rotation, 0f);
		Matrix4x4 trs = Matrix4x4.TRS(position + quaternion * (-handle.runtimeData.offset / 2f), quaternion, Vector3.one);
		ModelInstanceData value = _modelDataNested[handle.listIndex][handle.instanceIndex];
		InstanceComputeData value2 = _computeDataNested[handle.listIndex][handle.instanceIndex];
		value.trs = trs;
		value2.pos = position;
		_modelDataNested[handle.listIndex][handle.instanceIndex] = value;
		_computeDataNested[handle.listIndex][handle.instanceIndex] = value2;
		if (!_areDataResized)
		{
			int index = _ModelIndexOffsets[handle.listIndex] + handle.instanceIndex;
			_modelData[index] = value;
			_computeData[index] = value2;
		}
		_areDataModified = true;
	}

	public void UpdateOverlayColor(IndirectHandle handle, Color color)
	{
		ModelInstanceData value = _modelDataNested[handle.listIndex][handle.instanceIndex];
		value.overlayColor = color;
		handle.runtimeData.overlayColor = color;
		_modelDataNested[handle.listIndex][handle.instanceIndex] = value;
		if (!_areDataResized)
		{
			int index = _ModelIndexOffsets[handle.listIndex] + handle.instanceIndex;
			_modelData[index] = value;
		}
		_areDataModified = true;
	}

	public IndirectHandle InsertData(EntityID entityID, GameObject gobj, PerInstanceRuntimeData data)
	{
		int num = GameObjectToInt(gobj);
		Vector3 offset = data.offset;
		Vector3 position = data.position;
		Quaternion quaternion = Quaternion.Euler(0f, data.rotation, 0f);
		position += quaternion * (-offset / 2f);
		Matrix4x4 trs = Matrix4x4.TRS(position, quaternion, Vector3.one);
		InstanceComputeData item = new InstanceComputeData
		{
			pos = position,
			boundingRadius = GlobalBoundingRadius
		};
		ModelInstanceData item2 = new ModelInstanceData
		{
			trs = trs,
			paletteVars0 = data._paletteData.mat0,
			paletteVars1 = data._paletteData.mat1,
			paletteGroupIndex = data._paletteData.groupIndex,
			overlayColor = data.overlayColor,
			springHueShift = data.springHueShift,
			fallHueShift = data.fallHueShift,
			snowThreshold = data.snowThreshold,
			entityId = data.entityId,
			nodeId = data.nodeId
		};
		_modelDataNested[num].Add(item2);
		_computeDataNested[num].Add(item);
		IndirectHandle indirectHandle = new IndirectHandle
		{
			listIndex = num,
			instanceIndex = _handles[num].Count,
			runtimeData = data
		};
		_handles[num].Add(indirectHandle);
		_areDataResized = true;
		_areDataModified = true;
		return indirectHandle;
	}

	public void LogData()
	{
		string text = string.Empty;
		for (int i = 0; i < _IndirectMeshData.Count; i++)
		{
			int num = _ModelInstanceCount[i];
			int num2 = _ModelIndexOffsets[i];
			int num3 = i * 15;
			string text2 = $"[{_args[num3]},{_args[num3 + 1]},{_args[num3 + 2]},{_args[num3 + 3]},{_args[num3 + 4]}], " + $"[{_args[num3 + 5]},{_args[num3 + 6]},{_args[num3 + 7]},{_args[num3 + 8]},{_args[num3 + 9]}], " + $"[{_args[num3 + 10]},{_args[num3 + 11]},{_args[num3 + 12]},{_args[num3 + 13]},{_args[num3 + 14]}]";
			text += $"{i}: {num2}, {num} - {text2}\n";
		}
		Debug.Log(text);
	}

	public void RemoveData(IndirectHandle handle)
	{
		int listIndex = handle.listIndex;
		int count = _modelDataNested[listIndex].Count;
		int instanceIndex = handle.instanceIndex;
		int num = count - 1;
		List<ModelInstanceData> list = _modelDataNested[listIndex];
		List<InstanceComputeData> list2 = _computeDataNested[listIndex];
		List<IndirectHandle> list3 = _handles[listIndex];
		if (count > 1 && num != instanceIndex)
		{
			list.SwapRemoveAt(instanceIndex);
			list2.SwapRemoveAt(instanceIndex);
			list3.SwapRemoveAt(instanceIndex);
			list3[instanceIndex].instanceIndex = instanceIndex;
		}
		else
		{
			list.RemoveAt(instanceIndex);
			list2.RemoveAt(instanceIndex);
			list3.RemoveAt(instanceIndex);
		}
		_areDataResized = true;
		_areDataModified = true;
	}

	internal void ForceDirty()
	{
		_areDataModified = true;
	}

	private void RebuildFlatLists()
	{
		_modelData.Clear();
		_computeData.Clear();
		foreach (List<ModelInstanceData> item in _modelDataNested)
		{
			_modelData.AddRange(item);
		}
		foreach (List<InstanceComputeData> item2 in _computeDataNested)
		{
			_computeData.AddRange(item2);
		}
		int num = 0;
		for (int i = 0; i < _modelDataNested.Count; i++)
		{
			int count = _modelDataNested[i].Count;
			_ModelInstanceCount[i] = count;
			_ModelIndexOffsets[i] = num;
			num += count;
		}
	}
}
