using System;
using System.Collections.Generic;

namespace Game.Services;

public sealed class ItemToNameCache<E>
{
	private Dictionary<int, string> _cache = new Dictionary<int, string>();

	private Func<E, int> _keyfn;

	private Func<E, string> _valfn;

	public ItemToNameCache(Func<E, int> toKeyFn, Func<E, string> toValueFn)
	{
		_keyfn = toKeyFn;
		_valfn = toValueFn;
	}

	public string ToCachedString(E item)
	{
		int key = _keyfn(item);
		if (!_cache.TryGetValue(key, out var value))
		{
			value = _valfn(item);
			_cache[key] = value;
		}
		return value;
	}
}
