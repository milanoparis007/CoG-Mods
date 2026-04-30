using System.Collections.Generic;
using System.Diagnostics;
using Game.Core;
using Game.Session.Data;

namespace Game.Session.Player.AI;

public sealed class BusinessAdvisorData : BaseAdvisorData
{
	[DebuggerDisplay("{DebugString}")]
	public struct Pickup
	{
		public static readonly Pickup INVALID;

		public EntityID building;

		public EntityID playerBuilding;

		public MfgItem item;

		public bool humanTrades;

		public bool IsValid => building.IsValid;

		public bool IsNotValid => building.IsNotValid;

		private string DebugString => $"[Pickup {item}: {building} => {playerBuilding}, ht = {humanTrades}]";

		public override string ToString()
		{
			return DebugString;
		}
	}

	public SimTime nextPickupCheck = SimTime.MIN_DATE;

	public SimTime nextBldgCheck = SimTime.MIN_DATE;

	public SimTime nextCasinoCheck = SimTime.MIN_DATE;

	public List<Pickup> nextPickupList = new List<Pickup>();

	public EntityID nextTakeoverTarget = EntityID.INVALID;

	public EntityID nextGamblingTakeoverTarget = EntityID.INVALID;

	public SimTime tiedHouseCooldownExpiration = SimTime.MIN_DATE;
}
