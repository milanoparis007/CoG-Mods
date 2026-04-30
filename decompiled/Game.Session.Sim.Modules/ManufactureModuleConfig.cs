using System.Collections.Generic;
using System.Diagnostics;
using Game.Core;
using Game.Session.Data;
using SomaSim.Util;

namespace Game.Session.Sim.Modules;

public sealed class ManufactureModuleConfig : ModuleConfig<ManufactureModule, ManufactureModuleConfig, ManufactureModuleData>
{
	public List<Recipe> recipes = new List<Recipe>();

	public bool interesting;

	public string locintroflavor;

	[Conditional("UNITY_EDITOR")]
	public void DebugValidateResources()
	{
		recipes.ForEach(delegate
		{
		});
	}

	[Conditional("UNITY_EDITOR")]
	public void DebugValidateRecipe(Recipe r)
	{
		foreach (RefillElement item in r.refill)
		{
			_ = item;
		}
		foreach (ResourceAndQty item2 in r.consume)
		{
			_ = item2;
		}
		foreach (ResourceAndQty item3 in r.produce)
		{
			_ = item3;
		}
		foreach (SellOffElement item4 in r.selloff)
		{
			_ = item4;
		}
	}

	[Conditional("UNITY_EDITOR")]
	private void DebugValidateExists(Label id, Fixnum qty, bool producing)
	{
		Game.ctx.simman.FindResource(id);
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
