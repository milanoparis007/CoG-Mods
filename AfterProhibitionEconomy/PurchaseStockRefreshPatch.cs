using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using Game.Core;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Sim;
using Game.Session.Sim.Modules;
using Game.UI.Session.Convo;
using HarmonyLib;
using SomaSim.Util;

namespace AfterProhibitionEconomy
{
	internal static class PurchaseStockRefreshPatch
	{
		private const int RuntimePurchaseStockRefreshCooldownDays = 28;
		private const int RuntimePurchaseStockRefreshScanBudget = 2048;
		private const int RuntimePurchaseStockRefreshTimeBudgetMs = 8;
		private const int RuntimePurchaseStockRefreshCandidateBudget = 32;
		private const int RuntimePurchaseStockRefreshMaxCatchupPasses = 5;

		private static readonly Dictionary<string, int> LastRefreshDayBySource = new Dictionary<string, int>(StringComparer.Ordinal);
		private static readonly Dictionary<ulong, int> LastInteractiveRefreshDayByBuilding = new Dictionary<ulong, int>();
		private static readonly HashSet<string> LoggedRefreshedBusinesses = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		private static readonly HashSet<string> LoggedBlockedCivicPurchaseAccess = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		private static MethodInfo _modulesComponentDoUpdateMethod;
		private static int _skipRuntimeRefreshUntilDay = int.MinValue;
		private static int _lastRuntimeRefreshCooldownLogDay = int.MinValue;
		private static RuntimeRefreshScanState _runtimeRefreshScanState;

		internal static void ApplyPatch(Harmony harmony)
		{
			try
			{
				MethodInfo updateBusinessModulesMethod = AccessTools.Method(typeof(BusinessUpdate), "UpdateBusinessModules");
				if (updateBusinessModulesMethod != null)
				{
					harmony.Patch(
						updateBusinessModulesMethod,
						postfix: new HarmonyMethod(typeof(PurchaseStockRefreshPatch), nameof(UpdateBusinessModulesPostfix)));
					AfterProhibitionEconomyPlugin.Log?.LogInfo("purchase stock refresh patch applied target=BusinessUpdate.UpdateBusinessModules");
				}
				else
				{
					AfterProhibitionEconomyPlugin.Log?.LogWarning("purchase stock refresh patch skipped target=BusinessUpdate.UpdateBusinessModules reason=method-not-found");
				}

				MethodInfo checkAvailabilityMethod = AccessTools.Method(typeof(CheckCanBuySell), "CheckAvailability", new[]
				{
					typeof(VisitState)
				});
				if (checkAvailabilityMethod != null)
				{
					harmony.Patch(
						checkAvailabilityMethod,
						prefix: new HarmonyMethod(typeof(PurchaseStockRefreshPatch), nameof(CheckCanBuySellAvailabilityPrefix)));
					AfterProhibitionEconomyPlugin.Log?.LogInfo("purchase stock interactive refresh patch applied target=CheckCanBuySell.CheckAvailability");
				}
				else
				{
					AfterProhibitionEconomyPlugin.Log?.LogWarning("purchase stock interactive refresh patch skipped target=CheckCanBuySell.CheckAvailability reason=method-not-found");
				}

				MethodInfo convoButtonIsVisibleMethod = AccessTools.Method(typeof(ConvoButton), "IsVisible", new[]
				{
					typeof(VisitState)
				});
				if (convoButtonIsVisibleMethod != null)
				{
					harmony.Patch(
						convoButtonIsVisibleMethod,
						prefix: new HarmonyMethod(typeof(PurchaseStockRefreshPatch), nameof(ConvoButtonAvailabilityPrefix)));
					AfterProhibitionEconomyPlugin.Log?.LogInfo("purchase stock interactive refresh patch applied target=ConvoButton.IsVisible");
				}
				else
				{
					AfterProhibitionEconomyPlugin.Log?.LogWarning("purchase stock interactive refresh patch skipped target=ConvoButton.IsVisible reason=method-not-found");
				}

				MethodInfo convoButtonIsEnabledMethod = AccessTools.Method(typeof(ConvoButton), "IsEnabled", new[]
				{
					typeof(VisitState)
				});
				if (convoButtonIsEnabledMethod != null)
				{
					harmony.Patch(
						convoButtonIsEnabledMethod,
						prefix: new HarmonyMethod(typeof(PurchaseStockRefreshPatch), nameof(ConvoButtonAvailabilityPrefix)));
					AfterProhibitionEconomyPlugin.Log?.LogInfo("purchase stock interactive refresh patch applied target=ConvoButton.IsEnabled");
				}
				else
				{
					AfterProhibitionEconomyPlugin.Log?.LogWarning("purchase stock interactive refresh patch skipped target=ConvoButton.IsEnabled reason=method-not-found");
				}
			}
			catch (Exception ex)
			{
				AfterProhibitionEconomyPlugin.Log?.LogWarning("purchase stock refresh patch setup failed: " + ex.Message);
			}
		}

