using Game.Core;

namespace Game.Session.Player.Commands;

public sealed class AICommandPickUpItem : AbstractAICommandPickDropItem
{
	public AICommandPickUpItem()
	{
	}

	public AICommandPickUpItem(PlayerID pid, EntityID eid, Label item, EntityID building)
		: base(CommandType.PickupItem, pid, eid, item, building, peepPicksUp: true)
	{
	}
}
