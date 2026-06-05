using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;
using Game.Core;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Sim.Modules;

namespace AfterProhibitionEconomy
{
	internal static class EconomyStartupAudit
	{
		internal static void LogStartupAudit(string source, ManualLogSource logger)
		{
			if (logger == null)
			{
				return;
			}

			try
			{
				if (global::Game.Game.ctx?.entityman == null)
				{
					logger.LogInfo("economy-audit source=" + source + " unavailable reason=missing-entity-manager");
					return;
				}

				IEnumerable<Entity> buildings = global::Game.Game.ctx.entityman.GetCachedEntitiesBuildingsUnsafe();
				if (buildings == null)
				{
					logger.LogInfo("economy-audit source=" + source + " unavailable reason=missing-building-cache");
					return;
				}

				AuditCounts counts = new AuditCounts();
				Dictionary<string, TemplateCounts> byTemplate = new Dictionary<string, TemplateCounts>(StringComparer.OrdinalIgnoreCase);

				foreach (Entity building in buildings)
				{
					try
					{
						AuditBuilding(building, counts, byTemplate);
					}
					catch
					{
						counts.ScanErrors++;
					}
				}

				logger.LogInfo(
					"economy-audit source=" + source +
					" shops=" + counts.Shops +
					" banks=" + counts.Banks +
					" warehouses=" + counts.Warehouses +
					" fronts=" + counts.Fronts +
					" dirtyCashBackrooms=" + counts.DirtyCashBackrooms +
					" emptyModules=" + counts.EmptyModules +
					" buyStock=" + counts.BuyStock +
					" sellStock=" + counts.SellStock +
					" buyStockMissing=" + counts.BuyStockMissing +
					" sellStockOnly=" + counts.SellStockOnly);

				logger.LogInfo(
					"economy-audit-details source=" + source +
					" businessBuildings=" + counts.BusinessBuildings +
					" civic=" + counts.Civic +
					" purchaseProducing=" + counts.PurchaseProducing +
					" noInventory=" + counts.NoInventory +
					" noModules=" + counts.NoModules +
					" scanErrors=" + counts.ScanErrors);

				foreach (TemplateCounts entry in byTemplate.Values
					.Where(entry => entry.BuyStockMissing > 0 || entry.SellStockOnly > 0)
					.OrderByDescending(entry => entry.BuyStockMissing)
					.ThenByDescending(entry => entry.SellStockOnly)
					.Take(8))
				{
					logger.LogInfo(
						"economy-audit-template source=" + source +
						" template=" + entry.Template +
						" count=" + entry.Count +
						" buyStockMissing=" + entry.BuyStockMissing +
						" sellStockOnly=" + entry.SellStockOnly +
						" banks=" + entry.Banks +
						" warehouses=" + entry.Warehouses);
				}
			}
			catch (Exception ex)
			{
				logger.LogWarning("economy-audit failed source=" + source + " error=" + ex.GetType().Name + ":" + ex.Message);
			}
		}

		private static void AuditBuilding(Entity building, AuditCounts counts, Dictionary<string, TemplateCounts> byTemplate)
		{
			if (building?.components?.building == null)
			{
				return;
			}

			if (!building.components.building.IsBusinessBuildingType)
			{
				return;
			}

			counts.BusinessBuildings++;

			Entity biz = BuildingUtil.FindBizForBuilding(building);
			string buildingTemplate = GetTemplateString(building);
			string bizTemplate = GetTemplateString(biz);
			string templateKey = !string.IsNullOrEmpty(bizTemplate) && !string.Equals(bizTemplate, "(null)", StringComparison.Ordinal)
				? bizTemplate
				: buildingTemplate;

			bool isBank = ContainsAny(templateKey, "bank") || ContainsAny(buildingTemplate, "bank") || ContainsAny(bizTemplate, "bank");
			bool isWarehouse = ContainsAny(templateKey, "warehouse", "wholesale") || ContainsAny(buildingTemplate, "warehouse", "wholesale") || ContainsAny(bizTemplate, "warehouse", "wholesale");
			bool isCivic = isBank || ContainsAny(buildingTemplate, "school", "worship", "church") || ContainsAny(bizTemplate, "school", "worship", "church");

			if (isBank)
			{
				counts.Banks++;
			}
			if (isWarehouse)
			{
				counts.Warehouses++;
			}
			if (isCivic)
			{
				counts.Civic++;
			}
			if (!isBank && !isWarehouse && !isCivic)
			{
				counts.Shops++;
			}

			ModulesComponent modules = building.components.modules;
			if (modules == null)
			{
				counts.NoModules++;
				return;
			}
			if (modules.inventory == null)
			{
				counts.NoInventory++;
			}

			bool emptyModules = HasEmptyOrMissingBusinessModules(building, biz, modules);
			bool purchaseProducing = HasInstalledPurchaseProducingModule(modules);
			bool hasBuyStock = HasPositivePlayerOffer(modules, playerBuys: true, playerSells: false);
			bool hasSellStock = HasPositivePlayerOffer(modules, playerBuys: false, playerSells: true);
			bool hasFrontModule = HasInstalledModuleMatching(modules, IsFrontModuleId);
			bool hasDirtyCashBackroom = HasInstalledModuleMatching(modules, IsDirtyCashBackroomModuleId);

			if (emptyModules)
			{
				counts.EmptyModules++;
			}
			if (purchaseProducing)
			{
				counts.PurchaseProducing++;
			}
			if (hasBuyStock)
			{
				counts.BuyStock++;
			}
			if (hasSellStock)
			{
				counts.SellStock++;
			}
			if (purchaseProducing && !hasBuyStock)
			{
				counts.BuyStockMissing++;
			}
			if (hasSellStock && !hasBuyStock)
			{
				counts.SellStockOnly++;
			}
			if (hasFrontModule || ContainsAny(bizTemplate, "front", "player-legal"))
			{
				counts.Fronts++;
			}
			if (hasDirtyCashBackroom)
			{
				counts.DirtyCashBackrooms++;
			}

			if (purchaseProducing && !hasBuyStock || hasSellStock && !hasBuyStock)
			{
				TemplateCounts templateCounts = GetTemplateCounts(byTemplate, templateKey);
				templateCounts.Count++;
				if (purchaseProducing && !hasBuyStock)
				{
					templateCounts.BuyStockMissing++;
				}
				if (hasSellStock && !hasBuyStock)
				{
					templateCounts.SellStockOnly++;
				}
				if (isBank)
				{
					templateCounts.Banks++;
				}
				if (isWarehouse)
				{
					templateCounts.Warehouses++;
				}
			}
		}