		private static void CheckCanBuySellAvailabilityPrefix(VisitState visit)
		{
			if (!(AfterProhibitionEconomyPlugin.EnablePurchaseStockRefresh?.Value ?? false))
			{
				return;
			}

			TryRefreshInteractivePurchaseStock(visit, "convo-availability");
		}

		private static void ConvoButtonAvailabilityPrefix(VisitState visit)
		{
			if (!(AfterProhibitionEconomyPlugin.EnablePurchaseStockRefresh?.Value ?? false))
			{
				return;
			}

			TryRefreshInteractivePurchaseStock(visit, "convo-button");
		}

		private static void UpdateBusinessModulesPostfix(bool initial)
		{
			if (!(AfterProhibitionEconomyPlugin.EnablePurchaseStockRefresh?.Value ?? false))
			{
				return;
			}

			Run(initial ? "business-update-initial" : "business-update", force: false);
		}

		internal static void Run(string source, bool force)
		{
			try
			{
				if (global::Game.Game.ctx?.entityman == null || global::Game.Game.ctx?.clock == null)
				{
					return;
				}

				SimTime now = global::Game.Game.ctx.clock.Now;
				string sourceKey = string.IsNullOrEmpty(source) ? "unknown" : source;
				bool isInitialBusinessUpdate = string.Equals(sourceKey, "business-update-initial", StringComparison.Ordinal);
				bool isRuntimeBusinessUpdate = string.Equals(sourceKey, "business-update", StringComparison.Ordinal);
				if (!force && isInitialBusinessUpdate)
				{
					LastRefreshDayBySource[sourceKey] = now.days;
					_skipRuntimeRefreshUntilDay = Math.Max(_skipRuntimeRefreshUntilDay, now.days + RuntimePurchaseStockRefreshCooldownDays);
					AfterProhibitionEconomyPlugin.Log?.LogInfo(
						"purchase-stock-refresh skipped source=" + sourceKey +
						" reason=startup-safety deferredCooldownUntilDay=" + _skipRuntimeRefreshUntilDay +
						" day=" + now.days);
					return;
				}
				if (!force && isRuntimeBusinessUpdate && now.days < _skipRuntimeRefreshUntilDay)
				{
					if (_lastRuntimeRefreshCooldownLogDay != now.days)
					{
						_lastRuntimeRefreshCooldownLogDay = now.days;
						AfterProhibitionEconomyPlugin.Log?.LogInfo(
							"purchase-stock-refresh skipped source=" + sourceKey +
							" reason=runtime-cooldown untilDay=" + _skipRuntimeRefreshUntilDay +
							" day=" + now.days);
					}
					return;
				}
				if (!force && isRuntimeBusinessUpdate)
				{
					if (LastRefreshDayBySource.TryGetValue(sourceKey, out int runtimeLastDay) && runtimeLastDay == now.days)
					{
						return;
					}

					LastRefreshDayBySource[sourceKey] = now.days;
					RunRuntimeBusinessUpdateBatch(sourceKey, now);
					return;
				}
				if (!force
					&& LastRefreshDayBySource.TryGetValue(sourceKey, out int lastDay)
					&& lastDay == now.days)
				{
					return;
				}

				LastRefreshDayBySource[sourceKey] = now.days;

				int scanned = 0;
				int candidates = 0;
				int refreshed = 0;
				int failed = 0;
				int skipped = 0;
				int skippedHuman = 0;
				int skippedNoModule = 0;
				int skippedAlreadyStocked = 0;
				int skippedNoInventory = 0;
				int catchupPasses = 0;
				int partialRefreshes = 0;
				int directTopOffs = 0;
				bool trackCivicPurchaseAccess = isInitialBusinessUpdate;
				int banks = 0;
				int banksAccess = 0;
				int banksBlocked = 0;
				int warehouses = 0;
				int warehousesAccess = 0;
				int warehousesBlocked = 0;

				IEnumerable<Entity> buildings = global::Game.Game.ctx.entityman.GetCachedEntitiesBuildingsUnsafe();
				if (buildings == null)
				{
					return;
				}

				foreach (Entity building in buildings)
				{
					scanned++;
					if (trackCivicPurchaseAccess)
					{
						TrackCivicPurchaseAccessSummary(
							building,
							ref banks,
							ref banksAccess,
							ref banksBlocked,
							ref warehouses,
							ref warehousesAccess,
							ref warehousesBlocked);
					}

					if (!CanRefreshBusinessPurchaseStock(building, out ModulesComponent modules, out string skipReason))
					{
						skipped++;
						TrackSkip(skipReason, ref skippedHuman, ref skippedNoModule, ref skippedAlreadyStocked, ref skippedNoInventory);
						continue;
					}

					candidates++;
					if (TryRefreshPurchaseStock(modules, now, sourceKey, out PurchaseStockRefreshResult refreshResult))
					{
						refreshed++;
						catchupPasses += refreshResult.Passes;
						if (refreshResult.Partial)
						{
							partialRefreshes++;
						}
						if (refreshResult.DirectTopOff)
						{
							directTopOffs++;
						}
						LogBusinessPurchaseStockRefresh(sourceKey, building, refreshResult);
					}
					else
					{
						failed++;
						catchupPasses += refreshResult.Passes;
					}
				}

				if (candidates > 0 || refreshed > 0 || failed > 0 || AfterProhibitionEconomyPlugin.EnablePurchaseStockRefreshSummaryWhenNoCandidates.Value)
				{
					AfterProhibitionEconomyPlugin.Log?.LogInfo(
						"purchase-stock-refresh source=" + sourceKey +
						" scanned=" + scanned +
						" candidates=" + candidates +
						" refreshed=" + refreshed +
						" partial=" + partialRefreshes +
						" directTopOffs=" + directTopOffs +
						" failed=" + failed +
						" catchupPasses=" + catchupPasses +
						" skipped=" + skipped +
						" skippedHuman=" + skippedHuman +
						" skippedNoModule=" + skippedNoModule +
						" skippedAlreadyStocked=" + skippedAlreadyStocked +
						" skippedNoInventory=" + skippedNoInventory +
						" day=" + now.days);
				}

				if (trackCivicPurchaseAccess)
				{
					AfterProhibitionEconomyPlugin.Log?.LogInfo(
						"civic-purchase-runtime-summary source=" + sourceKey +
						" banks=" + banks +
						" banksAccess=" + banksAccess +
						" banksBlocked=" + banksBlocked +
						" warehouses=" + warehouses +
						" warehousesAccess=" + warehousesAccess +
						" warehousesBlocked=" + warehousesBlocked +
						" day=" + now.days);
				}

				if (isRuntimeBusinessUpdate || isInitialBusinessUpdate)
				{
					_skipRuntimeRefreshUntilDay = Math.Max(_skipRuntimeRefreshUntilDay, now.days + RuntimePurchaseStockRefreshCooldownDays);
				}
			}
			catch (Exception ex)
			{
				AfterProhibitionEconomyPlugin.Log?.LogWarning("purchase-stock-refresh failed source=" + source + " error=" + ex.GetType().Name + ":" + ex.Message);
			}
		}

