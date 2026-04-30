using Game.Core;
using Game.Session.Player.Commands;

namespace Game.Session.Entities;

public struct ActionHistoryElement
{
	public CommandType type;

	public int turn;

	public string ctx;

	public ActionHistoryElement(Command cmd, int turn)
	{
		this.turn = turn;
		type = cmd.type;
		ctx = GetContext();
		string GetContext()
		{
			if (cmd is CommandAttack commandAttack)
			{
				return commandAttack.target.ToString();
			}
			if (cmd is CommandGoto commandGoto)
			{
				return commandGoto.goalID.ToString();
			}
			if (cmd is CommandScopeOut commandScopeOut)
			{
				return commandScopeOut.nodeId.ToString();
			}
			if (cmd is AbstractAICommandBuySellItem abstractAICommandBuySellItem)
			{
				return $"{abstractAICommandBuySellItem.item} @ {abstractAICommandBuySellItem.building}";
			}
			if (cmd is AbstractAICommandPickDropItem abstractAICommandPickDropItem)
			{
				return $"{abstractAICommandPickDropItem.item} <=> {abstractAICommandPickDropItem.building}";
			}
			return "";
		}
	}

	public override string ToString()
	{
		return $"{type} {turn}";
	}
}
