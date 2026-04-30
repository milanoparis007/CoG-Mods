using SomaSim.Util;

namespace Game.Session.Data;

public sealed class RecentExpansionMod : BaseModifier
{
	public int maxdays;

	public int perday;

	public override ModQueryElement QueryMustProvide => ModQueryElement.Player;

	public override string Lockey => "mod.recent-expansion-mod";

	public override Fixnum Evaluate(ModQuery query, Fixnum source)
	{
		int daysSinceLastExpansion = query.FindPlayer().territory.GetDaysSinceLastExpansion(query.time);
		int num = MathUtil.ClampMin(maxdays - daysSinceLastExpansion, 0);
		return source + num * perday;
	}
}