		private static void RunRuntimeBusinessUpdateBatch(string sourceKey, SimTime now)
		{
			object currentContext = global::Game.Game.ctx;
			RuntimeRefreshScanState state = _runtimeRefreshScanState;
			if (state != null && (!ReferenceEquals(state.Context, currentContext) || now.days < state.StartedDay))
			{
				state = null;
				_runtimeRefreshScanState = null;
			}

			if (state == null)
			{
				IEnumerable<Entity> buildings = global::Game.Game.ctx.entityman.GetCachedEntitiesBuildingsUnsafe();
				if (buildings == null)
				{
					return;
				}

				state = new RuntimeRefreshScanState(sourceKey, now.days, buildings, currentContext);
				_runtimeRefreshScanState = state;
			}

			Stopwatch stopwatch = Stopwatch.StartNew();
			int processed = 0;
			int candidatesProcessed = 0;
			int budget = Math.Max(1, RuntimePurchaseStockRefreshScanBudget);
			int timeBudgetMs = Math.Max(1, RuntimePurchaseStockRefreshTimeBudgetMs);
			int candidateBudget = Math.Max(1, RuntimePurchaseStockRefreshCandidateBudget);
			while (state.Cursor < state.Buildings.Count && processed < budget)
			{
				Entity building = state.Buildings[state.Cursor++];
				processed++;
				state.Scanned++;

				if (!CanRefreshBusinessPurchaseStock(building, out ModulesComponent modules, out string skipReason))
				{
					state.Skipped++;
					TrackSkip(
						skipReason,
						ref state.SkippedHuman,
						ref state.SkippedNoModule,
						ref state.SkippedAlreadyStocked,
						ref state.SkippedNoInventory);
					if (stopwatch.ElapsedMilliseconds >= timeBudgetMs)
					{
						break;
					}

					continue;
				}

				if (candidatesProcessed >= candidateBudget)
				{
					state.DeferredCandidates++;
					state.Cursor--;
					state.Scanned--;
					break;
				}

				state.Candidates++;
				candidatesProcessed++;
				if (TryRefreshPurchaseStock(modules, now, sourceKey, out PurchaseStockRefreshResult refreshResult))
				{
					state.Refreshed++;
					state.CatchupPasses += refreshResult.Passes;
					if (refreshResult.Partial)
					{
						state.PartialRefreshes++;
					}
					if (refreshResult.DirectTopOff)
					{
						state.DirectTopOffs++;
					}
					LogBusinessPurchaseStockRefresh(sourceKey, building, refreshResult);
				}
				else
				{
					state.Failed++;
					state.CatchupPasses += refreshResult.Passes;
				}

				if (stopwatch.ElapsedMilliseconds >= timeBudgetMs)
				{
					break;
				}
			}
			stopwatch.Stop();

			bool completed = state.Cursor >= state.Buildings.Count;
			if (!completed)
			{
				if (state.Failed > 0 || AfterProhibitionEconomyPlugin.ShouldLogRuntimeEconomyProgress())
				{
					AfterProhibitionEconomyPlugin.Log?.LogInfo(
						"purchase-stock-refresh-progress source=" + sourceKey +
						" scannedBatch=" + processed +
						" scannedTotal=" + state.Scanned +
						" totalBuildings=" + state.Buildings.Count +
						" candidates=" + state.Candidates +
						" candidateUpdatesBatch=" + candidatesProcessed +
						" deferredCandidates=" + state.DeferredCandidates +
						" refreshed=" + state.Refreshed +
						" partial=" + state.PartialRefreshes +
						" directTopOffs=" + state.DirectTopOffs +
						" failed=" + state.Failed +
						" catchupPasses=" + state.CatchupPasses +
						" elapsedMs=" + stopwatch.ElapsedMilliseconds +
						" day=" + now.days);
				}
				return;
			}

			if (state.Candidates > 0 || state.Refreshed > 0 || state.Failed > 0 || AfterProhibitionEconomyPlugin.EnablePurchaseStockRefreshSummaryWhenNoCandidates.Value)
			{
				AfterProhibitionEconomyPlugin.Log?.LogInfo(
					"purchase-stock-refresh source=" + sourceKey +
					" phase=complete" +
					" scanned=" + state.Scanned +
					" candidates=" + state.Candidates +
					" deferredCandidates=" + state.DeferredCandidates +
					" refreshed=" + state.Refreshed +
					" partial=" + state.PartialRefreshes +
					" directTopOffs=" + state.DirectTopOffs +
					" failed=" + state.Failed +
					" catchupPasses=" + state.CatchupPasses +
					" skipped=" + state.Skipped +
					" skippedHuman=" + state.SkippedHuman +
					" skippedNoModule=" + state.SkippedNoModule +
					" skippedAlreadyStocked=" + state.SkippedAlreadyStocked +
					" skippedNoInventory=" + state.SkippedNoInventory +
					" day=" + now.days);
			}

			_skipRuntimeRefreshUntilDay = Math.Max(_skipRuntimeRefreshUntilDay, now.days + RuntimePurchaseStockRefreshCooldownDays);
			_runtimeRefreshScanState = null;
		}

