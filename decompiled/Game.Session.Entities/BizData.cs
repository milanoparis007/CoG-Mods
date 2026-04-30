using System.Collections.Generic;
using Game.Core;
using Game.Services;
using Game.Session.Data;
using SomaSim.Util;

namespace Game.Session.Entities;

public sealed class BizData : BaseData
{
	public BizOwner owner;

	public EntityID building = 0uL;

	public string bizname;

	public List<Label> modules;

	public TributeData tribute;

	public PlayerTradeSummaryList trades;

	public Discounts discounts;

	public TiedHouseInfo tiedhouse;

	public ForcedClosedInfo remodel;

	internal TributeData GetTributeOrAdd()
	{
		return tribute ?? (tribute = new TributeData());
	}

	internal TributeData GetTributeOrNull()
	{
		return tribute;
	}

	public PlayerTradeSummary GetTradeSummariesOrNull(PlayerID pid)
	{
		return trades?.GetOrNull(pid);
	}

	public PlayerTradeSummary GetTradeSummariesOrAdd(PlayerID pid)
	{
		PlayerTradeSummary tradeSummariesOrNull = GetTradeSummariesOrNull(pid);
		if (tradeSummariesOrNull != null)
		{
			return tradeSummariesOrNull;
		}
		trades = trades ?? new PlayerTradeSummaryList();
		return trades.GetOrMake(pid);
	}

	public SimTimeSpan? FindTimeSinceLastTradeOrNull(PlayerID pid)
	{
		PlayerTradeSummary tradeSummariesOrNull = GetTradeSummariesOrNull(pid);
		if (tradeSummariesOrNull == null)
		{
			return null;
		}
		return Game.ctx.clock.Now - tradeSummariesOrNull.lastUpdate;
	}

	public TiedHouseInfo GetTiedHouseOrAdd()
	{
		return tiedhouse ?? (tiedhouse = new TiedHouseInfo());
	}

	public TiedHouseInfo GetTiedHouseOrNull()
	{
		return tiedhouse;
	}

	private void TryExpireForcedClosed()
	{
		if (remodel != null && remodel.expires <= Game.ctx.clock.Now)
		{
			remodel = null;
		}
	}

	public ForcedClosedInfo GetForcedClosedOrNull()
	{
		TryExpireForcedClosed();
		return remodel;
	}

	public bool IsForcedClosed()
	{
		return GetForcedClosedOrNull() != null;
	}

	public ForcedClosedInfo SetForcedClosed(PlayerID originator, int days)
	{
		SimTime now = Game.ctx.clock.Now;
		SimTime expires = now.IncrementDays(days);
		ForcedClosedInfo obj = new ForcedClosedInfo
		{
			originator = originator,
			started = now,
			expires = expires
		};
		ForcedClosedInfo result = obj;
		remodel = obj;
		return result;
	}

	public void InitializeRealName(BizConfig config)
	{
		if (Game.serv.loc.IsCurrentLanguageNonLatin)
		{
			InitializeFakeName(config, owner.eth);
			return;
		}
		LocReplacementContext ctx = NameUtils.MakeLocContext(owner.id.FindEntity());
		ctx.forcedlang = "en";
		bizname = Loc.Get(config.bizname, ctx);
	}

	public void InitializeFakeName(BizConfig config, Label eth)
	{
		LocReplacementContext ctx = NameUtils.MakeLocContextForBizName(new Xorshift((uint)building.id), eth);
		bizname = Loc.Get(config.bizname, ctx);
	}

	public void ClearName()
	{
		bizname = null;
	}
}
