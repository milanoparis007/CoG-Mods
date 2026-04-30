using System.Collections.Generic;

namespace AwesomeCharts;

public abstract class LinearAxisLabelEntryProvider : AxisLabelEntryProvider
{
	public int labelCount;

	public float axisLength;

	public float valueMin;

	public float valueMax;

	public bool firstEntryVisible = true;

	public bool lastEntryVisible = true;

	public AxisLabelGravity labelsGravity;

	public AxisValueFormatter valueFormatter = new BasicAxisValueFormatter();

	protected abstract AxisOrientation GetEntryAxisOrientation();

	protected virtual float GetLabelAxisPosition(int index, int maxIndex)
	{
		if (!(axisLength > 0f))
		{
			return 0f;
		}
		return axisLength * ((float)index / (float)maxIndex);
	}

	public List<AxisLabelRendererExtry> getLabelRendererEntries()
	{
		List<AxisLabelRendererExtry> list = new List<AxisLabelRendererExtry>();
		int minLabelIndex = GetMinLabelIndex();
		int maxLabelIndex = GetMaxLabelIndex();
		for (int i = minLabelIndex; i < minLabelIndex + labelCount; i++)
		{
			AxisLabelRendererExtry axisLabelRendererExtry = new AxisLabelRendererExtry();
			axisLabelRendererExtry.PositionOnAxis = GetLabelAxisPosition(i, maxLabelIndex);
			axisLabelRendererExtry.Gravity = labelsGravity;
			axisLabelRendererExtry.Text = GetLabelValueText(i - minLabelIndex, axisLabelRendererExtry.PositionOnAxis);
			axisLabelRendererExtry.Orientation = GetEntryAxisOrientation();
			list.Add(axisLabelRendererExtry);
		}
		return list;
	}

	private int GetMinLabelIndex()
	{
		if (!firstEntryVisible)
		{
			return 1;
		}
		return 0;
	}

	private int GetMaxLabelIndex()
	{
		int num = GetMinLabelIndex() + labelCount - 1;
		if (!lastEntryVisible)
		{
			num++;
		}
		return num;
	}

	private string GetLabelValueText(int index, float axisPosition)
	{
		float value = 0f;
		float num = valueMax - valueMin;
		if (num > 0f && axisLength > 0f)
		{
			value = valueMin + num * (axisPosition / axisLength);
		}
		return valueFormatter.FormatAxisValue(index, value, valueMin, valueMax);
	}
}
