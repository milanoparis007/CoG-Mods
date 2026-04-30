using System.Collections.Generic;
using Game.Core;
using Game.Session.Entities;
using Game.Session.Sim.Modules;
using SomaSim.Util;

namespace Game.Session.Data;

public sealed class VehicleModulesCapacityMod : BaseModifier
{
	public AvatarType type;

	public override ModQueryElement QueryMustProvide => ModQueryElement.Player;

	public override string Lockey => "mod.vehicle-modules-capacity-mod";

	public override Fixnum Evaluate(ModQuery query, Fixnum source)
	{
		Fixnum fixnum = SumDeltas(type, query);
		return source + fixnum;
	}

	private static Fixnum SumDeltas(AvatarType type, ModQuery query)
	{
		Fixnum result = 0;
		List<EntityID> allControlledBuildingsUnsafe = query.FindPlayer().territory.GetAllControlledBuildingsUnsafe();
		for (int i = 0; i < allControlledBuildingsUnsafe.Count; i++)
		{
			VehicleModule vehicleModule = allControlledBuildingsUnsafe[i].FindEntity().components.modules.FindVehicleModuleOrNull();
			if (vehicleModule != null && vehicleModule.IsEnabled(query.time))
			{
				result += GetModValue(vehicleModule.config, type)?.Evaluate(query) ?? ((Fixnum)0);
			}
		}
		return result;
	}

	private static ModValue GetModValue(VehicleModuleConfig config, AvatarType type)
	{
		if (config == null || config.playerInfo == null)
		{
			return null;
		}
		return type switch
		{
			AvatarType.Car => config.playerInfo.carCapDelta, 
			AvatarType.Truck => config.playerInfo.truckCapDelta, 
			_ => null, 
		};
	}
}
