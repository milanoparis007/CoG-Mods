using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Services;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Player;
using Game.Session.Sim.Modules;
using Game.UI.Util;
using SomaSim.Util;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Game.UI.Session.OwnedBiz;

public class ModuleDescPanelBuilder
{
	private const string CONTAINER = "Contents";

	private const string TEMPLATES = "Templates";

	private const string TMPL_ROW_ENTRY = "Templates/Row Entry";

	private const string TMPL_TEXT_ENTRY = "Templates/Text Entry";

	private const string TMPL_ARROW_CAPSULE = "Templates/Arrow Capsule";

	private const string TMPL_BAR_CAPSULE = "Templates/Bar Capsule";

	private const string TMPL_BUILD_CAPSULE = "Templates/Build Capsule";

	private const string TMPL_BUTTON_CAPSULE = "Templates/Button Capsule";

	private const string TMPL_SPACER = "Templates/Spacer Capsule";

	private const string TMPL_RES_CAPSULE = "Templates/Res Capsule";

	private const string CAPSULE_BAR = "Bar";

	private const string CAPSULE_ICON = "Icon";

	private const string CAPSULE_QTY = "Qty";

	private const string CAPSULE_BTN_INFOTEXT = "Text";

	private const string CAPSULE_BTN_BTEXT = "Button/Text";

	private const string CAPSULE_BTN_BUTTON = "Button";

	public readonly GameObject panel;

	public readonly GameObject parent;

	public readonly GameObject container;

	public readonly GameObject templates;

	public readonly VisitState visit;

	public readonly ModuleQuery q;

	public ModuleDescPanelBuilder(GameObject template, GameObject parent, VisitState visit)
	{
		this.parent = parent;
		this.visit = visit;
		q = ModulesUtil.MakeModuleQuery(visit.building);
		panel = Object.Instantiate(template, parent.transform);
		container = panel.GetChild("Contents");
		templates = panel.GetChild("Templates");
		templates.SetActive(value: false);
	}

	public void ClearContainer()
	{
		container.transform.DestroyAllChildren();
	}

	public void AddConsumeRow(ConsumerRecipe sink, IModule module, InventoryModuleData inv = null)
	{
		if (sink.AllConsume.Count == 0)
		{
			AddTextEntry(Loc.Get("module.row.consume.quiet"));
			return;
		}
		List<ResourceAndQty> list = sink.AllConsume;
		ConsumerModule con = module as ConsumerModule;
		if (con != null)
		{
			list = list.OrderByDescending((ResourceAndQty item) => con.data.lastConsumed.Get(item.id)).ToList();
		}
		List<List<ResourceAndQty>> list2 = new List<List<ResourceAndQty>>();
		for (int num = 0; num < list.Count; num += 2)
		{
			List<ResourceAndQty> list3 = new List<ResourceAndQty>();
			if (num < list.Count)
			{
				list3.Add(list[num]);
			}
			if (num + 1 < list.Count)
			{
				list3.Add(list[num + 1]);
			}
			if (list3.Count > 0)
			{
				list2.Add(list3);
			}
		}
		foreach (List<ResourceAndQty> item in list2)
		{
			GameObject row = MakeRowEntry();
			foreach (ResourceAndQty item2 in item)
			{
				RecipeMods.Result result = sink.ModConsume(item2, q, module, module != null);
				AddResCapsule(row, result, inv, consumed: true, mfg: false);
				AddArrowCapsule(row);
				AddTextCapsule(row, "$", "", Loc.Get("module.row.consume.var-revenues"));
				RecipeMods.Result result2 = sink.ModConsumeDays(q, module, explain: true);
				Fixnum fixnum = result2.resAndQty.qty.Ceiling();
				int num2 = Game.ctx.clock.DaysToTurnsRoundedUp(fixnum);
				string text = Loc.Get("module.row.consume.text", "consumeDays", fixnum, "consumeTurns", num2);
				string text2 = Loc.Get("module.row.consume.text.mo", "consumeDays", fixnum, "consumeTurns", num2);
				if (!string.IsNullOrEmpty(result2.explanation))
				{
					text2 = text2 + "\n\n" + result2.explanation;
				}
				AddBarCapsule(row, text, text2, 0f);
				AddEmptyCapsule(row);
			}
		}
	}

