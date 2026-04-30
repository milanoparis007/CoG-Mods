using System.Collections.Generic;
using Game.Core;
using Game.Services;
using Game.Session.Data;
using Game.Session.Entities;
using UnityEngine;

namespace Game.Session.Overlays;

public class OverlayResDir
{
	public static readonly OverlayResDir EMPTY = new OverlayResDir();

	public EntityID eid;

	public List<EntityResDir> resources;

	public Color pickColor;

	public string pickIcon;

	public string pickMO;

	public bool IsMixed
	{
		get
		{
			if (HasFromBizToPlayer())
			{
				return HasFromPlayerToBiz();
			}
			return false;
		}
	}

	public bool IsValid => !object.Equals(this, EMPTY);

	public bool IsNotValid => object.Equals(this, EMPTY);

	public bool HasFromBizToPlayer()
	{
		foreach (EntityResDir resource in resources)
		{
			if (resource.IsFromBizToPlayer)
			{
				return true;
			}
		}
		return false;
	}

	public bool HasFromPlayerToBiz()
	{
		foreach (EntityResDir resource in resources)
		{
			if (resource.IsFromPlayerToBiz)
			{
				return true;
			}
		}
		return false;
	}

	public Color GetPickColor()
	{
		if (pickColor != default(Color))
		{
			return pickColor;
		}
		if (IsValid && IsMixed)
		{
			return ColorConstants.ARROW_SAFE;
		}
		if (IsValid && HasFromBizToPlayer())
		{
			return ColorConstants.ARROW_BUY;
		}
		if (IsValid && HasFromPlayerToBiz())
		{
			return ColorConstants.ARROW_SELL;
		}
		return ColorConstants.IMG_DISABLED;
	}

	public string GetPickMouseover()
	{
		if (pickMO != null)
		{
			return pickMO;
		}
		if (resources.Count != 0)
		{
			string text = BuildingUtil.FindBuildingName(eid.FindEntity());
			if (HasFromBizToPlayer())
			{
				text = text + "\n" + Loc.Get("convo.buysell.sells") + ": ";
			}
			foreach (EntityResDir resource in resources)
			{
				if (resource.IsFromBizToPlayer)
				{
					text = text + resource.res.GetIconAndName() + " ";
				}
			}
			if (HasFromPlayerToBiz())
			{
				text = text + "\n" + Loc.Get("convo.buysell.buys") + ": ";
			}
			{
				foreach (EntityResDir resource2 in resources)
				{
					if (resource2.IsFromPlayerToBiz)
					{
						text = text + resource2.res.GetIconAndName() + " ";
					}
				}
				return text;
			}
		}
		return "";
	}

	public string GetPickBuySell()
	{
		if (resources.Count != 0)
		{
			if (IsMixed)
			{
				return Loc.Get("ui.trade.buyonce") + " " + Loc.Get("ui.trade.sellonce");
			}
			if (HasFromBizToPlayer())
			{
				return Loc.Get("ui.trade.buyonce");
			}
			if (HasFromPlayerToBiz())
			{
				return Loc.Get("ui.trade.sellonce");
			}
		}
		return "";
	}
}
