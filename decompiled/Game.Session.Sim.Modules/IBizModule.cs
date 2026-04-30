using System.Collections.Generic;
using Game.Core;
using Game.Session.Data;
using SomaSim.Util;

namespace Game.Session.Sim.Modules;

public interface IBizModule : IModule
{
	Label BizModuleID { get; }

	bool IsInteresting { get; }

	BizModuleLocData LocData { get; }

	IEnumerable<MfgItem> ProduceAllItemsInCurrentRecipe();

	Fixnum ProduceBuyCap(Label resId, ModuleQuery q);

	DeliveryInfo ProduceDeliveryInfo(Label resId, ModuleQuery q);
}
