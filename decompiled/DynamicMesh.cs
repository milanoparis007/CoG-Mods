using System;
using System.Collections.Generic;
using Game.Session.Assets;
using Game.Session.Setup;
using UnityEngine;
using UnityEngine.Rendering;

public class DynamicMesh
{
	internal List<int> _triangles;

	internal List<Vector3> _vertices;

	internal List<Color> _colors;

	private Mesh _mesh;

	private GameObject _gobj;

	private MeshRenderer _renderer;

	private MeshFilter _filter;

	private List<DynamicMeshHandle> _handles = new List<DynamicMeshHandle>();

	public DynamicMesh(string name = "Dynamic Mesh")
	{
		_mesh = new Mesh();
		_gobj = new GameObject(name);
		_mesh = new Mesh();
		_mesh.MarkDynamic();
		_renderer = _gobj.AddComponent<MeshRenderer>();
		_renderer.shadowCastingMode = ShadowCastingMode.Off;
		_filter = _gobj.AddComponent<MeshFilter>();
		_filter.sharedMesh = _mesh;
		_vertices = new List<Vector3>();
		_colors = new List<Color>();
		_triangles = new List<int>();
	}

	public void SetMaterial(string resourcesPath)
	{
		_renderer.material = Resources.Load<Material>(resourcesPath);
	}

	public void SetData(VertexPaletteData data, Color overlayColor)
	{
		MaterialPropertyBlock block = new MaterialPropertyBlock();
		_renderer.GetPropertyBlock(block);
		data.SetPropertyBlock(ref block);
		block.SetColor("_OverlayColor", overlayColor);
		_renderer.SetPropertyBlock(block);
	}

	public void Release()
	{
		_triangles = null;
		_colors = null;
		_vertices = null;
		_filter = null;
		_renderer = null;
		_mesh = null;
		if ((bool)_gobj)
		{
			Debug.Log("Destroying Dynamic Mesh: " + _gobj.name);
			UnityEngine.Object.Destroy(_gobj);
		}
	}

	public void PushUpdates()
	{
		_mesh.Clear();
		_mesh.vertices = _vertices.ToArray();
		_mesh.triangles = _triangles.ToArray();
		_mesh.colors = _colors.ToArray();
		_mesh.RecalculateNormals();
		_mesh.RecalculateBounds();
		_filter.sharedMesh = _mesh;
	}

	public DynamicMeshHandle PushMesh(GameObject gobj)
	{
		DynamicMeshHandle dynamicMeshHandle = new DynamicMeshHandle(this);
		MeshFilter componentInChildren = gobj.GetComponentInChildren<MeshFilter>();
		Vector3 position = componentInChildren.transform.position;
		Mesh mesh = componentInChildren.mesh;
		int count = _vertices.Count;
		Vector3[] vertices = mesh.vertices;
		foreach (Vector3 point in vertices)
		{
			Vector3 vector = CreateTransitTiles.RotateXZAroundPoint(0f, 0f, (float)Math.PI / 180f * (0f - componentInChildren.transform.rotation.eulerAngles.y), point);
			Vector3 item = position + vector;
			_vertices.Add(item);
		}
		int[] triangles = mesh.triangles;
		for (int i = 0; i < triangles.Length; i++)
		{
			int item2 = triangles[i] + count;
			_triangles.Add(item2);
		}
		Color[] colors = mesh.colors;
		foreach (Color item3 in colors)
		{
			_colors.Add(item3);
		}
		gobj.SetActive(value: false);
		_handles.Add(dynamicMeshHandle);
		return dynamicMeshHandle;
	}

	public void RemoveMesh(DynamicMeshHandle handle)
	{
		_handles.Remove(handle);
		if (_handles.Count == 0)
		{
			Release();
		}
	}
}
