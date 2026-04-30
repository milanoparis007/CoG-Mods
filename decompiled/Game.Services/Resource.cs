using System.Collections.Generic;
using System.Diagnostics;
using Game.Core;
using Game.Session.Data;
using SomaSim.Util;

namespace Game.Services;

[DebuggerDisplay("{DebugString}")]
public sealed class Resource
{
	public sealed class RelBuffDef
	{
		public Fixnum points;

		public Fixnum forcedayz;
	}

	public enum Type
	{
		Basic,
		ModuleGroup
	}

	public Type type;

	public Price buy;

	public Price sell;

	public ModValue buyPriceModifier;

	public ModValue sellPriceModifier;

	public RelBuffDef relbuff;

	public Label unitid;

	public Label rescat;

	public string locname;

	public string loclistname;

	public string locdesc;

	public string locicon;

	public string locbatch;

	public bool illegal;

	public bool forceNoReveal;

	public Label entity;

	public List<Label> groupmembers;

	internal Label resid;

	internal UnitDef unitdef;

	public bool IsBasic => type == Type.Basic;

	public bool IsModuleGroup => type != Type.Basic;

	private string DebugString => ToString();

	public Volume FindTotalVolume(Fixnum qty)
	{
		return unitdef.volume * qty;
	}

	public Price GetPrice(bool playerBuying, PlayerID pid)
	{
		if (type != Type.ModuleGroup)
		{
			if (!playerBuying)
			{
				return sell * sellPriceModifier.Evaluate(new ModQuery(pid));
			}
			return buy * buyPriceModifier.Evaluate(new ModQuery(pid));
		}
		return 0;
	}

	public Price GetPriceWithMultiplier(bool playerBuying, Fixnum multiplier, PlayerID pid)
	{
		return GetPrice(playerBuying, pid) * multiplier;
	}

	public string GetPriceExplanation(bool playerBuying)
	{
		if (!playerBuying)
		{
			return sellPriceModifier.Explain(new ModQuery(PlayerID.HumanPlayer), addHeader: true);
		}
		return buyPriceModifier.Explain(new ModQuery(PlayerID.HumanPlayer), addHeader: true);
	}

	public static Resource Find(Label id)
	{
		return Game.ctx.simman.FindResource(id);
	}

	public string GetIcon()
	{
		return Loc.Get(locicon);
	}

	public string GetName()
	{
		return Loc.Get(locname);
	}

	public string GetListName()
	{
		return Loc.Get(loclistname);
	}

	public string GetDesc()
	{
		return Loc.Get(locdesc);
	}

	public string GetUnitsPlural()
	{
		return unitdef.GetBareUnitNoun(2);
	}

	public string GetIconAndName()
	{
		return Loc.Get("ui.combine.icon-and-name", "icon", GetIcon(), "name", GetName());
	}

	public string GetIconNameAndUnits(Fixnum qty)
	{
		return Loc.Get("ui.combine.icon-name-and-units", "icon", GetIcon(), "name", GetName(), "qtyunits", unitdef.GetQtyAndUnits(qty));
	}

	public string GetQtyIcon(Fixnum qty)
	{
		return Loc.Get("ui.resource.line-a-b", "a", Loc.FormatNumber(qty), "b", GetIcon());
	}

	public string GetQtyIconName(Fixnum qty)
	{
		return Loc.Get("ui.resource.line-a-b-c", "a", Loc.FormatNumber(qty), "b", GetIcon(), "c", GetName());
	}

	public string GetQtyXIconName(Fixnum qty)
	{
		return Loc.Get("ui.resource.line-ax-b-c", "a", Loc.FormatNumber(qty), "b", GetIcon(), "c", GetName());
	}

	public string GetQtyXName(Fixnum qty)
	{
		return Loc.Get("ui.resource.line-ax-b", "a", Loc.FormatNumber(qty), "b", GetName());
	}

	public string GetQtyIconNamePrice(Fixnum qty)
	{
		return Loc.Get("ui.resource.line-a-at-b", "a", GetQtyIconName(qty), "b", GetPrice(playerBuying: false, PlayerID.INVALID));
	}

	public bool GetIsIllegal()
	{
		if (Game.ctx.resManager == null)
		{
			return illegal;
		}
		if (!illegal || Game.ctx.resManager.IsForcedLegal(resid))
		{
			if (!illegal)
			{
				return Game.ctx.resManager.IsForcedIllegal(resid);
			}
			return false;
		}
		return true;
	}

	public (Fixnum points, Fixnum days) FindRelBuff(Fixnum basePoints, Fixnum defaultDays)
	{
		Fixnum fixnum = basePoints;
		Fixnum fixnum2 = defaultDays;
		if (relbuff != null)
		{
			fixnum += relbuff.points;
			if (relbuff.forcedayz > 0)
			{
				fixnum2 = Fixnum.Max(fixnum2, relbuff.forcedayz);
			}
		}
		fixnum = fixnum.PosFloorNegCeiling();
		return (points: fixnum, days: fixnum2);
	}

	public ResourceCategory GetCategory()
	{
		return Game.serv.globals.settings.resources.categories.FindOrNull(rescat);
	}

	public override string ToString()
	{
		return $"{resid} BUY {buy} SELL {sell}";
	}
}
