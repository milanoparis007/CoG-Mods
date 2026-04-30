using System;

namespace AwesomeCharts;

[Serializable]
public class VerticalAxisLabelEntryProvider : LinearAxisLabelEntryProvider
{
	protected override AxisOrientation GetEntryAxisOrientation()
	{
		return AxisOrientation.VERTICAL;
	}
}
