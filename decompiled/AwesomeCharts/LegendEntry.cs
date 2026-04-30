using System;
using UnityEngine;

namespace AwesomeCharts;

[Serializable]
public class LegendEntry
{
	[SerializeField]
	private string title;

	[SerializeField]
	private Color color;

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

	public LegendEntry()
	{
		title = "";
		color = Color.black;
	}

	public LegendEntry(string title, Color color)
	{
		this.title = title;
		this.color = color;
	}
}
