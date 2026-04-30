using Game.Session.Entities;
using SomaSim.Util;

namespace Game.Session.Data;

public sealed class OutpostClosuresMod : BaseModifier
{
	public int perclosure;

	public override ModQueryElement QueryMustProvide => ModQueryElement.Player | ModQueryElement.Target;

	public override string Lockey => "mod.outpost-closures-mod";

	public override Fixnum Evaluate(ModQuery query, Fixnum source)
	{
		Entity entity = BuildingUtil.FindBuildingForBizOwner(query.FindTarget());
		if (entity == null)
		{
			Logger.Error($"Failed to find building for owner {query.targetId.FindEntity()} in outpost mod");
			return source;
		}
		int outpostClosures = query.FindPlayer().outposts.GetOutpostClosures(entity);
		return source + outpostClosures * perclosure;
	}
}
