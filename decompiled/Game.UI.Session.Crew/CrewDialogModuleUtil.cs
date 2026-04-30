using System.Linq;
using Game.Core;
using Game.Services;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Sim.Modules;
using Game.UI.Session.OwnedBiz;
using Game.UI.Util;
using SomaSim.Util;

namespace Game.UI.Session.Crew;

public static class CrewDialogModuleUtil
{
	public static string DescribeBackModule(Entity building, bool showProgress = false)
	{
		IModule module = building.components.modules.FindBackroomModule();
		if (module == null)
		{
			return null;
		}
		InventoryModule inventory = ModulesUtil.GetInventory(building);
		return DescribeInstalledModule(building, module, inventory.data, showProgress);
	}

	public static string DescribeInstalledModule(Entity building, IModule module, InventoryModuleData inv, bool showProgress = false)
	{
		ModuleQuery q = ModulesUtil.MakeModuleQuery(building);
		ModuleCommon.Display display = module.ModuleConfig?.Common?.display;
		if (display != null && display.locname != null)
		{
			_ = display?.locInstallDetail;
		}
		var (flag, result) = ModulesUIUtil.DescribeDamaged(building);
		if (flag)
		{
			return result;
		}
		int item = ModulesUIUtil.DescribeEnable(module, Game.ctx.clock.Now, shortForm: false).daysleft;
		if (item > 0)
		{
			return MakeConstruction(item);
		}
		string result2 = "";
		if (!(module is ManufactureModule mfg))
		{
			if (!(module is ConsumerModule con))
			{
				if (!(module is VehicleModule))
				{
					if (!(module is ExplanationModule))
					{
						if (module is InventoryModuleConfig)
						{
						}
					}
					else
					{
						result2 = DescribeExplanation();
					}
				}
				else
				{
					result2 = DescribeVehicle();
				}
			}
			else
			{
				result2 = DescribeConsumer(con);
			}
		}
		else
		{
			result2 = DescribeManufacture(mfg, showProgress);
		}
		return result2;
		string DescribeConsumer(ConsumerModule consumerModule)
		{
			return MakeConsumeRow(q, consumerModule.config.sink, module, inv);
		}
		static string DescribeExplanation()
		{
			return "";
		}
		string DescribeManufacture(ManufactureModule manufactureModule, bool showProgress2)
		{
			Fixnum item2 = FindNextManufacture(manufactureModule, q).percentDone;
			return MakeManufactureRow(q, manufactureModule.CurrentRecipe, module, inv, (float)item2, showProgress2);
		}
		static string DescribeVehicle()
		{
			return Loc.Get("ui.crewinfo.veh-desc");
		}
		static (int daysLeft, Fixnum percentDone) FindNextManufacture(ManufactureModule manufactureModule, ModuleQuery q2)
		{
			VisitState visit = new VisitState(Game.ctx.players.Human.crew.GetCrewForPlayerPeep(), Game.ctx.clock.Now, PlayerID.HumanPlayer);
			int num = (q2.OwnerIsHumanPlayer ? (from x in manufactureModule.config.recipes
				where x.visreqs == null || x.visreqs.AllPass(visit)
				select x.ModProduceConsumeDays(q2, manufactureModule)).Min() : manufactureModule.CurrentRecipe.ModProduceConsumeDays(q2, manufactureModule));
			SimTime lastUpdate = manufactureModule.data.lastUpdate;
			int num2 = MathUtil.Clamp(lastUpdate.IncrementDays(num).days - Game.ctx.clock.Now.days, 0, num);
			Fixnum fixnum = num - num2;
			Fixnum item2 = ((num > 0) ? (fixnum / num) : ((Fixnum)0));
			return (daysLeft: num2, percentDone: item2);
		}
	}

	private static string MakeConsumeRow(ModuleQuery q, ConsumerRecipe sink, IModule module, InventoryModuleData inv = null)
	{
		string text = "";
		if (sink.AllConsume.Count == 0)
		{
			return text;
		}
		foreach (ResourceAndQty item in sink.AllConsume)
		{
			RecipeMods.Result result = sink.ModConsume(item, q, module, module != null);
			text += MakeResIcon(result, inv, consumed: true);
		}
		text = text + " " + Loc.Get("ui.crewinfo.building.mo.resultcash");
		Fixnum fixnum = sink.ModConsumeDays(q, module, explain: true).resAndQty.qty.Ceiling();
		int num = Game.ctx.clock.DaysToTurnsRoundedUp(fixnum);
		string text2 = Loc.Get("ui.crewinfo.building.mo.cycletime", "consumeDays", fixnum, "consumeTurns", num);
		return text + text2;
	}

	private static string MakeManufactureRow(ModuleQuery q, Recipe recipe, IModule module, InventoryModuleData inv = null, float? p = null, bool showProgress = false)
	{
		string text = "";
		if (!recipe.IsConsumer && !recipe.IsProducer)
		{
			return text;
		}
		if (recipe.consume.Count > 0)
		{
			foreach (ResourceAndQty item in recipe.consume)
			{
				RecipeMods.Result result = recipe.ModConsume(item, q, module, module != null);
				text += MakeResIcon(result, inv, consumed: true);
			}
		}
		else
		{
			text += Loc.Get("ui.clock");
		}
		text = text + " " + Loc.Get("ui.crewinfo.building.mo.resultres") + " ";
		foreach (ResourceAndQty item2 in recipe.produce)
		{
			RecipeMods.Result result2 = recipe.ModProduce(item2, q, module, module != null);
			text += MakeResIcon(result2, null, consumed: false);
		}
		Fixnum fixnum = recipe.ModProduceConsumeDays(q, module, explain: true).resAndQty.qty.Ceiling();
		int num = Game.ctx.clock.DaysToTurnsRoundedUp(fixnum);
		string text2 = ((!showProgress || !p.HasValue) ? Loc.Get("ui.crewinfo.building.mo.cycletime", "consumeDays", fixnum, "consumeTurns", num) : Loc.Get("ui.crewinfo.building.mo.cycletime-with-progress", "consumeDays", fixnum, "consumeTurns", num, "percentage", Loc.Percentage(new Fixnum(p.Value))));
		return text + text2;
	}

	private static string MakeConstruction(int daysleft)
	{
		int num = Game.ctx.clock.DaysToTurnsRoundedUp(daysleft);
		return Loc.Get("ui.clock") + " " + Loc.Get("ui.crewinfo.building.mo.buildtime", "daysleft", daysleft, "turnsleft", num);
	}

	private static string MakeResIcon(RecipeMods.Result result, InventoryModuleData inv, bool consumed)
	{
		Resource resource = result.resAndQty.FindResource();
		bool predicate = false;
		if (consumed && inv != null)
		{
			predicate = inv.Get(resource.resid).qty < result.resAndQty.qty.Abs;
		}
		return TextUtil.ColorRedIf(predicate, resource.GetIcon());
	}
}
