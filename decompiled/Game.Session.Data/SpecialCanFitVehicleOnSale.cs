using Game.Services;
using Game.Session.Entities;
using Game.Session.Player;
using Game.UI.Session.Convo;

namespace Game.Session.Data;

public class SpecialCanFitVehicleOnSale : IConvoButtonRequirement, IRequirement
{
	private EntityConfig FindVehicleConfig(ConvoButtonState bstate)
	{
		if (!(bstate.data is ConvoDataVehicleSales convoDataVehicleSales))
		{
			return null;
		}
		return convoDataVehicleSales.FindVehicleConfig();
	}

	public bool DoesPass(VisitState visit, ConvoButtonState bstate)
	{
		EntityConfig entityConfig = FindVehicleConfig(bstate);
		if (entityConfig == null)
		{
			return false;
		}
		AvatarType type = entityConfig.mobile.type;
		PlayerCrew crew = visit.GetPlayer().crew;
		int vehicleCap = crew.CrewGrowth.GetVehicleCap(crew, type == AvatarType.Car);
		return crew.CountVehiclesByType(type) < vehicleCap;
	}

	public ReqExplanation Explain(VisitState visit, ConvoButtonState bstate)
	{
		bool flag = FindVehicleConfig(bstate)?.mobile.IsCar ?? true;
		PlayerCrew crew = visit.GetPlayer().crew;
		int vehicleCap = crew.CrewGrowth.GetVehicleCap(crew, flag);
		string message = (flag ? Loc.Get("ui.requirements.vehicle.cars-max", "max", vehicleCap) : Loc.Get("ui.requirements.vehicle.trucks-max", "max", vehicleCap));
		return new ReqExplanation(DoesPass(visit, bstate), message);
	}
}
