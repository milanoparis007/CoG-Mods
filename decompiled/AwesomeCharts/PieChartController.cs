using UnityEngine;

namespace AwesomeCharts;

public class PieChartController : MonoBehaviour
{
	public PieChart pieChart;

	private void Start()
	{
		ConfigChart();
		AddChartData();
	}

	private void ConfigChart()
	{
		pieChart.Config.InnerPadding = 40;
		pieChart.Config.ValueIndicatorFontSize = 18;
		pieChart.Config.ValueIndicatorLineLength = 30;
		pieChart.Config.ValueIndicatorColor = Color.white;
		pieChart.Config.ValueIndicatorVisibility = PieChartConfig.ValueIndicatorVisibilityMode.ONLY_SELECTED;
		PieChartConfig config = new PieChartConfig
		{
			InnerPadding = 40,
			ValueIndicatorFontSize = 18,
			ValueIndicatorLineLength = 30,
			ValueIndicatorColor = Color.white,
			ValueIndicatorVisibility = PieChartConfig.ValueIndicatorVisibilityMode.ONLY_SELECTED
		};
		pieChart.Config = config;
	}

	private void AddChartData()
	{
		PieDataSet pieDataSet = new PieDataSet();
		pieDataSet.AddEntry(new PieEntry(20f, "Entry 1", Color.red));
		pieDataSet.AddEntry(new PieEntry(30f, "Entry 2", Color.green));
		pieDataSet.AddEntry(new PieEntry(25f, "Entry 3", Color.blue));
		pieChart.GetChartData().DataSet = pieDataSet;
		pieChart.SetDirty();
	}
}
