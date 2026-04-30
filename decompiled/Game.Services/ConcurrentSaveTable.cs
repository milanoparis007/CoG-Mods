using System.Collections;
using System.Collections.Generic;

namespace Game.Services;

public class ConcurrentSaveTable
{
	private Dictionary<string, object> _data = new Dictionary<string, object>();

	public Hashtable ToHashtable()
	{
		return new Hashtable(_data);
	}

	public void Set(string key, object value)
	{
		lock (this)
		{
			_data[key] = value;
		}
	}

	public bool ContainsKey(string key)
	{
		return _data.ContainsKey(key);
	}

	public void PopulateFrom(Hashtable table)
	{
		lock (this)
		{
			foreach (object key2 in table.Keys)
			{
				if (key2 is string key)
				{
					_data[key] = table[key2];
				}
			}
		}
	}

	public static ConcurrentSaveTable FromHashtable(Hashtable table)
	{
		ConcurrentSaveTable concurrentSaveTable = new ConcurrentSaveTable();
		concurrentSaveTable.PopulateFrom(table);
		return concurrentSaveTable;
	}
}
