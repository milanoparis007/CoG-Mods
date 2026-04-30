using Game.Core;
using Game.Services;
using Game.Session.Board;
using Game.Session.Entities;

namespace Game.Session.Player.Commands;

public class CommandScopeOut : NormalCommand
{
	public NodeID nodeId;

	public EntityID next;

	public override string Message => Loc.Get("ui.command.type.scope");

	public CommandScopeOut()
	{
	}

	public CommandScopeOut(PlayerID pid, EntityID peepId, Node node)
		: base(pid, CommandType.ScopeOut, peepId)
	{
		nodeId = node.id;
	}

	public CommandScopeOut(PlayerID pid, EntityID peepId, Node node, Entity building)
		: base(pid, CommandType.ScopeOut, peepId)
	{
		nodeId = node.id;
		next = building.Id;
	}

	protected override CrewCost CalculateCost()
	{
		return Game.serv.globals.settings.people.social.costs.scopeOutCost;
	}

	protected override void OnStarted()
	{
		base.OnStarted();
		if (next.IsNotValid)
		{
			next = nodeId.FindNode().MakeOneScopeOutCandidate(pid);
		}
		if (next.IsValid)
		{
			GetPlayer().territory.ReserveScopeOut(next);
		}
	}

	protected override void OnFinished(bool success)
	{
		if (next.IsValid)
		{
			GetPlayer().territory.UnreserveScopeOut(next);
		}
		base.OnFinished(success);
	}

	protected override bool CanContinueToRun()
	{
		if (next.IsNotValid)
		{
			return false;
		}
		return !GetPlayer().territory.IsScoped(next.FindEntity());
	}

	protected override void PerformCommandSuccess()
	{
		base.PerformCommandSuccess();
		GetPlayer().territory.ScopeOutBuildingWithFeedback(next.FindEntity(), peepId);
	}
}
