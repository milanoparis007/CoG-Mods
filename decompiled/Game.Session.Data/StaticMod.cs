using SomaSim.Util;

namespace Game.Session.Data;

public sealed class StaticMod : BaseDeltaMultiplierModifier
{
	public override ModQueryElement QueryMustProvide => ModQueryElement.Nothing;

	public override bool DoesPass(ModQuery query)
	{
		return true;
	}

	public override string Explain(ModQuery query, Fixnum delta)
	{
		return null;
	}
}
