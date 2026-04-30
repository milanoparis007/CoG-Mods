using SomaSim.Util;

namespace Game.Session.Data;

public sealed class TerritorySizeMod : BaseModifier
{
	public Fixnum pernode = 0;

	public Fixnum skip = 0;

	public override ModQueryElement QueryMustProvide => ModQueryElement.Player;

	public override string Lockey => "mod.player-territory-size";

	public override Fixnum Evaluate(ModQuery query, Fixnum source)
	{
		int num = query.FindPlayer().territory.OwnedNodeCount;
		if (skip > 0)
		{
			num = MathUtil.ClampMin(num - (int)skip, 0);
		}
		return source + num * pernode;
	}
}