		private static TemplateCounts GetTemplateCounts(Dictionary<string, TemplateCounts> byTemplate, string template)
		{
			string key = string.IsNullOrEmpty(template) ? "(unknown)" : template;
			if (!byTemplate.TryGetValue(key, out TemplateCounts counts))
			{
				counts = new TemplateCounts
				{
					Template = key
				};
				byTemplate[key] = counts;
			}
			return counts;
		}

		private static bool HasEmptyOrMissingBusinessModules(Entity building, Entity biz, ModulesComponent modules)
		{
			List<IModule> slots = modules?.GetAllSlotsUnsafe();
			if (slots == null || slots.Count == 0)
			{
				return true;
			}

			List<Label> expectedModuleIds = biz?.data?.biz?.modules;
			bool hasInstalled = slots.Any(slot => slot != null);
			if (expectedModuleIds == null || expectedModuleIds.Count == 0)
			{
				return !hasInstalled;
			}

			return expectedModuleIds.Any(moduleId => moduleId.IsSet && !modules.HasModuleInstalled(moduleId));
		}

		private static bool HasInstalledPurchaseProducingModule(ModulesComponent modules)
		{
			if (modules?.bizmodules == null || modules.inventory == null)
			{
				return false;
			}

			foreach (IBizModule bizModule in modules.bizmodules)
			{
				IEnumerable<MfgItem> items = bizModule?.ProduceAllItemsInCurrentRecipe();
				if (items != null && items.Any(item => !item.consumed))
				{
					return true;
				}
			}

			return false;
		}

		private static bool HasPositivePlayerOffer(ModulesComponent modules, bool playerBuys, bool playerSells)
		{
			if (modules?.inventory == null)
			{
				return false;
			}

			try
			{
				return modules
					.ProduceAllItemsPlayerCanBuyOrSell(PlayerID.HumanPlayer, playerBuys, playerSells)
					.Any(element => element.qty.IsPositive);
			}
			catch
			{
				return false;
			}
		}

		private static bool HasInstalledModuleMatching(ModulesComponent modules, Func<string, bool> predicate)
		{
			List<IModule> slots = modules?.GetAllSlotsUnsafe();
			if (slots == null || predicate == null)
			{
				return false;
			}

			foreach (IModule slot in slots)
			{
				string moduleId = slot?.ModuleConfig?.Id.ToString();
				if (!string.IsNullOrEmpty(moduleId) && predicate(moduleId))
				{
					return true;
				}
			}

			return false;
		}

		private static bool IsFrontModuleId(string moduleId)
		{
			return ContainsAny(moduleId, "player-legal", "legal-", "front");
		}

		private static bool IsDirtyCashBackroomModuleId(string moduleId)
		{
			return ContainsAny(moduleId, "dirty-cash", "dirtycash", "illegalbackroom");
		}

		private static bool ContainsAny(string value, params string[] needles)
		{
			if (string.IsNullOrEmpty(value) || needles == null)
			{
				return false;
			}

			foreach (string needle in needles)
			{
				if (!string.IsNullOrEmpty(needle) && value.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0)
				{
					return true;
				}
			}

			return false;
		}

		private static string GetTemplateString(Entity entity)
		{
			return entity?.config?.Template.String ?? "(null)";
		}

		private sealed class AuditCounts
		{
			internal int BusinessBuildings;
			internal int Shops;
			internal int Banks;
			internal int Warehouses;
			internal int Fronts;
			internal int Civic;
			internal int DirtyCashBackrooms;
			internal int EmptyModules;
			internal int PurchaseProducing;
			internal int BuyStock;
			internal int SellStock;
			internal int BuyStockMissing;
			internal int SellStockOnly;
			internal int NoInventory;
			internal int NoModules;
			internal int ScanErrors;
		}

		private sealed class TemplateCounts
		{
			internal string Template;
			internal int Count;
			internal int BuyStockMissing;
			internal int SellStockOnly;
			internal int Banks;
			internal int Warehouses;
		}
	}
}
