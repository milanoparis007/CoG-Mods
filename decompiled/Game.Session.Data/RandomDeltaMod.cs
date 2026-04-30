using System;
using SomaSim.Util;

namespace Game.Session.Data;

public sealed class RandomDeltaMod : BaseModifier
{
	public Fixnum from;

	public Fixnum to;

	public override ModQueryElement QueryMustProvide => ModQueryElement.Player;

	public override string Lockey
	{
		get
		{
			throw new NotImplementedException();
		}
	}

	public override Fixnum Evaluate(ModQuery query, Fixnum source)
	{
		if (from > to)
		{
			Logger.Warning($"Malformed random delta mod, from = {from}, to = {to}");
			return source;
		}
		float num = Game.ctx.scenario.MakeSeededRng((uint)(query.pid.id + query.time.days)).Generate((float)from, (float)to);
		return source + (Fixnum)num;
	}
}
