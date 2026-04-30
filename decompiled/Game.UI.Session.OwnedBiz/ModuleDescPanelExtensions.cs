using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Services;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Sim.Modules;
using Game.UI.Util;
using SomaSim.Util;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.Session.OwnedBiz;

public static class ModuleDescPanelExtensions
{
	public static void DescribeAddModule(this ModuleDescPanelBuilder b, IModuleConfig config)
	{
		ModuleCommon.Display display = config?.Common?.display;
		if (display != null && display.locname != null)
		{
			_ = display?.locInstallDetail;
		}
		string text = Loc.Get(display.locname);
		string text2 = Loc.Get(display.locInstallDetail);
		b.AddTextEntry(Loc.Get("module.desc.locname", "name", text));
		b.AddTextEntry(text2);
		if (!(config is ManufactureModuleConfig manufactureModuleConfig))
		{
			if (!(config is ConsumerModuleConfig consumerModuleConfig))
			{
				if (!(config is VehicleModuleConfig config2))
				{
					if (config is InventoryModuleConfig)
					{
					}
				}
				else
				{
					b.AddTextEntry(Loc.Get("module.desc.op-details"));
					DescribeVehicle(b, null, config2, showHeader: false);
				}
			}
			else
			{
				b.AddTextEntry(Loc.Get("module.desc.op-details"));
				b.AddConsumeRow(consumerModuleConfig.sink, null);
			}
		}
		else
		{
			VisitState visit = new VisitState(Game.ctx.players.Human.crew.GetCrewForPlayerPeep(), Game.ctx.clock.Now, PlayerID.HumanPlayer);
			b.AddTextEntry(Loc.Get("module.desc.prod-details"));
			foreach (Recipe recipe in manufactureModuleConfig.recipes)
			{
				if (recipe.visreqs == null || recipe.visreqs.AllPass(visit))
				{
					b.AddManufactureRow(recipe, null);
				}
			}
		}
		b.AddRequirementsRow(config);
		b.AddPurchaseRows(config.Common.purchase);
		GameObject parent = b.parent;
		parent.transform.parent.parent.gameObject.ResetScrollView();
		LayoutRebuilder.ForceRebuildLayoutImmediate(parent.GetComponent<RectTransform>());
	}

	public static void DescribeInstalledModule(this ModuleDescPanelBuilder b, Entity building, IModule module, InventoryModuleData inv)
	{
		IModuleConfig moduleConfig = module.ModuleConfig;
		ModuleCommon.Display display = moduleConfig?.Common?.display;
		if (display?.locname != null)
		{
			_ = display?.locInstallDetail;
		}
		var (flag, text) = ModulesUIUtil.DescribeDamaged(building);
		if (flag)
		{
			b.AddTextEntry(text);
		}
		string text2 = Loc.Get(display.locname);
		b.AddTextEntry(Loc.Get("module.desc.locname", "name", text2));
		var (text3, num) = ModulesUIUtil.DescribeEnable(module, Game.ctx.clock.Now, shortForm: false);
		if (num > 0)
		{
			b.AddConstructionRow(moduleConfig, text3, num);
		}
		if (!(module is ManufactureModule mfg))
		{
			if (!(module is ConsumerModule con))
			{
				if (!(module is VehicleModule vehicleModule))
				{
					if (!(module is ExplanationModule exp))
					{
						if (module is InventoryModuleConfig)
						{
						}
					}
					else
					{
						DescribeExplanation(exp);
					}
				}
				else
				{
					DescribeVehicle(b, vehicleModule, vehicleModule.config, showHeader: true);
				}
			}
			else
			{
				DescribeConsumer(con);
			}
		}
		else
		{
			DescribeManufacture(mfg);
		}
		GameObject parent = b.parent;
		parent.transform.parent.parent.gameObject.ResetScrollView();
		LayoutRebuilder.ForceRebuildLayoutImmediate(parent.GetComponent<RectTransform>());
		void DescribeConsumer(ConsumerModule consumerModule)
		{
			ConsumerRecipe sink = consumerModule.config.sink;
			string text4 = Loc.Get(sink.locFlavorDesc ?? display.locInstallDetail);
			b.AddTextEntry(text4);
			b.AddTextEntry(Loc.Get("module.desc.op-details"));
			b.AddConsumeRow(sink, module, inv);
			List<ResourceAndQty> data = consumerModule.data.lastConsumed.data;
			List<ResourceAndQty> list = new List<ResourceAndQty>();
			foreach (ResourceAndQty item2 in data)
			{
				list.Add(new ResourceAndQty(item2.id, -item2.qty));
			}
			ProduceResQtyDetails(Loc.Get("module.desc.sold"), list, consumed: true);
			if (consumerModule.config.sink?.aoe?.respectLockey != null)
			{
				string message = Loc.Get(consumerModule.config.sink.aoe.respectLockey);
				b.AddTextEntry(TextUtil.ColorWrap(message, ColorConstants.TEXT_HEX_RESPECT));
			}
		}
		void DescribeExplanation(ExplanationModule explanationModule)
		{
			string text4 = Loc.Get(explanationModule.config.Common.display.locdesc);
			b.AddTextEntry(text4);
		}
		void DescribeManufacture(ManufactureModule manufactureModule)
		{
			string text4 = Loc.Get(manufactureModule.CurrentRecipe.locFlavorDesc ?? display.locInstallDetail);
			b.AddTextEntry(text4);
			b.AddTextEntry(Loc.Get("module.desc.prod-details"));
			VisitState visit = new VisitState(Game.ctx.players.Human.crew.GetCrewForPlayerPeep(), Game.ctx.clock.Now, PlayerID.HumanPlayer);
			foreach (Recipe recipe in manufactureModule.config.recipes)
			{
				if (recipe.visreqs == null || recipe.visreqs.AllPass(visit))
				{
					Fixnum item = FindNextManufacture(manufactureModule, b.q).percentDone;
					b.AddManufactureRow(recipe, module, inv, (float)item);
				}
			}
			ProduceResQtyDetails(Loc.Get("module.desc.last-prod"), manufactureModule.data.lastProduced.data, consumed: false);
		}
		void ProduceResQtyDetails(string intro, List<ResourceAndQty> list, bool consumed)
		{
			if (list == null || list.Count > 0)
			{
				b.AddTextEntry(intro);
				b.AddResRow(list, consumed);
			}
			else
			{
				intro = intro + " " + Loc.Get("module.desc.nothing");
				b.AddTextEntry(intro);
			}
		}
	}

