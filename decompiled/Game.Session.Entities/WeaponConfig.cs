using System;
using System.Collections.Generic;
using Game.Core;
using Game.Services;
using Game.Session.Data;
using SomaSim.Util;

namespace Game.Session.Entities;

public class WeaponConfig : BaseConfig
{
	public Label resid;

	public ModValue hitpercent;

	public ModValue levelbuffs;

	public ModValue traitbuffs;

	public int low;

	public int high;

	public bool firearm;

	public bool tool;

	public override List<Type> RequiresConfigs => null;

	public override BaseComponent CreateComponent(EntityComponents ec)
	{
		return ec.weapon = new WeaponComponent();
	}

	public override BaseData MoveOrCreateData(EntityData target, EntityData source)
	{
		return null;
	}

	public Resource FindResource()
	{
		return Resource.Find(resid);
	}

	private Fixnum Evaluate(Entity peep, ModValue v)
	{
		ModQuery query = new ModQuery(peep.data.agent.pid, peep.Id, peep.Id);
		return v.Evaluate(query);
	}

	private string Explain(Entity peep, ModValue v)
	{
		ModQuery query = new ModQuery(peep.data.agent.pid, peep.Id, peep.Id);
		if (!v.Evaluate(query).IsNotZero)
		{
			return "";
		}
		return v.Explain(query, addHeader: false);
	}

	public string Describe(Entity peep, bool shortDesc)
	{
		Resource resource = FindResource();
		if (shortDesc)
		{
			return resource.GetIconAndName();
		}
		string text = DescribeDamageRange(peep);
		string text2 = Explain(peep, levelbuffs);
		string text3 = Explain(peep, traitbuffs);
		string text4 = (text2 + "\n" + text3).Trim();
		if (text4.Length > 0)
		{
			text4 = Loc.Get("ui.combat.explanation", "text", text4);
		}
		Fixnum hitProbability = GetHitProbability(peep);
		string text5 = Explain(peep, hitpercent).Trim();
		if (text5.Length > 0)
		{
			text5 = Loc.Get("ui.combat.explanation", "text", text5);
		}
		return Loc.Get("ui.combat.weapon-explanation", "icon", resource.GetIcon(), "name", resource.GetName(), "desc", resource.GetDesc(), "dmg", text, "dmgtext", text4, "hitrate", Loc.Percentage(hitProbability), "hittext", text5);
	}

	public string DescribeGeneric()
	{
		string text = DescribeDamageGeneric();
		Resource resource = FindResource();
		return resource.GetIcon() + " " + resource.GetName() + "\n\n" + Loc.Get("ui.viewinventory.invcard.mo.weapon", "value", text);
	}

	public Fixnum GetHitProbability(Entity attacker)
	{
		return Fixnum.Clamp(Evaluate(attacker, hitpercent) / 100, 0, 1);
	}

	public (int l, int h) GetDamageRange(Entity attacker)
	{
		Fixnum fixnum = Evaluate(attacker, levelbuffs);
		Fixnum fixnum2 = Evaluate(attacker, traitbuffs);
		int num = (fixnum + fixnum2).IntCeiling();
		int item = MathUtil.ClampMin(low + num, 0);
		int item2 = MathUtil.ClampMin(high + num, 0);
		return (l: item, h: item2);
	}

	public string DescribeDamageRange(Entity peep)
	{
		var (value, value2) = GetDamageRange(peep);
		return Loc.Get("ui.combat.weapon-damage", "low", Loc.FormatNumber(value), "high", Loc.FormatNumber(value2));
	}

	public string DescribeDamageGeneric()
	{
		return Loc.Get("ui.combat.weapon-damage", "low", Loc.FormatNumber(low), "high", Loc.FormatNumber(high));
	}

	internal int CalculateHitPoints(Entity peep, Xorshift rng)
	{
		var (min, max) = GetDamageRange(peep);
		return rng.Generate(min, max);
	}

	internal int GetHitpointAverage()
	{
		return (high + low) / 2;
	}
}
