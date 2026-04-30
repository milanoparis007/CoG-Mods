using Game.Services;
using SomaSim.Util;

namespace Game.Session.Data;

public abstract class BaseModifier : AbstractModifier
{
	public abstract string Lockey { get; }

	public override string Explain(ModQuery query, Fixnum delta)
	{
		return Loc.Get(Lockey, "delta", AbstractModifier.FormatDelta(delta));
	}
}
