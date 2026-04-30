using Game.Core;
using Game.Session.Entities;

namespace Game.Session.Heatmaps;

public class GroundBuildingHeatmap : Heatmap
{
	public const float NEAR_THRESHOLD = 0.3f;

	public const float VERY_NEAR_THRESHOLD = 0.5f;

	public const float ENTITY_VALUE = 0.2f;

	public override HeatmapType Type => HeatmapType.GroundBuildings;

	public override Config MakeConfig()
	{
		return new Config
		{
			convoKernel = HeatmapConvolutionKernel.GROUNDCOLOR_BLUR,
			updateDuringInteractive = false
		};
	}

	protected override void PopulateHeatmap()
	{
		foreach (Entity item in Game.ctx.entityman.GenerateListOfAllEntities())
		{
			if (item.config.lot != null || item.config.building != null)
			{
				float area = item.config.board.lotsize.Area;
				AddToHeatmap(item.data.board.worldpos, 0.2f * area);
			}
		}
	}

	public GroundFlags GetGroundFlag(WorldPos pos)
	{
		float valueSafe = GetValueSafe(pos);
		GroundFlags groundFlags = ((valueSafe > 0.3f) ? GroundFlags.NearBuilding : GroundFlags.None);
		GroundFlags groundFlags2 = ((valueSafe > 0.5f) ? GroundFlags.VeryNearBuilding : GroundFlags.None);
		return groundFlags | groundFlags2;
	}
}