	public void AddManufactureRow(Recipe recipe, IModule module, InventoryModuleData inv = null, float? p = null)
	{
		if (!recipe.IsConsumer && !recipe.IsProducer)
		{
			AddTextEntry(Loc.Get("module.row.manu.quiet"));
			return;
		}
		GameObject row = MakeRowEntry();
		if (recipe.consume.Count > 0)
		{
			foreach (ResourceAndQty item in recipe.consume)
			{
				RecipeMods.Result result = recipe.ModConsume(item, q, module, module != null);
				AddResCapsule(row, result, inv, consumed: true, mfg: true);
			}
		}
		else
		{
			AddTextCapsule(row, Loc.Get("ui.clock"), "", Loc.Get("module.row.manu.cost.time"));
		}
		AddArrowCapsule(row);
		foreach (ResourceAndQty item2 in recipe.produce)
		{
			RecipeMods.Result result2 = recipe.ModProduce(item2, q, module, module != null);
			AddResCapsule(row, result2, null, consumed: false, mfg: true);
		}
		RecipeMods.Result result3 = recipe.ModProduceConsumeDays(q, module, explain: true);
		Fixnum fixnum = result3.resAndQty.qty.Ceiling();
		int num = Game.ctx.clock.DaysToTurnsRoundedUp(fixnum);
		string text = Loc.Get("module.row.manu.text", "produceConsumeDays", fixnum, "produceConsumeTurns", num);
		string text2 = Loc.Get("module.row.manu.text.mo", "produceConsumeDays", fixnum, "produceConsumeTurns", num);
		if (!string.IsNullOrEmpty(result3.explanation))
		{
			text2 = text2 + "\n\n" + result3.explanation;
		}
		AddBarCapsule(row, text, text2, p.GetValueOrDefault());
	}

	public void AddRequirementsRow(IModuleConfig config)
	{
		string text = config.Common.reqs?.Explain(visit);
		if (!string.IsNullOrEmpty(text))
		{
			text = Loc.Get("module.row.requirements") + text;
			AddTextEntry(text);
		}
	}

	public void AddPurchaseRows(ModulePurchaseCost purchase)
	{
		PlayerInfo playerInfo = visit.pid.FindPlayer();
		Entity building = visit.building;
		AddTextEntry(Loc.Get("module.row.purchase.cost"));
		BusinessSettings.GlobalModuleModifiers globalModuleModifiers = Game.serv.globals.settings.people.businessSettings.globalModuleModifiers;
		Fixnum fixnum = globalModuleModifiers.buildCostModifier.Evaluate(new ModQuery(PlayerID.HumanPlayer));
		GameObject row = MakeRowEntry();
		if (purchase.cashCost.IsNonZero)
		{
			bool flag = playerInfo.finances.CanChangeMoney(building, purchase.cashCost * fixnum);
			AddBuildCapsule(row, purchase.cashCost * fixnum, !flag);
		}
		if (purchase.consume != null && purchase.consume.Count > 0)
		{
			InventoryModule inventory = building.components.modules.inventory;
			foreach (ResourceAndQty item in purchase.consume)
			{
				bool flag2 = inventory.data.WillBeNonNegative(item.id, item.qty * fixnum);
				AddBuildCapsule(row, item, !flag2);
			}
		}
		Fixnum fixnum2 = new Fixnum(purchase.FindInstallDays(visit.pid, building)).Ceiling();
		Fixnum fixnum3 = Game.ctx.clock.DaysToTurnsRoundedUp(fixnum2 * globalModuleModifiers.buildTimeModifier.Evaluate(new ModQuery(PlayerID.HumanPlayer)));
		AddBarCapsule(row, Loc.Get("module.row.purchase.text", "buildDays", fixnum2, "buildTurns", fixnum3), Loc.Get("module.row.purchase.text.mo", "buildDays", fixnum2, "buildTurns", fixnum3), 0f);
		AddTextEntry(Loc.Get("module.row.purchase.ready-location"));
		if (purchase.crewCost.IsNonZero)
		{
			AddTextEntry(FeedbackUtil.Explain(purchase.crewCost));
		}
	}

