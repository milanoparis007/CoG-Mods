using System.Collections.Generic;
using SomaSim.Util;

namespace Game.Session;

public class DebugConsoleManager : AbstractSessionManager
{
	public Dictionary<object, List<DebugConsoleEntry>> entries;

	public override void OnInitializeDone()
	{
		entries = new Dictionary<object, List<DebugConsoleEntry>>();
	}

	public override void OnReleased()
	{
		entries = null;
	}

	public void Add(object key, DebugConsoleEntry entry)
	{
		entries.AddToList(key, entry);
	}

	public void Remove(object key)
	{
		entries.Remove(key);
	}

	public bool HasEntriesFor(object key)
	{
		return entries.ContainsKey(key);
	}

	public IEnumerable<DebugConsoleEntry> GetAllEntries()
	{
		foreach (List<DebugConsoleEntry> value in entries.Values)
		{
			foreach (DebugConsoleEntry item in value)
			{
				yield return item;
			}
		}
	}
}
