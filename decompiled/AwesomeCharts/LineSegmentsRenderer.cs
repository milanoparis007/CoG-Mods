using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace AwesomeCharts;

[RequireComponent(typeof(RectTransform))]
public class LineSegmentsRenderer : BaseMaskableGraphic
{
	protected Vector2 GetSize()
	{
		Rect rect = GetComponent<RectTransform>().rect;
		return new Vector2(rect.width, rect.height);
	}

	protected void DrawSegments(VertexHelper vh, List<LineSegment> segments, GridLineConfig config)
	{
		segments.ForEach(delegate(LineSegment segment)
		{
			vh.AddUIVertexQuad(CreateUIVertices(segment.CreateSegmentVertices(), CreateDefaultUVs(), config.Color));
		});
	}

	protected List<LineSegment> CreateVerticalOrHorizontalSegments(int linesCount, GridLineConfig config, float width, float height, bool vertical)
	{
		return CreateSplitLinePoints(linesCount, height).SelectMany((float point) => CreateLineSegments(point, config, width, vertical)).ToList();
	}

	protected List<float> CreateSplitLinePoints(int pointsCount, float lineLenght)
	{
		float num = lineLenght / (float)(pointsCount + 1);
		List<float> list = new List<float>();
		for (int i = 0; i < pointsCount; i++)
		{
			list.Add(num * (float)(i + 1));
		}
		return list;
	}

	protected List<LineSegment> CreateLineSegments(float startingPoint, GridLineConfig config, float totalLenght, bool vertical)
	{
		float desiredSegmentLenght = (config.ShouldDrawDashedLines() ? ((float)config.DashLenght) : totalLenght);
		float num = (config.ShouldDrawDashedLines() ? config.DashSpacing : 0);
		float num2 = 0f;
		List<LineSegment> list = new List<LineSegment>();
		float num3;
		for (; num2 < totalLenght; num2 += num3 + num)
		{
			num3 = calculateSegmentLength(num2, desiredSegmentLenght, num, totalLenght);
			Vector2 startingPoint2 = new Vector2(vertical ? startingPoint : num2, vertical ? num2 : startingPoint);
			list.Add(CreateLine(startingPoint2, config.Thickness, num3, vertical));
		}
		return list;
	}

	protected float calculateSegmentLength(float startingPoint, float desiredSegmentLenght, float segmentSpacing, float totalLenght)
	{
		float num = totalLenght - startingPoint;
		if (desiredSegmentLenght + segmentSpacing < num)
		{
			return desiredSegmentLenght;
		}
		return num;
	}

	protected LineSegment CreateLine(Vector2 startingPoint, int thickness, float lenght, bool vertical)
	{
		Vector2 bottomLeft = (vertical ? new Vector2(startingPoint.x - (float)thickness / 2f, startingPoint.y) : new Vector2(startingPoint.x, startingPoint.y - (float)thickness / 2f));
		Vector2 topRight = (vertical ? new Vector2(startingPoint.x + (float)thickness / 2f, startingPoint.y + lenght) : new Vector2(startingPoint.x + lenght, startingPoint.y + (float)thickness / 2f));
		return new LineSegment(bottomLeft, topRight);
	}
}
