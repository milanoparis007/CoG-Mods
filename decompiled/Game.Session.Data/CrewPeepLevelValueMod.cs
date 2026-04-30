using Game.Core;
using Game.Services;
using SomaSim.Util;

namespace Game.Session.Data;

public sealed class CrewPeepLevelValueMod : BaseModifier
{
	public Label id;

	public Fixnum perlevel = 0;

	public override ModQueryElement QueryMustProvide => ModQueryElement.CrewPeep;

	public override string Lockey => "mod.crew-experience";

	public override Fixnum Evaluate(ModQuery query, Fixnum source)
	{
		Fixnum fixnum = Fixnum.Max(GetLevelup(query).value * perlevel, 0);
		return source + fixnum;
	}

	private (LevelupChain chain, int value) GetLevelup(ModQuery query)
	{
		int? num = query.FindCrewPeep()?.components.agent?.GetLevel(id);
		return (chain: num.HasValue ? Game.serv.globals.settings.skills.experience.GetLevelup(id) : null, value: num.GetValueOrDefault());
	}

	public override string Explain(ModQuery query, Fixnum delta)
	{
		(LevelupChain chain, int value) levelup = GetLevelup(query);
		LevelupChain item = levelup.chain;
		int item2 = levelup.value;
		string text = item?.Describe(item2) ?? Loc.Get("mod.unspecified");
		return Loc.Get(Lockey, "delta", AbstractModifier.FormatDelta(delta), "details", text);
	}
}
