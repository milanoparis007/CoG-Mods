using System;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using Game.Session.Entities;
using SomaSim.SION;
using SomaSim.Util;

namespace Game.Core;

[StructLayout(LayoutKind.Explicit)]
[DebuggerDisplay("{DebugString}")]
public struct EntityID : IEquatable<EntityID>
{
	public const ulong INVALID_ID = 0uL;

	public static readonly EntityID INVALID = FromID(0uL);

	[FieldOffset(0)]
	public int index;

	[FieldOffset(4)]
	public int version;

	[FieldOffset(0)]
	public ulong id;

	private const int ZERO = 48;

	public bool IsValid => id != 0;

	public bool IsNotValid => id == 0;

	internal string DebugString => MakeDebugString(this);

	public static EntityID FromID(ulong id)
	{
		return new EntityID
		{
			id = id
		};
	}

	public static EntityID FromIndex(int index)
	{
		return new EntityID
		{
			index = index,
			version = 0
		};
	}

	public static implicit operator ulong(EntityID id)
	{
		return id.id;
	}

	public static implicit operator EntityID(ulong id)
	{
		return FromID(id);
	}

	public static bool operator ==(EntityID a, EntityID b)
	{
		return a.id == b.id;
	}

	public static bool operator !=(EntityID a, EntityID b)
	{
		return a.id != b.id;
	}

	public static bool Equals(EntityID a, EntityID b)
	{
		return a.id == b.id;
	}

	public bool Equals(EntityID id)
	{
		return Equals(this, id);
	}

	public string GenerateName()
	{
		return "E" + version.ToString(CultureInfo.InvariantCulture) + "_" + index.ToString(CultureInfo.InvariantCulture);
	}

	public override bool Equals(object obj)
	{
		if (obj is EntityID)
		{
			return ((EntityID)obj).id == id;
		}
		return false;
	}

	public override int GetHashCode()
	{
		return index;
	}

	public override string ToString()
	{
		return $"E{version}_{index}";
	}

	private static string MakeDebugString(EntityID eid)
	{
		Entity entity = eid.FindEntity();
		string text = "";
		string text2 = "";
		if (entity != null)
		{
			if (entity.components.building != null)
			{
				text = "Bldg ";
			}
			else if (entity.components.person != null)
			{
				text = "Prsn ";
				text2 = $" ({entity.data.person.FullName} / {entity.data.agent.pid})";
			}
			else if (entity.components.mobile != null)
			{
				text = "Vhcl ";
				text2 = $" ({entity.data.mobile.pid})";
			}
		}
		return $"{text}E{eid.version}_{eid.index}{text2}";
	}

	public EntityID IncrementVersion()
	{
		return new EntityID
		{
			index = index,
			version = version + 1
		};
	}

	public static object Serialize(EntityID id, Serializer _)
	{
		StringBuilder stringBuilder = StringBuilderPool.Instance.Allocate();
		stringBuilder.Append("E");
		stringBuilder.Append(id.version);
		stringBuilder.Append("_");
		stringBuilder.Append(id.index);
		return stringBuilder.ToStringAndReturnToPool();
	}

	public static EntityID Deserialize(object value, Serializer _)
	{
		if (!(value is string text))
		{
			return 0uL;
		}
		int num = text.IndexOf('_', 1);
		int num2 = ParseSubstring(text, 1, num - 1);
		int num3 = ParseSubstring(text, num + 1);
		return new EntityID
		{
			version = num2,
			index = num3
		};
	}

	private static int ParseSubstring(string str, int startIndex, int length = -1)
	{
		int num = 0;
		if (length < 0)
		{
			length = str.Length - startIndex;
			length = ((length > 0) ? length : 0);
		}
		int i = startIndex;
		for (int num2 = startIndex + length; i < num2; i++)
		{
			int num3 = str[i] - 48;
			int num4 = ((num3 >= 0 && num3 <= 9) ? num3 : 0);
			num = num * 10 + num4;
		}
		return num;
	}
}
