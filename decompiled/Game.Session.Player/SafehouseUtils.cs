using Game.Core;
using Game.Services;
using Game.Session.Board;
using Game.Session.Entities;
using Game.Session.Sim.Modules;
using Game.UI.Session;
using SomaSim.Util;

namespace Game.Session.Player;

public static class SafehouseUtils
{
	public static Entity FindAnySafehouseAtNode(Node node)
	{
		foreach (EntityID item in node.contained)
		{
			Entity entity = item.FindEntity();
			if (entity.components.building.IsSafehouse)
			{
				return entity;
			}
		}
		return null;
	}

	public static Entity FindAnyControlledBuildingAtNode(Node node)
	{
		foreach (EntityID item in node.contained)
		{
			Entity entity = item.FindEntity();
			if (entity.data.building.controlled.IsSet)
			{
				return entity;
			}
		}
		return null;
	}

	public static bool CanRaidSafehouse(NodeID nid, PlayerID pid)
	{
		if (nid.IsNotValid)
		{
			return false;
		}
		Entity entity = FindAnySafehouseAtNode(nid.FindNode());
		if (entity == null)
		{
			return false;
		}
		return CanRaidSafehouse(entity, pid);
	}

	public static bool CanRaidSafehouse(Entity building, PlayerID pid)
	{
		BuildingComponent buildingComponent = building?.components.building;
		if (buildingComponent == null)
		{
			return false;
		}
		if (!buildingComponent.IsSafehouseNotOf(pid))
		{
			return false;
		}
		if (!buildingComponent.IsScopedBy(pid))
		{
			return false;
		}
		PlayerInfo playerInfo = buildingComponent.SafehouseOwner.FindPlayer();
		if (playerInfo.crew.IsCrewDefeated)
		{
			return !WasSafehouseRaided(playerInfo);
		}
		return false;
	}

	private static bool WasSafehouseRaided(PlayerInfo player)
	{
		if (player == null)
		{
			return false;
		}
		return player.territory.SafehouseData?.raided ?? false;
	}

	internal static bool WasSafehouseRaided(Entity building)
	{
		PlayerID pid = building?.components.building?.SafehouseOwner ?? PlayerID.INVALID;
		if (pid.IsAIPlayer)
		{
			return WasSafehouseRaided(pid.FindPlayer());
		}
		return false;
	}

	public static void ClearSafehouseAndTerritory(PlayerInfo player, EntityID safehouseId)
	{
		Entity building = safehouseId.FindEntity();
		RemoveMoney();
		RemoveSafehouse();
		ShowNewspaper();
		void RemoveMoney()
		{
			Money money = ModulesUtil.GetInventory(building).data.money;
			player.finances.DoChangeMoney(building, new Price(-money.cash), MoneyReason.CombatDefeat);
		}
		void RemoveSafehouse()
		{
			building.components.building.ClearSafehouse();
			Node node = building.components.board.GetNode();
			node.respect.ClearSafehouse(player.PID);
			PlayerSocial.DebugLogAIHistory(player.PID, player.PID, building, node, "ai", "safehouse-eliminated");
			player.territory.RecomputeHeatAndRespect(node, forceCurrent: true);
			player.outposts.RemoveOutpostsOnDefeat();
			player.territory.RemoveBuildingsAndTerritoryOnDefeat();
			player.territory.RemoveAllRecentTradeHistories();
			Game.ctx.events.EnqueueOnce(new SessionEvent(SessionEventType.SafehouseRemoved, safehouseId, player.PID));
		}
		void ShowNewspaper()
		{
			if (player.IsJustGang)
			{
				PhotoConfig value = Game.serv.globals.settings.people.combatSettings.eliminateGangPhotos.LastOrDefaultFast();
				string playerGroupName = player.social.PlayerGroupName;
				string cornerNameShort = building.components.board.GetNode().GetCornerNameShort();
				string header = Loc.Get("newspaper-headline.outfitgone", "cornername", cornerNameShort, "groupname", playerGroupName);
				Game.serv.ui.AddPopup(new NewspaperPopup(header, value));
			}
		}
	}
}
