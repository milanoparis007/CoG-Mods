using System.Collections.Generic;
using System.Linq;
using System.Text;
using Game.Core;
using Game.Services;
using Game.Services.Audio;
using Game.Session;
using Game.Session.Assets;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Player;
using Game.Session.Sim.Modules;
using Game.UI.Mouseovers;
using Game.UI.Util;
using SomaSim.Util;
using UnityEngine;

namespace Game.UI.Session.OwnedBiz;

internal sealed class ViewInventory : SubviewEntry
{
	internal enum Style
	{
		BuildingPanelDefault,
		BuildingPanelLoading,
		VehiclePanelLoading
	}

	internal class InvCardContext : MonoBehaviour
	{
		public ResOrCash item;

		public InventoryModule inv;

		public Style style;

		public bool isVehicleJunk;

		public bool IsBldgDefaultList => style == Style.BuildingPanelDefault;

		public bool IsBldgLoadingList => style == Style.BuildingPanelLoading;

		public bool IsVehicleLoadingList => style == Style.VehiclePanelLoading;

		internal InvCardContext Set(ResOrCash item, InventoryModule inv, Style style, bool isVehicleJunk)
		{
			this.item = item;
			this.inv = inv;
			this.style = style;
			this.isVehicleJunk = isVehicleJunk;
			return this;
		}

		internal void Reset()
		{
			Set(default(ResOrCash), null, Style.BuildingPanelDefault, isVehicleJunk: false);
		}

		public void UpdateCard(OwnedBizController ctrl)
		{
			GameObject gameObject = base.gameObject;
			string text;
			string text2;
			string text3;
			if (item.IsCash)
			{
				text = Loc.FormatNumber(item.money.cash.PosFloorNegCeiling());
				text2 = Loc.Get("ui.viewinventory.invcard.cash.icon");
				text3 = Loc.Get("ui.viewinventory.invcard.cash.desc");
			}
			else
			{
				Resource resource = item.raq.FindResource();
				string text4 = resource.GetName();
				string bareUnitNoun = resource.unitdef.GetBareUnitNoun(item.raq.qty);
				text = Loc.FormatNumber(item.raq.qty);
				text2 = resource.GetIcon();
				text3 = Loc.Get("ui.viewinventory.invcard.res.desc", "resname", text4, "unitname", bareUnitNoun);
			}
			gameObject.SetText("Button/Count", text);
			gameObject.SetText("Button/Icon", text2);
			gameObject.SetText("Button/Description", text3);
			gameObject.SetActive("Button/Arrow Right", IsBldgLoadingList && !isVehicleJunk);
			gameObject.SetActive("Button/Arrow Left", IsVehicleLoadingList);
			bool value = !IsVehicleLoadingList && item.IsResource && !Game.ctx.tutorial.ShowingTutorial;
			gameObject.SetActive("Button/Destroy", value);
			gameObject.SetButtonListener("Button/Destroy", delegate
			{
				ctrl.ShowDestroyPopup(item);
			});
			SFXType type = (IsVehicleLoadingList ? SFXType.ClickButtonDown : SFXType.ClickButtonUp);
			PlayUISound.UpdateEffectOn(gameObject, "", type);
			gameObject.GetButton("Button").interactable = IsBldgDefaultList || ctrl.CanLoadAtLeastOne(item, IsVehicleLoadingList, isVehicleJunk);
		}
	}

	private const string LEFT_PANE_ICON = "Header/Image Left";

	private const string LEFT_PANE_HEADER = "Header/Text Left";

	private const string RIGHT_PANE_HEADER = "Header/Text Right";

	private const string RIGHT_PANE_ICON = "Header/Image Right";

	private const string ITEM_DESCRIBE_PANE = "Description View";

	private const string ITEM_DESCRIBE_TEXT = "Description View/Viewport/Content/Text";

	private const string INV_BUILDING_LIST = "Contents View/Viewport/Content";

	private const string INV_VEHICLE_PANE = "Vehicle View";

	private const string INV_VEHICLE_LIST = "Vehicle View/Viewport/Content";

	private const string LOAD_BUTTON = "Load Button";

	private const string LOAD_BUTTON_TEXT = "Load Button/Text";

	private const string INVCARD_BUTTON = "Button";

	private const string INVCARD_COUNT = "Button/Count";

	private const string INVCARD_ICON = "Button/Icon";

	private const string INVCARD_DESC = "Button/Description";

	private const string INVCARD_RARROW = "Button/Arrow Right";

	private const string INVCARD_LARROW = "Button/Arrow Left";

