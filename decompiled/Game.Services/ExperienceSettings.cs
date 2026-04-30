using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Session.Data;

namespace Game.Services;

public sealed class ExperienceSettings
{
	public struct XPPointsBySource
	{
		public XPSource source;

		public ModValue points;
	}

	public struct Tuning
	{
		public ModValue firstThreshold;

		public ModValue thresholdMultiplier;
	}

	public List<XPPointsBySource> points;

	public Tuning tuning;

	public List<LevelupChain> levelups;

	private KeyedListCache<Label, LevelupChain> _levelupCache;

	private LevelupChain _captainCache;

	public LevelupChain GetCaptainLevelup()
	{
		if (levelups == null)
		{
			Logger.Error("Prematurely querying levelups");
			return null;
		}
		_captainCache = _captainCache ?? levelups.FirstOrDefault((LevelupChain c) => c.captain);
		return _captainCache;
	}

	public ModValue GetPoints(XPSource source)
	{
		if (points == null)
		{
			Logger.Error("Prematurely querying xp points");
			return null;
		}
		int i = 0;
		for (int count = points.Count; i < count; i++)
		{
			if (points[i].source == source)
			{
				return points[i].points;
			}
		}
		return null;
	}

	public LevelupChain GetLevelup(Label id)
	{
		if (levelups == null)
		{
			Logger.Error("Prematurely querying levelups");
			return null;
		}
		_levelupCache = _levelupCache ?? new KeyedListCache<Label, LevelupChain>(levelups, LevelupChain.Matcher);
		return _levelupCache.Get(id);
	}
}
