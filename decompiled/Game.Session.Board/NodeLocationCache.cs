using System.Collections.Generic;
using Game.Core;
using SomaSim.Util;

namespace Game.Session.Board;

internal sealed class NodeLocationCache
{
	private class Entry : List<Node>
	{
	}

	private Entry[,] _data;

	private IntSize _cacheSize;

	private int _cellSize;

	internal void Initialize(BoardManager board, int cellSize)
	{
		_cellSize = cellSize;
		IntSize mapSize = board.MapConfig.mapSize;
		_cacheSize = new IntSize(1 + mapSize.width / cellSize, 1 + mapSize.height / cellSize);
		_data = new Entry[_cacheSize.width, _cacheSize.height];
		for (int i = 0; i < _cacheSize.width; i++)
		{
			for (int j = 0; j < _cacheSize.height; j++)
			{
				_data[i, j] = new Entry();
			}
		}
	}

	internal void Release()
	{
		Clear();
		_data = null;
		_cacheSize = default(IntSize);
		_cellSize = 0;
	}

	private void Clear()
	{
		for (int i = 0; i < _cacheSize.width; i++)
		{
			for (int j = 0; j < _cacheSize.height; j++)
			{
				_data[i, j].Clear();
			}
		}
	}

	internal void Add(Node node)
	{
		if (!HasAnyNodeInRadius(node.pos, 1f, out var _))
		{
			GetEntry(node.pos).Add(node);
			return;
		}
		Logger.Error("Double add of node in position ", node.pos);
	}

	internal bool HasAnyNodeInRadius(WorldPos pos, float radius, out Node foundNode)
	{
		float num = radius * radius;
		int num2 = MathUtil.Clamp((int)((pos.x - radius) / (float)_cellSize), 0, _cacheSize.width - 1);
		int num3 = MathUtil.Clamp((int)((pos.x + radius) / (float)_cellSize), 0, _cacheSize.width - 1);
		int num4 = MathUtil.Clamp((int)((pos.y - radius) / (float)_cellSize), 0, _cacheSize.height - 1);
		int num5 = MathUtil.Clamp((int)((pos.y + radius) / (float)_cellSize), 0, _cacheSize.height - 1);
		for (int i = num2; i <= num3; i++)
		{
			for (int j = num4; j <= num5; j++)
			{
				Entry entry = _data[i, j];
				int k = 0;
				for (int count = entry.Count; k < count; k++)
				{
					if ((entry[k].pos - pos).MagnitudeSquared <= num)
					{
						foundNode = entry[k];
						return true;
					}
				}
			}
		}
		foundNode = null;
		return false;
	}

	internal void FindAndSortNodesInRadius(WorldPos pos, float radius, bool sort, List<Node> results)
	{
		float num = radius * radius;
		int num2 = MathUtil.Clamp((int)((pos.x - radius) / (float)_cellSize), 0, _cacheSize.width - 1);
		int num3 = MathUtil.Clamp((int)((pos.x + radius) / (float)_cellSize), 0, _cacheSize.width - 1);
		int num4 = MathUtil.Clamp((int)((pos.y - radius) / (float)_cellSize), 0, _cacheSize.height - 1);
		int num5 = MathUtil.Clamp((int)((pos.y + radius) / (float)_cellSize), 0, _cacheSize.height - 1);
		for (int i = num2; i <= num3; i++)
		{
			for (int j = num4; j <= num5; j++)
			{
				Entry entry = _data[i, j];
				int k = 0;
				for (int count = entry.Count; k < count; k++)
				{
					if ((entry[k].pos - pos).MagnitudeSquared <= num)
					{
						results.Add(entry[k]);
					}
				}
			}
		}
		if (sort)
		{
			results.Sort(delegate(Node a, Node b)
			{
				float magnitudeSquared = (a.pos - pos).MagnitudeSquared;
				float magnitudeSquared2 = (b.pos - pos).MagnitudeSquared;
				return (magnitudeSquared != magnitudeSquared2) ? ((!(magnitudeSquared < magnitudeSquared2)) ? 1 : (-1)) : 0;
			});
		}
	}

	internal Node FindClosestNodeInRadius(WorldPos pos, float radius)
	{
		float num = radius * radius;
		Node result = null;
		int num2 = MathUtil.Clamp((int)((pos.x - radius) / (float)_cellSize), 0, _cacheSize.width - 1);
		int num3 = MathUtil.Clamp((int)((pos.x + radius) / (float)_cellSize), 0, _cacheSize.width - 1);
		int num4 = MathUtil.Clamp((int)((pos.y - radius) / (float)_cellSize), 0, _cacheSize.height - 1);
		int num5 = MathUtil.Clamp((int)((pos.y + radius) / (float)_cellSize), 0, _cacheSize.height - 1);
		for (int i = num2; i <= num3; i++)
		{
			for (int j = num4; j <= num5; j++)
			{
				Entry entry = _data[i, j];
				int k = 0;
				for (int count = entry.Count; k < count; k++)
				{
					float magnitudeSquared = (entry[k].pos - pos).MagnitudeSquared;
					if (magnitudeSquared <= num)
					{
						num = magnitudeSquared;
						result = entry[k];
					}
				}
			}
		}
		return result;
	}

	internal void Refresh(NodePersistedData data)
	{
		Clear();
		foreach (Node node in data.nodes)
		{
			if (node.IsValid)
			{
				Add(node);
			}
		}
	}

	private Entry GetEntry(WorldPos pos)
	{
		int num = (int)(pos.x / (float)_cellSize);
		int num2 = (int)(pos.y / (float)_cellSize);
		return _data[num, num2];
	}
}
