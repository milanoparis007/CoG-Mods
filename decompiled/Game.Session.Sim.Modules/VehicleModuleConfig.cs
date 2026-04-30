using System.Collections.Generic;
using System.Diagnostics;
using Game.Core;
using Game.Session.Data;

namespace Game.Session.Sim.Modules;

public sealed class VehicleModuleConfig : ModuleConfig<VehicleModule, VehicleModuleConfig, VehicleModuleData>
{
	public class VehicleModuleLoc : BizModuleLocData
	{
	}

	public class PlayerInfo
	{
		public ModValue carCapDelta = new ModValue();

		public ModValue truckCapDelta = new ModValue();
	}

	public class RepairInfo
	{
		public ModValue fullHealPrice = new ModValue();
	}

	public class VehicleTypeInfo
	{
		public List<Label> entities = new List<Label>();

		public VisitRequirementList visreqs = new VisitRequirementList();

		public ModifierList pricemods = new ModifierList();
	}

	public class BuyBackInfo
	{
		public List<Label> entities = new List<Label>();

		public ModifierList pricemods = new ModifierList();
	}

	public class SellInfo
	{
		public List<VehicleTypeInfo> vehicles = new List<VehicleTypeInfo>();
	}

	public VehicleModuleLoc vehicleModuleLoc = new VehicleModuleLoc();

	public PlayerInfo playerInfo;

	public RepairInfo repairInfo;

	public SellInfo sellInfo;

	public BuyBackInfo buyBackInfo;

	public bool interesting;

	[Conditional("UNITY_EDITOR")]
	public void DebugValidate()
	{
		_ = playerInfo;
		if (sellInfo == null)
		{
			return;
		}
		foreach (VehicleTypeInfo vehicle in sellInfo.vehicles)
		{
			foreach (Label entity in vehicle.entities)
			{
				Game.ctx.entityman.FindTemplate(entity);
			}
		}
	}
}
