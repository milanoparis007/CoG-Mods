using System;
using Game.Session.Assets;
using SomaSim.Util;
using UnityEngine;

public class ShaderTest : MonoBehaviour
{
	private IRandom _rng;

	public void SetNewValues()
	{
		_rng = new Xorshift();
		_rng.Init(DateTime.Now);
		int childCount = base.transform.childCount;
		for (int i = 0; i < childCount; i++)
		{
			MeshRenderer[] componentsInChildren = base.transform.GetChild(i).GetComponentsInChildren<MeshRenderer>();
			VertexPaletteData vertexPaletteData = new VertexPaletteData();
			vertexPaletteData.GenerateNewValues(_rng);
			MeshRenderer[] array = componentsInChildren;
			foreach (MeshRenderer obj in array)
			{
				MaterialPropertyBlock block = new MaterialPropertyBlock();
				obj.GetPropertyBlock(block);
				vertexPaletteData.SetPropertyBlock(ref block);
				obj.SetPropertyBlock(block);
			}
		}
	}

	public void SetNewValues(VertexPaletteData data)
	{
		int childCount = base.transform.childCount;
		for (int i = 0; i < childCount; i++)
		{
			MeshRenderer[] componentsInChildren = base.transform.GetChild(i).GetComponentsInChildren<MeshRenderer>();
			MaterialPropertyBlock block = new MaterialPropertyBlock();
			data.SetPropertyBlock(ref block);
			MeshRenderer[] array = componentsInChildren;
			for (int j = 0; j < array.Length; j++)
			{
				array[j].SetPropertyBlock(block);
			}
		}
	}
}
