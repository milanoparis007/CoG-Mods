using System.Collections.Generic;
using Game.Core;

namespace Game.Session.Player.AI;

public sealed class SafehouseAdvisorData : BaseAdvisorData
{
	public EntityID nextBackroomToInstall = EntityID.INVALID;

	public SimTime nextSafehouseCheck = SimTime.MIN_DATE;

	public EntityID nextCrewToRefill = EntityID.INVALID;

	public SimTime nextSkillCheck = SimTime.MIN_DATE;

	public List<Label> skillTrack;

	public SimTime nextDonationCheck = SimTime.MIN_DATE;
}
