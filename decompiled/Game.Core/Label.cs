using System;
using System.Diagnostics;
using SomaSim.SION;

namespace Game.Core;

[DebuggerDisplay("{DebugString}")]
public struct Label : IEquatable<Label>, IComparable<Label>
{
	public static readonly Label NULL = new Label
	{
		Index = 0,
		String = null
	};

	public string String;

	public int Index;

	public bool IsSet => Index != 0;

	public bool IsNotSet => Index == 0;

	private string DebugString => $"LABEL {Index}: {String}";

	public Label(string value)
	{
		Label label = LabelDB.Intern(value);
		String = label.String;
		Index = label.Index;
	}

	public Label(Label value)
	{
		String = value.String;
		Index = value.Index;
	}

	public static explicit operator Label(string str)
	{
		return new Label(str);
	}

	public static object Serialize(Label label, Serializer s)
	{
		return label.String;
	}

	public static Label Deserialize(object value, Serializer s)
	{
		if (!(value is string))
		{
			return default(Label);
		}
		return new Label((string)value);
	}

	public int CompareTo(Label other)
	{
		return String.CompareTo(other.String);
	}

	public static bool Equals(Label a, Label b)
	{
		return a.Index == b.Index;
	}

	public bool Equals(Label other)
	{
		return Index == other.Index;
	}

	public static bool operator ==(Label a, Label b)
	{
		return a.Index == b.Index;
	}

	public static bool operator !=(Label a, Label b)
	{
		return a.Index != b.Index;
	}

	public override bool Equals(object obj)
	{
		if (obj is Label label)
		{
			return label.Index == Index;
		}
		return false;
	}

	public override int GetHashCode()
	{
		return Index;
	}

	public override string ToString()
	{
		return String;
	}
}
