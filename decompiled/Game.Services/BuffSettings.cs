using System.Collections.Generic;
using Game.Core;
using Game.Session.Data;

namespace Game.Services;

public sealed class BuffSettings
{
	public List<BuffConfig> definitions;

	private KeyedListCache<Label, BuffConfig> _buffCache;

	private KeyedListCache<Label, BuffConfig> BuffCache
	{
		get
		{
			if (_buffCache == null)
			{
				_buffCache = new KeyedListCache<Label, BuffConfig>(new LabelEqualityComparer(), definitions, (Label label, BuffConfig config) => config.id == label);
			}
			return _buffCache;
		}
	}

	public BuffConfig GetConfig(Label id)
	{
		return BuffCache.Get(id);
	}
}
