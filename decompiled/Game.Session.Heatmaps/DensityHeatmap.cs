using System.Collections.Generic;
using Game.Core;
using Game.Session.Entities;
using Game.Session.Setup;
using UnityEngine;

namespace Game.Session.Heatmaps;

public class DensityHeatmap : Heatmap
{
	public const float ENTITY_VALUE = 0.2f;

	private bool _riversCached;

	private List<HeatmapPos> _riverHeatmapPositions;

	public override HeatmapType Type => HeatmapType.Density;

	public override Config MakeConfig()
	{
		return new Config
		{
			convoKernel = HeatmapConvolutionKernel.RES_BLUR
		};
	}

	protected override void PopulateHeatmap()
	{
		foreach (Entity item in Game.ctx.entityman.GetCachedEntitiesByTagUnsafe(tag))
		{
			AddToHeatmap(item.data.board.worldpos, 0.2f);
		}
		TryCacheRivers();
		foreach (HeatmapPos riverHeatmapPosition in _riverHeatmapPositions)
		{
			AddToHeatmap(riverHeatmapPosition, 0.2f);
		}
	}

	private void TryCacheRivers()
	{
		if (_riversCached)
		{
			return;
		}
		_riverHeatmapPositions = new List<HeatmapPos>();
		if (tag == TagConstants.TAG_INDUSTRIAL)
		{
			foreach (WaterRegion waterRegion in Game.ctx.board.terrain.WaterData.waterRegions)
			{
				WalkAndStampRiver(waterRegion.leftCenter, waterRegion.left.normal, waterRegion.length);
				WalkAndStampRiver(waterRegion.rightCenter, waterRegion.right.normal, waterRegion.length);
			}
		}
		_riversCached = true;
	}

	private void WalkAndStampRiver(Vector3 center, Vector3 normal, float length)
	{
		_ = Quaternion.LookRotation(normal, Vector3.up) * Quaternion.Euler(0f, 90f, 0f);
		Vector3 vector = Quaternion.Euler(0f, 90f, 0f) * normal;
		Vector3 vector2 = center + vector * length / 2f;
		_ = vector2 - vector * length;
		for (int i = 0; (float)i < length; i++)
		{
			WorldPos wpos = new WorldPos(vector2 - vector * i);
			HeatmapPos item = WorldToHeatmap(wpos);
			if (item.x >= 0 && item.x < heatmapSize.width && item.y >= 0 && item.y < heatmapSize.height)
			{
				_riverHeatmapPositions.Add(item);
			}
		}
	}
}
