using System;
using System.Collections.Generic;
using UnityEngine;

namespace AwesomeCharts;

[Serializable]
public class BarAxisValueFormatterConfig
{
	[SerializeField]
	private List<string> customValues;

	public List<string> CustomValues
	{
		get
		{
			return customValues;
		}
		set
		{
			customValues = value;
		}
	}
}
