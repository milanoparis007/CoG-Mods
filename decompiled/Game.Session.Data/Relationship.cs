using System.Diagnostics;
using System.Text;
using Game.Core;
using Game.Services;
using Game.Session.Entities;
using SomaSim.Util;
using UnityEngine;

namespace Game.Session.Data;

[DebuggerDisplay("{DebugString}")]
public sealed class Relationship
{
	public struct Value
	{
		public Fixnum current;

		public Fixnum high;

		public Value(Fixnum current, Fixnum high)
		{
			this.current = current;
			this.high = high;
		}
	}

	public RelationshipType type;

	public EntityID from;

	public EntityID to;

	public BuffStack buffs;

	public RelMilestone milestone;

	public RelTradeStats trades;

	public RelFlagList flags;

	public Fixnum high;

	public SocialHistoryData socialhistory;

	public int convos;

	private int _lastUpdateFrame;

	public bool IsSelf => type == RelationshipType.Self;

	public bool IsAnyFamily
	{
		get
		{
			if ((int)type > 1)
			{
				return (int)type <= 90;
			}
			return false;
		}
	}

	public bool IsCloseFamily
	{
		get
		{
			if ((int)type > 1)
			{
				return (int)type <= 50;
			}
			return false;
		}
	}

	public bool IsExtendedFamily
	{
		get
		{
			if ((int)type > 50)
			{
				return (int)type <= 90;
			}
			return false;
		}
	}

	public bool IsFirstConvo => convos <= 1;

	public bool IsNotFirstConvo => convos > 1;

	public string DebugString => ToString();

	public Relationship()
	{
	}

	public Relationship(RelationshipType type, EntityID from, EntityID to)
	{
		this.type = type;
		this.from = from;
		this.to = to;
	}

	public ModQuery MakeModQuery()
	{
		return MakeModQuery(EntityID.INVALID);
	}

	public ModQuery MakeModQuery(EntityID crewPeep)
	{
		AgentData obj = to.FindEntity()?.data.agent;
		PlayerID pid = obj?.pid ?? PlayerID.INVALID;
		NodeID nodeId = obj?.nid ?? NodeID.INVALID;
		return new ModQuery(pid, from, crewPeep, nodeId);
	}

	public Value Evaluate()
	{
		return new Value(EvaluateCurrent(), high);
	}

	private Fixnum EvaluateCurrent()
	{
		if (buffs == null || buffs.IsEmpty)
		{
			return Fixnum.ZERO;
		}
		int num = milestone?.avail ?? 0;
		Fixnum fixnum = buffs.EvaluateDelta(MakeModQuery());
		UpdateTickets(fixnum);
		UpdateHighWatermark(fixnum);
		int num2 = milestone?.avail ?? 0;
		if (num != num2)
		{
			Game.ctx.events.EnqueueOnce(SessionEventType.SomeEntityTicketsChanged);
		}
		return fixnum;
	}

	public void Explain(StringBuilder sb)
	{
		if (buffs != null && !buffs.IsEmpty)
		{
			buffs.Explain(sb, MakeModQuery());
		}
	}

	public bool AddBuffNoCrew(Label buffId)
	{
		return AddBuff(buffId, EntityID.INVALID);
	}

	public bool AddBuff(Label buffId, EntityID crewpeep)
	{
		if (buffs == null)
		{
			buffs = new BuffStack();
		}
		bool num = buffs.Add(buffId, MakeModQuery(crewpeep), crewpeep);
		if (!num)
		{
			Game.ctx.events.EnqueueOnce(SessionEventType.SomeEntityRelationshipChanged);
		}
		return num;
	}

	public bool HasBuff(Label buffId)
	{
		if (buffs != null)
		{
			return buffs.Contains(buffId);
		}
		return false;
	}

	public bool RemoveBuff(Label buffId)
	{
		int num;
		if (buffs != null)
		{
			num = (buffs.Remove(buffId) ? 1 : 0);
			if (num != 0)
			{
				Game.ctx.events.EnqueueOnce(SessionEventType.SomeEntityRelationshipChanged);
			}
		}
		else
		{
			num = 0;
		}
		return (byte)num != 0;
	}

