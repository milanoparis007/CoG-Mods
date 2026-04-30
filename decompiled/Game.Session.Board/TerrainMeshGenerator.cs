using Game.Core;
using Game.Services.Maps;
using Game.Session.Setup;
using UnityEngine;

namespace Game.Session.Board;

internal sealed class TerrainMeshGenerator
{
	private MapConfig.TerrainConfig _topo;

	private Vector2 _uvoffset;

	private Vector2 _uvsize;

	private int _xPos;

	private int _yPos;

	private int _xSize;

	private int _ySize;

	private int[] _triangles;

	private Vector3[] _vertices;

	private Vector2[] _uvs;

	private Color[] _colors;

	private bool hardEdges = true;

	private HeightmapData _heightmapData;

	private TerrainMetadata _terrainMetadata;

	private Mesh _mesh;

	public void Generate(ref Mesh terrainQuad, MeshFilter meshFilter, TerrainGenData data, MapConfig.TerrainConfig topo, IntSize maprect, Rect chunkrect)
	{
		_topo = topo;
		_heightmapData = data.heightmapData;
		_terrainMetadata = Resources.Load<TerrainMetadata>("TerrainMetadata");
		_uvoffset = new Vector2(chunkrect.x / (float)maprect.width, chunkrect.y / (float)maprect.height);
		_uvsize = new Vector2(chunkrect.width / (float)maprect.width, chunkrect.height / (float)maprect.height);
		_xPos = (int)(chunkrect.x * (float)_topo.tileverts.width);
		_yPos = (int)(chunkrect.y * (float)_topo.tileverts.height);
		_xSize = (int)(chunkrect.width * (float)_topo.tileverts.width);
		_ySize = (int)(chunkrect.height * (float)_topo.tileverts.height);
		WorldRect terrRect = new WorldRect(_xPos, _yPos, _xSize, _ySize);
		if (data.CheckIntersection(terrRect))
		{
			_mesh = new Mesh();
			meshFilter.mesh = _mesh;
			if (hardEdges)
			{
				MakeHardEdges();
			}
			else
			{
				MakeSoftEdges();
			}
			_mesh.vertices = _vertices;
			_mesh.uv = _uvs;
			_mesh.triangles = _triangles;
			_mesh.colors = _colors;
			_mesh.RecalculateNormals();
			_mesh.RecalculateTangents();
			_mesh.RecalculateBounds();
			_mesh.name = $"Terrain Grid {_vertices.Length} verts";
		}
		else
		{
			_mesh = terrainQuad;
			meshFilter.sharedMesh = terrainQuad;
		}
	}

	private void MakeHardEdges()
	{
		_triangles = new int[_xSize * _ySize * 6];
		_vertices = new Vector3[_xSize * _ySize * 6];
		_uvs = new Vector2[_vertices.Length];
		_colors = new Color[_vertices.Length];
		int num = 0;
		for (int i = 0; i < _ySize; i++)
		{
			int num2 = 0;
			while (num2 < _xSize)
			{
				if (false)
				{
					UpdateUniqueVertex(num, num2 + 1, i);
					UpdateUniqueVertex(num + 1, num2, i);
					UpdateUniqueVertex(num + 2, num2, i + 1);
					UpdateUniqueVertex(num + 3, num2, i + 1);
					UpdateUniqueVertex(num + 4, num2 + 1, i + 1);
					UpdateUniqueVertex(num + 5, num2 + 1, i);
				}
				else
				{
					UpdateUniqueVertex(num, num2, i);
					UpdateUniqueVertex(num + 1, num2, i + 1);
					UpdateUniqueVertex(num + 2, num2 + 1, i + 1);
					UpdateUniqueVertex(num + 3, num2 + 1, i + 1);
					UpdateUniqueVertex(num + 4, num2 + 1, i);
					UpdateUniqueVertex(num + 5, num2, i);
				}
				num2++;
				num += 6;
			}
		}
	}

	private void UpdateUniqueVertex(int i, int x, int y)
	{
		UpdateVertexAndUV(i, x, y);
		_triangles[i] = i;
	}

	private void UpdateVertexAndUV(int i, int x, int y)
	{
		float num = 0f;
		float num2 = 0f;
		if (_heightmapData != null)
		{
			for (int j = -1; j < 2; j++)
			{
				for (int k = -1; k < 2; k++)
				{
					int num3 = x + j;
					int num4 = y + k;
					float num5 = (float)num3 / (float)_xSize;
					float num6 = (float)num4 / (float)_ySize;
					float u = num5 * _uvsize.x + _uvoffset.x;
					float v = num6 * _uvsize.y + _uvoffset.y;
					Color pixelBilinear = _heightmapData.heightMap.GetPixelBilinear(u, v);
					num += pixelBilinear.r;
					num2 += pixelBilinear.g;
				}
			}
		}
		num /= 9f;
		num2 /= 9f;
		float num7 = 0.4975f;
		if (num > 0f)
		{
			num7 -= 0.5f * num;
		}
		else if (num2 > 0f)
		{
			num7 += 0.5f * num2;
		}
		float num8 = 1f - num7;
		float num9 = (float)x / (float)_xSize;
		float num10 = (float)y / (float)_ySize;
		float x2 = num9 * _uvsize.x + _uvoffset.x;
		float y2 = num10 * _uvsize.y + _uvoffset.y;
		float z = (num8 - _topo.groundheight) * _topo.GetTerrainScale(num8 - _topo.groundheight < 0f);
		_vertices[i] = new Vector3(num9, num10, z);
		_uvs[i] = new Vector2(x2, y2);
		_triangles[i] = i;
		_colors[i] = _terrainMetadata.Sample(num7);
	}

	private void MakeSoftEdges()
	{
		_triangles = new int[_xSize * _ySize * 6];
		_vertices = new Vector3[(_xSize + 1) * (_ySize + 1)];
		_uvs = new Vector2[_vertices.Length];
		_colors = new Color[_vertices.Length];
		int num = 0;
		for (int i = 0; i <= _ySize; i++)
		{
			int num2 = 0;
			while (num2 <= _xSize)
			{
				UpdateVertexAndUV(num, num2, i);
				num2++;
				num++;
			}
		}
		int num3 = 0;
		int num4 = 0;
		int num5 = 0;
		while (num5 < _ySize)
		{
			int num6 = 0;
			while (num6 < _xSize)
			{
				_triangles[num3] = num4;
				_triangles[num3 + 1] = num4 + _xSize + 1;
				_triangles[num3 + 2] = num4 + 1;
				_triangles[num3 + 3] = num4 + 1;
				_triangles[num3 + 4] = num4 + _xSize + 1;
				_triangles[num3 + 5] = num4 + _xSize + 2;
				num6++;
				num3 += 6;
				num4++;
			}
			num5++;
			num4++;
		}
	}
}
