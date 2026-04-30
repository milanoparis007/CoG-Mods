using System.Collections.Generic;

namespace Game.Core;

public class LabelDictionary<V> : Dictionary<Label, V>
{
	public LabelDictionary()
		: base((IEqualityComparer<Label>)new LabelEqualityComparer())
	{
	}

	public LabelDictionary(int capacity)
		: base(capacity, (IEqualityComparer<Label>)new LabelEqualityComparer())
	{
	}
}
