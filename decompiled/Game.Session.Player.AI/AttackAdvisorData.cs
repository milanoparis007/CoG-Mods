using Game.Core;

namespace Game.Session.Player.AI;

public sealed class AttackAdvisorData : BaseAdvisorData
{
	public SimTime nextDropCheck = SimTime.MIN_DATE;

	public SimTime nextBuildingCheck = SimTime.MIN_DATE;

	public SimTime nextClosureCheck = SimTime.MIN_DATE;

	public SimTime nextSellOutToFedsCheck = SimTime.MIN_DATE;

	public SimTime nextCoordCheck = SimTime.MIN_DATE;

	public GenericAttackTarget nextBuildingTarget;

	public GenericAttackTarget nextClosureTarget;

	public CoordinatedAttackTarget nextCoordTarget;

	public CoordinatedAttackState coordState;
}
