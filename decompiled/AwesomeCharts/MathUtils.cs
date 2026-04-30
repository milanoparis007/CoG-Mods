using System;
using UnityEngine;

namespace AwesomeCharts;

public class MathUtils
{
	public static double GetAngle(Vector2 from, Vector2 to)
	{
		return Math.Atan2(to.y - from.y, to.x - from.x) * (180.0 / Math.PI);
	}

	public static double AngleToCircleAngle(double angle)
	{
		if (angle >= 0.0 && angle < 90.0)
		{
			return 90.0 - angle;
		}
		if (angle >= 90.0 && angle <= 180.0)
		{
			return 450.0 - angle;
		}
		return 90.0 - angle;
	}

	public static Vector2 GetPositionOnCircle(float angle, float radius)
	{
		float f = angle * ((float)Math.PI / 180f);
		return new Vector2(Mathf.Sin(f), Mathf.Cos(f)) * radius;
	}
}
