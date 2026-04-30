using Game.Services;
using Game.UI.Util;
using SomaSim.Util;

namespace Game.Session.Data;

public abstract class AbstractModifier : IModifier
{
	public abstract ModQueryElement QueryMustProvide { get; }

	public abstract Fixnum Evaluate(ModQuery query, Fixnum source);

	public abstract string Explain(ModQuery query, Fixnum delta);

	public static string FormatDelta(Fixnum delta)
	{
		return TextUtil.ColorGreenRed(delta, Loc.FormatNumberPlusMinus(delta));
	}
}
