using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Services;
using Game.Session.Entities;
using SomaSim.Util;

namespace Game.Session.Player.Commands;

public abstract class HumanCommandValidator
{
	private static readonly List<HumanCommandValidator> _all;

	public static List<HumanCommandValidator> AllHandlers => _all;

	public abstract CommandType Type { get; }

	public abstract int SortOrder { get; }

	static HumanCommandValidator()
	{
		_all = FindCommandButtonHandlers();
	}

	public HumanCommandValidator()
	{
	}

	private static List<HumanCommandValidator> FindCommandButtonHandlers()
	{
		return (from type in TypeUtils.FindAllChildrenOf(typeof(HumanCommandValidator))
			select (HumanCommandValidator)Activator.CreateInstance(type)).ToList();
	}

	public static HumanCommandValidator FindValidator(CommandType type)
	{
		int i = 0;
		for (int count = _all.Count; i < count; i++)
		{
			if (_all[i].Type == type)
			{
				return _all[i];
			}
		}
		return null;
	}

	public static CommandStatus Validate(CommandType type, PlayerID pid, CrewAssignment crew)
	{
		return FindValidator(type)?.Validate(pid, crew) ?? new CommandStatus(type, "?");
	}

	public static List<CommandButtonState> GetAvailableCommands(CrewAssignment crew)
	{
		if (crew.peepId.IsNotValid)
		{
			return new List<CommandButtonState>();
		}
		List<CommandButtonState> list = AllHandlers.Select((HumanCommandValidator handler) => handler.MakeButtonState(PlayerID.HumanPlayer, crew)).ToList();
		list.Sort(CommandButtonState.Compare);
		return list;
	}

	public abstract CommandStatus Validate(PlayerID pid, CrewAssignment crew);

	protected abstract void PostCommandAsHuman(CrewAssignment crew);

	public void OnHumanButtonClick(CrewAssignment crew, bool deselectWhenDone = true)
	{
		PostCommandAsHuman(crew);
		if (!crew.GetPeep().components.agent.HasActionsOrMovesRemaining && deselectWhenDone)
		{
			Game.ctx.selection.SetActive(null);
		}
	}

	public CommandButtonState MakeButtonState(PlayerID pid, CrewAssignment crew)
	{
		return new CommandButtonState(crew, this, Validate(pid, crew));
	}

	public static Node GetNode(Entity peep)
	{
		return peep?.components.agent?.GetNode();
	}

	public static void PostCommandHelper(Command cmd)
	{
		cmd.pid.FindPlayer().commands.AddCommandImmediate(cmd);
	}

	protected bool CanCrewPayActionCost(CrewAssignment crew, CrewCost cost)
	{
		return crew.peepId.FindEntity().components.agent.CanPay(cost);
	}

	protected bool IsCrewUnpaid(CrewAssignment crew)
	{
		return crew.GetPeep().components.agent.WasCrewUnpaidLastTurn();
	}

	protected bool IsCrewInjured(CrewAssignment crew)
	{
		return crew.GetPeep().components.agent.IsInjured();
	}

	protected string GetSalaryString()
	{
		return Loc.Get("ui.command.fail.salary");
	}

	protected string GetInjuryString()
	{
		return Loc.Get("ui.command.fail.injury");
	}

	protected string GetActionCostString()
	{
		return Loc.Get("ui.command.fail.points");
	}
}
