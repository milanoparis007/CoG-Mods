using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Game.Core;
using Game.Session.Entities;

namespace Game.Session.Data;

[DebuggerDisplay("{DebugString}")]
public sealed class RelationshipList
{
	public EntityID sourceId;

	public List<Relationship> data;

	private string DebugString => ToString();

	public RelationshipList()
	{
		data = new List<Relationship>(16);
	}

	public int IndexOf(EntityID targetId)
	{
		int i = 0;
		for (int count = data.Count; i < count; i++)
		{
			if (data[i].to == targetId)
			{
				return i;
			}
		}
		return -1;
	}

	public int IndexOfFirst(RelationshipType type)
	{
		int i = 0;
		for (int count = data.Count; i < count; i++)
		{
			if (data[i].type == type)
			{
				return i;
			}
		}
		return -1;
	}

	public RelationshipType FindRelType(EntityID targetId)
	{
		int num = IndexOf(targetId);
		if (num < 0)
		{
			return RelationshipType.None;
		}
		return data[num].type;
	}

	public Relationship FindOrNull(EntityID targetId)
	{
		int num = IndexOf(targetId);
		if (num < 0)
		{
			return null;
		}
		return data[num];
	}

	public Relationship FindFirstOrNull(RelationshipType type)
	{
		int num = IndexOfFirst(type);
		if (num < 0)
		{
			return null;
		}
		return data[num];
	}

	public int Count(RelationshipType type)
	{
		int num = 0;
		int i = 0;
		for (int count = data.Count; i < count; i++)
		{
			if (data[i].type == type)
			{
				num++;
			}
		}
		return num;
	}

	public bool HasAny(RelationshipType type)
	{
		return IndexOfFirst(type) >= 0;
	}

	public bool HasAny(EntityID targetId)
	{
		return IndexOf(targetId) >= 0;
	}

	public void Add(RelationshipList sentinel, Relationship rel)
	{
		data.Add(rel);
	}

	public bool Remove(EntityID targetId)
	{
		int num = IndexOf(targetId);
		if (num >= 0)
		{
			data.RemoveAt(num);
			return true;
		}
		return false;
	}

	public bool HasSpouse()
	{
		return HasAny(RelationshipType.Spouse);
	}

	public bool HasChildren()
	{
		return HasAny(RelationshipType.Child);
	}

	public bool HasParents()
	{
		if (!HasAny(RelationshipType.Mother))
		{
			return HasAny(RelationshipType.Father);
		}
		return true;
	}

	public Entity GetFather()
	{
		return FindFirstOfType(RelationshipType.Father).FindEntity();
	}

	public Entity GetMother()
	{
		return FindFirstOfType(RelationshipType.Mother).FindEntity();
	}

	public Entity GetSpouse()
	{
		return FindFirstOfType(RelationshipType.Spouse).FindEntity();
	}

	public Entity GetFirstSibling()
	{
		return FindFirstOfType(RelationshipType.Sibling).FindEntity();
	}

	public override string ToString()
	{
		return string.Join(", ", data.Select((Relationship f) => f.ToString()));
	}

	private EntityID FindFirstOfType(RelationshipType type)
	{
		int num = IndexOfFirst(type);
		if (num < 0)
		{
			return EntityID.INVALID;
		}
		return data[num].to;
	}
}
