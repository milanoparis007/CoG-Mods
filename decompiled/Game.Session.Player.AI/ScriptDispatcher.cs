using Game.Core;
using Game.Session.Board;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Player.Commands;
using SomaSim.Util;

namespace Game.Session.Player.AI;

public static class ScriptDispatcher
{
	public static bool RunScript(Label scriptLabel, PlayerID pid, Entity peep, Deictics vars = null)
	{
		NPCScript nPCScript = Game.serv.globals.settings.npc.FindScriptDef(scriptLabel);
		if (nPCScript == null)
		{
			Logger.Warning("Unknown script", scriptLabel);
			return false;
		}
		PlayerInfo playerInfo = pid.FindPlayer();
		_ = peep.data.person.FullName;
		vars = vars ?? new Deictics();
		vars.scriptname = scriptLabel;
		EnsureSafehouse(vars, playerInfo);
		if (!vars.Validate(nPCScript.steps).Item1)
		{
			return false;
		}
		foreach (ScriptStep step in nPCScript.steps)
		{
			Command command = StepToCommand(step, vars, pid, peep.Id);
			if (command != null)
			{
				playerInfo.commands.AddCommand(command);
			}
		}
		return true;
	}

	private static void EnsureSafehouse(Deictics vars, PlayerInfo player)
	{
		if (!vars.mySafehouse.IsValid)
		{
			if (player.territory.Safehouse.IsValid)
			{
				vars.mySafehouse = player.territory.Safehouse;
			}
			else if (player.territory.Station.IsValid)
			{
				vars.mySafehouse = player.territory.Station;
			}
		}
	}

	private static Command StepToCommand(ScriptStep step, Deictics vars, PlayerID pid, EntityID eid)
	{
		switch (step.type)
		{
		case CommandType.GoTo:
			return MakeGoto(pid, eid, vars, step.argument);
		case CommandType.WaitTurns:
			return MakeWaitTurns(pid, eid, vars, step.argument);
		case CommandType.TeleportTo:
			return MakeTeleportTo(pid, eid, vars, step.argument);
		case CommandType.AutomationAction:
			return MakeAutomationAt(pid, eid, vars, step.argument);
		case CommandType.RemoveFromBoard:
		case CommandType.ReturnToBoard:
			return MakeBoardCallback(pid, eid, vars, step);
		case CommandType.ScopeOut:
			return MakeScopeOut(pid, eid, vars, step.argument);
		case CommandType.Attack:
			return MakeCommandAttack(pid, eid, vars, step.argument);
		case CommandType.Heal:
			return MakeHeal(pid, eid);
		case CommandType.EndTurn:
			return MakeEndTurn(pid, eid);
		case CommandType.EmptyInventory:
			return MakeEmptyInventory(pid, eid);
		case CommandType.BuyItem:
			return MakeBuySell(pid, eid, vars, buy: true);
		case CommandType.SellItem:
			return MakeBuySell(pid, eid, vars, buy: false);
		case CommandType.PickupItem:
			return MakePickUpDropOff(pid, eid, vars, pickup: true);
		case CommandType.DropoffItem:
			return MakePickUpDropOff(pid, eid, vars, pickup: false);
		case CommandType.RefillCash:
			return MakeRefillCash(pid, eid, vars, step);
		case CommandType.WaitAtMeetingPoint:
			return MakeWaitAtMeetingPoint(pid, eid, vars, step);
		case CommandType.InstallBackroom:
		case CommandType.TradeCallback:
		case CommandType.RecordHarassment:
			return MakeBuildingCallback(pid, eid, vars, step);
		case CommandType.AtOutpost:
			return MakeAtOutpost(pid, eid, vars, step);
		case CommandType.ExploreNode:
			return MakeExploreNode(pid, eid, vars, step);
		case CommandType.BurgleBuilding:
			return MakeBurgleBuilding(pid, eid, vars);
		case CommandType.PoliceOrFedVisit:
			return MakePoliceFedVisit(pid, eid, vars);
		case CommandType.PoliceCollectVehicle:
			return MakePoliceCollectVehicle(pid, eid, vars);
		case CommandType.GetPoliceBeat:
			return MakePoliceBeat(pid, eid, vars);
		case CommandType.SocialAction:
			return MakeSocialAction(pid, eid, vars, step.label);
		case CommandType.EnsurePeepHasCar:
			return MakeEnsureCar(pid, eid, vars);
		case CommandType.RemovePeepFromCar:
			return MakeRemoveCar(pid, eid);
		case CommandType.AttackBuilding:
			return MakeCommandAttackBuilding(pid, eid, vars, step);
		case CommandType.GenMessageCallback:
			return MakeMessageCallBack(pid, eid, vars, step.message);
		default:
			return null;
		case CommandType.Sleep:
		case CommandType.Cancel:
			return null;
		}
	}

