using Game.Core;
using Game.Services;
using Game.Session.Entities;
using SomaSim.Util;

namespace Game.Session.Data;

public abstract class LevelTestMod : BaseDeltaMultiplierModifier
{
	public Test @is;

	public Fixnum value = 0;

	public Label id;

	protected abstract string LocKey { get; }

	public override bool DoesPass(ModQuery query)
	{
		return ValueUtil.TestCurrentValue(GetLevelup(query).value, @is, value);
	}

	protected abstract Entity FindAgentToTest(ModQuery query);

	private (LevelupChain chain, int value) GetLevelup(ModQuery query)
	{
		int? num = FindAgentToTest(query)?.components.agent?.GetLevel(id);
		return (chain: num.HasValue ? Game.serv.globals.settings.skills.experience.GetLevelup(id) : null, value: num.GetValueOrDefault());
	}

	public override string Explain(ModQuery query, Fixnum delta)
	{
		(LevelupChain chain, int value) levelup = GetLevelup(query);
		LevelupChain item = levelup.chain;
		int item2 = levelup.value;
		string text = item?.Describe(item2) ?? Loc.Get("mod.unspecified");
		return Loc.Get(LocKey, "delta", AbstractModifier.FormatDelta(delta), "details", text);
	}
}
