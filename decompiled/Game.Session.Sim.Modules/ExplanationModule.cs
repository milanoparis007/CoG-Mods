using Game.Core;

namespace Game.Session.Sim.Modules;

public sealed class ExplanationModule : Module<ExplanationModule, ExplanationModuleConfig, ExplanationModuleData>
{
	public override ModuleResult DoUpdate(ModuleQuery q, SimTime time, bool initial, bool enabled)
	{
		return ModuleResult.Default;
	}

	public override bool IsEnabled(SimTime time)
	{
		return true;
	}
}
