using System.Collections.Generic;
using UnityEngine;

namespace IndirectRendering;

public class IndirectRendererStats : MonoBehaviour
{
	private List<IndirectStats> _renderStats = new List<IndirectStats>();

	public void UpdateRenderStats(ref List<IndirectMeshData> meshData, ref ComputeBuffer argsBuffer)
	{
		uint[] array = new uint[meshData.Count * 5 * 3];
		argsBuffer.GetData(array);
		_renderStats = new List<IndirectStats>();
		for (int i = 0; i < meshData.Count; i++)
		{
			IndirectMeshData indirectMeshData = meshData[i];
			IndirectStats indirectStats = new IndirectStats();
			indirectStats.name = indirectMeshData.name;
			indirectStats.totalInstances = 0u;
			int argsOffset = indirectMeshData.argsOffset;
			int num = 1;
			for (int j = 0; j < indirectMeshData.lodData.Length; j++)
			{
				uint num2 = array[argsOffset + num];
				IndirectLODData obj = indirectMeshData.lodData[j];
				uint totalVerts = obj.numVerts * num2;
				uint totalIndices = obj.numIndices * num2;
				LODStats lODStats = new LODStats();
				lODStats.visibleInstanceCount = num2;
				lODStats.totalVerts = totalVerts;
				lODStats.totalIndices = totalIndices;
				indirectStats.stats.Add(lODStats);
				num += 5;
			}
			_renderStats.Add(indirectStats);
		}
	}

	public void OnGUI()
	{
		GUIStyle gUIStyle = new GUIStyle();
		gUIStyle.fontSize = 50;
		if (_renderStats.Count > 0)
		{
			uint num = 0u;
			uint num2 = 0u;
			uint num3 = 0u;
			foreach (IndirectStats renderStat in _renderStats)
			{
				num2 += renderStat.TotalVisibleInstances;
				num3 += renderStat.totalInstances;
				num += renderStat.TotalVerts;
			}
			GUILayout.Label("Total Instances: " + num2.ToString("N0") + " / " + num3.ToString("N0"), gUIStyle);
			GUILayout.Label("Total Verts: " + num.ToString("N0"), gUIStyle);
		}
		else
		{
			GUILayout.Label("No Render Stats Gathered..", gUIStyle);
		}
	}
}
