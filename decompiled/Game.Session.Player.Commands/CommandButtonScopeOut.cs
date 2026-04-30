using Game.Core;
using Game.Services;
using Game.Session.Board;
using Game.Session.Entities;

namespace Game.Session.Player.Commands;

public sealed class CommandButtonScopeOut : HumanCommandValidator
{
	public override CommandType Type => CommandType.ScopeOut;

	public override int SortOrder => 10;

	public override CommandStatus Validate(PlayerID pid, CrewAssignment crew)
	{
		CommandStatus result = new CommandStatus(Type, Loc.Get("ui.command.scope.icon"));
		Entity peep = crew.GetPeep();
		if (!HumanCommandValidator.GetNode(peep).CanBeScopedOut(pid))
		{
			return result;
		}
		if (IsCrewInjured(crew))
		{
			return result.Set(CommandEnabledStatus.DisabledInjury, GetInjuryString());
		}
		if (IsCrewUnpaid(crew))
		{
			return result.Set(CommandEnabledStatus.DisabledSalary, GetSalaryString());
		}
		if (!peep.components.agent.HasActionsRemaining)
		{
			return result.Set(CommandEnabledStatus.DisabledOther, GetActionCostString());
		}
		return result.Set(CommandEnabledStatus.Enabled, Loc.Get("ui.command.scope.mo"));
	}

	protected override void PostCommandAsHuman(CrewAssignment crew)
	{
		Node node = HumanCommandValidator.GetNode(crew.GetPeep());
		HumanCommandValidator.PostCommandHelper(new CommandScopeOut(PlayerID.HumanPlayer, crew.peepId, node));
	}

	public void OnHumanButtonClick(CrewAssignment crew, Entity building)
	{
		Node node = HumanCommandValidator.GetNode(crew.GetPeep());
		HumanCommandValidator.PostCommandHelper(new CommandScopeOut(PlayerID.HumanPlayer, crew.peepId, node, building));
	}
}
