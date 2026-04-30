using System;
using System.Diagnostics;

namespace Game.Services.Mods;

[DebuggerDisplay("{DebugString}")]
public struct ModID : IEquatable<ModID>
{
	public enum Source
	{
		Local,
		Workshop
	}

	public string id;

	public Source source;

	private string DebugString => ToString();

	public static ModID MakeLocal(string id)
	{
		return new ModID
		{
			id = id,
			source = Source.Local
		};
	}

	public static ModID MakeWorkshop(string id)
	{
		return new ModID
		{
			id = id,
			source = Source.Workshop
		};
	}

	public override string ToString()
	{
		return $"[ModID {id} / {source}]";
	}

	public static bool Equals(ModID a, ModID b)
	{
		if (a.source == b.source)
		{
			return a.id == b.id;
		}
		return false;
	}

	public bool Equals(ModID other)
	{
		return Equals(this, other);
	}

	public override bool Equals(object obj)
	{
		if (obj is ModID b)
		{
			return Equals(this, b);
		}
		return false;
	}

	public override int GetHashCode()
	{
		return id.GetHashCode() ^ source.GetHashCode();
	}
}
