using Game.Core;

namespace Game.Session.Player.AI;

public sealed class UnitsAdvisorData : BaseAdvisorData
{
	public SimTime nextExpansion = SimTime.MIN_DATE;
}
