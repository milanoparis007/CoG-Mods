using Game.Core;
using SomaSim.Util;

namespace Game.Session.Data;

public sealed class AddPlayerCounterMod : BaseModifier
{
	public Fixnum delta = 0;

	public Fixnum multiplier = 1;

	public Label id;

	public string lockey;

	public override ModQueryElement QueryMustProvide => ModQueryElement.Player;

	public override string Lockey => lockey;

	public override Fixnum Evaluate(ModQuery query, Fixnum source)
	{
		int valueOrDefault = (query.FindPlayer()?.skills.GetCounterOrNull(id)).GetValueOrDefault();
		return source + (valueOrDefault + delta) * multiplier;
	}
}
