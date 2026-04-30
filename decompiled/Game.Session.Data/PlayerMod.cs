using Game.Core;
using Game.Services;
using SomaSim.Util;

namespace Game.Session.Data;

public sealed class PlayerMod : BaseDeltaMultiplierModifier
{
	public short id;

	public override ModQueryElement QueryMustProvide => ModQueryElement.Player;

	public override bool DoesPass(ModQuery query)
	{
		return new PlayerID(id) == query.pid;
	}

	public override string Explain(ModQuery query, Fixnum delta)
	{
		return Loc.Get("mod.player", "delta", AbstractModifier.FormatDelta(delta));
	}
}
