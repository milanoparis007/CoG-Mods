using System.Collections.Generic;
using Game.Core;
using Game.Session.Entities;

namespace Game.Session.Heatmaps;

public class LotZoneHeatmap : Heatmap
{
	public const float ENTITY_VALUE = 0.2f;

	public override HeatmapType Type => HeatmapType.LotZone;

	internal void ClearAndSeed(List<WorldPos> list)
	{
		Clear();
		foreach (WorldPos item in list)
		{
			AddToHeatmap(item, 1f);
		}
	}

	public override Config MakeConfig()
	{
		return new Config
		{
			convoKernel = HeatmapConvolutionKernel.LOT_ZONE_BLUR,
			updateDuringInteractive = false
		};
	}

	public ZoneType GetZone()
	{
		if (!(tag == TagConstants.TAG_COMMERCIAL))
		{
			if (!(tag == TagConstants.TAG_INDUSTRIAL))
			{
				if (!(tag == TagConstants.TAG_RESIDENTIAL))
				{
					return ZoneType.Unknown;
				}
				return ZoneType.Res;
			}
			return ZoneType.Ind;
		}
		return ZoneType.Com;
	}

	protected override void PopulateHeatmap()
	{
		ZoneType zone = GetZone();
		foreach (Entity item in Game.ctx.entityman.GetCachedEntitiesByTagUnsafe(TagConstants.TAG_EMPTY_LOT))
		{
			if (item.data.lot.zone == zone)
			{
				AddToHeatmap(item.data.board.worldpos, 0.2f);
			}
		}
	}
}
