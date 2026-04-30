using System;
using System.Linq;
using UnityEngine;

namespace AwesomeCharts;

public static class BezierUtils
{
	public static Vector2[] CreateBezierPointsFromLinePoints(Vector2[] points)
	{
		GetCurveControlPoints(points, out var firstControlPoints, out var secondControlPoints);
		Vector2[] array = new Vector2[points.Length * 3 - 2];
		for (int i = 0; i < points.Length; i++)
		{
			array[i * 3] = points[i];
			if (i < points.Length - 1)
			{
				array[i * 3 + 1] = firstControlPoints[i];
				array[i * 3 + 2] = secondControlPoints[i];
			}
		}
		return array;
	}

	public static void GetCurveControlPoints(Vector2[] knots, out Vector2[] firstControlPoints, out Vector2[] secondControlPoints)
	{
		if (knots == null)
		{
			throw new ArgumentNullException("knots");
		}
		int num = knots.Length - 1;
		if (num < 1)
		{
			throw new ArgumentException("At least two knot points required", "knots");
		}
		if (num == 1)
		{
			firstControlPoints = new Vector2[1];
			firstControlPoints[0].x = (2f * knots[0].x + knots[1].x) / 3f;
			firstControlPoints[0].y = (2f * knots[0].y + knots[1].y) / 3f;
			secondControlPoints = new Vector2[1];
			secondControlPoints[0].x = 2f * firstControlPoints[0].x - knots[0].x;
			secondControlPoints[0].y = 2f * firstControlPoints[0].y - knots[0].y;
			return;
		}
		float[] knotValues = knots.Select((Vector2 knot) => knot.x).ToArray();
		float[] knotValues2 = knots.Select((Vector2 knot) => knot.y).ToArray();
		float[] firstControlPoints2 = GetFirstControlPoints(CreateRightHandVectors(knotValues));
		float[] firstControlPoints3 = GetFirstControlPoints(CreateRightHandVectors(knotValues2));
		firstControlPoints = new Vector2[num];
		secondControlPoints = new Vector2[num];
		for (int num2 = 0; num2 < num; num2++)
		{
			firstControlPoints[num2] = new Vector2(firstControlPoints2[num2], firstControlPoints3[num2]);
			if (num2 < num - 1)
			{
				secondControlPoints[num2] = new Vector2(2f * knots[num2 + 1].x - firstControlPoints2[num2 + 1], 2f * knots[num2 + 1].y - firstControlPoints3[num2 + 1]);
			}
			else
			{
				secondControlPoints[num2] = new Vector2((knots[num].x + firstControlPoints2[num - 1]) / 2f, (knots[num].y + firstControlPoints3[num - 1]) / 2f);
			}
		}
	}

	private static float[] CreateRightHandVectors(float[] knotValues)
	{
		int num = knotValues.Length - 1;
		float[] array = new float[num];
		for (int i = 1; i < num - 1; i++)
		{
			array[i] = 4f * knotValues[i] + 2f * knotValues[i + 1];
		}
		array[0] = knotValues[0] + 2f * knotValues[1];
		array[num - 1] = (8f * knotValues[num - 1] + knotValues[num]) / 2f;
		return array;
	}

	private static float[] GetFirstControlPoints(float[] rightVectors)
	{
		int num = rightVectors.Length;
		float[] array = new float[num];
		float[] array2 = new float[num];
		float num2 = 2f;
		array[0] = rightVectors[0] / num2;
		for (int i = 1; i < num; i++)
		{
			array2[i] = 1f / num2;
			num2 = ((i < num - 1) ? 4f : 3.5f) - array2[i];
			array[i] = (rightVectors[i] - array[i - 1]) / num2;
		}
		for (int j = 1; j < num; j++)
		{
			array[num - j - 1] -= array2[num - j] * array[num - j];
		}
		return array;
	}
}
