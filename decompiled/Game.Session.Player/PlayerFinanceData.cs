using System.Collections.Generic;
using Game.Core;
using SomaSim.Util;

namespace Game.Session.Player;

public sealed class PlayerFinanceData
{
	public struct MoneyStoredStruct
	{
		public EntityID eid;

		public Money amt;

		public bool isCrew;

		public bool isBuilding;

		public EntityID crewPeepId;

		public int sortOrder;

		public bool IsValid => eid.IsValid;

		public MoneyStoredStruct(EntityID eid, Money amt, bool isVehicle, bool isBuilding, EntityID crewPeepId, int sortOrder)
		{
			this.eid = eid;
			this.amt = amt;
			isCrew = isVehicle;
			this.isBuilding = isBuilding;
			this.crewPeepId = crewPeepId;
			this.sortOrder = sortOrder;
		}

		public static int EntrySort(MoneyStoredStruct a, MoneyStoredStruct b)
		{
			if (a.isCrew && !b.isCrew)
			{
				return -1;
			}
			if (!a.isCrew && b.isCrew)
			{
				return 1;
			}
			if (a.isCrew && b.isCrew)
			{
				return a.sortOrder - b.sortOrder;
			}
			return 0;
		}
	}

	public bool isStoredCacheDirty;

	public List<MoneyStoredStruct> moneyStoredPerLocation = new List<MoneyStoredStruct>();

	public MoneyLedger moneyLedger = new MoneyLedger();

	public List<MoneyStoredStruct> GetMoneyStoredPerLocationUnsafe(PlayerInfo player)
	{
		if (isStoredCacheDirty)
		{
			isStoredCacheDirty = false;
			RefreshStoredMoney(player);
		}
		return moneyStoredPerLocation;
	}

	private int FindStoredIndex(EntityID eid)
	{
		int i = 0;
		for (int count = moneyStoredPerLocation.Count; i < count; i++)
		{
			if (moneyStoredPerLocation[i].eid == eid)
			{
				return i;
			}
		}
		return -1;
	}

	public bool HasStored(EntityID eid)
	{
		return FindStoredIndex(eid) >= 0;
	}

	public MoneyStoredStruct FindStored(EntityID eid)
	{
		int num = FindStoredIndex(eid);
		if (num < 0)
		{
			return default(MoneyStoredStruct);
		}
		return moneyStoredPerLocation[num];
	}

	private void SetStoredInVehicle(EntityID vehicleId, EntityID peepId, int sortOrder, Money money)
	{
		moneyStoredPerLocation.Add(new MoneyStoredStruct(vehicleId, money, isVehicle: true, isBuilding: false, peepId, sortOrder));
	}

	private void SetStoredInBuilding(EntityID buildingId, Money money)
	{
		moneyStoredPerLocation.Add(new MoneyStoredStruct(buildingId, money, isVehicle: false, isBuilding: true, EntityID.INVALID, -1));
	}

	internal void RefreshStoredMoney(PlayerInfo player)
	{
		moneyStoredPerLocation.Clear();
		foreach (EntityID item in player.territory.GetAllControlledBuildingsUnsafe())
		{
			SetStoredInBuilding(item, player.finances.GetMoney(item));
		}
		int num = 0;
		foreach (EntityID allVehicle in player.crew.AllVehicles)
		{
			EntityID peepId = player.crew.FindPeepAssignedToVehicle(allVehicle);
			Money money = player.finances.GetMoney(allVehicle);
			SetStoredInVehicle(allVehicle, peepId, num++, money);
		}
		moneyStoredPerLocation.StableSort(MoneyStoredStruct.EntrySort);
	}
}
