using System.Collections.Generic;
using Game.Core;
using Game.Session.Data;
using SomaSim.Util;

namespace Game.Services;

public sealed class GoonSettings
{
	public class GoonCosts
	{
		public LabelDictionary<Buyout> buyout;

		public Buyout hitman;
	}

	public class Buyout
	{
		public ModValue cash;
	}

	public ModValue relToStartLoot;

	public ModValue relToStopLoot;

	public ModValue specialGoonChance;

	public List<SpecialGoonReqs> specialGoonReqs;

	public LabelDictionary<SpecialGoonConfig> configs;

	public GoonCosts goonCosts;

	public Label FindFirstConfigPassingReqs(VisitState visit)
	{
		foreach (SpecialGoonReqs specialGoonReq in specialGoonReqs)
		{
			if (specialGoonReq.reqs == null || specialGoonReq.reqs.AllPass(visit))
			{
				return specialGoonReq.config;
			}
		}
		return Label.NULL;
	}

	public SpecialGoonConfig FindSpecialGoonConfig(Label goontype)
	{
		return configs.FindOrNullAndWarn(goontype, "Missing special goon config for goontype");
	}

	public Buyout FindBuyoutDef(Label goontype)
	{
		return goonCosts.buyout.FindOrNullAndWarn(goontype, "Missing buyout definition for goontype");
	}

	public List<GoonLootTableEntry> FindLootTable(Label goontype)
	{
		SpecialGoonConfig specialGoonConfig = FindSpecialGoonConfig(goontype);
		if (specialGoonConfig == null)
		{
			return null;
		}
		if (specialGoonConfig.loottable != null && specialGoonConfig.loottable.Count != 0)
		{
			return specialGoonConfig.loottable;
		}
		return null;
	}

	public GoonLootTableEntry FindLootTableEntry(Label goontype, Label entry)
	{
		List<GoonLootTableEntry> list = FindLootTable(goontype);
		if (list == null)
		{
			return null;
		}
		GoonLootTableEntry goonLootTableEntry = list.Find((GoonLootTableEntry c) => c.id == entry);
		if (goonLootTableEntry != null)
		{
			return goonLootTableEntry;
		}
		return null;
	}
}
