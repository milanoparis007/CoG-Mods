using Game.Core;

namespace Game.Session.Player.AI;

public sealed class ConvoInitiative
{
	public enum Topic
	{
		NotSet,
		TiedHouse,
		TruceRequest,
		ExpansionHalt,
		StolenOutpost,
		JointWar
	}

	public Topic topic;

	public PlayerID pid;

	public EntityID targetId;

	public NodeID meetingPoint;

	public SimTime expiration;

	public bool peepSent;

	public bool IsValid
	{
		get
		{
			if (topic != Topic.NotSet)
			{
				return pid.IsAnyPlayer;
			}
			return false;
		}
	}

	public bool IsExpired => Game.ctx.clock.Now > expiration;

	public ConvoInitiative()
	{
		Reset();
	}

	public void Set(Topic topic, PlayerID pid, EntityID targetId, NodeID meetingPoint, int turns)
	{
		this.topic = topic;
		this.pid = pid;
		this.targetId = targetId;
		peepSent = false;
		this.meetingPoint = meetingPoint;
		expiration = Game.ctx.clock.Now.IncrementTurns(turns);
	}

	public void Reset()
	{
		topic = Topic.NotSet;
		pid = PlayerID.INVALID;
		targetId = EntityID.INVALID;
		peepSent = false;
		meetingPoint = NodeID.INVALID;
		expiration = SimTime.MIN_DATE;
	}

	public bool NeedToSendPeep()
	{
		return !peepSent;
	}

	public bool WasPeepSent()
	{
		return peepSent;
	}

	public void MarkPeepAsSent()
	{
		peepSent = true;
	}
}