		private sealed class RuntimeRefreshScanState
		{
			internal readonly string SourceKey;
			internal readonly int StartedDay;
			internal readonly object Context;
			internal readonly List<Entity> Buildings;
			internal int Cursor;
			internal int Scanned;
			internal int Candidates;
			internal int DeferredCandidates;
			internal int Refreshed;
			internal int PartialRefreshes;
			internal int DirectTopOffs;
			internal int Failed;
			internal int CatchupPasses;
			internal int Skipped;
			internal int SkippedHuman;
			internal int SkippedNoModule;
			internal int SkippedAlreadyStocked;
			internal int SkippedNoInventory;

			internal RuntimeRefreshScanState(string sourceKey, int startedDay, IEnumerable<Entity> buildings, object context)
			{
				SourceKey = sourceKey;
				StartedDay = startedDay;
				Context = context;
				Buildings = new List<Entity>(buildings);
			}
		}

		private static bool CanRefreshBusinessPurchaseStock(Entity building, out ModulesComponent modules, out string skipReason)
		{
			modules = null;
			skipReason = "unknown";
			if (building?.components?.building == null || building.data?.building == null)
			{
				skipReason = "not-business-building";
				return false;
			}

			if (!building.components.building.IsBusinessBuildingType || building.components.building.IsSafehouse || building.components.building.IsOutpost)
			{
				skipReason = "not-refreshable-business";
				return false;
			}

			if (building.data.building.controlled.Get().IsHumanPlayer)
			{
				skipReason = "human-controlled";
				return false;
			}
			try
			{
				if (global::Game.Game.ctx?.players?.Human?.territory?.IsControlled(building) == true)
				{
					skipReason = "human-controlled";
					return false;
				}
			}
			catch
			{
			}

			modules = building.components.modules;
			if (modules == null)
			{
				skipReason = "no-modules";
				return false;
			}
			if (modules.inventory == null)
			{
				skipReason = "no-inventory";
				return false;
			}
			if (!TryGetPurchaseStockState(modules, out bool hasPurchaseProducingModule, out Fixnum currentBuyOfferTotal, out Fixnum targetBuyOfferTotal))
			{
				skipReason = "no-purchase-producing-module";
				return false;
			}
			if (!hasPurchaseProducingModule)
			{
				skipReason = "no-purchase-producing-module";
				return false;
			}
			if (IsPurchaseStockAtTarget(currentBuyOfferTotal, targetBuyOfferTotal))
			{
				skipReason = "already-stocked";
				return false;
			}

			return true;
		}

