using System;
using Game.Services;
using Game.Session.Player;
using SomaSim.Util;

namespace Game.Session.Data;

public sealed class NumCasinosInPrecinctMod : BaseModifier
{
	public Fixnum multiplier;

	public override ModQueryElement QueryMustProvide => ModQueryElement.Target;

	public override string Lockey
	{
		get
		{
			throw new NotImplementedException();
		}
	}

	public override Fixnum Evaluate(ModQuery query, Fixnum source)
	{
		PlayerInfo precinct = query.FindTarget().data.agent.pid.FindPlayer();
		int num = Game.ctx.players.Human.gambling.CountAllMyGamblingHousesInPrecinct(precinct);
		Fixnum fixnum = new Fixnum((float)Math.Pow((float)multiplier, num));
		return source * fixnum;
	}

	public override string Explain(ModQuery query, Fixnum delta)
	{
		return Loc.Get("mod.num-casinos-in-precinct", "delta", delta);
	}
}
