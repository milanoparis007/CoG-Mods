using SomaSim.Util;

namespace Game.Session.Data;

public abstract class BaseDeltaMultiplierModifier : AbstractModifier
{
	public Fixnum delta = 0;

	public Fixnum multiplier = 1;

	public abstract bool DoesPass(ModQuery query);

	public override Fixnum Evaluate(ModQuery query, Fixnum source)
	{
		if (!DoesPass(query))
		{
			return source;
		}
		return (source + delta) * multiplier;
	}
}