	private const string INVCARD_DESTROY = "Button/Destroy";

	public ViewInventory(ViewType type, GameObject go)
		: base(type, go)
	{
	}

	public override void OnActivated()
	{
		go.GetButton("Load Button").onClick.SetListener(delegate
		{
			OnLoadButtonClick();
		});
		bool loading = Model.visit.vehicle != null;
		Controller.SetInventoryLoadingMode(loading, shutdown: false);
		base.OnActivated();
	}

	public override void OnDeactivated()
	{
		Controller.SetInventoryLoadingMode(loading: false, shutdown: false);
		base.OnDeactivated();
	}

	private void OnLoadButtonClick()
	{
		Controller.SetInventoryLoadingMode(!Model.invstate.IsLoading, shutdown: false);
	}

	public override void RefreshSubview()
	{
		Game.ctx.events.EnqueueOnce(SessionEventType.UIOwnedBizViewInventory, PlayerID.HumanPlayer);
		go.SetImage("Header/Image Left", ModulesUIUtil.FindModuleIcon(Model.currentSlot.common));
		RefreshAllPanels();
	}

	internal void RefreshAllPanels()
	{
		string text = ModulesUIUtil.FindModuleName(Model.currentSlot.common);
		InventoryModule obj = Model.currentSlot.module as InventoryModule;
		string text2 = Loc.Volume(obj.config.capacity);
		string text3 = Loc.Percentage(obj.GetUsedCapacityAsPercent());
		go.SetText("Header/Text Left", Loc.Get("ui.viewinventory.h2", "name", text, "capacity", text2, "percent", text3));
		RefreshBuildingPanel();
		RefreshRightHandPanels();
	}

	private void RefreshBuildingPanel()
	{
		InventoryModule inv = Model.currentSlot.module as InventoryModule;
		GameObject child = go.GetChild("Contents View/Viewport/Content");
		Style style = (Model.invstate.IsLoading ? Style.BuildingPanelLoading : Style.BuildingPanelDefault);
		RegeneratePanelCards(style, inv, child);
	}

	private void RefreshVehiclePanel()
	{
		if (!Model.invstate.IsNotLoading)
		{
			InventoryModule vehicle = Model.invstate.vehicle;
			GameObject child = go.GetChild("Vehicle View/Viewport/Content");
			RegeneratePanelCards(Style.VehiclePanelLoading, vehicle, child);
		}
	}

	private void RefreshDescribePanel(string desc)
	{
		go.SetText("Description View/Viewport/Content/Text", desc);
	}

	private void RegeneratePanelCards(Style style, InventoryModule inv, GameObject container)
	{
		List<ResOrCash> list = (from r in inv.data.contents
			where r.qty.IsNotZero
			select new ResOrCash(r)).ToList();
		list.StableSort(ResOrCash.CompareByName);
		if (inv.data.money.IsNotZero)
		{
			list.Insert(0, new ResOrCash(inv.data.money));
		}
		bool isJunk = Model.visit.vehicle?.components.mobile.IsJunk() ?? false;
		GameObject tmpl = Dialog.GetTmpl("Templates/Inventory Card");
		container.EnsureChildCount(list, tmpl);
		container.InitializeChildren(list, delegate(int i, GameObject card, ResOrCash item)
		{
			InitializeCard(card, item, inv, style, isJunk);
		});
	}

	private void InitializeCard(GameObject card, ResOrCash item, InventoryModule inv, Style style, bool isJunk)
	{
		InvCardContext ctx = card.GetOrAddComponent<InvCardContext>().Set(item, inv, style, isJunk);
		ctx.UpdateCard(Controller);
		card.GetButton("Button").onClick.SetListener(delegate
		{
			OnCardClick(ctx);
		});
	}

	private void OnCardClick(InvCardContext ctx)
	{
		Game.serv.mouseovers.OnMouseOut(MouseoverType.InventoryCard);
		if (ctx.IsBldgDefaultList)
		{
			DescribeItem(ctx.item);
			return;
		}
		Controller.DoPerformLoading(ctx.IsVehicleLoadingList, ctx.item);
		RefreshAllPanels();
	}

	private void DescribeItem(ResOrCash item)
	{
		string desc = (item.IsCash ? DescribeCash(item.money) : DescribeRaq(item.raq));
		RefreshDescribePanel(desc);
	}

	private string DescribeCash(Money money)
	{
		string text = Loc.Money(money);
		string text2 = Loc.Get("ui.viewinventory.cash.h1", "cash", text);
		string text3 = (money.IsNotZero ? Loc.Get("ui.viewinventory.cash.nonzero", "cash", text) : Loc.Get("ui.viewinventory.cash.zero"));
		return Loc.Get("ui.viewinventory.cash.desc", "header", text2, "body", text3);
	}

