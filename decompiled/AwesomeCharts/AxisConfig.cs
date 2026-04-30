using System;
using UnityEngine;

namespace AwesomeCharts;

[Serializable]
public abstract class AxisConfig<V, H> where V : SingleAxisConfig where H : SingleAxisConfig
{
	[SerializeField]
	private V verticalAxisConfig;

	[SerializeField]
	private H horizontalAxisConfig;

	public V VerticalAxisConfig
	{
		get
		{
			return verticalAxisConfig;
		}
		set
		{
			verticalAxisConfig = value;
		}
	}

	public H HorizontalAxisConfig
	{
		get
		{
			return horizontalAxisConfig;
		}
		set
		{
			horizontalAxisConfig = value;
		}
	}

	protected abstract V CreateDefaultVerticalAxis();

	protected abstract H CreateDefaultHorizontalAxis();

	public AxisConfig()
	{
		verticalAxisConfig = CreateDefaultVerticalAxis();
		horizontalAxisConfig = CreateDefaultHorizontalAxis();
	}
}
