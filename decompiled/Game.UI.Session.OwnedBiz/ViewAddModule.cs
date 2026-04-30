using Game.Core;
using Game.Services;
using Game.Session;
using Game.Session.Sim.Modules;
using Game.UI.Util;
using SomaSim.Util;
using UnityEngine;

namespace Game.UI.Session.OwnedBiz;

internal sealed class ViewAddModule : SubviewEntry
{
	private const string HEADER = "Header";

	private const string BG_SPRITE = "BG/Sprite";

	private const string ADD_MODULE_BTN = "Add Button";

	private const string ADD_STORAGE_BTN = "Add Storage";

	public static readonly Label INVENTORY_UPGRADE_MODULE = (Label)"inventory-basement-improved";

	public static readonly Label INVENTORY_EXPANSION_BACKROOM = (Label)"explanation-inventory-improved";

	public ViewAddModule(ViewType type, GameObject go)
		: base(type, go)
	{
	}

	public override void Initialize(OwnedBizController controller)
	{
		base.Initialize(controller);
		string moduleHeader = ViewDescribeModule.GetModuleHeader(isFrontRoom: false);
		go.SetText("Header", moduleHeader + "\n\n" + Loc.Get("ui.viewaddmodule.available"));
		go.SetImageOrHide("BG/Sprite", null);
		go.GetButton("Add Button").onClick.SetListener(OnAddModuleClick);
		go.GetChild("Add Button").SetChildText(Loc.Get("ui.viewaddmodule.button.add"));
		go.GetChild("Add Storage").SetChildText(Loc.Get("ui.viewaddmodule.button.addstorage"));
	}

	public void OnAddModuleClick()
	{
		Controller.OnModuleAddButtonClick();
	}

	public override void RefreshSubview()
	{
		Sprite sprite = ModulesUIUtil.FindBusinessEmptySprite(Model.visit.building);
		go.SetImageOrHide("BG/Sprite", sprite);
		RefreshStorageUpgrade();
		Game.ctx.events.EnqueueOnce(SessionEventType.UIOwnedBizAddModule, PlayerID.HumanPlayer);
	}

	private void RefreshStorageUpgrade()
	{
		InventoryModuleConfig replacement = ModulesUtil.FindModuleDef(INVENTORY_UPGRADE_MODULE) as InventoryModuleConfig;
		ExplanationModuleConfig backroom = ModulesUtil.FindModuleDef(INVENTORY_EXPANSION_BACKROOM) as ExplanationModuleConfig;
		InventoryModule inventory = Model.visit.building.components.modules.inventory;
		bool flag = inventory != null && !inventory.config.isManualExpansion && replacement != null && Game.ctx.players.Human.territory.CountControlledBuildings() > 1;
		string text = TextUtil.ColorEnabledIf(flag, Loc.Get("ui.viewaddmodule.button.addstorage"));
		go.GetChild("Add Storage").SetChildText(text);
		go.GetButton("Add Storage").interactable = flag;
		go.GetButton("Add Storage").onClick.SetListener(delegate
		{
			Controller.OnInventoryReplacementClick(replacement, backroom);
		});
	}
}
