using System.Collections.Generic;
using Game.Core;
using Game.UI.Session.Crew;

namespace Game.Session.Player;

public sealed class PlayerCrewData
{
	public struct FamilyPayment
	{
		public EntityID peepId;

		public Price perTurn;
	}

	public struct RolePrompt
	{
		public CrewAssignment peep;

		public Label roleId;
	}

	public enum OffBoardReason
	{
		Arrested,
		Jailed,
		Scheme,
		LayingLow
	}

	public PlayerID pid;

	public PlayerCrewGrowth crewcap = new PlayerCrewGrowth();

	public List<CrewAssignment> rawcrew = new List<CrewAssignment>();

	public int countLiving;

	public int countDead;

	public List<EntityID> allVehicles = new List<EntityID>();

	public List<EntityID> scavengeableCars = new List<EntityID>();

	public List<FamilyPayment> deadCrewPayments = new List<FamilyPayment>();

	public List<RolePrompt> rolesPrompted = new List<RolePrompt>();

	public List<QueuedBoardReturnTime> queuedBoardReturns = new List<QueuedBoardReturnTime>();

	public List<OffBoardInfo> crewOffBoard = new List<OffBoardInfo>();

	public Dictionary<CrewCardType, List<CrewCardInfoInitData>> orderingByType = new Dictionary<CrewCardType, List<CrewCardInfoInitData>>();

	public int Count => rawcrew.Count;

	public PlayerCrewData()
	{
	}

	public PlayerCrewData(PlayerID pid)
	{
		this.pid = pid;
	}

	public IEnumerable<CrewAssignment> WhereTypeIs(CrewType type)
	{
		foreach (CrewAssignment item in rawcrew)
		{
			if (item.type == type)
			{
				yield return item;
			}
		}
	}

	public IEnumerable<CrewAssignment> WhereTypeIsNot(CrewType type)
	{
		foreach (CrewAssignment item in rawcrew)
		{
			if (item.type != type)
			{
				yield return item;
			}
		}
	}

	public void Add(CrewAssignment crew)
	{
		rawcrew.Add(crew);
		RecountTheLivingAndTheDead();
	}

	public CrewAssignment Get(int index)
	{
		return rawcrew[index];
	}

	public void Set(int index, CrewAssignment crew)
	{
		rawcrew[index] = crew;
		RecountTheLivingAndTheDead();
	}

	public void Remove(EntityID peepId)
	{
		int index = FindPeepIndex(peepId);
		Remove(index);
		RecountTheLivingAndTheDead();
	}

	public void Remove(int index)
	{
		rawcrew.RemoveAt(index);
		RecountTheLivingAndTheDead();
	}

	public void MarkAsDead(int index)
	{
		rawcrew[index] = rawcrew[index].SetDead();
		RecountTheLivingAndTheDead();
	}

	private void RecountTheLivingAndTheDead()
	{
		countLiving = (countDead = 0);
		foreach (CrewAssignment item in rawcrew)
		{
			if (item.IsNotDead)
			{
				countLiving++;
			}
			else
			{
				countDead++;
			}
		}
	}

	public int FindPeepIndex(EntityID peepId)
	{
		int i = 0;
		for (int count = rawcrew.Count; i < count; i++)
		{
			if (rawcrew[i].peepId == peepId)
			{
				return i;
			}
		}
		return -1;
	}

	public int FindTargetIndex(EntityID targetId)
	{
		int i = 0;
		for (int count = rawcrew.Count; i < count; i++)
		{
			if (rawcrew[i].targetId == targetId)
			{
				return i;
			}
		}
		return -1;
	}

	internal CrewAssignment FindCrewForPeep(EntityID peepId)
	{
		int num = FindPeepIndex(peepId);
		if (num < 0)
		{
			return CrewAssignment.EMPTY;
		}
		return rawcrew[num];
	}

	internal CrewAssignment FindCrewForTarget(EntityID targetId)
	{
		int num = FindTargetIndex(targetId);
		if (num < 0)
		{
			return CrewAssignment.EMPTY;
		}
		return rawcrew[num];
	}

	public bool TracksVehicle(EntityID eid)
	{
		return allVehicles.Contains(eid);
	}

	internal void AddVehicleTracking(EntityID eid)
	{
		allVehicles.Add(eid);
	}

	internal void RemoveVehicleTracking(EntityID eid)
	{
		allVehicles.Remove(eid);
	}
}
