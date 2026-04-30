using System;
using Game.Core;
using Game.Services;
using Game.Session.Entities;
using UnityEngine;

namespace Game.Session.Data;

public struct EntityResDir : IEquatable<EntityResDir>
{
	public static readonly EntityResDir EMPTY;

	public EntityID eid;

	public Resource res;

	public bool toBldg;

	public Color pickColor;

	public string pickIcon;

	public string pickMO;

	public bool IsFromBizToPlayer => !toBldg;

	public bool IsFromPlayerToBiz => toBldg;

	public bool IsValid => !Equals(this, EMPTY);

	public bool IsNotValid => Equals(this, EMPTY);

	public Color GetPickColor()
	{
		if (pickColor != default(Color))
		{
			return pickColor;
		}
		if (IsValid && IsFromBizToPlayer)
		{
			return ColorConstants.ARROW_BUY;
		}
		if (IsValid && IsFromPlayerToBiz)
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
		if (res != null)
		{
			string text = BuildingUtil.FindBuildingName(eid.FindEntity());
			string text2 = Loc.Get(IsFromBizToPlayer ? "convo.buysell.sells" : "convo.buysell.buys");
			return text + "\n" + Loc.Get("convo.buysell.type-and-resource", "prefix", text2, "resource", res.GetIconAndName());
		}
		return "";
	}

	public string GetPickBuySell()
	{
		if (res != null)
		{
			if (IsFromBizToPlayer)
			{
				return Loc.Get("ui.trade.buyonce");
			}
			if (IsFromPlayerToBiz)
			{
				return Loc.Get("ui.trade.sellonce");
			}
		}
		return "";
	}

	public static bool Equals(EntityResDir a, EntityResDir b)
	{
		if (EntityID.Equals(a.eid, b.eid) && a.res == b.res && a.toBldg == b.toBldg && a.pickColor == b.pickColor && a.pickIcon == b.pickIcon)
		{
			return a.pickMO == b.pickMO;
		}
		return false;
	}

	public bool Equals(EntityResDir other)
	{
		return Equals(this, other);
	}
}
