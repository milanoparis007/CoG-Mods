using Game.Core;
using Game.Session.Data;
using SomaSim.Util;

namespace Game.Session.Sim.Modules;

public static class IBizModuleExtensions
{
	public static void TryDeliveryOrFakeIt(this IBizModule _1, ModuleQuery _2, InventoryModule inventory, Label id, Fixnum signedQty, bool canFakeRefill)
	{
		Fixnum toBuilding = QtyAndDir.FromSignedQuantity(signedQty).ToBuilding;
		if (toBuilding.IsNotZero && canFakeRefill)
		{
			inventory.data.Increment(id, toBuilding);
		}
	}
}
