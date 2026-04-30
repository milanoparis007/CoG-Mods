using System;
using UnityEngine;

namespace AwesomeCharts;

[Serializable]
public class PieEntry : Entry
{
	[SerializeField]
	private Color color = Color.white;

	[SerializeField]
	private string label = "";

	public Color Color
	{
		get
		{
			return color;
		}
		set
		{
			color = value;
		}
	}

	public string Label
	{
		get
		{
			return label;
		}
		set
		{
			label = value;
		}
	}

	public PieEntry()
	{
	}

	public PieEntry(float value, string label, Color color)
		: base(value)
	{
		this.label = label;
		this.color = color;
	}
}