	public void AddConstructionRow(IModuleConfig config, string text, int daysleft)
	{
		Fixnum fixnum = config.Common.purchase?.FindInstallDonePercentage(visit.pid, visit.building, daysleft) ?? Fixnum.ZERO;
		int num = Game.ctx.clock.DaysToTurnsRoundedUp(daysleft);
		string text2 = Loc.Get("module.row.construction.text", "daysleft", daysleft, "turnsleft", num);
		AddTextEntry(text);
		GameObject row = MakeRowEntry();
		AddArrowCapsule(row);
		AddTextCapsule(row, Loc.Get("ui.clock"), "", text);
		AddBarCapsule(row, text2, text, (float)fixnum, ColorUtil.HexToColor(ColorConstants.TEXT_HEX_CONSTRUCTION));
	}

	public void AddVehicleRepairRow(VehicleModule module)
	{
		if (module == null)
		{
			AddTextEntry(Loc.Get("module.row.vehicle-repair.empty"));
			return;
		}
		VehicleModule.OwnedBizRepairInfo ownedBizRepairInfo = module.MakeRepairInfo(visit);
		GameObject row = MakeRowEntry();
		AddButtonCapsule(row, ownedBizRepairInfo.infoText, ownedBizRepairInfo.buttonText, ownedBizRepairInfo.moText, ownedBizRepairInfo.canAfford && ownedBizRepairInfo.canRepair, delegate
		{
			module.StartRepairVehicleAtHome(visit);
		});
	}

	internal void AddResRow(List<ResourceAndQty> list, bool consumed)
	{
		GameObject row = MakeRowEntry();
		foreach (ResourceAndQty item in list)
		{
			AddResCapsule(row, item, null, consumed, mfg: true);
		}
	}

	public GameObject AddTextEntry(string text)
	{
		GameObject gameObject = Add("Templates/Text Entry", container);
		gameObject.SetChildText(text);
		return gameObject;
	}

	private GameObject MakeRowEntry()
	{
		return Add("Templates/Row Entry", container);
	}

	private GameObject AddTextCapsule(GameObject row, string icon, string text, string mo, bool consumed)
	{
		Color color = (consumed ? ColorConstants.ARROW_BUY : ColorConstants.ARROW_SELL);
		return AddTextCapsule(row, icon, text, mo, ColorUtil.ColorToHex(color));
	}

	private GameObject AddTextCapsule(GameObject row, string icon, string text, string mo, string color = null)
	{
		GameObject gameObject = Add("Templates/Res Capsule", row);
		gameObject.SetText("Icon", icon);
		gameObject.SetText("Qty", TextUtil.ColorIf(color != null, text, color));
		AddMouseover(gameObject, mo);
		return gameObject;
	}

	private GameObject AddResCapsule(GameObject row, RecipeMods.Result result, InventoryModuleData inv, bool consumed, bool mfg)
	{
		return AddResCapsule(row, result.resAndQty, inv, consumed, mfg, result.explanation);
	}

	private GameObject AddResCapsule(GameObject row, ResourceAndQty raq, InventoryModuleData inv, bool consumed, bool mfg, string explanation = null)
	{
		GameObject gameObject = Add("Templates/Res Capsule", row);
		Color color = (consumed ? Color.white : ColorConstants.ARROW_SELL);
		Resource resource = raq.FindResource();
		if (consumed && inv != null)
		{
			ResourceAndQty resourceAndQty = inv.Get(resource.resid);
			if ((mfg && resourceAndQty.qty < raq.qty.Abs) || (!mfg && resourceAndQty.qty == 0))
			{
				color = ColorUtil.HexToColor(ColorConstants.TEXT_HEX_RED);
			}
		}
		Fixnum value = (consumed ? raq.qty.Abs.Ceiling() : raq.qty.Abs.Floor());
		gameObject.SetText("Qty", TextUtil.ColorWrap(Loc.FormatNumber(value), color));
		gameObject.SetText("Icon", resource.GetIcon());
		AddMouseover(gameObject, raq, explanation);
		return gameObject;
	}

	private GameObject AddBuildCapsule(GameObject row, ResourceAndQty raq, bool missing)
	{
		GameObject gameObject = Add("Templates/Build Capsule", row);
		Resource resource = raq.FindResource();
		gameObject.SetText("Icon", resource.GetIcon());
		gameObject.SetText("Qty", TextUtil.ColorRedIf(missing, Loc.FormatNumber(raq.qty.Abs)));
		AddMouseover(gameObject, raq, null);
		return gameObject;
	}

