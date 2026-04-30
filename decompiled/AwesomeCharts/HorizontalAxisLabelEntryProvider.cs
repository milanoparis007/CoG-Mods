using System;

namespace AwesomeCharts;

[Serializable]
public class HorizontalAxisLabelEntryProvider : LinearAxisLabelEntryProvider
{
	protected override AxisOrientation GetEntryAxisOrientation()
	{
		return AxisOrientation.HORIZONTAL;
	}
}
