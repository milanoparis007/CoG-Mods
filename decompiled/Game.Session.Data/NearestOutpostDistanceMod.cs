using Game.Core;
using Game.Session.Entities;
using SomaSim.Util;

namespace Game.Session.Data;

public sealed class NearestOutpostDistanceMod : BaseModifier
{
	public Fixnum perunit = 1;

	public override ModQueryElement QueryMustProvide => ModQueryElement.Player | ModQueryElement.Target;

	public override string Lockey => "mod.nearest-outpost";

	public override Fixnum Evaluate(ModQuery query, Fixnum source)
	{
		var (flag, fixnum) = FindDistance(query);
		if (!flag)
		{
			return source;
		}
		return source + fixnum * perunit;
	}

	private (bool hasOutposts, Fixnum distance) FindDistance(ModQuery query)
	{
		Node node = BuildingUtil.FindBuildingForBizOwner(query.FindTarget())?.components.board?.GetNode();
		if (node == null)
		{
			Logger.Error($"Failed to find building and node for owner {query.targetId.FindEntity()} in outpost mod");
			return (hasOutposts: false, distance: 0);
		}
		var (outpostID, num) = query.FindPlayer().outposts.FindClosestOutpostToNode(node);
		return (hasOutposts: outpostID.IsValid, distance: (Fixnum)num);
	}
}
