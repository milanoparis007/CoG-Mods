using System;
using UnityEngine;

namespace AwesomeCharts;

[Serializable]
public class LineEntry : Entry
{
	[SerializeField]
	private float position;

	public float Position
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

	public override float Value
	{
		get
		{
			return value;
		}
		set
		{
			base.value = value;
		}
	}

	public LineEntry()
	{
		position = 0f;
	}

	public LineEntry(float position, float value)
		: base(value)
	{
		this.position = position;
	}
}
