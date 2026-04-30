using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AwesomeCharts;

public abstract class DataSet<T> where T : Entry
{
	[SerializeField]
	private string title;

	[SerializeField]
	private List<T> entries;

	public string Title
	{
		get
		{
			return title;
		}
		set
		{
			title = value;
		}
	}

	public List<T> Entries
	{
		get
		{
			return entries;
		}
		set
		{
			entries = value;
			OnEntriesChanged();
		}
	}

	public abstract List<T> GetSortedEntries();

	protected virtual void OnEntriesChanged()
	{
	}

	protected DataSet(string title)
	{
		this.title = title;
		Entries = new List<T>();
	}

	protected DataSet(string title, List<T> entries)
	{
		this.title = title;
		Entries = entries;
	}

	public int GetEntriesCount()
	{
		if (entries == null)
		{
			return 0;
		}
		return entries.Count;
	}

	public void AddEntry(T entry, int position)
	{
		entries.Insert(position, entry);
		OnEntriesChanged();
	}

	public void AddEntry(T entry)
	{
		AddEntry(entry, entries.Count);
	}

	public void RemoveEntry(int position)
	{
		entries.RemoveAt(position);
		OnEntriesChanged();
	}

	public void Clear()
	{
		entries.Clear();
		OnEntriesChanged();
	}

	public float GetMaxValue()
	{
		if (entries == null || entries.Count == 0)
		{
			return 0f;
		}
		return entries.OrderByDescending((T a) => a.Value).ToList()[0].Value;
	}

	public float GetMinValue()
	{
		if (entries == null || entries.Count == 0)
		{
			return 0f;
		}
		return entries.OrderBy((T a) => a.Value).ToList()[0].Value;
	}

	public T GetEntryAt(int position)
	{
		if (position < 0 || position >= GetEntriesCount())
		{
			return null;
		}
		return entries[position];
	}
}