		private static void TryRefreshInteractivePurchaseStock(VisitState visit, string source)
		{
			try
			{
				if (visit?.building?.Id.IsValid != true || global::Game.Game.ctx?.clock == null)
				{
					return;
				}

				Entity building = visit.building;
				int day = global::Game.Game.ctx.clock.Now.days;
				if (LastInteractiveRefreshDayByBuilding.TryGetValue(building.Id.id, out int lastDay) && lastDay == day)
				{
					return;
				}

				LastInteractiveRefreshDayByBuilding[building.Id.id] = day;
				if (!CanRefreshBusinessPurchaseStock(building, out ModulesComponent modules, out string skipReason))
				{
					if (!string.Equals(skipReason, "already-stocked", StringComparison.Ordinal))
					{
						LogInteractivePurchaseStockRefresh(source, building, false, skipReason, default);
					}
					return;
				}

				if (TryRefreshPurchaseStock(modules, global::Game.Game.ctx.clock.Now, source, out PurchaseStockRefreshResult result))
				{
					LogBusinessPurchaseStockRefresh(source, building, result);
					LogInteractivePurchaseStockRefresh(source, building, true, result.Reason, result);
				}
				else
				{
					LogInteractivePurchaseStockRefresh(source, building, false, result.Reason, result);
				}
			}
			catch (Exception ex)
			{
				AfterProhibitionEconomyPlugin.Log?.LogWarning("purchase-stock-interactive-refresh failed source=" + source + " error=" + ex.GetType().Name + ":" + ex.Message);
			}
		}

		private static void TrackSkip(string skipReason, ref int skippedHuman, ref int skippedNoModule, ref int skippedAlreadyStocked, ref int skippedNoInventory)
		{
			switch (skipReason)
			{
			case "human-controlled":
				skippedHuman++;
				break;
			case "no-modules":
			case "no-purchase-producing-module":
				skippedNoModule++;
				break;
			case "already-stocked":
				skippedAlreadyStocked++;
				break;
			case "no-inventory":
				skippedNoInventory++;
				break;
			}
		}