	private static CommandGoto MakeGoto(PlayerID pid, EntityID eid, Deictics vars, DeicticVariable variable)
	{
		NodeID nodeId = ((variable != DeicticVariable.TargetNode) ? GetEntityNode(vars.GetVariable<EntityID>(variable)) : vars.GetVariable<NodeID>(variable));
		if (nodeId.IsValid)
		{
			return new CommandGoto(pid, eid, nodeId.FindNode());
		}
		return null;
	}

	private static CommandWaitTurns MakeWaitTurns(PlayerID pid, EntityID eid, Deictics vars, DeicticVariable variable)
	{
		Fixnum variable2 = vars.GetVariable<Fixnum>(variable);
		return new CommandWaitTurns(pid, eid, (int)variable2);
	}

	private static CommandTeleportTo MakeTeleportTo(PlayerID pid, EntityID eid, Deictics vars, DeicticVariable variable)
	{
		NodeID nodeId = ((variable != DeicticVariable.TargetNode) ? GetEntityNode(vars.GetVariable<EntityID>(variable)) : vars.GetVariable<NodeID>(variable));
		if (nodeId.IsValid)
		{
			return new CommandTeleportTo(pid, eid, nodeId.FindNode());
		}
		return null;
	}

	private static CommandAutomationStep MakeAutomationAt(PlayerID pid, EntityID eid, Deictics vars, DeicticVariable variable)
	{
		EntityID variable2 = vars.GetVariable<EntityID>(variable);
		return new CommandAutomationStep(pid, eid, variable2);
	}

	private static CommandScopeOut MakeScopeOut(PlayerID pid, EntityID eid, Deictics vars, DeicticVariable variable)
	{
		Node node = vars.GetVariable<NodeID>(variable).FindNode();
		return new CommandScopeOut(pid, eid, node);
	}

	private static CommandAttack MakeCommandAttack(PlayerID pid, EntityID eid, Deictics vars, DeicticVariable variable)
	{
		EntityID variable2 = vars.GetVariable<EntityID>(variable);
		return new CommandAttack(pid, eid, variable2);
	}

	private static CommandHeal MakeHeal(PlayerID pid, EntityID eid)
	{
		return new CommandHeal(pid, eid);
	}

	private static AICommandEndTurn MakeEndTurn(PlayerID pid, EntityID eid)
	{
		return new AICommandEndTurn(pid, eid);
	}

	private static AbstractAICommandBuySellItem MakeBuySell(PlayerID pid, EntityID eid, Deictics vars, bool buy)
	{
		EntityID variable = vars.GetVariable<EntityID>(DeicticVariable.TargetBuilding);
		ResourceAndQty variable2 = vars.GetVariable<ResourceAndQty>(DeicticVariable.TargetItem);
		if (!buy)
		{
			return new AICommandSellItem(pid, eid, variable2.id, variable);
		}
		return new AICommandBuyItem(pid, eid, variable2.id, variable);
	}

	private static AbstractAICommandPickDropItem MakePickUpDropOff(PlayerID pid, EntityID eid, Deictics vars, bool pickup)
	{
		EntityID variable = vars.GetVariable<EntityID>(DeicticVariable.MySafehouse);
		ResourceAndQty variable2 = vars.GetVariable<ResourceAndQty>(DeicticVariable.TargetItem);
		if (!pickup)
		{
			return new AICommandDropOffItem(pid, eid, variable2.id, variable);
		}
		return new AICommandPickUpItem(pid, eid, variable2.id, variable);
	}

	private static AICommandWaitAtMeetingPoint MakeWaitAtMeetingPoint(PlayerID pid, EntityID eid, Deictics vars, ScriptStep step)
	{
		return new AICommandWaitAtMeetingPoint(pid, eid, vars.GetVariable<NodeID>(step.argument));
	}

