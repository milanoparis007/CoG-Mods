using Game.Core;
using SomaSim.Util;

namespace Game.Session.Data;

public abstract class TagListMod : AbstractModifier
{
	public TagList @if;

	public Fixnum delta = 0;

	public Fixnum multiplier = 1;

	public override Fixnum Evaluate(ModQuery query, Fixnum source)
	{
		return DoEvaluate(query, source);
	}

	protected abstract Fixnum DoEvaluate(ModQuery query, Fixnum source);

	protected abstract void Validate(ModQuery query);
}
