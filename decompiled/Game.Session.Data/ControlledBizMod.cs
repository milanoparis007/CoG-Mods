using SomaSim.Util;

namespace Game.Session.Data;

public sealed class ControlledBizMod : BaseModifier
{
	public Fixnum perbiz = 0;

	public Fixnum skip = 0;

	public override ModQueryElement QueryMustProvide => ModQueryElement.Player;

	public override string Lockey => "mod.player-controlled-biz";

	public override Fixnum Evaluate(ModQuery query, Fixnum source)
	{
		int num = query.FindPlayer().territory.CountControlledBuildings();
		if (skip > 0)
		{
			num = MathUtil.ClampMin(num - (int)skip, 0);
		}
		return source + num * perbiz;
	}
}
