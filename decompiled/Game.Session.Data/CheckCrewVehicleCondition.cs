using Game.Services;
using Game.Session.Entities;

namespace Game.Session.Data;

public sealed class CheckCrewVehicleCondition : AbstractVisitRequirement
{
	public Test @is;

	public VehicleHealthType type;

	public override bool DoesPass(VisitState visit)
	{
		Entity vehicle = visit.vehicle;
		bool flag = @is == Test.EqualTo;
		if (vehicle == null)
		{
			return !flag;
		}
		return vehicle.components.mobile.FindHealthInfo()?.category == type == flag;
	}

	public override ReqExplanation Explain(VisitState visit)
	{
		string key = ((@is == Test.EqualTo) ? "ui.requirements.checkcrew.vehcond.is" : "ui.requirements.checkcrew.vehcond.isnot");
		string text = visit.vehicle.components.mobile.MakeConditionString(shortinfo: false);
		string message = Loc.Get(key, "condition", text);
		return new ReqExplanation(DoesPass(visit), message);
	}
}
