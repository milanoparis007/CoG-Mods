using Game.Core;
using Game.Services;
using Game.Session.Data;
using Game.Session.Entities;
using UnityEngine;

namespace Game.Session.Overlays;

public sealed class ArrowChainLink
{
	public Entity from;

	public Entity to;

	public Resource res;

	public AutomationStep step;

	public Color ResColor
	{
		get
		{
			if (step != null)
			{
				if (step.action != AutoAction.Buy)
				{
					if (step.action != AutoAction.Sell)
					{
						return ColorConstants.ARROW_SAFE;
					}
					return ColorConstants.ARROW_SELL;
				}
				return ColorConstants.ARROW_BUY;
			}
			return ColorConstants.ARROW_SELL;
		}
	}

	public ArrowChainLink(EntityID from, EntityID to, Resource res, AutomationStep step)
	{
		this.from = from.FindEntity();
		this.to = to.FindEntity();
		this.res = res;
		this.step = step;
	}

	public ArrowChainLink(EntityID from, EntityID to)
	{
		this.from = from.FindEntity();
		this.to = to.FindEntity();
		res = null;
		step = null;
	}

	public Entity GetBuilding()
	{
		if (from.components.building == null)
		{
			if (to.components.building == null)
			{
				return null;
			}
			return to;
		}
		return from;
	}
}
