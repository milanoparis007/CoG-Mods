using System;
using UnityEngine;

namespace AwesomeCharts;

[Serializable]
public class AxisLabelRendererExtry
{
	[SerializeField]
	private string text;

	[SerializeField]
	private float positionOnAxis;

	[SerializeField]
	private AxisOrientation orientation;

	[SerializeField]
	private AxisLabelGravity gravity;

	public string Text
	{
		get
		{
			return text;
		}
		set
		{
			text = value;
		}
	}

	public float PositionOnAxis
	{
		get
		{
			return positionOnAxis;
		}
		set
		{
			positionOnAxis = value;
		}
	}

	public AxisOrientation Orientation
	{
		get
		{
			return orientation;
		}
		set
		{
			orientation = value;
		}
	}

	public AxisLabelGravity Gravity
	{
		get
		{
			return gravity;
		}
		set
		{
			gravity = value;
		}
	}
}
