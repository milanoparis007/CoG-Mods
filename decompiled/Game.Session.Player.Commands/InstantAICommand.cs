using Game.Core;
using Game.Session.Entities;

namespace Game.Session.Player.Commands;

public abstract class InstantAICommand : MultiActionCommand
{
	public sealed override string Message => $"You should not be seeing this.\n{this}";

	public InstantAICommand()
	{
	}

	protected InstantAICommand(PlayerID pid, CommandType type, EntityID eid)
		: base(pid, type, eid)
	{
	}

	protected override StartStatus CanStart()
	{
		return StartStatus.OK;
	}

	protected sealed override void OnTurnStarted()
	{
	}

	protected sealed override bool ContinuesToNextTurn()
	{
		return false;
	}

	protected sealed override bool CanConsumePoints()
	{
		return true;
	}

	protected sealed override void DoConsumePoints()
	{
	}

	protected sealed override void OnFinished(bool success)
	{
	}

	protected void ConsumePeepActions()
	{
		peepId.FindEntity().components.agent.ConsumeAllPoints();
	}

	protected bool IsPeepAtSafehouse()
	{
		EntityData data = peepId.FindEntity().data;
		Node node = GetPlayer()?.territory?.GetHeadquartersNode();
		if (node == null)
		{
			return false;
		}
		return data.agent.nid.Equals(node.id);
	}
}
