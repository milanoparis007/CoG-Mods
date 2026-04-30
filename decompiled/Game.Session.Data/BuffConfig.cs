using System.Collections.Generic;
using Game.Core;
using Game.Services;
using Game.Session.Board;
using Game.Session.Entities;
using Game.Session.Player;
using SomaSim.Util;

namespace Game.Session.Data;

public sealed class BuffConfig
{
	public struct Expiration
	{
		public bool neverExpires;

		public SimTime expiration;

		public int daysTillExpiration
		{
			get
			{
				if (!neverExpires)
				{
					return (expiration - Game.ctx.clock.Now).deltadays;
				}
				return 0;
			}
		}

		public bool IsExpired
		{
			get
			{
				if (!neverExpires)
				{
					return daysTillExpiration <= 0;
				}
				return false;
			}
		}

		public bool HasDaysLeft
		{
			get
			{
				if (!neverExpires)
				{
					return daysTillExpiration > 0;
				}
				return false;
			}
		}

		public Expiration(SimTime? expiration)
		{
			neverExpires = !expiration.HasValue;
			this.expiration = expiration ?? SimTime.MIN_DATE;
		}
	}

	public Label id;

	public string locdesc;

	public ModValue delta;

	public ModValue dayz;

	public int priority;

	public List<Label> cancels;

	public BuffDeferralType type;

	public bool IsDeferred => type != BuffDeferralType.Normal;

	public string GetDesc()
	{
		return Loc.Get(locdesc);
	}

	internal Expiration GetExpiration(BuffState state, ModQuery query)
	{
		if (!IsDeferred)
		{
			return GetExpirationNormal(state);
		}
		return GetExpirationDeferred(state, query);
	}

	internal Fixnum EvaluateDelta(ModQuery query, bool explain)
	{
		if (!IsDeferred)
		{
			return EvaluateNormalDelta(query, explain);
		}
		return EvaluateDeferredDelta(query, explain);
	}

	private Expiration GetExpirationNormal(BuffState state)
	{
		if (!state.expires.NeverHappens)
		{
			return new Expiration(state.expires);
		}
		return new Expiration(null);
	}

	private Fixnum EvaluateNormalDelta(ModQuery query, bool _)
	{
		if (delta == null)
		{
			Logger.Warning($"Missing delta in buff id {id}");
			return 0;
		}
		return delta.Evaluate(query);
	}

	private Fixnum EvaluateDeferredDelta(ModQuery query, bool explain)
	{
		switch (type)
		{
		case BuffDeferralType.DeferToOutpost:
		{
			bool current = !explain;
			return query.pid.FindPlayer().outposts.FindRespectBuffFromOutposts(query.nodeId, current);
		}
		case BuffDeferralType.DeferToOneTimeTradeStats:
		case BuffDeferralType.DeferToRecurringTradeStats:
		{
			bool recurring = type == BuffDeferralType.DeferToRecurringTradeStats;
			return FindRelationshipFromTarget(query)?.SumTradeEffects(query.time, recurring, illegal: false) ?? Fixnum.ZERO;
		}
		case BuffDeferralType.DeferToOneTimeIllegalStats:
		case BuffDeferralType.DeferToRecurringIllegalStats:
			return CalculateIllegalTradeDelta(query);
		case BuffDeferralType.Normal:
			return Fixnum.ZERO;
		default:
			return Fixnum.ZERO;
		}
	}

	private Fixnum CalculateIllegalTradeDelta(ModQuery query)
	{
		Node node = query.nodeId.FindNode();
		if (node == null)
		{
			return Fixnum.ZERO;
		}
		RelationshipTradeEffects tradeEffects = Game.serv.globals.settings.people.social.relationships.tradeEffects;
		Fixnum max = tradeEffects.maxHeatPerIllegalBiz.Evaluate(query);
		bool recurring = type == BuffDeferralType.DeferToRecurringIllegalStats;
		Fixnum zERO = Fixnum.ZERO;
		using ListPool<Entity>.PooledBlockList pooledBlockList = ListPool<Entity>.Allocate();
		node.FindAllBuildings(pooledBlockList);
		PlayerSocial social = query.pid.FindPlayer().social;
		foreach (Entity item in pooledBlockList)
		{
			if (BuildingUtil.FindBizForBuilding(item) == null)
			{
				continue;
			}
			Entity entity = BuildingUtil.FindOwnerOrManagerForAnyBuilding(item);
			if (entity != null)
			{
				Relationship relationshipFromSourceToPlayer = social.GetRelationshipFromSourceToPlayer(entity.Id);
				if (relationshipFromSourceToPlayer != null)
				{
					Fixnum fixnum = Fixnum.Clamp(relationshipFromSourceToPlayer.CountTradeEffects(query.time, recurring, illegal: true) * tradeEffects.heatPerIllegalTrade.Evaluate(query), 0, max);
					zERO += fixnum;
				}
			}
		}
		return zERO;
	}

	private Expiration GetExpirationDeferred(BuffState state, ModQuery query)
	{
		switch (type)
		{
		case BuffDeferralType.DeferToOneTimeIllegalStats:
		case BuffDeferralType.DeferToRecurringIllegalStats:
		case BuffDeferralType.DeferToOutpost:
			return GetExpirationNormal(state);
		case BuffDeferralType.DeferToOneTimeTradeStats:
		case BuffDeferralType.DeferToRecurringTradeStats:
		{
			bool recurring = type == BuffDeferralType.DeferToRecurringTradeStats;
			return new Expiration(FindRelationshipFromTarget(query)?.GetTradeExpiration(recurring));
		}
		default:
			return default(Expiration);
		}
	}

	internal SimTime GenerateExpirationDate(ModQuery query)
	{
		int num = (int)(dayz?.Evaluate(query) ?? Fixnum.ZERO);
		if (num > 0)
		{
			return query.time.IncrementDays(num);
		}
		return SimTime.MAX_DATE;
	}

	private Relationship FindRelationshipFromTarget(ModQuery query)
	{
		if (query.targetId.IsNotValid)
		{
			Logger.Warning($"Missing target when evaluating buff {id}");
			return null;
		}
		if (query.pid.IsNotValid)
		{
			Logger.Warning($"Missing player id when evaluating buff {id}");
			return null;
		}
		return query.pid.FindPlayer().social.GetRelationshipFromSourceToPlayer(query.targetId);
	}
}
