namespace Game.Core;

public sealed class HeatmapConvolutionKernel
{
	public float[] values = new float[9];

	public static readonly HeatmapConvolutionKernel RES_BLUR = Make(1f, 0.25f, 0f);

	public static readonly HeatmapConvolutionKernel ETH_BLUR = Make(0f, 0.24f, 0f);

	public static readonly HeatmapConvolutionKernel PLACEMENT_BLUR = Make(0.6f, 0.1f, 0f);

	public static readonly HeatmapConvolutionKernel BUILDING_DENSITY_BLUR = Make(1f, 0.2f, 0.05f);

	public static readonly HeatmapConvolutionKernel LOT_ZONE_BLUR = Make(1f, 0.25f, 0.15f);

	public static readonly HeatmapConvolutionKernel APPEAL_BLUR = Make(0.6f, 0.1f, 0f);

	public static readonly HeatmapConvolutionKernel GROUNDCOLOR_BLUR = Make(1f, 0.2f, 0.05f);

	public static HeatmapConvolutionKernel Make(float core, float side, float diag)
	{
		HeatmapConvolutionKernel heatmapConvolutionKernel = new HeatmapConvolutionKernel();
		heatmapConvolutionKernel.values = new float[9] { diag, side, diag, side, core, side, diag, side, diag };
		return heatmapConvolutionKernel;
	}
}