	private GameObject AddBuildCapsule(GameObject row, Price price, bool missing)
	{
		GameObject gameObject = Add("Templates/Build Capsule", row);
		string message = Loc.FormatNumber(price.cash);
		gameObject.SetText("Icon", Loc.Get("module.desc.cash.symbol"));
		gameObject.SetText("Qty", TextUtil.ColorRedIf(missing, message));
		AddMouseover(gameObject, price);
		return gameObject;
	}

	private GameObject AddBuildCapsule(GameObject row, string icon, string text, string mo, bool missing)
	{
		GameObject gameObject = Add("Templates/Build Capsule", row);
		gameObject.SetText("Icon", icon);
		gameObject.SetText("Qty", TextUtil.ColorRedIf(missing, text));
		AddMouseover(gameObject, mo);
		return gameObject;
	}

	private GameObject AddBarCapsule(GameObject row, string text, string mo, float p, Color? barcolor = null)
	{
		GameObject gameObject = Add("Templates/Bar Capsule", row);
		gameObject.SetText("Qty", text);
		gameObject.GetChild("Bar").SetUIElementWidth(MathUtil.Clamp(p * 70f, 0f, 70f));
		if (barcolor.HasValue)
		{
			gameObject.GetImage("Bar").color = barcolor.Value;
		}
		AddMouseover(gameObject, mo);
		return gameObject;
	}

	private GameObject AddButtonCapsule(GameObject row, string infotext, string btext, string mo, bool enabled, UnityAction onClick)
	{
		GameObject gameObject = Add("Templates/Button Capsule", row);
		gameObject.SetTextOrHide("Text", infotext);
		gameObject.SetText("Button/Text", TextUtil.ColorEnabledIf(enabled, btext));
		Button button = gameObject.GetButton("Button");
		button.interactable = enabled;
		button.onClick.SetListener(onClick);
		AddMouseover(gameObject, mo);
		return gameObject;
	}

	private GameObject AddArrowCapsule(GameObject row)
	{
		return Add("Templates/Arrow Capsule", row);
	}

	private GameObject AddEmptyCapsule(GameObject row)
	{
		return Add("Templates/Spacer Capsule", row);
	}

	private GameObject Add(string panelTemplate, GameObject attachTo)
	{
		return Object.Instantiate(panel.GetChild(panelTemplate), attachTo.transform);
	}

	private ModuleDescCapsuleContext AddMouseover(GameObject card, string text)
	{
		return card.GetOrAddComponent<ModuleDescCapsuleContext>().Set(text);
	}

	private ModuleDescCapsuleContext AddMouseover(GameObject card, ResourceAndQty raq, string extra)
	{
		bool flag = !raq.qty.IsNegative;
		Resource resource = raq.FindResource();
		string iconNameAndUnits = resource.GetIconNameAndUnits(raq.qty.Abs);
		string text = (flag ? Loc.Get("module.desc.mo.produce", "restext", iconNameAndUnits) : Loc.Get("module.desc.mo.consume", "restext", iconNameAndUnits));
		if (!string.IsNullOrEmpty(extra))
		{
			text = text + "\n\n" + extra;
		}
		if (!flag)
		{
			Fixnum fixnum = (ModulesUtil.GetInventory(visit.building)?.data)?.Get(raq.id).qty ?? Fixnum.ZERO;
			string qtyAndUnits = resource.unitdef.GetQtyAndUnits(fixnum);
			bool predicate = fixnum < raq.qty.Abs;
			text += TextUtil.ColorRedIf(predicate, Loc.Get("module.desc.mo.cash", "textOnHand", qtyAndUnits));
		}
		return AddMouseover(card, text);
	}

	private ModuleDescCapsuleContext AddMouseover(GameObject card, Price price)
	{
		bool flag = !price.IsNegative;
		string text = Loc.Price(price.Abs);
		string text2 = (flag ? Loc.Get("module.desc.mo.result", "cashtext", text) : Loc.Get("module.desc.mo.cost", "cashtext", text));
		if (!flag)
		{
			PlayerFinances finances = visit.pid.FindPlayer().finances;
			Money money = finances.GetMoney(visit.building);
			bool predicate = !finances.CanChangeMoney(visit.building, price);
			text2 += TextUtil.ColorRedIf(predicate, Loc.Get("module.desc.mo.cash", "textOnHand", money));
		}
		return AddMouseover(card, text2);
	}
}
