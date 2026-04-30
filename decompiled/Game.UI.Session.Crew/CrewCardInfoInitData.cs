using System;
using Game.Core;
using Game.Session.Data;
using Game.Session.Player;

namespace Game.UI.Session.Crew;

public struct CrewCardInfoInitData : IEquatable<CrewCardInfoInitData>
{
	public readonly CrewCardType type;

	public readonly CrewAssignment crew;

	public readonly EntityID building;

	public readonly EntityID emptyVehicle;

	public readonly AutomationID automation;

	public static bool Equals(CrewCardInfoInitData a, CrewCardInfoInitData b)
	{
		if (a.type == b.type && a.crew.peepId == b.crew.peepId && a.building == b.building && a.emptyVehicle == b.emptyVehicle)
		{
			return a.automation.Equals(b.automation);
		}
		return false;
	}

	public bool Equals(CrewCardInfoInitData other)
	{
		return Equals(this, other);
	}

	public override bool Equals(object obj)
	{
		if (obj is CrewCardInfoInitData b)
		{
			return Equals(this, b);
		}
		return false;
	}

	public static bool operator ==(CrewCardInfoInitData a, CrewCardInfoInitData b)
	{
		return Equals(a, b);
	}

	public static bool operator !=(CrewCardInfoInitData a, CrewCardInfoInitData b)
	{
		return !Equals(a, b);
	}

	public CrewCardInfoInitData(CrewCardType type, CrewAssignment? crew = null, EntityID? building = null, EntityID? emptyVehicle = null, AutomationID? automation = null)
	{
		this.type = type;
		this.crew = crew ?? CrewAssignment.EMPTY;
		this.building = building ?? EntityID.INVALID;
		this.emptyVehicle = emptyVehicle ?? EntityID.INVALID;
		this.automation = automation ?? AutomationID.INVALID;
	}

	public CrewCardInfoInitData(CrewCardInfo card)
		: this(card.type, card.crew, card.building, card.emptyVehicle, card.automation)
	{
	}

	public override int GetHashCode()
	{
		return crew.peepId.index;
	}
}
