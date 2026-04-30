using System.Collections.Generic;
using Game.Core;
using Game.Session.Data;
using SomaSim.Util;

namespace Game.Session.Player.AI;

public class Deictics
{
	public Fixnum number;

	public EntityID mySafehouse;

	public EntityID targetBuilding;

	public EntityID targetPeep;

	public EntityID targetVehicle;

	public PlayerID targetPlayer;

	public NodeID targetNode;

	public ResourceAndQty targetItem;

	public Label scriptname;

	private static HashSet<DeicticVariable> _args = new HashSet<DeicticVariable>();

	public T GetVariable<T>(DeicticVariable variable)
	{
		switch (variable)
		{
		case DeicticVariable.MySafehouse:
		{
			EntityID entityID = mySafehouse;
			if (entityID is T)
			{
				object obj5 = entityID;
				return (T)((obj5 is T) ? obj5 : null);
			}
			return default(T);
		}
		case DeicticVariable.TargetBuilding:
		{
			EntityID entityID = targetBuilding;
			if (entityID is T)
			{
				object obj2 = entityID;
				return (T)((obj2 is T) ? obj2 : null);
			}
			return default(T);
		}
		case DeicticVariable.TargetPeep:
		{
			EntityID entityID = targetPeep;
			if (entityID is T)
			{
				object obj6 = entityID;
				return (T)((obj6 is T) ? obj6 : null);
			}
			return default(T);
		}
		case DeicticVariable.TargetVehicle:
		{
			EntityID entityID = targetVehicle;
			if (entityID is T)
			{
				object obj8 = entityID;
				return (T)((obj8 is T) ? obj8 : null);
			}
			return default(T);
		}
		case DeicticVariable.TargetNode:
		{
			NodeID nodeID = targetNode;
			if (nodeID is T)
			{
				object obj3 = nodeID;
				return (T)((obj3 is T) ? obj3 : null);
			}
			return default(T);
		}
		case DeicticVariable.TargetPlayer:
		{
			PlayerID playerID = targetPlayer;
			if (playerID is T)
			{
				object obj7 = playerID;
				return (T)((obj7 is T) ? obj7 : null);
			}
			return default(T);
		}
		case DeicticVariable.TargetItem:
		{
			ResourceAndQty resourceAndQty = targetItem;
			if (resourceAndQty is T)
			{
				object obj4 = resourceAndQty;
				return (T)((obj4 is T) ? obj4 : null);
			}
			return default(T);
		}
		case DeicticVariable.TargetNumber:
		{
			Fixnum fixnum = number;
			if (fixnum is T)
			{
				object obj = fixnum;
				return (T)((obj is T) ? obj : null);
			}
			return default(T);
		}
		case DeicticVariable.None:
			return default(T);
		default:
			return default(T);
		}
	}

	public Fixnum GetNumber()
	{
		return GetVariable<Fixnum>(DeicticVariable.TargetNumber);
	}

	public EntityID GetMySafehouse()
	{
		return GetVariable<EntityID>(DeicticVariable.MySafehouse);
	}

	public EntityID GetTargetBuilding()
	{
		return GetVariable<EntityID>(DeicticVariable.TargetBuilding);
	}

	public EntityID GetTargetPeep()
	{
		return GetVariable<EntityID>(DeicticVariable.TargetPeep);
	}

	public EntityID GetTargetVehicle()
	{
		return GetVariable<EntityID>(DeicticVariable.TargetVehicle);
	}

	public ResourceAndQty GetTargetItem()
	{
		return GetVariable<ResourceAndQty>(DeicticVariable.TargetItem);
	}

	public NodeID GetTargetNode()
	{
		return GetVariable<NodeID>(DeicticVariable.TargetNode);
	}

	public PlayerID GetTargetPlayer()
	{
		return GetVariable<PlayerID>(DeicticVariable.TargetPlayer);
	}

	public (bool, string) Validate(IEnumerable<ScriptStep> steps)
	{
		_args.Clear();
		foreach (ScriptStep step in steps)
		{
			if (step.argument != DeicticVariable.None)
			{
				_args.Add(step.argument);
			}
		}
		foreach (DeicticVariable arg in _args)
		{
			object variable = GetVariable<object>(arg);
			if (variable != null)
			{
				if (!(variable is EntityID entityID))
				{
					if (variable is ResourceAndQty { IsSet: false })
					{
						return (false, $"item not set: {arg}");
					}
				}
				else if (entityID.IsNotValid)
				{
					return (false, $"entity not valid: {arg}");
				}
				continue;
			}
			return (false, $"got null result: {arg}");
		}
		return (true, "");
	}
}
