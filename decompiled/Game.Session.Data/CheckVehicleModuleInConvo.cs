using System.Linq;
using Game.Core;
using Game.Services;
using Game.Session.Sim.Modules;

namespace Game.Session.Data;

public class CheckVehicleModuleInConvo : CheckListOfItems
{
	public static readonly Label REPAIR = (Label)"vehicle-repair";

	public static readonly Label SELLTOPLAYER = (Label)"vehicle-sell-to-player";

	public static readonly Label BUYFROMPLAYER = (Label)"vehicle-buy-from-player";

	public static readonly Label[] ALL_LABELS = new Label[3] { REPAIR, SELLTOPLAYER, BUYFROMPLAYER };

	protected override void VerifyData(VisitState _)
	{
		foreach (Label item in of)
		{
			if (!ALL_LABELS.Contains(item))
			{
				Label label = item;
				Logger.Warning("Check vehicle module in convo: unknown element id: " + label.ToString());
			}
			string text = IdToName(item);
			if (text == "?" || string.IsNullOrWhiteSpace(text))
			{
				Label label = item;
				Logger.Warning("Check vehicle module in convo: lacking locstring for: " + label.ToString());
			}
		}
	}

	protected override int Count(VisitState visit)
	{
		VehicleModule vehicleModule = visit.building?.components.modules?.FindVehicleModuleOrNull();
		if (vehicleModule == null)
		{
			return 0;
		}
		int num = 0;
		if (of != null)
		{
			foreach (Label item in of)
			{
				if (item == REPAIR && vehicleModule.config.repairInfo != null)
				{
					num++;
				}
				if (item == SELLTOPLAYER && vehicleModule.config.sellInfo != null)
				{
					num++;
				}
				if (item == BUYFROMPLAYER && vehicleModule.config.buyBackInfo != null)
				{
					num++;
				}
			}
		}
		return num;
	}

	protected override string MakeExplanationText()
	{
		return MakeExplanationText(Loc.Get("ui.requirements.modules.veh.expected"), Loc.Get("ui.requirements.modules.veh.unexpected"), Loc.Get("ui.requirements.modules.veh.all"), Loc.Get("ui.requirements.modules.veh.any"), Loc.Get("ui.requirements.modules.veh.none"));
	}

	protected override string IdToName(Label id)
	{
		return Loc.Get("ui.requirements.modules.veh." + id.String);
	}
}
