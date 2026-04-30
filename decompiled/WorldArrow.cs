using System;
using System.Collections.Generic;
using SomaSim.Util;
using UnityEngine;

[ExecuteAlways]
public class WorldArrow : MonoBehaviour
{
	public float width = 1f;

	public float length = 1f;

	public float archeight = 1f;

	public int segments = 5;

	private float _lastSize;

	private List<Vector3> _verts = new List<Vector3>();

	private List<Vector2> _uvs = new List<Vector2>();

	private List<int> _indices = new List<int>();

	private void Start()
	{
		RecreateMesh(force: true);
	}

	private void Reset()
	{
		RecreateMesh(force: true);
	}

	private void Update()
	{
		RecreateMesh(force: false);
	}

	public void Move(Vector3 center, float deg, float length)
	{
		Quaternion rotation = base.transform.rotation;
		rotation.eulerAngles = rotation.eulerAngles.SetY(deg);
		base.transform.rotation = rotation;
		base.transform.position = center;
		this.length = length;
		RecreateMesh(force: true);
	}

	public void Recolor(Color c)
	{
		base.gameObject.GetComponent<MeshRenderer>().material.color = c;
	}

	private void RecreateMesh(bool force)
	{
		float num = width * length;
		if (num != _lastSize || force)
		{
			_lastSize = num;
			_verts.Clear();
			_uvs.Clear();
			_indices.Clear();
			float num2 = 0.2f;
			float segsize = 0.3f;
			MakeQuad(new Vector3(0f - num2, 0f, 0f), num2, 0, nouv: true);
			float num3 = length / (float)segments;
			for (int i = 0; i < segments; i++)
			{
				MakeQuad(new Vector3((float)i * num3, 0f), num3, i, nouv: false);
			}
			MakeQuad(new Vector3(length, 0f, 0f), num2, 0, nouv: true);
			MakePointyQuad(new Vector3(length + num2, 0f), segsize);
			RecenterAndDefineArc();
			Mesh mesh = new Mesh();
			mesh.SetVertices(_verts);
			mesh.SetTriangles(_indices, 0);
			mesh.SetUVs(0, _uvs);
			mesh.RecalculateNormals();
			mesh.RecalculateTangents();
			mesh.RecalculateBounds();
			base.gameObject.GetComponent<MeshFilter>().mesh = mesh;
		}
	}

	private void RecenterAndDefineArc()
	{
		double num = Math.PI / (double)length;
		for (int i = 0; i < _verts.Count; i++)
		{
			float y = (float)Math.Sin((double)_verts[i].x * num) * archeight;
			_verts[i] = _verts[i].Add(0f, y, 0f);
		}
		Vector3 vector = new Vector3((0f - length) / 2f, 0f, (0f - width) / 2f);
		for (int j = 0; j < _verts.Count; j++)
		{
			_verts[j] += vector;
		}
	}

	private void MakePointyQuad(Vector3 start, float segsize)
	{
		MakeQuad(start, segsize, 0, nouv: true);
		Vector3 value = Vector3.Lerp(_verts[_verts.Count - 1], _verts[_verts.Count - 2], 0.5f);
		_verts[_verts.Count - 1] = value;
		_verts[_verts.Count - 2] = value;
	}

	private void MakeQuad(Vector3 start, float segsize, int segindex, bool nouv)
	{
		int count = _verts.Count;
		_indices.Add(count);
		_indices.Add(count + 1);
		_indices.Add(count + 2);
		_indices.Add(count + 2);
		_indices.Add(count + 3);
		_indices.Add(count);
		_verts.Add(start.Add(0f, 0f, 0f));
		_verts.Add(start.Add(0f, 0f, width));
		_verts.Add(start.Add(segsize, 0f, width));
		_verts.Add(start.Add(segsize, 0f, 0f));
		Vector2 vec = new Vector2((float)segindex * segsize, 0f);
		Vector2 vector = (nouv ? Vector2.zero : new Vector2(segsize, 1f));
		_uvs.Add(vec.Add(0f, 0f));
		_uvs.Add(vec.Add(0f, vector.y));
		_uvs.Add(vec.Add(vector.x, vector.y));
		_uvs.Add(vec.Add(vector.x, 0f));
	}
}
