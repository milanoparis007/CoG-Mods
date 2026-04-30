using System.Diagnostics;
using Game.Core;
using Game.Session.Data;
using SomaSim.Util;

namespace Game.Session.Sim.Modules;

public sealed class ConsumerModuleConfig : ModuleConfig<ConsumerModule, ConsumerModuleConfig, ConsumerModuleData>
{
	public ConsumerRecipe sink;

	public bool interesting;

	public string locintroflavor;

	[Conditional("UNITY_EDITOR")]
	public void DebugValidateResources()
	{
	}

	[Conditional("UNITY_EDITOR")]
	public void DebugValidateRecipe(ConsumerRecipe r)
	{
		foreach (RefillElement item in r.AllRefill)
		{
			_ = item;
		}
		foreach (ResourceAndQty item2 in r.AllConsume)
		{
			_ = item2;
		}
	}

	[Conditional("UNITY_EDITOR")]
	private void DebugValidateExists(Label id, Fixnum qty, bool producing)
	{
		Game.serv.globals.settings.resources.FindResource(id);
		if (producing && qty < 0)
		{
			Logger.Warning($"Produced quantity should be not-negative for {id} = {qty}");
		}
		if (!producing && qty > 0)
		{
			Logger.Warning($"Consumed quantity should be not-positive for {id} = {qty}");
		}
	}
}