		private static bool TryRefreshPurchaseStock(ModulesComponent modules, SimTime now, string source, out PurchaseStockRefreshResult result)
		{
			result = new PurchaseStockRefreshResult();
			if (modules == null)
			{
				result.Reason = "no-modules";
				return false;
			}

			if (!TryGetPurchaseStockState(modules, out bool hasPurchaseProducingModule, out Fixnum currentBuyOfferTotal, out Fixnum targetBuyOfferTotal)
				|| !hasPurchaseProducingModule)
			{
				result.Reason = "no-purchase-producing-module";
				return false;
			}

			result.Before = currentBuyOfferTotal;
			result.Target = targetBuyOfferTotal;
			Fixnum previousBuyOfferTotal = currentBuyOfferTotal;
			int stableBelowTargetPasses = 0;
			int maxPasses = Math.Max(1, RuntimePurchaseStockRefreshMaxCatchupPasses);
			for (int pass = 0; pass < maxPasses; pass++)
			{
				if (IsPurchaseStockAtTarget(currentBuyOfferTotal, targetBuyOfferTotal))
				{
					result.After = currentBuyOfferTotal;
					result.Reason = "already-at-target";
					result.Partial = false;
					return currentBuyOfferTotal.IsPositive;
				}

				if (!TryForceBusinessModulesUpdate(modules, now, source))
				{
					result.After = currentBuyOfferTotal;
					result.Reason = "update-failed";
					result.Partial = currentBuyOfferTotal.IsPositive && targetBuyOfferTotal.IsPositive && currentBuyOfferTotal < targetBuyOfferTotal;
					return false;
				}

				result.Passes++;
				if (!TryGetPurchaseStockState(modules, out hasPurchaseProducingModule, out currentBuyOfferTotal, out targetBuyOfferTotal)
					|| !hasPurchaseProducingModule)
				{
					result.After = currentBuyOfferTotal;
					result.Target = targetBuyOfferTotal;
					result.Reason = "state-lost";
					return false;
				}

				result.Target = targetBuyOfferTotal;
				if (IsPurchaseStockAtTarget(currentBuyOfferTotal, targetBuyOfferTotal))
				{
					result.After = currentBuyOfferTotal;
					result.Reason = "target-reached";
					result.Partial = false;
					return true;
				}

				if (currentBuyOfferTotal <= previousBuyOfferTotal)
				{
					stableBelowTargetPasses++;
					if (stableBelowTargetPasses >= 2 || result.Passes >= maxPasses)
					{
						TryTopOffPurchaseStockToTargets(modules, ref result, currentBuyOfferTotal, targetBuyOfferTotal);
						return result.DirectTopOff || currentBuyOfferTotal.IsPositive;
					}
					continue;
				}

				stableBelowTargetPasses = 0;
				previousBuyOfferTotal = currentBuyOfferTotal;
			}

			result.After = currentBuyOfferTotal;
			result.Partial = currentBuyOfferTotal.IsPositive && targetBuyOfferTotal.IsPositive && currentBuyOfferTotal < targetBuyOfferTotal;
			result.Reason = currentBuyOfferTotal.IsPositive ? "max-passes-positive" : "max-passes-empty";
			TryTopOffPurchaseStockToTargets(modules, ref result, currentBuyOfferTotal, targetBuyOfferTotal);
			return result.DirectTopOff || currentBuyOfferTotal.IsPositive;
		}

		private static bool TryGetPurchaseStockState(
			ModulesComponent modules,
			out bool hasPurchaseProducingModule,
			out Fixnum currentBuyOfferTotal,
			out Fixnum targetBuyOfferTotal)
		{
			hasPurchaseProducingModule = false;
			currentBuyOfferTotal = Fixnum.ZERO;
			targetBuyOfferTotal = Fixnum.ZERO;
			if (modules?.bizmodules == null || modules.inventory == null)
			{
				return false;
			}

			try
			{
				foreach (IBizModule bizModule in modules.bizmodules)
				{
					IEnumerable<MfgItem> items = bizModule?.ProduceAllItemsInCurrentRecipe();
					if (items == null)
					{
						continue;
					}

					foreach (MfgItem item in items)
					{
						if (item.consumed)
						{
							continue;
						}

						hasPurchaseProducingModule = true;
						Fixnum storedQty = modules.inventory.data.Get(item.id).qty;
						if (storedQty.IsPositive)
						{
							currentBuyOfferTotal += storedQty;
						}
						targetBuyOfferTotal += FindPurchaseStockTargetFloor(bizModule, item.id);
					}
				}

				return true;
			}
			catch
			{
			}

			return false;
		}