	public void RemoveAllBuffs()
	{
		if (buffs != null && buffs.RemoveAll())
		{
			Game.ctx.events.EnqueueOnce(SessionEventType.SomeEntityRelationshipChanged);
		}
	}

	public bool HasSocialHistory()
	{
		if (socialhistory == null)
		{
			return false;
		}
		socialhistory.ExpireOldHistory();
		return socialhistory.items.Count > 0;
	}

	public bool IsRelToAIWithSocialHistory()
	{
		AgentData agentData = to.FindEntity()?.data.agent;
		if (agentData == null)
		{
			return false;
		}
		if (agentData.pid.IsHumanPlayer)
		{
			return false;
		}
		if (!HasSocialHistory())
		{
			return false;
		}
		return true;
	}

	public SocialHistoryData GetOrCreateHistory()
	{
		if (socialhistory == null)
		{
			socialhistory = new SocialHistoryData();
			socialhistory.Initialize(this);
		}
		return socialhistory;
	}

	public SocialHistoryData GetHistoryOrNull()
	{
		return socialhistory;
	}

	public bool HistoryContains(SocialValence v)
	{
		return IndexOf(null, v) >= 0;
	}

	public bool HistoryContains(SocialCategory c)
	{
		return IndexOf(c) >= 0;
	}

	public bool HistoryContains(Label socialAction)
	{
		return socialhistory?.ContainsAction(socialAction) ?? false;
	}

	private int IndexOf(SocialCategory? category = null, SocialValence? valence = null)
	{
		return socialhistory?.IndexOf(category, valence) ?? (-1);
	}

	public SocialActionInfo? GetRandomHistoryOrNull(Entity owner)
	{
		Xorshift rng = Game.ctx.scenario.MakeSeededRng((uint)(owner.Id.index + Game.ctx.clock.Now.days));
		return socialhistory?.GetRandomHistoryOrNull(rng);
	}

	private void RefreshTickets()
	{
		EvaluateCurrent();
	}

	private void UpdateHighWatermark(Fixnum current)
	{
		if (current > high)
		{
			high = current;
		}
	}

	public RelMilestone GetOrCreateMilestone()
	{
		return milestone ?? (milestone = new RelMilestone());
	}

	public int GetTicketsAvailable(bool forceRecalc = true)
	{
		if (forceRecalc)
		{
			RefreshTickets();
		}
		return milestone?.avail ?? 0;
	}

	public int GetTicketsSpent(bool forceRecalc = true)
	{
		if (forceRecalc)
		{
			RefreshTickets();
		}
		return milestone?.spent ?? 0;
	}

	public int GetTicketsGrantedTotal(bool forceRecalc = true)
	{
		if (forceRecalc)
		{
			RefreshTickets();
		}
		return milestone?.GrantedTotal ?? 0;
	}

	public bool DoSpendTickets(int delta = 1)
	{
		if (delta <= 0)
		{
			return false;
		}
		if (milestone == null || delta > milestone.avail)
		{
			return false;
		}
		milestone.avail -= delta;
		milestone.spent += delta;
		return true;
	}

	internal void GrantFreebieTickets(int delta)
	{
		RelMilestone orCreateMilestone = GetOrCreateMilestone();
		orCreateMilestone.avail += delta;
		orCreateMilestone.spent -= delta;
		if (orCreateMilestone.spent < 0)
		{
			orCreateMilestone.spent = 0;
		}
	}

	private void UpdateTickets(Fixnum currentRelationship)
	{
		if (!currentRelationship.IsZero && ShouldUpdateTickets())
		{
			Fixnum fixnum = CalculateMilestonePeriod();
			int num = (int)(currentRelationship / fixnum).Floor();
			int num2 = milestone?.GrantedTotal ?? 0;
			int num3 = num - num2;
			if (num3 > 0)
			{
				GetOrCreateMilestone().avail += num3;
			}
		}
	}

