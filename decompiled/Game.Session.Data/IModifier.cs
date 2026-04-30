using SomaSim.Util;

namespace Game.Session.Data;

public interface IModifier
{
	ModQueryElement QueryMustProvide { get; }

	Fixnum Evaluate(ModQuery query, Fixnum source);

	string Explain(ModQuery query, Fixnum delta);
}
