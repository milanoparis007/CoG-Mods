using Game.Core;
using Game.Session.Entities;
using SomaSim.Util;

namespace Game.Session.Heatmaps;

public class EthnicityHeatmap : Heatmap
{
	public const float ENTITY_VALUE = 0.2f;

	public override HeatmapType Type => HeatmapType.Ethnicity;

	public override Config MakeConfig()
	{
		return new Config
		{
			convoKernel = HeatmapConvolutionKernel.ETH_BLUR
		};
	}

	protected override void PopulateHeatmap()
	{
		foreach (Entity item in Game.ctx.entityman.GetCachedEntitiesByTagUnsafe(TagConstants.TAG_BUILDING_ALL))
		{
			if (FindEthnicityInfluence(item) > 0f)
			{
				AddToHeatmap(item.data.board.worldpos, 0.2f);
			}
		}
	}

	private float FindEthnicityInfluence(Entity e)
	{
		if (e.components.residence != null)
		{
			return MathUtil.Clamp((float)e.components.residence.CountEthnicApartments(tag).residents * 0.2f, 0f, 1f);
		}
		if (e.components.building.HasBizOwner)
		{
			Entity entity = e.components.building.GetAttachedBusiness().FindEntity();
			if (entity != null && entity.components.biz.HasOwnerWithEthnicity(tag))
			{
				return e.config.board.lotsize.width * 0.2f;
			}
		}
		return 0f;
	}
}
