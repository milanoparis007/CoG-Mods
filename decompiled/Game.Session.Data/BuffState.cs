using Game.Core;
using Game.Services;
using Game.UI.Util;
using SomaSim.Util;

namespace Game.Session.Data;

public sealed class BuffState
{
	public Label id;

	public SimTime expires;

	public int priority;

	public EntityID crewpeep;

	private BuffConfig _cachedConfig;

	internal BuffConfig CachedConfig => _cachedConfig ?? (_cachedConfig = BuffStack.GetSettings().GetConfig(id));

	public static BuffState Make(BuffConfig config, ModQuery query, EntityID crewpeep)
	{
		return new BuffState
		{
			id = config.id,
			priority = config.priority + (config.delta?.value.IntFloor() ?? 0),
			expires = config.GenerateExpirationDate(query),
			crewpeep = crewpeep
		};
	}

	public bool IsExpired(ModQuery query)
	{
		if (CachedConfig == null)
		{
			return true;
		}
		return CachedConfig.GetExpiration(this, query).IsExpired;
	}

	private ModQuery AugmentQueryWithCrewPeep(ModQuery q)
	{
		if (!crewpeep.IsValid)
		{
			return q;
		}
		return q.SetCrew(crewpeep);
	}

	public Fixnum EvaluateDelta(ModQuery query)
	{
		return ComputeDelta(AugmentQueryWithCrewPeep(query), explain: false);
	}

	public string Explain(ModQuery query)
	{
		return ComputeExplanation(AugmentQueryWithCrewPeep(query));
	}

	private Fixnum ComputeDelta(ModQuery query, bool explain)
	{
		return CachedConfig?.EvaluateDelta(query, explain) ?? ((Fixnum)0);
	}

	private string ComputeExplanation(ModQuery query)
	{
		Fixnum fixnum = ComputeDelta(query, explain: true);
		if (CachedConfig == null)
		{
			return null;
		}
		if (!CachedConfig.IsDeferred && fixnum == 0)
		{
			return null;
		}
		bool num = fixnum >= 0;
		BuffConfig.Expiration expiration = CachedConfig.GetExpiration(this, query);
		string text = TextUtil.ColorRedIf(!num, Loc.Get(CachedConfig.locdesc));
		bool hasDaysLeft = expiration.HasDaysLeft;
		int num2 = (hasDaysLeft ? expiration.daysTillExpiration : 0);
		Fixnum fixnum2 = (hasDaysLeft ? Game.ctx.clock.DaysToTurns(num2).Floor() : ((Fixnum)0));
		string text2 = (hasDaysLeft ? Loc.Get("ui.buffs.daysturns", "days", num2, "turns", fixnum2) : "");
		string text3 = "";
		if (CachedConfig.delta != null && CachedConfig.delta.HasMods)
		{
			text3 = CachedConfig.delta.Explain(query, addHeader: false);
			if (text3 != "")
			{
				text3 = "\n" + text3;
			}
		}
		return Loc.Get("ui.buffs.explain", "delta", fixnum, "msg", text, "daystext", text2, "modstext", text3);
	}
}
