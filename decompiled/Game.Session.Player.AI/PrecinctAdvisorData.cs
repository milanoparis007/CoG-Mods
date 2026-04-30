using System.Collections.Generic;
using Game.Core;
using Game.Session.Data;

namespace Game.Session.Player.AI;

public sealed class PrecinctAdvisorData : BaseAdvisorData
{
	public EntityID stationId;

	public PrecinctID precinctId;

	public SimTime lastRaid = SimTime.MIN_DATE;

	public SimTime nextRaidCheck;

	public RaidTarget nextRaidTarget = RaidTarget.INVALID;

	public List<CopBeat> copbeats = new List<CopBeat>();

	public List<CopDonation> donations = new List<CopDonation>();

	public FedHintStatus fedhints = new FedHintStatus();

	public CopBeat GetBeat(EntityID officer)
	{
		foreach (CopBeat copbeat in copbeats)
		{
			if (copbeat.officerId == officer)
			{
				return copbeat;
			}
		}
		return null;
	}

	public bool HasBeat(EntityID officer)
	{
		return GetBeat(officer) != null;
	}
}
