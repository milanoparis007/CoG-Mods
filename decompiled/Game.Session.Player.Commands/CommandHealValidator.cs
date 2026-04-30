using Game.Core;
using Game.Services;
using Game.Session.Entities;

namespace Game.Session.Player.Commands;

public sealed class CommandHealValidator : HumanCommandValidator
{
	public override CommandType Type => CommandType.Heal;

	public override int SortOrder => 8;

	public override CommandStatus Validate(PlayerID pid, CrewAssignment crew)
	{
		CommandStatus result = new CommandStatus(Type, Loc.Get("ui.command.heal.icon"));
		Entity peep = crew.GetPeep();
		if (!peep.components.agent.IsWounded)
		{
			return result;
		}
		if (!AtSafehouseOrControlledBuilding(pid, crew))
		{
			return result.Set(CommandEnabledStatus.DisabledOther, Loc.Get("ui.command.heal.mo.loc"));
		}
		if (!peep.components.agent.HasActionsRemaining)
		{
			return result.Set(CommandEnabledStatus.DisabledOther, GetActionCostString());
		}
		return result.Set(CommandEnabledStatus.Enabled, Loc.Get("ui.command.heal.mo.loc"));
	}

	protected override void PostCommandAsHuman(CrewAssignment crew)
	{
		HumanCommandValidator.PostCommandHelper(new CommandHeal(PlayerID.HumanPlayer, crew.peepId));
	}

	private static bool AtSafehouseOrControlledBuilding(PlayerID pid, CrewAssignment crew)
	{
		PlayerInfo playerInfo = pid.FindPlayer();
		Node node = crew.GetPeep().components.agent.GetNode();
		if (playerInfo.territory.GetHeadquartersNode() == node)
		{
			return true;
		}
		foreach (EntityID item in playerInfo.territory.GetAllControlledBuildingsUnsafe())
		{
			if (item.FindEntity().components.board.GetNodeID() == node.id)
			{
				return true;
			}
		}
		return false;
	}
}
