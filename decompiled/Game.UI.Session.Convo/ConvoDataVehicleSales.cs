using Game.Services;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Sim.Modules;

namespace Game.UI.Session.Convo;

public class ConvoDataVehicleSales : ConvoData
{
	public bool foundVehicle;

	public bool canAfford;

	public VehicleModule.VehicleForSale info;

	public override string[] MakeReplacements(VisitState visit, int index)
	{
		return new string[6]
		{
			"vname",
			info.displayName,
			"vdesc",
			info.displayDesc,
			"price",
			Loc.Price(info.salePrice, abs: true)
		};
	}

	public ConvoDataVehicleSales()
	{
	}

	public ConvoDataVehicleSales(bool foundVehicle, bool canAfford, VehicleModule.VehicleForSale info)
	{
		this.foundVehicle = foundVehicle;
		this.canAfford = canAfford;
		this.info = info;
	}

	public EntityConfig FindVehicleConfig()
	{
		return Game.ctx.entityman.FindTemplate(info.template);
	}
}
