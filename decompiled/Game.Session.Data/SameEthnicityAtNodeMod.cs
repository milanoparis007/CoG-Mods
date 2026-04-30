using Game.Core;
using SomaSim.Util;

namespace Game.Session.Data;

public sealed class SameEthnicityAtNodeMod : BaseModifier
{
	public Fixnum multiplier = 1;

	public override ModQueryElement QueryMustProvide => ModQueryElement.Node | ModQueryElement.Player;

	public override string Lockey => "mod.same-ethnicity-at-node";

	public override Fixnum Evaluate(ModQuery query, Fixnum source)
	{
		Node node = query.FindNode();
		if (node == null)
		{
			Logger.Warning("Missing peep location", this, query);
			return source;
		}
		Label playerEthnicity = query.FindPlayer().social.PlayerEthnicity;
		if (playerEthnicity.IsNotSet)
		{
			Logger.Warning("Missing player ethnicity", query.pid, this);
			return source;
		}
		float valueSafe = Game.ctx.heatmaps.FindEthnicityMap(playerEthnicity).GetValueSafe(node.pos);
		return source + (Fixnum)valueSafe * multiplier;
	}
}
