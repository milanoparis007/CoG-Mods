using System;
using UnityEngine;

namespace AwesomeCharts;

[Serializable]
public class AxisValue
{
	[SerializeField]
	private float min;

	[SerializeField]
	private float max = 100f;

	[SerializeField]
	private bool minAutoValue;

	[SerializeField]
	private bool maxAutoValue;

	public float Min
	{
		get
		{
			return min;
		}
		set
		{
			min = value;
		}
	}

	public float Max
	{
		get
		{
			return max;
		}
		set
		{
			max = value;
		}
	}

	public bool MinAutoValue
	{
		get
		{
			return minAutoValue;
		}
		set
		{
			minAutoValue = value;
		}
	}

	public bool MaxAutoValue
	{
		get
		{
			return maxAutoValue;
		}
		set
		{
			maxAutoValue = value;
		}
	}
}
