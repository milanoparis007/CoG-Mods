using Game.Core;

namespace Game.Session.Player.Commands;

public sealed class AICommandBuyItem : AbstractAICommandBuySellItem
{
	public AICommandBuyItem()
	{
	}

	public AICommandBuyItem(PlayerID pid, EntityID eid, Label item, EntityID building)
		: base(CommandType.BuyItem, pid, eid, item, building, playerBuys: true)
	{
	}
}
