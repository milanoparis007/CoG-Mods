using System;
using System.Collections.Generic;

namespace AwesomeCharts;

[Serializable]
public class BarAxisLabelEntryProvider : AxisLabelEntryProvider
{
	public BarCharPositioner barChartPositioner;

	public AxisLabelGravity labelsGravity;

	public AxisValueFormatter valueFormatter = new BarAxisValueFormatter();

	public List<AxisLabelRendererExtry> getLabelRendererEntries()
	{
		List<AxisLabelRendererExtry> list = new List<AxisLabelRendererExtry>();
		if (barChartPositioner == null)
		{
			return list;
		}
		int visibleEntriesRange = barChartPositioner.GetVisibleEntriesRange();
		int num = (int)barChartPositioner.axisBounds.XMin;
		for (int i = num; i < visibleEntriesRange + num; i++)
		{
			AxisLabelRendererExtry axisLabelRendererExtry = new AxisLabelRendererExtry();
			axisLabelRendererExtry.PositionOnAxis = barChartPositioner.GetBarCenterPosition(i).x;
			axisLabelRendererExtry.Gravity = labelsGravity;
			axisLabelRendererExtry.Text = GetLabelValueText(i, num, visibleEntriesRange + num);
			axisLabelRendererExtry.Orientation = AxisOrientation.HORIZONTAL;
			list.Add(axisLabelRendererExtry);
		}
		return list;
	}

	private string GetLabelValueText(int index, int minIndex, int maxIndex)
	{
		return valueFormatter.FormatAxisValue(index, index, minIndex, maxIndex);
	}
}
