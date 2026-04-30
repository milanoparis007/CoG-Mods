using System.Collections.Generic;
using Game.Core;

namespace Game.Session.Entities;

public sealed class ResidenceData : BaseData
{
	public ResidenceAssignment assignment;

	public List<ApartmentData> apartments = new List<ApartmentData>(4);

	public ResEventData hostedResEvent;

	public EntityID npcResident;

	public bool IsAssigned => assignment != ResidenceAssignment.None;

	public bool IsNotAssigned => assignment == ResidenceAssignment.None;

	public bool ShouldHaveNpcResident
	{
		get
		{
			if (assignment != ResidenceAssignment.EventSpaceActive && assignment != ResidenceAssignment.DebtorResidence && assignment != ResidenceAssignment.GamblingHouse)
			{
				return assignment == ResidenceAssignment.PoliticianResidence;
			}
			return true;
		}
	}

	public bool IsEventHostingSpace
	{
		get
		{
			if (assignment != ResidenceAssignment.EventSpaceReserved)
			{
				return assignment == ResidenceAssignment.EventSpaceActive;
			}
			return true;
		}
	}

	public bool IsEventHostingReserved => assignment == ResidenceAssignment.EventSpaceReserved;

	public bool IsEventHostingActive => assignment == ResidenceAssignment.EventSpaceActive;
}
