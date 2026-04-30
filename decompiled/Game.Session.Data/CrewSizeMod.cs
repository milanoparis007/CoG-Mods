using SomaSim.Util;

namespace Game.Session.Data;

public sealed class CrewSizeMod : BaseModifier
{
	public Fixnum percrew = 0;

	public Fixnum skip = 0;

	public override ModQueryElement QueryMustProvide => ModQueryElement.Player;

	public override string Lockey => "mod.player-crew-size";

	public override Fixnum Evaluate(ModQuery query, Fixnum source)
	{
		int num = query.FindPlayer().crew.LivingCrewCount;
		if (skip > 0)
		{
			num = MathUtil.ClampMin(num - (int)skip, 0);
		}
		return source + num * percrew;
	}
}