	public Fixnum CalculateRelPointsForMilestone(Fixnum mstone)
	{
		return CalculateMilestonePeriod() * mstone;
	}

	private Fixnum CalculateMilestonePeriod()
	{
		return Game.serv.globals.settings.people.social.relationships.milestones.Evaluate(MakeModQuery());
	}

	public int GetAffiliateThreshold()
	{
		return Game.serv.globals.settings.people.social.relationships.affiliateLevel.Evaluate(MakeModQuery()).IntCeiling();
	}

	public bool IsAffiliate()
	{
		int ticketsGrantedTotal = GetTicketsGrantedTotal(forceRecalc: false);
		if (ticketsGrantedTotal == 0)
		{
			return false;
		}
		int affiliateThreshold = GetAffiliateThreshold();
		return ticketsGrantedTotal >= affiliateThreshold;
	}

	private bool ShouldUpdateTickets()
	{
		int frameCount = Time.frameCount;
		if (frameCount != _lastUpdateFrame)
		{
			_lastUpdateFrame = frameCount;
			return true;
		}
		return false;
	}

	private RelTradeStats GetOrCreateStats()
	{
		return trades ?? (trades = new RelTradeStats());
	}

	public void AddTradeStat(Entity crewPeep, Resource res, bool recurring)
	{
		ModQuery query = MakeModQuery();
		query.crewPeepId = crewPeep.Id;
		query.nodeId = crewPeep.data.agent.nid;
		RelationshipTradeEffects tradeEffects = Game.serv.globals.settings.people.social.relationships.tradeEffects;
		Fixnum defaultDays = tradeEffects.rememberForDayz.Evaluate(query);
		Fixnum basePoints = tradeEffects.pointsPerTradeExtra.Evaluate(query);
		(Fixnum points, Fixnum days) tuple = res.FindRelBuff(basePoints, defaultDays);
		Fixnum item = tuple.points;
		Fixnum item2 = tuple.days;
		SimTime now = Game.ctx.clock.Now;
		SimTime expires = now.IncrementDays((int)item2);
		int maxCount = (int)tradeEffects.maxEntries.Evaluate(query);
		if (GetOrCreateStats().AddIfNew(now, item, expires, recurring, res.GetIsIllegal(), maxCount))
		{
			Game.ctx.events.EnqueueOnce(SessionEventType.SomeEntityRelationshipChanged);
		}
	}

	public Fixnum SumTradeEffects(SimTime now, bool recurring, bool illegal)
	{
		return trades?.Evaluate(now, recurring, illegal) ?? Fixnum.ZERO;
	}

	public Fixnum CountTradeEffects(SimTime now, bool recurring, bool illegal)
	{
		return ((Fixnum?)trades?.Count(now, recurring, illegal)) ?? Fixnum.ZERO;
	}

	public SimTime? GetTradeExpiration(bool recurring)
	{
		if (trades == null || trades.IsEmpty)
		{
			return null;
		}
		return trades.GetMostRecentTradeExpiration(recurring);
	}

	public int IncrementConvoCount()
	{
		return convos++;
	}

	public void AddFlag(Label flag, SimTime? expiration)
	{
		SimTime now = Game.ctx.clock.Now;
		if (flags == null)
		{
			flags = new RelFlagList();
		}
		flags.Add(new RelFlag(flag, expiration), now);
	}

	public void RemoveFlag(Label flag)
	{
		if (flags != null)
		{
			SimTime now = Game.ctx.clock.Now;
			flags.Remove(flag, now);
			if (flags.IsEmpty(now))
			{
				flags = null;
			}
		}
	}

	public bool ContainsFlag(Label flag)
	{
		return flags?.Contains(flag, Game.ctx.clock.Now) ?? false;
	}

	public bool TestFlag(Label flag, bool expected)
	{
		return ContainsFlag(flag) == expected;
	}

	public override string ToString()
	{
		return $"[{type}: {from}=>{to} ({buffs?.states?.Count} buffs)]";
	}
}