		private static void TryTopOffPurchaseStockToTargets(
			ModulesComponent modules,
			ref PurchaseStockRefreshResult result,
			Fixnum currentBuyOfferTotal,
			Fixnum targetBuyOfferTotal)
		{
			result.After = currentBuyOfferTotal;
			result.Target = targetBuyOfferTotal;
			result.Partial = currentBuyOfferTotal.IsPositive && targetBuyOfferTotal.IsPositive && currentBuyOfferTotal < targetBuyOfferTotal;
			result.Reason = currentBuyOfferTotal.IsPositive ? "stock-stable-below-target" : "stock-still-empty";
			if (modules?.bizmodules == null || modules.inventory == null)
			{
				return;
			}

			try
			{
				Dictionary<Label, Fixnum> targetByResource = new Dictionary<Label, Fixnum>();
				foreach (IBizModule bizModule in modules.bizmodules)
				{
					IEnumerable<MfgItem> items = bizModule?.ProduceAllItemsInCurrentRecipe();
					if (items == null)
					{
						continue;
					}

					foreach (MfgItem item in items)
					{
						if (item.consumed)
						{
							continue;
						}

						Fixnum targetFloor = FindPurchaseStockTargetFloor(bizModule, item.id);
						if (!targetFloor.IsPositive)
						{
							continue;
						}

						if (targetByResource.TryGetValue(item.id, out Fixnum existing))
						{
							targetByResource[item.id] = existing + targetFloor;
						}
						else
						{
							targetByResource[item.id] = targetFloor;
						}
					}
				}

				if (targetByResource.Count == 0)
				{
					return;
				}

				Fixnum added = Fixnum.ZERO;
				Fixnum after = Fixnum.ZERO;
				Fixnum target = Fixnum.ZERO;
				foreach (KeyValuePair<Label, Fixnum> pair in targetByResource)
				{
					Fixnum current = modules.inventory.data.Get(pair.Key).qty;
					if (current < pair.Value)
					{
						Fixnum delta = pair.Value - current;
						if (modules.inventory.data.Increment(pair.Key, delta))
						{
							added += delta;
							current = pair.Value;
						}
					}

					if (current.IsPositive)
					{
						after += current;
					}
					target += pair.Value;
				}

				if (!added.IsPositive)
				{
					return;
				}

				result.DirectTopOff = true;
				result.TopOffAdded = added;
				result.After = after;
				result.Target = target;
				result.Partial = after < target;
				result.Reason = result.Partial ? "direct-target-topoff-partial" : "direct-target-topoff";
			}
			catch (Exception ex)
			{
				result.Reason = "direct-target-topoff-error-" + ex.GetType().Name;
			}
		}

		private static Fixnum FindPurchaseStockTargetFloor(IBizModule bizModule, Label resourceId)
		{
			try
			{
				if (!(bizModule?.LocData is Recipe recipe))
				{
					return Fixnum.ZERO;
				}

				SellOffElement? selloff = recipe.FindSellOffByID(resourceId);
				if (selloff.HasValue && selloff.Value.above.IsPositive)
				{
					return selloff.Value.above;
				}
			}
			catch
			{
			}

			return Fixnum.ZERO;
		}

		private static bool IsPurchaseStockAtTarget(Fixnum currentBuyOfferTotal, Fixnum targetBuyOfferTotal)
		{
			if (!currentBuyOfferTotal.IsPositive)
			{
				return false;
			}

			return !targetBuyOfferTotal.IsPositive || currentBuyOfferTotal >= targetBuyOfferTotal;
		}

		private static bool TryForceBusinessModulesUpdate(ModulesComponent modules, SimTime now, string source)
		{
			if (modules == null)
			{
				return false;
			}

			try
			{
				MethodInfo method = _modulesComponentDoUpdateMethod ?? (_modulesComponentDoUpdateMethod = AccessTools.Method(typeof(ModulesComponent), "DoUpdate", new[]
				{
					typeof(SimTime),
					typeof(bool)
				}));
				if (method == null)
				{
					return false;
				}

				method.Invoke(modules, new object[]
				{
					now,
					true
				});
				return true;
			}
			catch (Exception ex)
			{
				AfterProhibitionEconomyPlugin.Log?.LogWarning(
					"purchase-stock-refresh failed-business source=" + source +
					" building=" + (modules.entity?.Id.ToString() ?? "null") +
					" error=" + ex.GetType().Name + ":" + ex.Message);
				return false;
			}
		}

