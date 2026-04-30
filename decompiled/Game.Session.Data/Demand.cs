using System;
using System.Collections.Generic;
using System.Diagnostics;
using Game.Core;
using Game.Services;
using Game.Session.Board;
using Game.Session.Entities;
using Game.Session.Player;

namespace Game.Session.Data;

[DebuggerDisplay("{DebugString}")]
public sealed class Demand
{
	[Flags]
	public enum Type
	{
		None = 0,
		Protection = 2,
		CloseOutpost = 4,
		AllowBizAccess = 0x100,
		StopAggro = 0x10000,
		StopHarassing = 0x20000,
		Truce = 0x1000000,
		StopStealingOutpost = 0x2000000
	}

	[DebuggerDisplay("{DebugString}")]
	public struct Target : IEquatable<Target>
	{
		public static readonly Target EMPTY;

		public EntityID peep;

		public PlayerID pid;

		public PlayerType ptype;

		public bool IsPlayer => pid.IsAnyPlayer;

		public bool IsOwner => pid.IsNotAnyPlayer;

		private string DebugString => ToString();

		public static Target MakeForPlayer(PlayerID pid)
		{
			PlayerInfo playerInfo = pid.FindPlayer();
			EntityID playerPeepId = playerInfo.social.PlayerPeepId;
			return new Target
			{
				peep = playerPeepId,
				pid = pid,
				ptype = playerInfo.PlayerType
			};
		}

		public static Target MakeForBizOwner(Entity peep)
		{
			return new Target
			{
				peep = peep.Id,
				pid = PlayerID.System,
				ptype = PlayerType.SystemPlayer
			};
		}

		public NodeID FindNodeId()
		{
			if (!IsOwner)
			{
				return peep.FindEntity().data.agent.nid;
			}
			return BuildingUtil.FindBuildingForBizOwner(peep).components.board.GetNodeID();
		}

		public Node FindNode()
		{
			return FindNodeId().FindNode();
		}

		public EntityID FindTargetPeepOrPlayerPeep()
		{
			if (!IsOwner)
			{
				return pid.FindPlayer().social.PlayerPeepId;
			}
			return peep;
		}

		public static bool Equals(Target a, Target b)
		{
			if (a.peep == b.peep && a.pid == b.pid)
			{
				return a.ptype == b.ptype;
			}
			return false;
		}

		public bool Equals(Target other)
		{
			return Equals(this, other);
		}

		public override bool Equals(object obj)
		{
			if (obj is Target b)
			{
				return Equals(this, b);
			}
			return false;
		}

		public override int GetHashCode()
		{
			return peep.GetHashCode();
		}

		public override string ToString()
		{
			return $"[Target {peep}/{pid}/{ptype}]";
		}
	}

	public enum State
	{
		Neutral,
		Compliant,
		Defiant
	}

	public static readonly Type[] ALL_TYPES = Enum.GetValues(typeof(Type)) as Type[];

	public PlayerID source;

	public Target target = Target.EMPTY;

	public Type types;

	public State state;

	public SimTime started = SimTime.MAX_DATE;

	public bool IsTargetPlayer => target.IsPlayer;

	public bool IsTargetHumanPlayer
	{
		get
		{
			if (target.IsPlayer)
			{
				return target.pid.IsHumanPlayer;
			}
			return false;
		}
	}

	public bool IsTargetBizOwner => target.IsOwner;

	public bool HasAnyDemandSet => types != Type.None;

	public bool HasNoDemandsSet => types == Type.None;

	public bool IsStateCompliant => state == State.Compliant;

	public bool IsStateDefiant => state == State.Defiant;

	public bool IsStateNeutral => state == State.Neutral;

	private string DebugString => ToString();

	public Demand()
	{
	}

	public Demand(PlayerID source, Target target)
	{
		this.source = source;
		this.target = target;
	}

	public void AddDemand(Type t)
	{
		types |= t;
	}

	public void RemoveDemand(Type t)
	{
		types &= ~t;
	}

	public bool HasDemandSet(Type t)
	{
		if (t != Type.None)
		{
			return (types & t) == t;
		}
		return false;
	}

	public void ResetDemandsTo(Type t)
	{
		types = t;
	}

	public IEnumerable<Type> EnumerateSetTypes()
	{
		Type[] aLL_TYPES = ALL_TYPES;
		foreach (Type type in aLL_TYPES)
		{
			if (type != Type.None && HasDemandSet(type))
			{
				yield return type;
			}
		}
	}

	public void SetState(State newState, SimTime? time = null)
	{
		state = newState;
		started = time ?? Game.ctx.clock.Now;
	}

	public (Relationship toTarget, Relationship fromTarget) FindRelsSymmetrical()
	{
		EntityID playerPeepId = source.FindPlayer().social.PlayerPeepId;
		EntityID peep = target.peep;
		return Game.ctx.simman.rels.GetOrMakeSymmetrical(playerPeepId, peep, RelationshipType.Acquaintance, warnOnExisting: false);
	}

	public Relationship FindRelFromSourceToTarget()
	{
		return FindRelsSymmetrical().toTarget;
	}

	public Relationship FindRelFromTargetToSource()
	{
		return FindRelsSymmetrical().fromTarget;
	}

	public static DemandDef FindDefinition(Type t)
	{
		return Game.serv.globals.settings.people.social.demands.FindOrNullDemand(t);
	}

	[Conditional("UNITY_EDITOR")]
	internal void DebugValidateTypeAndTargetMatch()
	{
		Type[] aLL_TYPES = ALL_TYPES;
		foreach (Type type in aLL_TYPES)
		{
			if (type != Type.None && HasDemandSet(type))
			{
				FindDefinition(type);
			}
		}
	}

	[Conditional("UNITY_EDITOR")]
	private static void DebugValidateTypeAndTargetMatch(PlayerID source, Target target, Type type, DemandDef def)
	{
		_ = target.pid.FindPlayer()?.PlayerType;
		_ = def.atOwner;
		if (def.atGoon)
		{
			_ = def.atGang;
		}
		if (def.atGoon)
		{
			_ = def.atGang;
		}
		if (def.atGang)
		{
			_ = def.atGoon;
		}
		if (type <= Type.CloseOutpost)
		{
			if (type != Type.Protection)
			{
				_ = 4;
			}
		}
		else if (type != Type.AllowBizAccess && type != Type.StopAggro)
		{
			_ = 131072;
		}
	}

	public override string ToString()
	{
		return $"[Demand ({types}): {source} => {target} @ {state}]";
	}

	public static string GetDemandStateIcon(State state)
	{
		return (state switch
		{
			State.Defiant => TickerIcon.DEMANDS_DEFIANT, 
			State.Compliant => TickerIcon.DEMANDS_COMPLIANT, 
			_ => TickerIcon.DEMANDS_GENERIC, 
		}).GetIcon();
	}

	public static string GetDemandStateText(State state)
	{
		return Loc.GetDemandState(state);
	}
}
