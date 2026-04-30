using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core;

public class LabelKeyedList<V> : List<V>
{
	protected struct KeyValuePair
	{
		public Label Label;

		public V Value;
	}

	protected List<KeyValuePair> _cache;

	protected Func<V, Label> _keyMapperFn;

	public LabelKeyedList(Func<V, Label> valueToKeyMapperFn)
	{
		_keyMapperFn = valueToKeyMapperFn;
		_cache = null;
	}

	public void InitializeCache()
	{
		_cache = this.Select(delegate(V element)
		{
			Label label = _keyMapperFn(element);
			return new KeyValuePair
			{
				Label = label,
				Value = element
			};
		}).ToList();
	}

	public void FlushCache()
	{
		_cache = null;
	}

	public V FindCachedOrDefault(Label key)
	{
		if (_cache == null)
		{
			InitializeCache();
		}
		int i = 0;
		for (int count = base.Count; i < count; i++)
		{
			if (_cache[i].Label == key)
			{
				return _cache[i].Value;
			}
		}
		return default(V);
	}
}
