using Game.Core;
using Game.Services;
using Game.Session.Entities;

namespace Game.Session.Player.Commands;

public sealed class AICommandAttackBuilding : MultiActionCommand
{
	private readonly string MSG_NPCBIZ = "npcbiz";

	private readonly string MSG_GANGBIZ = "gangbiz";

	public EntityID buildingId;

	public PlayerID enemyId;

	public string message;

	public bool targetGang;

	public bool targetNpc;

	public override string Message => Loc.Get("ui.command.internal.attack");

	public AICommandAttackBuilding()
	{
	}

	public AICommandAttackBuilding(PlayerID pid, EntityID peepId, EntityID building, PlayerID enemyId, string message)
		: base(pid, CommandType.AttackBuilding, peepId)
	{
		buildingId = building;
		this.message = message;
		this.enemyId = enemyId;
		targetGang = message == MSG_GANGBIZ;
		targetNpc = message == MSG_NPCBIZ;
	}

	protected override StartStatus CanStart()
	{
		if (!ValidateState())
		{
			return StartStatus.Failed;
		}
		return StartStatus.OK;
	}

	private bool ValidateState()
	{
		if (!buildingId.IsValid)
		{
			return false;
		}
		NodeID nodeID = buildingId.FindEntity().components.board.GetNodeID();
		NodeID nid = peepId.FindEntity().data.agent.nid;
		if (nodeID != nid)
		{
			return false;
		}
		return IsTargetStillValid();
	}

	private bool IsTargetStillValid()
	{
		Entity entity = buildingId.FindEntity();
		if (targetGang)
		{
			PlayerID controllingPlayer = entity.components.building.GetControllingPlayer();
			if (controllingPlayer.IsNotAnyPlayer || controllingPlayer == pid)
			{
				return false;
			}
			return GetPlayer()?.ai.combat?.IsAttackAllowed(controllingPlayer) == true;
		}
		if (targetNpc)
		{
			return !((BuildingUtil.FindDataForBuilding(entity).biz?.components.biz?.FindTradeRestrictions(pid))?.IsLocked ?? false);
		}
		return false;
	}

	protected override void PerformTurnActions()
	{
		if (IsTargetStillValid())
		{
			if (targetGang)
			{
				Game.ctx.simman.combat.AttackBuilding(buildingId.FindEntity(), pid);
			}
			if (targetNpc)
			{
				GetPlayer().territory.ForceCloseBusiness(buildingId.FindEntity(), enemyId, peepId);
			}
		}
	}

	protected override bool ContinuesToNextTurn()
	{
		return IsTargetStillValid();
	}

	protected override bool CanConsumePoints()
	{
		return Game.ctx.simman.combat.CanPayAttackCost(peepId.FindEntity());
	}

	protected override void DoConsumePoints()
	{
		Game.ctx.simman.combat.DoPayAttackCost(peepId.FindEntity());
	}
}
