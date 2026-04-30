using Game.Core;
using Game.Session.Entities;

namespace Game.Session.Heatmaps;

public class AppealHeatmap : Heatmap
{
	public const float ENTITY_VALUE = -0.1f;

	public override HeatmapType Type => HeatmapType.Appeal;

	public override Config MakeConfig()
	{
		return new Config
		{
			convoKernel = HeatmapConvolutionKernel.APPEAL_BLUR,
			clearValue = 1f
		};
	}

	protected override void PopulateHeatmap()
	{
		foreach (Entity item in Game.ctx.entityman.GetCachedEntitiesBuildingsUnsafe())
		{
			if (item.config.building.type == ZoneType.Ind)
			{
				AddToHeatmap(item.data.board.worldpos, -0.1f);
			}
		}
		foreach (Entity item2 in Game.ctx.entityman.GetCachedEntitiesByTagUnsafe(TagConstants.TAG_EMPTY_LOT))
		{
			if (item2.data.lot.zone == ZoneType.Ind)
			{
				AddToHeatmap(item2.data.board.worldpos, -0.1f);
			}
		}
	}

	public AppealLevel GetAppealLevel(WorldPos pos)
	{
		float valueSafe = GetValueSafe(pos);
		AppealLevel appealLevel = AppealLevel.None;
		if (valueSafe < Game.ctx.session.mapconfig.generator.lowAppealThreshold)
		{
			return AppealLevel.Low;
		}
		if (valueSafe < Game.ctx.session.mapconfig.generator.midAppealThreshold)
		{
			return AppealLevel.Mid;
		}
		return AppealLevel.High;
	}
}
