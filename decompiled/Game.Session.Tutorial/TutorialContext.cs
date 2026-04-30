using Game.Core;
using Game.Session.Entities;

namespace Game.Session.Tutorial;

public sealed class TutorialContext
{
	public EntityID francineId;

	public EntityID ziggyId;

	public EntityID ziggyBuildingId;

	public EntityID neighborToVisitId;

	public EntityID sameNodeBizToVisitId;

	public EntityID ziggyFriendID;

	public Entity Francine => francineId.FindEntity();

	public Entity Ziggy => ziggyId.FindEntity();

	public Entity ZiggyBuilding => ziggyBuildingId.FindEntity();

	public Entity NeighborToVisit => neighborToVisitId.FindEntity();

	public Entity SameNodeBizToVisit => sameNodeBizToVisitId.FindEntity();

	public Entity ZiggyFriend => ziggyFriendID.FindEntity();
}