	private static void DescribeVehicle(ModuleDescPanelBuilder b, VehicleModule module, VehicleModuleConfig config, bool showHeader)
	{
		if (showHeader)
		{
			string text = Loc.Get(config.vehicleModuleLoc.locFlavorDesc ?? config.common.display.locInstallDetail);
			b.AddTextEntry(text);
			b.AddTextEntry(Loc.Get("module.desc.op-details"));
		}
		b.AddVehicleRepairRow(module);
		string text2 = "\n";
		ModQuery q = b.q.MakeManagerModQuery();
		text2 = text2 + AddParkingInfo(config.playerInfo.carCapDelta, Loc.Get("module.desc.vehicle.increase-car"), q) + "\n";
		text2 += AddParkingInfo(config.playerInfo.truckCapDelta, Loc.Get("module.desc.vehicle.increase-truck"), q);
		b.AddTextEntry(text2);
	}

	private static string AddParkingInfo(ModValue v, string header, ModQuery q)
	{
		if (v == null)
		{
			return header + " " + Loc.FormatNumber(0);
		}
		Fixnum value = v.Evaluate(q);
		string text = header + " " + Loc.FormatNumber(value);
		if (value.IsNotZero)
		{
			string text2 = v.Explain(q, addHeader: false);
			if (!string.IsNullOrWhiteSpace(text2))
			{
				text = text + "\n" + text2;
			}
		}
		return text;
	}

	private static (int daysLeft, Fixnum percentDone) FindNextManufacture(ManufactureModule module, ModuleQuery q)
	{
		VisitState visit = new VisitState(Game.ctx.players.Human.crew.GetCrewForPlayerPeep(), Game.ctx.clock.Now, PlayerID.HumanPlayer);
		int num = (q.OwnerIsHumanPlayer ? (from x in module.config.recipes
			where x.visreqs == null || x.visreqs.AllPass(visit)
			select x.ModProduceConsumeDays(q, module)).Min() : module.CurrentRecipe.ModProduceConsumeDays(q, module));
		SimTime lastUpdate = module.data.lastUpdate;
		int num2 = MathUtil.Clamp(lastUpdate.IncrementDays(num).days - Game.ctx.clock.Now.days, 0, num);
		Fixnum fixnum = num - num2;
		Fixnum item = ((num > 0) ? (fixnum / num) : ((Fixnum)0));
		return (daysLeft: num2, percentDone: item);
	}
}
