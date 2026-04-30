using SomaSim.Util;

namespace Game.Session.Data;

public sealed class IsNodeInTerritory : BaseModifier
{
	public bool expected = true;

	public Fixnum multiplier = 1;

	public Fixnum delta = 0;

	public override ModQueryElement QueryMustProvide => ModQueryElement.Node | ModQueryElement.Player;

	public override string Lockey => "mod.is-node-in-territory";

	public override Fixnum Evaluate(ModQuery query, Fixnum source)
	{
		if (query.FindPlayer().territory.IsOwnerOfNode(query.FindNode()) != expected)
		{
			return source;
		}
		return (source + delta) * multiplier;
	}
}
