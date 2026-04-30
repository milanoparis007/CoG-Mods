using System.Text;
using Game.Services;
using Game.Session.Data;
using Game.Session.Sim.Modules;
using SomaSim.Util;

namespace Game.UI.Session.Convo;

public sealed class ConvoDataSafehouseCheck : ConvoData
{
	public override string[] MakeReplacements(VisitState visit, int index)
	{
		StringBuilder stringBuilder = StringBuilderPool.AllocateInstance();
		InventoryModule inventory = ModulesUtil.GetInventory(visit.npc.components.agent.GetPlayer().territory.Safehouse);
		stringBuilder.AppendLine(Loc.Money(inventory.data.money.cash));
		foreach (ResourceAndQty content in inventory.data.contents)
		{
			stringBuilder.AppendLine(content.MakeQuantityLocString());
		}
		return new string[2]
		{
			"safehouseInfo",
			(inventory == null) ? Loc.Get("module.desc.nothing") : stringBuilder.ToStringAndReturnToPool()
		};
	}
}
