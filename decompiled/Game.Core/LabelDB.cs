using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Game.Core;

internal static class LabelDB
{
	private static ConcurrentDictionary<string, Label> _interned = new ConcurrentDictionary<string, Label>();

	private static int _nextIndex = 1;

	public static IEnumerable<string> GetAllInternedStrings => _interned.Keys;

	public static Label Intern(string str)
	{
		if (str == null)
		{
			return Label.NULL;
		}
		if (_interned.TryGetValue(str, out var value))
		{
			return value;
		}
		lock (_interned)
		{
			value = new Label
			{
				String = str,
				Index = _nextIndex++
			};
			if (_interned.TryAdd(str, value))
			{
				return value;
			}
			return _interned[str];
		}
	}
}
