using System;
using UnityEngine;

namespace AwesomeCharts;

[Serializable]
public class BarEntry : Entry
{
	[SerializeField]
	private long position;

	public long Position
	{
		get
		{
			return position;
		}
		set
		{
			position = value;
		}
	}

	public BarEntry()
	{
		position = 0L;
	}

	public BarEntry(long position, float value)
		: base(value)
	{
		this.position = position;
	}
}