	private string DescribeRaq(ResourceAndQty raq)
	{
		StringBuilder sb = StringBuilderPool.AllocateInstance();
		Resource resource = raq.FindResource();
		string iconAndName = resource.GetIconAndName();
		sb.AppendLine(Loc.Get("ui.viewinventory.raq.h1.1", "header", iconAndName));
		if (resource.GetIsIllegal())
		{
			sb.AppendLine(Loc.Get("ui.viewinventory.raq.h1.2"));
		}
		sb.AppendLine();
		sb.AppendLine(Loc.Get("ui.viewinventory.raq.h1.3", "res", resource.unitdef.GetQtyAndUnits(raq.qty)));
		sb.AppendLine();
		ModulesUtil.GetBizModules(Model.visit.building).ForEach(delegate(IBizModule mod)
		{
			MaybeDescribeModuleRaq(mod, raq, sb);
		});
		return sb.ToStringAndReturnToPool();
	}

	private void MaybeDescribeModuleRaq(IBizModule mod, ResourceAndQty raq, StringBuilder sb)
	{
		foreach (MfgItem item in mod.ProduceAllItemsInCurrentRecipe())
		{
			if (item.id == raq.id)
			{
				string text = ModulesUIUtil.FindModuleName(mod.ModuleConfig.Common);
				string text2 = ((item.consumed && mod is ConsumerModule) ? Loc.Get("ui.viewinventory.modraq.consumed") : (item.consumed ? Loc.Get("ui.viewinventory.modraq.needed") : Loc.Get("ui.viewinventory.modraq.produced")));
				sb.AppendLine(Loc.Get("ui.viewinventory.modraq.format", "producedOrConsumed", text2, "name", text));
				break;
			}
		}
	}

	internal void RefreshRightHandPanels()
	{
		bool flag = Model.visit.vehicle != null;
		bool isLoading = Model.invstate.IsLoading;
		go.SetActive("Vehicle View", isLoading);
		go.SetActive("Description View", !isLoading);
		go.SetText("Load Button/Text", (!flag) ? TextUtil.ColorDisabled(Loc.Get("ui.viewinventory.vehicle.none")) : (isLoading ? Loc.Get("ui.viewinventory.vehicle.loading") : Loc.Get("ui.viewinventory.vehicle.notloading")));
		go.GetButton("Load Button").interactable = flag;
		RefreshPaneHeader();
		if (isLoading)
		{
			RefreshVehiclePanel();
		}
		else
		{
			RefreshDescribePanel(Loc.Get("ui.viewinventory.item.h1"));
		}
		void RefreshPaneHeader()
		{
			if (isLoading)
			{
				CrewAssignment crew = Model.visit.crew;
				Entity vehicle = crew.GetVehicle();
				string text = vehicle?.config.mobile.locname;
				string text2 = ((text != null) ? Loc.Get(text) : null);
				string peepFullName = NameUtils.GetPeepFullName(crew.GetPeep());
				string text3 = Loc.Volume(ModulesUtil.GetInventory(vehicle).config.capacity);
				string inventoryFullPercent = ModulesUtil.GetInventoryFullPercent(vehicle);
				string text4 = Loc.Get("ui.viewinventory.vehicle.h1", "vehname", text2, "capacity", text3, "percent", inventoryFullPercent, "peepname", peepFullName);
				Sprite vehicleSprite = HUDUtil.GetVehicleSprite(crew.GetVehicle());
				go.SetText("Header/Text Right", text4);
				go.SetImageOrHide("Header/Image Right", vehicleSprite);
			}
			else
			{
				string text5 = ModulesUIUtil.FindModuleDesc(Model.currentSlot.common);
				go.SetText("Header/Text Right", text5);
				go.SetImageOrHide("Header/Image Right", null);
			}
		}
	}

	internal void UpdateAllInventoryCards()
	{
		UpdateCards(go.GetChild("Contents View/Viewport/Content"));
		if (Model.invstate.IsLoading)
		{
			UpdateCards(go.GetChild("Vehicle View/Viewport/Content"));
		}
		void UpdateCards(GameObject container)
		{
			container.WalkChildren(delegate(Transform tr)
			{
				InvCardContext component = tr.GetComponent<InvCardContext>();
				if (component != null)
				{
					component.UpdateCard(Controller);
				}
			}, recursive: false);
		}
	}
}
