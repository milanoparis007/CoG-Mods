using Game.Core;
using Game.Services;
using Game.Session.Data;
using Game.UI.Mouseovers;
using SomaSim.Util;
using UnityEngine.UI;

namespace Game.UI.Session.OwnedBiz;

public class InventoryCardMouseover : BaseCustomTextMouseover
{
	public override UIMouseoverName MouseoverAssetName => UIMouseoverName.TextMouseoverBoundedTL;

	protected override string ProduceText()
	{
		ViewInventory.InvCardContext componentInParent = context.GetComponentInParent<ViewInventory.InvCardContext>();
		Button childButton = context.GetChildButton();
		ResOrCash item = componentInParent.item;
		bool flag = childButton != null && childButton.interactable;
		string text = "?";
		if (item.IsCash || (item.IsResource && item.raq.FindResource().rescat != new Label("rescat-weapon")))
		{
			text = (item.IsCash ? Loc.Money(item.money.cash) : (item.raq.FindResource().GetIconNameAndUnits(item.raq.qty) + "\n\n" + item.raq.FindResource().GetDesc()));
		}
		else if (item.raq.FindResource().rescat == new Label("rescat-weapon"))
		{
			text = Game.ctx.simman.combat.FindWeaponConfig(item.raq.FindResource()).DescribeGeneric();
		}
		text += "\n\n";
		if (componentInParent != null && componentInParent.IsBldgDefaultList)
		{
			return text + Loc.Get("ui.viewinventory.invcard.mo.default");
		}
		if (!flag)
		{
			if (!componentInParent.isVehicleJunk)
			{
				return Loc.Get("ui.viewinventory.invcard.mo.cantfit");
			}
			return Loc.Get("ui.viewinventory.invcard.mo.junk");
		}
		return text + Loc.Get("ui.inventory.move.mo");
	}
}
