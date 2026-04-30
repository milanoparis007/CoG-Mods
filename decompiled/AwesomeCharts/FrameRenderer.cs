using UnityEngine;
using UnityEngine.UI;

namespace AwesomeCharts;

public class FrameRenderer : LineSegmentsRenderer
{
	[SerializeField]
	private GridFrameConfig gridFrameConfig = new GridFrameConfig();

	public GridFrameConfig GridFrameConfig
	{
		get
		{
			return gridFrameConfig;
		}
		set
		{
			gridFrameConfig = value;
			SetAllDirty();
		}
	}

	protected override void OnPopulateMesh(VertexHelper vh)
	{
		vh.Clear();
		Vector2 size = GetSize();
		DrawGridFrame(vh, size, gridFrameConfig);
	}

	private void DrawGridFrame(VertexHelper vh, Vector2 size, GridFrameConfig frameConfig)
	{
		if (frameConfig != null)
		{
			float num = (float)frameConfig.LinesConfig.Thickness / 2f;
			if (frameConfig.DrawLeftLine)
			{
				DrawSegments(vh, CreateLineSegments(num, frameConfig.LinesConfig, size.y, vertical: true), frameConfig.LinesConfig);
			}
			if (frameConfig.DrawRightLine)
			{
				DrawSegments(vh, CreateLineSegments(size.x - num, frameConfig.LinesConfig, size.y, vertical: true), frameConfig.LinesConfig);
			}
			if (frameConfig.DrawBottomLine)
			{
				DrawSegments(vh, CreateLineSegments(num, frameConfig.LinesConfig, size.x, vertical: false), frameConfig.LinesConfig);
			}
			if (frameConfig.DrawTopLine)
			{
				DrawSegments(vh, CreateLineSegments(size.y - num, frameConfig.LinesConfig, size.x, vertical: false), frameConfig.LinesConfig);
			}
		}
	}
}
