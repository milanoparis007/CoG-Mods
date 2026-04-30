using Game.Core;
using Game.Services;
using Game.Session.Board;
using Game.Session.Entities;
using SomaSim.Util;

namespace Game.Session.Player.Commands;

public sealed class CommandGoto : MultiActionCommand
{
	public NodeID goalID;

	public bool shouldContinue;

	public bool freeDrive;

	private PathData _path;

	public PathData Path => _path;

	public override string Message => Loc.Get("ui.command.goto.action");

	public CommandGoto()
	{
	}

	public CommandGoto(PlayerID pid, EntityID peepId, Node goal, bool freeDrive = false)
		: base(pid, CommandType.GoTo, peepId)
	{
		if (goal == null || goal.id.IsNotValid)
		{
			Logger.Error("Invalid node passed to CommandGoto.");
			return;
		}
		goalID = goal.id;
		this.freeDrive = freeDrive;
	}

	public override bool CanActivateAfterDequeue()
	{
		SetAndValidatePath();
		return base.CanActivateAfterDequeue();
	}

	protected override StartStatus CanStart()
	{
		return SetAndValidatePath();
	}

	protected override void OnStarted()
	{
		base.OnStarted();
	}

	protected override void OnTurnStarted()
	{
		SetAndValidatePath();
	}

	protected override void PerformTurnActions()
	{
		shouldContinue = StartDrivingCar();
	}

	protected override bool ContinuesToNextTurn()
	{
		return shouldContinue;
	}

	protected override void OnFinished(bool success)
	{
		Game.ctx.events.EnqueueOnce(new SessionEvent(SessionEventType.CrewGotoFinished, peepId, pid, goalID));
		base.OnFinished(success);
	}

	private int FindMovesRemaining()
	{
		if (!freeDrive)
		{
			return peepId.FindEntity().components.agent.MovesRemaining;
		}
		return int.MaxValue;
	}

	protected override bool CanConsumePoints()
	{
		if (_path == null || _path.world.Count < 1)
		{
			return false;
		}
		int num = FindMovesRemaining();
		return _path.cost <= num;
	}

	protected override void DoConsumePoints()
	{
		Entity entity = peepId.FindEntity();
		CrewCost cost = CrewCost.OnlyMovement((int)_path.cost);
		entity.components.agent.DoPay(cost);
		entity.components.agent.IncrementStat(CrewStats.DriveDistance, (int)_path.cost);
	}

	public static PathData MakePath(PlayerID pid, Entity peep, Node goal, Fixnum maxcost)
	{
		PathData data = null;
		Game.ctx.transit.FindDrivingPath(pid, peep, goal.pos, delegate(Pathfinding.Result result)
		{
			if (result.status == Pathfinding.Status.Success)
			{
				data = new PathData();
				WorldPos pos = peep.components.agent.GetNode().pos;
				result.PopulatePath(data, pos, maxcost);
			}
		});
		return data;
	}

	private StartStatus SetAndValidatePath()
	{
		int num = FindMovesRemaining();
		if (num <= 0)
		{
			return StartStatus.SkipThisTurn;
		}
		Entity peep = peepId.FindEntity();
		_path = MakePath(pid, peep, goalID.FindNode(), num);
		if (_path == null || _path.world.Count <= 1)
		{
			return StartStatus.Failed;
		}
		return StartStatus.OK;
	}

	private bool StartDrivingCar()
	{
		if (_path == null || _path.world.Count <= 1)
		{
			return false;
		}
		Entity entity = peepId.FindEntity();
		WorldPos pos = _path.world.LastOrDefaultFast();
		Node node = Game.ctx.board.nodes.FindNearestNodeAround(pos, 5f);
		if (node == null || pos.IsZero)
		{
			return false;
		}
		Game.ctx.transit.SetAgentAtNode(node.id, entity);
		PlayerInfo player = GetPlayer();
		bool instant = !player.IsHuman;
		foreach (PathNode node2 in _path.nodes)
		{
			if (player.IsHuman && !node2.node.known.Get(PlayerID.HumanPlayer))
			{
				entity.components.agent.IncrementStat(CrewStats.NodeScouted, 1);
			}
			player.meetings.MarkNodeAsKnown(node2.node, expectedSeen: true, instant);
		}
		CrewAssignment crewForPeep = player.crew.GetCrewForPeep(peepId);
		Entity vehicle = crewForPeep.GetVehicle();
		if (vehicle == null)
		{
			_ = entity.data.person.FullName;
			return false;
		}
		Game.ctx.transit.DriveOnPath(player.PID, vehicle, _path);
		entity.components.agent.AddXP(XPSource.FromDriving);
		vehicle.components.mobile.UpdateHealthFrom(crewForPeep, VehicleHealthSource.FromDriving);
		return entity.components.agent.GetNode() != goalID.FindNode();
	}
}
