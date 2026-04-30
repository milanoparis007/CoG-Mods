using System.Collections.Generic;
using Game.Services;
using UnityEngine;

namespace IndirectRendering;

public class IndirectMeshData
{
	public const uint LOD_COUNT = 4u;

	public Mesh mesh;

	public int argsOffset;

	public readonly string name;

	public IndirectLODData[] lodData;

	private float _lodDist1;

	private float _lodDist2;

	private float _lodDist3;

	private float _cullDist;

	private uint[] _tempArgsBuffer = new uint[5];

	public IndirectMeshData(string name, Material mat, MeshFilter[] filters, LODDefinitionValues lodDef)
	{
		this.name = name;
		_lodDist1 = lodDef.lod1;
		_lodDist2 = lodDef.lod2;
		_lodDist3 = lodDef.lod3;
		_cullDist = lodDef.cull;
		List<CombineInstance> list = new List<CombineInstance>();
		List<MeshFilter> list2 = new List<MeshFilter>(filters);
		for (int i = filters.Length; (long)i < 4L; i++)
		{
			list2.Add(filters[filters.Length - 1]);
		}
		while ((long)list2.Count > 4L)
		{
			list2.RemoveAt(list2.Count - 1);
		}
		lodData = new IndirectLODData[list2.Count];
		int count = list2.Count;
		for (int j = 0; j < count; j++)
		{
			Mesh mesh = list2[j].mesh;
			lodData[j] = new IndirectLODData
			{
				numVerts = (uint)mesh.vertexCount,
				numIndices = mesh.GetIndexCount(0),
				propertyBlock = new MaterialPropertyBlock()
			};
			CombineInstance item = new CombineInstance
			{
				mesh = mesh
			};
			list.Add(item);
		}
		this.mesh = new Mesh();
		this.mesh.name = $"{name} Combined {count} LOD";
		this.mesh.CombineMeshes(list.ToArray(), mergeSubMeshes: true, useMatrices: false, hasLightmapData: false);
	}

	public void FillBufferArgs(ref List<uint> args, ref List<LODRule> lodRules)
	{
		argsOffset = args.Count;
		int num = lodData.Length;
		uint num2 = 0u;
		for (int i = 0; i < num; i++)
		{
			_ = args.Count;
			uint numIndices = lodData[i].numIndices;
			_tempArgsBuffer[0] = numIndices;
			_tempArgsBuffer[1] = 0u;
			_tempArgsBuffer[2] = num2;
			_tempArgsBuffer[3] = 0u;
			_tempArgsBuffer[4] = 0u;
			args.AddRange(_tempArgsBuffer);
			num2 += numIndices;
		}
		LODRule item = new LODRule
		{
			dist1 = _lodDist1,
			dist2 = _lodDist2,
			dist3 = _lodDist3,
			cullDist = _cullDist
		};
		lodRules.Add(item);
	}

	public void SetupPropertyBlocks()
	{
		int num = lodData.Length;
		for (int i = 0; i < num; i++)
		{
			IndirectLODData obj = lodData[i];
			Color value = Color.blue;
			switch (i)
			{
			case 0:
				value = Color.red;
				break;
			case 1:
				value = Color.yellow;
				break;
			case 2:
				value = Color.green;
				break;
			}
			int value2 = argsOffset + i * 5 + 4;
			obj.propertyBlock.SetInt("_ArgsOffset", value2);
			obj.propertyBlock.SetColor("_DebugLODColor", value);
		}
	}
}
