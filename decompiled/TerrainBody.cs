using System.Collections.Generic;
using Game.Core;
using SomaSim.Util;
using UnityEngine;

public class TerrainBody : MonoBehaviour
{
	[SerializeField]
	private SkinnedMeshRenderer _skinnedMeshRenderer;

	[SerializeField]
	private MeshCollider _meshCollider;

	[SerializeField]
	private Transform[] _joints;

	private Vector3[] _startPositions;

	private float _minHeight;

	private float _maxHeight;

	private float _width;

	private Vector2[] _verts;

	public bool AllowBridges = true;

	public void GenerateTerrainBodyVariant(float minHeight, float maxHeight, float width, IRandom rng, List<WorldPos> deformers)
	{
		_minHeight = minHeight;
		_maxHeight = maxHeight;
		_width = width;
		RandomizeJointPositions(rng, deformers);
		UpdateMeshCollider();
	}

	private void TryInitializePositions()
	{
		if (_startPositions == null)
		{
			_startPositions = new Vector3[_joints.Length];
			for (int i = 0; i < _joints.Length; i++)
			{
				_startPositions[i] = _joints[i].position;
			}
		}
	}

	private void RandomizeJointPositions(IRandom rng, List<WorldPos> deformers)
	{
		TryInitializePositions();
		int num = deformers?.Count ?? 0;
		for (int i = 0; i < _joints.Length; i++)
		{
			float y = rng.Generate(_minHeight, _maxHeight);
			Vector3 position = _joints[i].position;
			position = ((i >= num) ? (_startPositions[i] + new Vector3(rng.GenerateFloat() * _width, y, rng.GenerateFloat() * _width) * 0.5f) : new Vector3(deformers[i].x, y, deformers[i].y));
			_joints[i].position = position;
		}
	}

	private void UpdateMeshCollider()
	{
		Mesh mesh = new Mesh();
		_skinnedMeshRenderer.BakeMesh(mesh);
		_meshCollider.sharedMesh = mesh;
		_meshCollider.transform.localScale = new Vector3(1f / _width, 1f, 1f / _width);
		Vector3[] vertices = _meshCollider.sharedMesh.vertices;
		int vertexCount = mesh.vertexCount;
		_verts = new Vector2[vertexCount];
		for (int i = 0; i < vertexCount; i++)
		{
			Vector3 vector = _meshCollider.transform.TransformPoint(vertices[i]);
			_verts[i] = new Vector2(vector.x, vector.z);
		}
	}

	public bool IsPointWithinBodyCautious(WorldPos pos, float maxSqrMag)
	{
		if (IsPointWithinBody(pos, out var _))
		{
			return true;
		}
		Vector2 asVector = pos.AsVector2;
		Vector2[] verts = _verts;
		for (int i = 0; i < verts.Length; i++)
		{
			if ((verts[i] - asVector).sqrMagnitude < maxSqrMag)
			{
				return true;
			}
		}
		return false;
	}

	public bool IsPointWithinBody(WorldPos pos)
	{
		TerrainInfo info;
		return IsPointWithinBody(pos, out info);
	}

	public bool IsPointWithinBody(WorldPos pos, out TerrainInfo info)
	{
		bool result = false;
		float num = 1f;
		info = new TerrainInfo();
		Vector3 asVector3XZ = pos.AsVector3XZ;
		asVector3XZ.y = _maxHeight + num;
		float num2 = _maxHeight - _minHeight;
		num2 += num + 0.1f;
		if (_meshCollider.Raycast(new Ray(asVector3XZ, Vector3.down), out var hitInfo, num2))
		{
			result = true;
			info.height = Mathf.Clamp01(1f - hitInfo.distance / num2);
		}
		return result;
	}
}
