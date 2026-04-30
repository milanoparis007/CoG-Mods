using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Services;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Sim.Modules;
using SomaSim.Util;

namespace Game.UI.Session.Convo;

public sealed class ConvoDataGamblingHouseSelection : ConvoData, IConvoDataWithSelector
{
	public List<BuildingUtil.PotentialGamblingHouse> entries;

	public BuildingUtil.PotentialGamblingHouse selected;

	public Label gamblingModuleId;

	public List<EntityID> Entries => entries.Select((BuildingUtil.PotentialGamblingHouse x) => x.targetId).ToList();

	public bool HasAny
	{
		get
		{
			if (entries != null)
			{
				return entries.Count > 0;
			}
			return false;
		}
	}

	public bool SelectTarget(EntityID targetId)
	{
		int num = entries.FindIndex((BuildingUtil.PotentialGamblingHouse e) => e.targetId == targetId);
		if (num < 0)
		{
			return false;
		}
		selected = entries[num];
		return true;
	}

	public override string[] MakeReplacements(VisitState visit, int index)
	{
		GamblingModuleConfig gamblingModuleConfig = ModulesUtil.FindModuleDef(gamblingModuleId) as GamblingModuleConfig;
		ModQuery query = new ModQuery(PlayerID.HumanPlayer, EntityID.INVALID, visit.crew.peepId);
		Fixnum size = gamblingModuleConfig?.gambling?.aoeRadius.Evaluate(query) ?? ((Fixnum)0);
		string key = Game.serv.globals.settings.gambling.startup.FindSizeLabelFor(size);
		return new string[8]
		{
			"name",
			selected?.name,
			"amenitySlots",
			Loc.FormatNumber(gamblingModuleConfig?.gambling?.amenityCount.Evaluate(query) ?? ((Fixnum)0)),
			"influencetext",
			Loc.Get(key),
			"cost",
			Loc.Money(gamblingModuleConfig?.gambling?.purchaseCost.Evaluate(query).Abs ?? ((Fixnum)0))
		};
	}

	public ConvoDataGamblingHouseSelection()
	{
	}

	public ConvoDataGamblingHouseSelection(IEnumerable<BuildingUtil.PotentialGamblingHouse> entries, Label gamblingModuleId, BuildingUtil.PotentialGamblingHouse selected = null)
	{
		this.entries = new List<BuildingUtil.PotentialGamblingHouse>(entries);
		this.selected = selected;
		this.gamblingModuleId = gamblingModuleId;
	}

	private GamblingModuleConfig GetConfig()
	{
		if (!gamblingModuleId.IsSet)
		{
			return null;
		}
		return ModulesUtil.FindModuleDef(gamblingModuleId) as GamblingModuleConfig;
	}

	public override bool IsConvoStepEnabled(VisitState visit, ConvoButtonState bstate)
	{
		GamblingModuleConfig config = GetConfig();
		if (config?.gambling?.installReqs == null)
		{
			return true;
		}
		return config.gambling.installReqs.AllPass(visit, bstate);
	}

	public override string ExplainConvoStepNotEnabled(VisitState visit, ConvoButtonState bstate)
	{
		return GetConfig().gambling.installReqs.Explain(visit, bstate);
	}
}
