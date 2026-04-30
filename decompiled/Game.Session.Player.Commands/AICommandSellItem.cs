using Game.Core;

namespace Game.Session.Player.Commands;

public sealed class AICommandSellItem : AbstractAICommandBuySellItem
{
	public AICommandSellItem()
	{
	}

	public AICommandSellItem(PlayerID pid, EntityID eid, Label item, EntityID building)
		: base(CommandType.BuyItem, pid, eid, item, building, playerBuys: false)
	{
	}
}
