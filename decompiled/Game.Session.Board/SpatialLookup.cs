using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Game.Core;
using Game.Session.Entities;
using SomaSim.Util;

namespace Game.Session.Board;

public sealed class SpatialLookup : ISubManager<BoardManager>
{
	[DebuggerDisplay("{DebugString}")]
	private class Cell
	{
		public static readonly List<Entity> EMPTY = new List<Entity>(0);

		public IntPos index;

		public WorldRect world;

		public List<Entity> items;

		private string DebugString => ToString();

		public override string ToString()
		{
			return $"Cell {items?.Count ?? 0} @ {index} / {world}";
		}
	}

	private BoardManager _manager;

	private Cell[,] _data;

	private Dictionary<EntityID, List<Cell>> _entityToCells;

	private const int DEFAULT_CELL_SIZE = 8;

	private IntSize _cacheSize;

	private int _cellSize;

	public void Initialize(BoardManager manager)
	{
		_manager = manager;
		_cellSize = 8;
		IntSize mapSize = manager.MapConfig.mapSize;
		_cacheSize = new IntSize(1 + mapSize.width / _cellSize, 1 + mapSize.height / _cellSize);
		_entityToCells = new Dictionary<EntityID, List<Cell>>(new EntityIDEqualityComparer());
		_data = new Cell[_cacheSize.width, _cacheSize.height];
		for (int i = 0; i < _cacheSize.width; i++)
		{
			for (int j = 0; j < _cacheSize.height; j++)
			{
				Cell cell = new Cell
				{
					items = new List<Entity>(64),
					index = new IntPos(i, j)
				};
				cell.world = new WorldRect(IndexToWorldCorner(cell.index), new WorldSize(_cellSize, _cellSize));
				_data[i, j] = cell;
			}
		}
	}

	public void Release()
	{
		int num = _entityToCells.Select((KeyValuePair<EntityID, List<Cell>> entry) => entry.Value.Count).Sum();
		if (num > 0)
		{
			Logger.Warning("Lot cache entities found at shutdown, count = " + num);
		}
		_entityToCells.ClearDeep();
		_data = null;
		_entityToCells = null;
		_manager = null;
	}

	private WorldPos IndexToWorldCorner(IntPos pos)
	{
		return new WorldPos(pos.x * _cellSize, pos.y * _cellSize);
	}

	private IntPos WorldToIndex(WorldPos pos)
	{
		return new IntPos((int)(pos.x / (float)_cellSize), (int)(pos.y / (float)_cellSize));
	}

	public bool IsValidIndex(IntPos pos)
	{
		if (pos.x >= 0 && pos.y >= 0 && pos.x < _cacheSize.width)
		{
			return pos.y < _cacheSize.height;
		}
		return false;
	}

	private Cell FindCell(WorldPos pos)
	{
		return FindCell(WorldToIndex(pos));
	}

	private Cell FindCell(IntPos index)
	{
		if (!IsValidIndex(index))
		{
			return null;
		}
		return _data[index.x, index.y];
	}

	public List<Entity> FindEntitiesInCellUnsafe(WorldPos pos)
	{
		return FindCell(pos)?.items ?? Cell.EMPTY;
	}

	public void AddOrMoveEntity(Entity e)
	{
		TryRemoveEntity(e);
		List<Cell> list = FindCellsUnderEntity(e);
		_entityToCells[e.Id] = list;
		foreach (Cell item in list)
		{
			item.items.Add(e);
		}
	}

	private List<Cell> FindCellsUnderEntity(Entity e)
	{
		WorldPosList worldPosList = _manager.cornerListPool.Allocate();
		List<Cell> list = new List<Cell>(4);
		e.components.board.GetFootprintInsidePoints(1f, worldPosList);
		for (int i = 0; i < worldPosList.Count; i++)
		{
			WorldPos pos = worldPosList[i];
			Cell cell = FindCell(pos);
			if (cell != null && !list.Contains(cell))
			{
				list.Add(cell);
			}
		}
		_manager.cornerListPool.Free(worldPosList);
		return list;
	}

	public bool HasEntity(Entity e)
	{
		return _entityToCells.ContainsKey(e.Id);
	}

	public bool TryRemoveEntity(Entity e)
	{
		List<Cell> list = _entityToCells.FindOrNull(e.Id);
		if (list == null || list.Count == 0)
		{
			return false;
		}
		_entityToCells.Remove(e.Id);
		foreach (Cell item in list)
		{
			item.items.Remove(e);
		}
		return true;
	}
}
