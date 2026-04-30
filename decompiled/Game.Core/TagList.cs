using System.Collections.Generic;
using System.Diagnostics;

namespace Game.Core;

[DebuggerDisplay("{ToString()}")]
public sealed class TagList : List<Label>
{
	public TagList()
	{
	}

	public TagList(IEnumerable<Label> source)
		: base(source)
	{
	}

	public TagList(int capacity)
		: base(capacity)
	{
	}

	public Label FindFirstMatch(TagList candidates)
	{
		int i = 0;
		for (int count = candidates.Count; i < count; i++)
		{
			Label label = candidates[i];
			if (Contains(label))
			{
				return label;
			}
		}
		return Label.NULL;
	}

	public bool ContainsAtLeastOneOf(TagList candidates)
	{
		int i = 0;
		for (int count = candidates.Count; i < count; i++)
		{
			if (Contains(candidates[i]))
			{
				return true;
			}
		}
		return false;
	}

	public bool ContainsAllOf(TagList candidates)
	{
		int i = 0;
		for (int count = candidates.Count; i < count; i++)
		{
			if (!Contains(candidates[i]))
			{
				return false;
			}
		}
		return true;
	}

	public override string ToString()
	{
		return "[TAGS " + string.Join(" ", this) + "]";
	}
}
