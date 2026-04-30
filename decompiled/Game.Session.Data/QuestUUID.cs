using System;
using System.Diagnostics;

namespace Game.Session.Data;

[DebuggerDisplay("{DebugString}")]
public struct QuestUUID : IEquatable<QuestUUID>
{
	public static readonly QuestUUID EMPTY;

	public int value;

	public bool IsSet => !Equals(this, EMPTY);

	public bool IsNotSet => Equals(this, EMPTY);

	private string DebugString => $"[QuestUUID {value}]";

	public QuestUUID(int uuid)
	{
		this = default(QuestUUID);
		value = uuid;
	}

	public static bool Equals(QuestUUID a, QuestUUID b)
	{
		return a.value == b.value;
	}

	public static bool operator ==(QuestUUID a, QuestUUID b)
	{
		return a.value == b.value;
	}

	public static bool operator !=(QuestUUID a, QuestUUID b)
	{
		return a.value != b.value;
	}

	public bool Equals(QuestUUID other)
	{
		return Equals(this, other);
	}

	public override bool Equals(object obj)
	{
		if (obj is QuestUUID b)
		{
			return Equals(this, b);
		}
		return false;
	}

	public override int GetHashCode()
	{
		return value;
	}
}
