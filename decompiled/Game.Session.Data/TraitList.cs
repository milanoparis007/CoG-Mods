using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Game.Core;

namespace Game.Session.Data;

[DebuggerDisplay("{DebugString}")]
public sealed class TraitList : List<Trait>
{
	private string DebugString => string.Join(", ", this.Select((Trait rel) => rel.id));

	public int FirstIndexById(Label id)
	{
		int i = 0;
		for (int count = base.Count; i < count; i++)
		{
			if (base[i].id == id)
			{
				return i;
			}
		}
		return -1;
	}

	public Trait Find(Label id)
	{
		int num = FirstIndexById(id);
		if (num < 0)
		{
			return null;
		}
		return base[num];
	}

	public bool ContainsById(Label id)
	{
		return FirstIndexById(id) >= 0;
	}

	public override string ToString()
	{
		return DebugString;
	}
}