	private static AICommandEmptyInventory MakeEmptyInventory(PlayerID pid, EntityID eid)
	{
		return new AICommandEmptyInventory(pid, eid);
	}

	private static AICommandAtOutpost MakeAtOutpost(PlayerID pid, EntityID eid, Deictics vars, ScriptStep step)
	{
		return new AICommandAtOutpost(pid, eid, vars.GetTargetBuilding(), step.message);
	}

	private static AICommandExploreNode MakeExploreNode(PlayerID pid, EntityID eid, Deictics vars, ScriptStep step)
	{
		return new AICommandExploreNode(pid, eid, vars.GetVariable<NodeID>(step.argument));
	}

	private static AICommandRefillCash MakeRefillCash(PlayerID pid, EntityID eid, Deictics vars, ScriptStep step)
	{
		return new AICommandRefillCash(pid, eid, vars.GetVariable<EntityID>(step.argument));
	}

	private static AICommandBuildingCallback MakeBuildingCallback(PlayerID pid, EntityID eid, Deictics vars, ScriptStep step)
	{
		return new AICommandBuildingCallback(step.type, pid, eid, vars.GetVariable<EntityID>(step.argument), step.message);
	}

	private static AICommandBurgleBuilding MakeBurgleBuilding(PlayerID pid, EntityID eid, Deictics vars)
	{
		return new AICommandBurgleBuilding(pid, eid, vars.GetTargetBuilding());
	}

	private static AICommandPoliceFedVisit MakePoliceFedVisit(PlayerID pid, EntityID eid, Deictics vars)
	{
		return new AICommandPoliceFedVisit(pid, eid, vars.GetVariable<NodeID>(DeicticVariable.TargetNode), (int)vars.GetVariable<Fixnum>(DeicticVariable.TargetNumber));
	}

	private static AICommandPoliceCollectVehicle MakePoliceCollectVehicle(PlayerID pid, EntityID eid, Deictics vars)
	{
		return new AICommandPoliceCollectVehicle(pid, eid, vars.GetTargetNode(), vars.GetTargetVehicle());
	}

	private static AICommandGetPoliceBeat MakePoliceBeat(PlayerID pid, EntityID eid, Deictics vars)
	{
		return new AICommandGetPoliceBeat(pid, eid, vars);
	}

	private static AICommandSocialAction MakeSocialAction(PlayerID pid, EntityID eid, Deictics vars, Label label)
	{
		return new AICommandSocialAction(pid, eid, vars.GetTargetPeep(), label);
	}

	private static AICommandEnsureCar MakeEnsureCar(PlayerID pid, EntityID eid, Deictics vars)
	{
		return new AICommandEnsureCar(pid, eid, vars.GetMySafehouse());
	}

	private static AICommandRemoveCar MakeRemoveCar(PlayerID pid, EntityID eid)
	{
		return new AICommandRemoveCar(pid, eid);
	}

	private static AICommandGenMessageCallBack MakeMessageCallBack(PlayerID pid, EntityID eid, Deictics vars, string message)
	{
		return new AICommandGenMessageCallBack(pid, eid, vars.GetTargetBuilding(), message);
	}

	private static CommandBoardCallback MakeBoardCallback(PlayerID pid, EntityID eid, Deictics vars, ScriptStep step)
	{
		return new CommandBoardCallback(step.type, pid, eid, step.message);
	}

	private static AICommandAttackBuilding MakeCommandAttackBuilding(PlayerID pid, EntityID eid, Deictics vars, ScriptStep step)
	{
		EntityID variable = vars.GetVariable<EntityID>(step.argument);
		PlayerID targetPlayer = vars.GetTargetPlayer();
		return new AICommandAttackBuilding(pid, eid, variable, targetPlayer, step.message);
	}

	private static NodeID GetEntityNode(EntityID eid)
	{
		Entity entity = eid.FindEntity();
		if (entity == null)
		{
			return NodeID.INVALID;
		}
		if (entity.components.board != null)
		{
			return entity.components.board.GetNodeID();
		}
		if (entity.components.agent != null)
		{
			return entity.components.agent.NodeID;
		}
		return BuildingUtil.FindBuildingForBizOwner(eid)?.components.board.GetNodeID() ?? NodeID.INVALID;
	}
}