		private static void LogBusinessPurchaseStockRefresh(string source, Entity building, PurchaseStockRefreshResult result)
		{
			if (!AfterProhibitionEconomyPlugin.ShouldLogRuntimeEconomyRoutine())
			{
				return;
			}

			Entity biz = BuildingUtil.FindBizForBuilding(building);
			string key = (building?.Id.ToString() ?? "null") + "|" + (biz?.Id.ToString() ?? "null");
			if (!LoggedRefreshedBusinesses.Add(key))
			{
				return;
			}

			AfterProhibitionEconomyPlugin.Log?.LogInfo(
				"purchase-stock-refreshed source=" + source +
				" building=" + (building?.Id.ToString() ?? "null") +
				" biz=" + (biz?.Id.ToString() ?? "null") +
				" passes=" + result.Passes +
				" before=" + result.Before +
				" after=" + result.After +
				" target=" + result.Target +
				" partial=" + result.Partial +
				" directTopOff=" + result.DirectTopOff +
				" topOffAdded=" + result.TopOffAdded +
				" reason=" + result.Reason);
		}

		private static void LogInteractivePurchaseStockRefresh(string source, Entity building, bool refreshed, string reason, PurchaseStockRefreshResult result)
		{
			Entity biz = BuildingUtil.FindBizForBuilding(building);
			AfterProhibitionEconomyPlugin.Log?.LogInfo(
				"purchase-stock-interactive-refresh source=" + source +
				" building=" + (building?.Id.ToString() ?? "null") +
				" biz=" + (biz?.Id.ToString() ?? "null") +
				" refreshed=" + refreshed +
				" passes=" + result.Passes +
				" before=" + result.Before +
				" after=" + result.After +
				" target=" + result.Target +
				" partial=" + result.Partial +
				" directTopOff=" + result.DirectTopOff +
				" topOffAdded=" + result.TopOffAdded +
				" reason=" + (reason ?? "unknown"));
		}

		private struct PurchaseStockRefreshResult
		{
			internal int Passes;
			internal Fixnum Before;
			internal Fixnum After;
			internal Fixnum Target;
			internal Fixnum TopOffAdded;
			internal bool Partial;
			internal bool DirectTopOff;
			internal string Reason;
		}

		private static void TrackCivicPurchaseAccessSummary(
			Entity building,
			ref int banks,
			ref int banksAccess,
			ref int banksBlocked,
			ref int warehouses,
			ref int warehousesAccess,
			ref int warehousesBlocked)
		{
			if (!IsLikelyCivicPurchaseCandidate(building))
			{
				return;
			}

			CivicPurchaseAccessSummary summary = CivicPurchaseAccessClassifier.Classify(building?.Id ?? EntityID.INVALID);
			if (summary.IsBank)
			{
				banks++;
				if (summary.Access)
				{
					banksAccess++;
				}
				else
				{
					banksBlocked++;
					LogBlockedCivicPurchaseAccess("bank", summary);
				}
				return;
			}

			if (summary.IsWarehouse)
			{
				warehouses++;
				if (summary.Access)
				{
					warehousesAccess++;
				}
				else
				{
					warehousesBlocked++;
					LogBlockedCivicPurchaseAccess("warehouse", summary);
				}
			}
		}

		private static void LogBlockedCivicPurchaseAccess(string type, CivicPurchaseAccessSummary summary)
		{
			if (summary == null)
			{
				return;
			}

			string key = type + "|" + summary.BuildingID.id + "|" + summary.BizID.id + "|" + summary.Reason;
			if (!LoggedBlockedCivicPurchaseAccess.Add(key))
			{
				return;
			}

			AfterProhibitionEconomyPlugin.Log?.LogInfo(
				"civic-purchase-runtime-blocked type=" + type + " " +
				summary.FormatBridgeSummary());
		}

		private static bool IsLikelyCivicPurchaseCandidate(Entity building)
		{
			if (building == null)
			{
				return false;
			}

			string buildingTemplate = building.config?.Template.String ?? string.Empty;
			if (ContainsCandidateToken(buildingTemplate))
			{
				return true;
			}

			Entity biz = BuildingUtil.FindBizForBuilding(building);
			string bizTemplate = biz?.config?.Template.String ?? string.Empty;
			return ContainsCandidateToken(bizTemplate);
		}

		private static bool ContainsCandidateToken(string template)
		{
			return !string.IsNullOrEmpty(template)
				&& (template.IndexOf("bank", StringComparison.OrdinalIgnoreCase) >= 0
					|| template.IndexOf("warehouse", StringComparison.OrdinalIgnoreCase) >= 0
					|| template.IndexOf("wholesale", StringComparison.OrdinalIgnoreCase) >= 0);
		}
	}
}
