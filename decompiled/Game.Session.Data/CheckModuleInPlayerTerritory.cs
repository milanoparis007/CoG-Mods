using Game.Core;
using Game.Services;
using Game.Session.Board;
using Game.Session.Entities;
using Game.Session.Player;
using Game.Session.Sim.Modules;

namespace Game.Session.Data;

public class CheckModuleInPlayerTerritory : CheckListOfItems
{
	public enum Scope
	{
		AllBuildings,
		PlayerOnly,
		InterestingOnly
	}

	public Scope scope;

	protected override int Count(VisitState visit)
	{
		int num = 0;
		foreach (Label item in of)
		{
			if (HasModuleAnywhereInTerritory(GetPlayer(visit), item))
			{
				num++;
			}
		}
		return num;
	}

	public bool HasModuleAnywhereInTerritory(PlayerInfo player, Label moduleId)
	{
		if (scope == Scope.AllBuildings || scope == Scope.PlayerOnly)
		{
			foreach (EntityID item in player.territory.GetAllControlledBuildingsUnsafe())
			{
				if (ModulesUtil.HasModule(item.FindEntity(), moduleId))
				{
					return true;
				}
			}
		}
		if (scope == Scope.AllBuildings || scope == Scope.InterestingOnly)
		{
			foreach (NodeID item2 in player.territory.GetAllOwnedNodesUnsafe())
			{
				foreach (EntityID item3 in item2.FindNode().interesting)
				{
					if (ModulesUtil.HasModule(item3.FindEntity(), moduleId))
					{
						return true;
					}
				}
			}
		}
		return false;
	}

	protected override void VerifyData(VisitState _)
	{
		foreach (Label item in of)
		{
			if (ModulesUtil.FindModuleDef(item) == null)
			{
				Label label = item;
				Logger.Warning("Checking module in player territory: unknown module id " + label.ToString());
			}
		}
	}

	protected override string IdToName(Label id)
	{
		string text = ModulesUtil.FindModuleDef(id)?.Common.display.locname;
		if (text == null)
		{
			return "?";
		}
		return Loc.Get(text);
	}

	protected override string MakeExplanationText()
	{
		return MakeExplanationText(Loc.Get("ui.requirements.modules.player.territory.expected"), Loc.Get("ui.requirements.modules.player.territory.unexpected"), Loc.Get("ui.requirements.modules.player.territory.all"), Loc.Get("ui.requirements.modules.player.territory.any"), Loc.Get("ui.requirements.modules.player.territory.none"));
	}
}
