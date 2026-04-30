using System;
using System.Collections.Generic;

namespace Game.Services;

public class KeyedListCache<K, V> where V : class
{
	private Dictionary<K, V> _cache;

	private List<V> _source;

	private Func<K, V, bool> _finder;

	public KeyedListCache(List<V> list, Func<K, V, bool> matcher)
	{
		_cache = new Dictionary<K, V>();
		_source = list;
		_finder = matcher;
	}

	public KeyedListCache(IEqualityComparer<K> comparer, List<V> list, Func<K, V, bool> matcher)
	{
		_cache = new Dictionary<K, V>(comparer);
		_source = list;
		_finder = matcher;
	}

	public V Get(K key)
	{
		if (!_cache.TryGetValue(key, out var value))
		{
			value = _source.Find((V arg) => _finder(key, arg));
			if (value != null)
			{
				_cache.Add(key, value);
			}
		}
		return value;
	}
}
