namespace Game.Core;

public static class DirectionUtil
{
	public static readonly Direction[] ALL_DIRECTIONS = new Direction[4]
	{
		Direction.N,
		Direction.E,
		Direction.S,
		Direction.W
	};

	public static readonly Direction[] OPP_DIRECTIONS = new Direction[4]
	{
		Direction.S,
		Direction.W,
		Direction.N,
		Direction.E
	};

	public static readonly float[] FACING_ROTATIONS = new float[4] { 0f, 90f, 180f, 270f };

	public static readonly IntSize[] SPREAD_DIRECTIONS = new IntSize[4]
	{
		new IntSize(0, 1),
		new IntSize(1, 0),
		new IntSize(0, -1),
		new IntSize(-1, 0)
	};

	public static Direction GetOppositeDirection(Direction dir)
	{
		return OPP_DIRECTIONS[(int)dir];
	}

	public static float GetFacingRotation(Direction dir)
	{
		return FACING_ROTATIONS[(int)dir];
	}

	public static IntSize GetSpreadDirection(Direction dir)
	{
		return SPREAD_DIRECTIONS[(int)dir];
	}

	public static int GetInDirection(this IntSize size, Direction dir)
	{
		if (dir != Direction.N && dir != Direction.S)
		{
			return size.width;
		}
		return size.height;
	}

	public static bool IsHorizontal(Direction dir)
	{
		if (dir != Direction.E)
		{
			return dir == Direction.W;
		}
		return true;
	}

	public static bool IsVertical(Direction dir)
	{
		if (dir != Direction.N)
		{
			return dir == Direction.S;
		}
		return true;
	}
}
