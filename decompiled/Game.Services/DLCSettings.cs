using System.Collections.Generic;
using Game.Services.Store;

namespace Game.Services;

public sealed class DLCSettings
{
	public class Entry
	{
		public PackID packid;

		public string locname;

		public string locicon;
	}

	public List<Entry> entries = new List<Entry>();
}
