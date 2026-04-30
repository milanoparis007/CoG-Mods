using System.Linq;
using System.Text;
using Game.Core;
using Game.Services;
using Game.Session;
using Game.Session.Assets;
using Game.Session.Board;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Player;
using Game.Session.Sim.Modules;
using Game.UI.Session.Crew;
using Game.UI.Session.Popups;
using SomaSim.Util;

namespace Game.UI.Session.Picks;

public static class ScavengeUtil
{
	public static void ShowSafehouseRaidPopup(Entity safehouse)
	{
		EntitySelectionPopup.ShowCrewSelector(safehouse.components.board.GetNodeID(), Loc.Get("ui.scavenge.raid.popup.select"), delegate(EntityID selected)
		{
			ShowConfirmRaidPopup(safehouse, selected);
		});
	}

	private static void ShowConfirmRaidPopup(Entity safehouse, EntityID peepid)
	{
		string fullName = peepid.FindEntity().components.agent.FindCrewAssignment().GetPeep().data.person.FullName;
		string message = Loc.Get("ui.scavenge.raid.popup", "name", fullName);
		OkPopup.ShowOkCancel(peepid, message, delegate
		{
			Game.ctx.selection.HandleDeselect();
			Game.ctx.players.Human.territory.PerformRaid(safehouse);
			DoScavenge(safehouse, peepid);
		}, delegate
		{
			Game.ctx.selection.HandleDeselect();
		});
	}

	public static void ShowVehicleScavengingPopup(Entity target)
	{
		EntitySelectionPopup.ShowCrewSelector(GetNodeIDForEntity(target), Loc.Get("ui.scavenge.select"), delegate(EntityID selected)
		{
			DoScavenge(target, selected);
		});
	}

	private static void DoScavenge(Entity target, EntityID selected)
	{
		if (!selected.IsNotValid)
		{
			Entity entity = selected.FindEntity();
			PlayerID pid = entity.data.agent.pid;
			PlayerID playerIDForEntity = GetPlayerIDForEntity(target);
			DoScavenge(entity, pid, target, playerIDForEntity);
			selected.FindEntity()?.components.agent.IncrementStat(CrewStats.SafehouseRaids, 1);
			Game.ctx.events.SendImmediate(new SessionEvent(SessionEventType.AchieveScavenged, target.Id, playerIDForEntity));
			Game.ctx.sfx.PlayMoneyConfirm();
			WorldPos? worldPos = BoardUtil.FindBoardPositionFor(target);
			if (worldPos.HasValue)
			{
				Game.ctx.vfx.PlayOneShotPFX(PFXType.AttackFX, worldPos.Value, PlayerID.HumanPlayer, 2f);
			}
		}
	}

	private static NodeID GetNodeIDForEntity(Entity e)
	{
		if (e.components.building != null)
		{
			return e.components.board.GetNodeID();
		}
		return e.components.mobile?.FindNodeNearThisMobile() ?? NodeID.INVALID;
	}

	private static PlayerID GetPlayerIDForEntity(Entity e)
	{
		if (e.components.building != null)
		{
			return e.components.building.SafehouseOwner;
		}
		if (e.components.mobile == null)
		{
			return PlayerID.System;
		}
		return e.data.mobile.pid;
	}

	private static void DoScavenge(Entity attacker, PlayerID attackerPid, Entity victimEntity, PlayerID victimPid)
	{
		PlayerInfo targetPlayer = attackerPid.FindPlayer();
		PlayerInfo playerInfo = victimPid.FindPlayer();
		var (money, items) = MoveLoot(playerInfo, victimEntity, targetPlayer, attacker);
		OkPopup.Show(DescribeLoot(money, items, playerInfo, victimEntity));
	}

	private static (Price money, ResourceAndQtyList items) MoveLoot(PlayerInfo sourcePlayer, Entity sourceEntity, PlayerInfo targetPlayer, Entity targetEntity)
	{
		InventoryModule inventory = ModulesUtil.GetInventory(sourceEntity);
		InventoryModule inventory2 = ModulesUtil.GetInventory(targetEntity);
		inventory.data.contents.SelectToString((ResourceAndQty item) => $"{item.id}:{item.qty}", " ");
		Price asPrice = inventory.data.money.AsPrice;
		sourcePlayer.finances.DoChangeMoney(inventory.data, -asPrice, MoneyReason.Other);
		targetPlayer.finances.DoChangeMoney(inventory2.data, asPrice, MoneyReason.Other);
		ResourceAndQtyList resourceAndQtyList = new ResourceAndQtyList();
		foreach (ResourceAndQty item in inventory.data.contents.OrderByDescending((ResourceAndQty raq) => raq.FindResource().sell.cash))
		{
			Fixnum fixnum = ModulesUtil.FindHowMuchCanBeTransferredIn(inventory2, item.id, item.qty);
			if (fixnum.IsPositive)
			{
				resourceAndQtyList.data.Add(new ResourceAndQty(item.id, fixnum));
				if (item.FindResource().rescat == new Label("rescat-booze"))
				{
					Game.ctx.achievements.IncrementBoozeStolen((int)fixnum);
				}
				inventory.data.Increment(item.id, -fixnum);
				inventory2.data.Increment(item.id, fixnum);
				if (!targetPlayer.skills.HasResourceUnlocked(item.id))
				{
					targetPlayer.skills.UnlockResource(item.id, startup: false);
				}
			}
		}
		return (money: asPrice, items: resourceAndQtyList);
	}

	private static string DescribeLoot(Price money, ResourceAndQtyList items, PlayerInfo player, Entity target)
	{
		StringBuilder stringBuilder = StringBuilderPool.AllocateInstance();
		string value = ((target.components.mobile != null) ? Loc.Get("ui.scavenge.title.vehicle") : Loc.Get("ui.scavenge.title.safehouse", "groupname", player.social.FindPlayerGroupNameColorized()));
		stringBuilder.AppendLine(value);
		bool flag = false;
		if (money.IsNonZero)
		{
			flag = true;
			stringBuilder.AppendLine(Loc.Get("ui.scavenge.money", "amt", Loc.Price(money)));
		}
		if (items.data.Count > 0)
		{
			flag = true;
			stringBuilder.AppendLine(Loc.Get("ui.scavenge.items"));
			foreach (ResourceAndQty datum in items.data)
			{
				stringBuilder.AppendLine(Loc.Get("ui.scavenge.oneline", "item", datum.MakeQuantityXLocString()));
			}
		}
		if (!flag)
		{
			stringBuilder.AppendLine(Loc.Get("ui.scavenge.none"));
		}
		return stringBuilder.ToString();
	}
}
