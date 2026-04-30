using System;
using System.Diagnostics;
using System.Text;
using SomaSim.SION;
using SomaSim.Util;

namespace Game.Core;

[DebuggerDisplay("{DebugString}")]
public struct PlayerID : IEquatable<PlayerID>, IComparable<PlayerID>
{
	public const short INVALID_ID = -1;

	public const short SYSTEM_ID = 0;

	public const short HUMAN_PLAYER_ID = 1;

	public static readonly PlayerID INVALID = new PlayerID
	{
		id = -1
	};

	public static readonly PlayerID HumanPlayer = new PlayerID(1);

	public static readonly PlayerID System = new PlayerID(0);

	public short id;

	public int ArrayIndex => id;

	public bool IsValid => id != -1;

	public bool IsNotValid => id == -1;

	public bool IsSystem => id == 0;

	public bool IsNotAnyPlayer => id < 1;

	public bool IsAnyPlayer => id >= 1;

	public bool IsHumanPlayer => id == 1;

	public bool IsAIPlayer => id > 1;

	private string DebugString => $"[PID_{id}]";

	public PlayerID(short id)
	{
		this.id = id;
	}

	public static bool operator ==(PlayerID a, PlayerID b)
	{
		return Equals(a, b);
	}

	public static bool operator !=(PlayerID a, PlayerID b)
	{
		return !Equals(a, b);
	}

	public static bool Equals(PlayerID a, PlayerID b)
	{
		return a.id == b.id;
	}

	public bool Equals(PlayerID other)
	{
		return id == other.id;
	}

	public override bool Equals(object obj)
	{
		if (obj is PlayerID playerID)
		{
			return id == playerID.id;
		}
		return false;
	}

	public override int GetHashCode()
	{
		return id;
	}

	public int CompareTo(PlayerID other)
	{
		return id - other.id;
	}

	public static int CompareAscending(PlayerID a, PlayerID b)
	{
		return a.CompareTo(b);
	}

	public bool IsPlayerOtherThan(PlayerID other)
	{
		if (id >= 1)
		{
			return id != other.id;
		}
		return false;
	}

	public override string ToString()
	{
		return DebugString;
	}

	public static object Serialize(PlayerID pid, Serializer s)
	{
		StringBuilder stringBuilder = StringBuilderPool.Instance.Allocate();
		stringBuilder.Append("PID_");
		stringBuilder.Append(s.Serialize(pid.id));
		return StringBuilderPool.Instance.ProduceValueAndFree(stringBuilder);
	}

	public static PlayerID Deserialize(object value, Serializer s)
	{
		if (!(value is string text))
		{
			return INVALID;
		}
		int startIndex = 4;
		return new PlayerID(s.Deserialize<short>(text.Substring(startIndex)));
	}
}
