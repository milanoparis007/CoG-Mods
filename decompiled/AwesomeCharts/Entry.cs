using System;
using UnityEngine;

namespace AwesomeCharts;

[Serializable]
public class Entry
{
	[SerializeField]
	protected float value;

	public virtual float Value
	{
		get
		{
			return Mathf.Max(value, 0f);
		}
		set
		{
			this.value = value;
		}
	}

	public Entry()
	{
		value = 0f;
	}

	public Entry(float value)
	{
		this.value = value;
	}
}
