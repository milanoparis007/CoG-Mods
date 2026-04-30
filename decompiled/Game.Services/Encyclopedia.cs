using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using SomaSim.Util;

namespace Game.Services;

public class Encyclopedia
{
	public class Topic
	{
		public string id;

		public string locicon;

		public string locname;

		public string locdesc;

		public string cachedIcon;

		public string cachedName;

		public string cachedDesc;

		public void UpdateCached()
		{
			cachedIcon = ((locicon != null) ? Loc.Get(locicon) : null);
			cachedName = Loc.Get(locname);
			cachedDesc = Loc.Get(locdesc);
		}
	}

	private class LineMatch
	{
		public string topic;

		public string type;

		public string lockey;
	}

	public Dictionary<string, Topic> topics = new Dictionary<string, Topic>();

	public void Rebuild()
	{
		topics = MakeAllTopics();
		if (Game.settings.IsEditor)
		{
			ValidateAllLinks();
		}
	}

	public IEnumerable<Topic> FindAllSorted()
	{
		return topics.Values.OrderBy((Topic t) => t.cachedName);
	}

	public Topic FindTopicOrNull(string id)
	{
		return topics.FindOrNull(id);
	}

	private Dictionary<string, Topic> MakeAllTopics()
	{
		IEnumerable<IGrouping<string, LineMatch>> enumerable = from m in TryMatchAll()
			group m by m.topic;
		Dictionary<string, Topic> dictionary = new Dictionary<string, Topic>();
		foreach (IGrouping<string, LineMatch> item in enumerable)
		{
			Topic topic = dictionary.FindOrAddNew(item.Key);
			foreach (LineMatch item2 in item)
			{
				if (item2.type == "icon")
				{
					topic.locicon = item2.lockey;
				}
				else if (item2.type == "desc")
				{
					topic.locdesc = item2.lockey;
				}
				else if (item2.type == "name")
				{
					topic.locname = item2.lockey;
				}
			}
			topic.id = item.Key;
			topic.UpdateCached();
		}
		return dictionary;
	}

	private IEnumerable<LineMatch> TryMatchAll()
	{
		LocLangData langDataUnsafe = Game.serv.loc.GetLangDataUnsafe();
		foreach (string key in langDataUnsafe.Keys)
		{
			if (key.StartsWith("info."))
			{
				var (text, text2) = TryMatch(key);
				if (text != null && text2 != null)
				{
					yield return new LineMatch
					{
						topic = text,
						type = text2,
						lockey = key
					};
				}
			}
		}
	}

	private (string topic, string type) TryMatch(string lockey)
	{
		Match match = Regex.Match(lockey, "info\\.(\\S+)\\.(\\S+)");
		if (!match.Success || match.Groups.Count < 2)
		{
			return (topic: null, type: null);
		}
		if (match.Groups.Count < 3)
		{
			return (topic: null, type: null);
		}
		string value = match.Groups[1].Captures[0].Value;
		string value2 = match.Groups[2].Captures[0].Value;
		return (topic: value, type: value2);
	}

	private void ValidateAllLinks()
	{
		foreach (KeyValuePair<string, List<string>> item in Game.serv.loc.GetLangDataUnsafe())
		{
			foreach (string item2 in item.Value)
			{
				TryValidate(item.Key, item2);
			}
		}
		void TryValidate(string lockey, string line)
		{
			Match match = Regex.Match(line, "<link=\"([^\"]+)\">");
			if (match.Success && match.Groups.Count >= 1)
			{
				string value = match.Groups[1].Captures[0].Value;
				topics.FindOrNull(value);
			}
		}
	}
}
