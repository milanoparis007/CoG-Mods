using System.Collections.Generic;
using Game.Core;
using Game.Services;
using Game.Session.Entities;
using Game.Session.Player;
using Game.Session.Player.KB;
using Game.UI.Session.OwnedBiz;
using SomaSim.Util;
using UnityEngine;

namespace Game.UI.Session.Picks;

public struct BuildingPickData
{
	public bool interactable;

	public bool scopable;

	public bool humanControlled;

	public bool humanSafehouse;

	public bool aiSafehouse;

	public bool policeStation;

	public bool scoped;

	public bool canCrewInteract;

	public bool showGamblingFlare;

	public bool showResidentialFlare;

	public List<KBResult> pips;

	public bool showpips;

	public Resource resUnlocked;

	public int tickets;

	public string icon;

	public Color color;

	public Sprite ownedBuildingIcon;

	public Fixnum healthCurrent;

	public Fixnum healthMax;

	public bool HasWarningsToShow
	{
		get
		{
			if (tickets <= 0)
			{
				return resUnlocked != null;
			}
			return true;
		}
	}

	public bool HasDamage => healthCurrent < healthMax;

	public static BuildingPickData GenerateBuildingButtonData(BuildingAndBusinessData data)
	{
		PlayerInfo human = Game.ctx.players.Human;
		PlayerKB kb = human.kb;
		EntityID id = data.building.Id;
		BuildingComponent building = data.building.components.building;
		ResidenceComponent residence = data.building.components.residence;
		PlayerID controllingPlayer = building.GetControllingPlayer();
		PlayerID safehouseOwner = building.SafehouseOwner;
		bool flag = building.CanBeScopedByPlayerCrew(human.PID);
		bool flag2 = building.IsInteractableByPlayer(human.PID) || flag;
		bool crewhere = building.IsAnyCrewAtThisNode(human.PID);
		(bool scoped, string icon) tuple = BuildingPickUtil.GenerateBuildingButtonIcon(data.building);
		bool item = tuple.scoped;
		string item2 = tuple.icon;
		Color playerBuildingButtonColor = BuildingPickUtil.GetPlayerBuildingButtonColor(data.building, item, crewhere);
		Sprite sprite = (controllingPlayer.IsHumanPlayer ? ModulesUIUtil.FindIconBackModuleOrGambling(data.building) : null);
		int num = GenerateTicketCounts(data, human);
		Resource resource = FindIfUnlockedRes(data.building, human);
		BuildingData.Health healthDataOrNull = building.GetHealthDataOrNull();
		Fixnum fixnum = healthDataOrNull?.current ?? ((Fixnum)0);
		Fixnum fixnum2 = healthDataOrNull?.max ?? ((Fixnum)0);
		bool flag3 = human.gambling.IsOwnedGamblingHouse(id);
		bool flag4 = residence != null && (residence.IsDebtorResidence || residence.IsEventHostingActive);
		BuildingPickData result = new BuildingPickData
		{
			icon = item2,
			color = playerBuildingButtonColor,
			humanControlled = controllingPlayer.IsHumanPlayer,
			humanSafehouse = safehouseOwner.IsHumanPlayer,
			aiSafehouse = safehouseOwner.IsAIPlayer,
			policeStation = building.IsPoliceStation,
			showGamblingFlare = flag3,
			showResidentialFlare = flag4,
			ownedBuildingIcon = sprite,
			canCrewInteract = crewhere,
			interactable = flag2,
			scopable = flag,
			scoped = item,
			pips = new List<KBResult>(),
			healthCurrent = fixnum,
			healthMax = fixnum2,
			tickets = num,
			resUnlocked = resource,
			showpips = false
		};
		if (item)
		{
			result.pips = new List<KBResult>
			{
				kb.GetStatusForBuilding(id, PlayerKBQueryNames.RESEVENT_READY),
				kb.GetStatusForBuilding(id, PlayerKBQueryNames.QREQ_SHOULD_START),
				kb.GetStatusForBuilding(id, PlayerKBQueryNames.QREQ_SHOULD_FINISH),
				kb.GetStatusForBuilding(id, PlayerKBQueryNames.BUILDING_DAMAGED),
				kb.GetStatusForBuilding(id, PlayerKBQueryNames.COP_PAIDOFF),
				kb.GetStatusForBuilding(id, PlayerKBQueryNames.BIZ_STALLED),
				kb.GetStatusForBuilding(id, PlayerKBQueryNames.BIZ_DELIVERY_FAILED),
				kb.GetStatusForBuilding(id, PlayerKBQueryNames.BIZ_DELIVERY_INSUFFICIENT),
				kb.GetStatusForBuilding(id, PlayerKBQueryNames.BIZ_DELIVERY_SUCCESS),
				kb.GetStatusForBuilding(id, PlayerKBQueryNames.OUTPOST_SUPPORT_URGENT),
				kb.GetStatusForBuilding(id, PlayerKBQueryNames.OUTPOST_SUPPORT),
				kb.GetStatusForBuilding(id, PlayerKBQueryNames.OUTPOST_COLLECT),
				kb.GetStatusForBuilding(id, PlayerKBQueryNames.OUTPOST_EXPANDING),
				kb.GetStatusForBuilding(id, PlayerKBQueryNames.OUTPOST_CAN_EXPAND),
				kb.GetStatusForBuilding(id, PlayerKBQueryNames.OUTPOST_ACTIVE),
				kb.GetStatusForBuilding(id, PlayerKBQueryNames.OUTPOST_IS_AI),
				kb.GetStatusForBuilding(id, PlayerKBQueryNames.OUTPOST_IS_HUMAN),
				kb.GetStatusForBuilding(id, PlayerKBQueryNames.HEAL_PROMPT),
				kb.GetStatusForBuilding(id, PlayerKBQueryNames.OWNER_ACTIVE_QUEST),
				kb.GetStatusForBuilding(id, PlayerKBQueryNames.OWNER_EXPIRED_QUEST),
				kb.GetStatusForBuilding(id, PlayerKBQueryNames.OWNER_CASH_BOOST),
				kb.GetStatusForBuilding(id, PlayerKBQueryNames.OWNER_CAN_LEARN_SKILL),
				kb.GetStatusForBuilding(id, PlayerKBQueryNames.OWNER_CAN_INTRO_FOR_BIZ),
				kb.GetStatusForBuilding(id, PlayerKBQueryNames.OWNER_CAN_HIRE_CREW),
				kb.GetStatusForBuilding(id, PlayerKBQueryNames.OWNER_CAN_TAKE_BIZ),
				kb.GetStatusForBuilding(id, PlayerKBQueryNames.CTRL_EMPTY_SLOT),
				kb.GetStatusForBuilding(id, PlayerKBQueryNames.CTRL_UPGRADE_MODULE),
				kb.GetStatusForBuilding(id, PlayerKBQueryNames.BIZ_EXTORTED),
				kb.GetStatusForBuilding(id, PlayerKBQueryNames.BIZ_RECENT_TRADE_AI),
				kb.GetStatusForBuilding(id, PlayerKBQueryNames.BIZ_RECENT_TRADE_HUMAN),
				kb.GetStatusForBuilding(id, PlayerKBQueryNames.CASINO_OUT_OF_CASH),
				kb.GetStatusForBuilding(id, PlayerKBQueryNames.CANADA_RELATED)
			};
			for (int i = 0; i < result.pips.Count; i++)
			{
				if (result.pips[i].showpip)
				{
					result.showpips = true;
					break;
				}
			}
		}
		return result;
	}

	private static Resource FindIfUnlockedRes(Entity building, PlayerInfo player)
	{
		PlayerSkills.Unlocked unlockedThisTurn = player.skills.UnlockedThisTurn;
		if (unlockedThisTurn.res == null || !unlockedThisTurn.Contains(building.Id))
		{
			return null;
		}
		return unlockedThisTurn.res;
	}

	private static int GenerateTicketCounts(BuildingAndBusinessData data, PlayerInfo player)
	{
		if (data.owner != null)
		{
			return player.social.GetSocialTicketsAvailable(data.owner.Id);
		}
		return 0;
	}
}
