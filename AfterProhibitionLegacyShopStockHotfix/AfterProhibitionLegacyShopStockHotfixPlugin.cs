using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using Game.Core;
using Game.Services;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Player;
using Game.Session.Player.KB;
using Game.Session.Sim;
using Game.Session.Sim.Modules;
using Game.UI.Session.Crew;
using HarmonyLib;
using SomaSim.Util;

namespace AfterProhibitionLegacyShopStockHotfix
{
	[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
	[BepInDependency("com.mods.gameplaytweaks", BepInDependency.DependencyFlags.HardDependency)]
	public sealed class AfterProhibitionLegacyShopStockHotfixPlugin : BaseUnityPlugin
	{
		public const string PluginGuid = "afterprohibition.legacyshopstockhotfix";
		public const string PluginName = "After Prohibition Legacy Shop Stock Hotfix";
		public const string PluginVersion = "1.3.0";

		internal static ManualLogSource Log { get; private set; }

		internal static ConfigEntry<bool> EnableStableExistingBusinessModules { get; private set; }

		internal static ConfigEntry<bool> EnableLegacySellOfferZeroCapFallback { get; private set; }

		internal static ConfigEntry<bool> EnableLegacyCrewHireListNullGuard { get; private set; }

		internal static ConfigEntry<bool> EnableLegacyNpcPurchaseStockRefresh { get; private set; }

		internal static ConfigEntry<bool> EnableLegacyNpcPurchaseStockRefreshSummaryWhenNoCandidates { get; private set; }

		internal static ConfigEntry<int> SampleLogLimit { get; private set; }

		internal static ConfigEntry<int> SellCapSampleLogLimit { get; private set; }

		internal static ConfigEntry<int> CrewHireListSampleLogLimit { get; private set; }

		internal static ConfigEntry<int> StockRefreshSampleLogLimit { get; private set; }

		private Harmony _harmony;

		private void Awake()
		{
			Log = Logger;
			EnableStableExistingBusinessModules = Config.Bind(
				"Features",
				"EnableStableExistingBusinessModules",
				true,
				"Prevents the legacy GameplayTweaks empty-business repair from re-randomizing an NPC shop's existing supply modules on later turns.");

			EnableLegacySellOfferZeroCapFallback = Config.Bind(
				"Features",
				"EnableLegacySellOfferZeroCapFallback",
				true,
				"Allows human players to sell resources to legacy shop offers when the game advertises the item but computes a zero one-time sale cap.");

			EnableLegacyCrewHireListNullGuard = Config.Bind(
				"Features",
				"EnableLegacyCrewHireListNullGuard",
				true,
				"Prevents the archived crew information hire list from breaking when a known business points at a missing or invalid hire candidate.");

			EnableLegacyNpcPurchaseStockRefresh = Config.Bind(
				"Features",
				"EnableLegacyNpcPurchaseStockRefresh",
				true,
				"Refreshes NPC shop stock when legacy shop/source/booze modules are installed but advertise no player-buy inventory.");

			EnableLegacyNpcPurchaseStockRefreshSummaryWhenNoCandidates = Config.Bind(
				"Features",
				"EnableLegacyNpcPurchaseStockRefreshSummaryWhenNoCandidates",
				true,
				"Logs a summary line for the legacy NPC purchase stock refresh even when no empty-stock candidates are found.");

			SampleLogLimit = Config.Bind(
				"Logging",
				"StableExistingBusinessModuleSampleLimit",
				8,
				"Maximum sample lines to log when existing business module lists are reused.");

			SellCapSampleLogLimit = Config.Bind(
				"Logging",
				"LegacySellOfferZeroCapSampleLimit",
				12,
				"Maximum sample lines to log when zero-cap sell offers are repaired.");

			CrewHireListSampleLogLimit = Config.Bind(
				"Logging",
				"LegacyCrewHireListSampleLimit",
				12,
				"Maximum sample lines to log when invalid crew hire list candidates are skipped.");

			StockRefreshSampleLogLimit = Config.Bind(
				"Logging",
				"LegacyNpcPurchaseStockRefreshSampleLimit",
				12,
				"Maximum sample lines to log when legacy NPC shop purchase stock is refreshed.");

			_harmony = new Harmony(PluginGuid);
			TryPatchGameplayTweaks();
			TryPatchSellOfferCaps();
			TryPatchCrewHireList();
			TryPatchNpcPurchaseStockRefresh();
			Logger.LogInfo($"{PluginName} {PluginVersion} loaded.");
		}

		private void OnDestroy()
		{
			try
			{
				_harmony?.UnpatchSelf();
			}
			catch (Exception ex)
			{
				Logger.LogWarning("Could not unpatch Harmony hooks: " + ex.Message);
			}
		}

		private void TryPatchGameplayTweaks()
		{
			try
			{
				Type compatibilityPatchType = AccessTools.TypeByName("GameplayTweaks.GameplayTweaksPlugin+DirtyCashEconomyCompatibilityPatch");
				if (compatibilityPatchType == null)
				{
					Logger.LogWarning("stable-shop-stock patch skipped reason=compatibility-patch-type-missing");
					return;
				}

				MethodInfo target = AccessTools.Method(compatibilityPatchType, "GetBusinessModuleIdsForRepair");
				if (target == null)
				{
					Logger.LogWarning("stable-shop-stock patch skipped reason=target-method-missing");
					return;
				}

				_harmony.Patch(
					target,
					prefix: new HarmonyMethod(typeof(StableBusinessModuleSelectionPatch), nameof(StableBusinessModuleSelectionPatch.Prefix)));
				Logger.LogInfo("stable-shop-stock patch applied target=GameplayTweaks.DirtyCashEconomyCompatibilityPatch.GetBusinessModuleIdsForRepair");
			}
			catch (Exception ex)
			{
				Logger.LogWarning("stable-shop-stock patch setup failed: " + ex.GetType().Name + ": " + ex.Message);
			}
		}

		private void TryPatchSellOfferCaps()
		{
			try
			{
				Type orderType = AccessTools.TypeByName("Game.UI.Session.Convo.ViewItemPicker+Order");
				ConstructorInfo orderConstructor = orderType == null ? null : AccessTools.Constructor(orderType, new[] { typeof(BuySellElement), typeof(bool), typeof(VisitState) });
				if (orderConstructor == null)
				{
					Logger.LogWarning("legacy-sell-cap picker patch skipped reason=order-constructor-missing");
				}
				else
				{
					_harmony.Patch(
						orderConstructor,
						postfix: new HarmonyMethod(typeof(LegacySellOfferZeroCapPatch), nameof(LegacySellOfferZeroCapPatch.OrderConstructorPostfix)));
					Logger.LogInfo("legacy-sell-cap picker patch applied target=Game.UI.Session.Convo.ViewItemPicker+Order..ctor");
				}

				MethodInfo findMaxTarget = AccessTools.Method(
					typeof(BuySellUtils),
					nameof(BuySellUtils.FindMaxAffordedToBuySell),
					new[] { typeof(PlayerInfo), typeof(Entity), typeof(Entity), typeof(InventoryModule), typeof(Label), typeof(Fixnum), typeof(bool), typeof(CrewAssignment) });
				if (findMaxTarget == null)
				{
					Logger.LogWarning("legacy-sell-cap max patch skipped reason=find-max-method-missing");
				}
				else
				{
					_harmony.Patch(
						findMaxTarget,
						postfix: new HarmonyMethod(typeof(LegacySellOfferZeroCapPatch), nameof(LegacySellOfferZeroCapPatch.FindMaxPostfix)));
					Logger.LogInfo("legacy-sell-cap max patch applied target=BuySellUtils.FindMaxAffordedToBuySell");
				}
			}
			catch (Exception ex)
			{
				Logger.LogWarning("legacy-sell-cap patch setup failed: " + ex.GetType().Name + ": " + ex.Message);
			}
		}

		private void TryPatchCrewHireList()
		{
			try
			{
				MethodInfo target = AccessTools.Method(typeof(CrewInfoListDialog), "CompilePersonList");
				if (target == null)
				{
					Logger.LogWarning("legacy-crew-hire-list patch skipped reason=compile-person-list-missing");
					return;
				}

				_harmony.Patch(
					target,
					prefix: new HarmonyMethod(typeof(LegacyCrewHireListNullGuardPatch), nameof(LegacyCrewHireListNullGuardPatch.Prefix)));
				Logger.LogInfo("legacy-crew-hire-list patch applied target=CrewInfoListDialog.CompilePersonList");
			}
			catch (Exception ex)
			{
				Logger.LogWarning("legacy-crew-hire-list patch setup failed: " + ex.GetType().Name + ": " + ex.Message);
			}
		}

		private void TryPatchNpcPurchaseStockRefresh()
		{
			try
			{
				MethodInfo updateBusinessModulesMethod = AccessTools.Method(typeof(BusinessUpdate), "UpdateBusinessModules");
				if (updateBusinessModulesMethod == null)
				{
					Logger.LogWarning("legacy-stock-refresh patch skipped target=BusinessUpdate.UpdateBusinessModules reason=method-not-found");
				}
				else
				{
					_harmony.Patch(
						updateBusinessModulesMethod,
						postfix: new HarmonyMethod(typeof(LegacyNpcPurchaseStockRefreshPatch), nameof(LegacyNpcPurchaseStockRefreshPatch.UpdateBusinessModulesPostfix)));
					Logger.LogInfo("legacy-stock-refresh patch applied target=BusinessUpdate.UpdateBusinessModules");
				}

				MethodInfo playerTurnStartedMethod = AccessTools.Method(typeof(PlayerInfo), "OnPlayerTurnStarted");
				if (playerTurnStartedMethod == null)
				{
					Logger.LogWarning("legacy-stock-refresh patch skipped target=PlayerInfo.OnPlayerTurnStarted reason=method-not-found");
				}
				else
				{
					_harmony.Patch(
						playerTurnStartedMethod,
						postfix: new HarmonyMethod(typeof(LegacyNpcPurchaseStockRefreshPatch), nameof(LegacyNpcPurchaseStockRefreshPatch.PlayerInfoOnPlayerTurnStartedPostfix)));
					Logger.LogInfo("legacy-stock-refresh patch applied target=PlayerInfo.OnPlayerTurnStarted");
				}
			}
			catch (Exception ex)
			{
				Logger.LogWarning("legacy-stock-refresh patch setup failed: " + ex.GetType().Name + ": " + ex.Message);
			}
		}
	}

	internal static class StableBusinessModuleSelectionPatch
	{
		private static readonly HashSet<string> LoggedSamples = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		public static bool Prefix(Entity biz, ref List<Label> __result)
		{
			if (!(AfterProhibitionLegacyShopStockHotfixPlugin.EnableStableExistingBusinessModules?.Value ?? false))
			{
				return true;
			}

			List<Label> existingModuleIds = biz?.data?.biz?.modules;
			if (existingModuleIds == null || existingModuleIds.Count == 0)
			{
				return true;
			}

			__result = new List<Label>(existingModuleIds);
			LogSample(biz, existingModuleIds);
			return false;
		}

		private static void LogSample(Entity biz, List<Label> existingModuleIds)
		{
			try
			{
				int limit = Math.Max(0, AfterProhibitionLegacyShopStockHotfixPlugin.SampleLogLimit?.Value ?? 0);
				if (LoggedSamples.Count >= limit)
				{
					return;
				}

				string key = biz?.Id.ToString() ?? "null";
				if (!LoggedSamples.Add(key))
				{
					return;
				}

				AfterProhibitionLegacyShopStockHotfixPlugin.Log?.LogInfo(
					"stable-shop-stock reused-existing-modules biz=" + key +
					" template=" + (biz?.config?.Template.String ?? "(null)") +
					" modules=" + string.Join(",", existingModuleIds.ConvertAll(label => label.ToString()).ToArray()));
			}
			catch
			{
				// Logging should never affect the hotfix path.
			}
		}
	}

	internal static class LegacySellOfferZeroCapPatch
	{
		private static readonly HashSet<string> LoggedSamples = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		public static void OrderConstructorPostfix(object __instance, BuySellElement elt, bool isPlayerBuying, VisitState visit)
		{
			try
			{
				if (!IsEnabled() || isPlayerBuying || !elt.item.consumed || !IsHumanVisit(visit))
				{
					return;
				}

				FieldInfo maxDeltaField = AccessTools.Field(__instance.GetType(), "maxdelta");
				if (maxDeltaField == null)
				{
					return;
				}

				int currentMax = (int)maxDeltaField.GetValue(__instance);
				if (currentMax > 0)
				{
					return;
				}

				int fallback = FindFallbackSellCapFromVisit(visit, elt.item.id);
				if (fallback <= 0)
				{
					return;
				}

				maxDeltaField.SetValue(__instance, fallback);
				LogSample("picker", visit.building, visit.biz, elt.item.id, currentMax, fallback);
			}
			catch (Exception ex)
			{
				AfterProhibitionLegacyShopStockHotfixPlugin.Log?.LogWarning("legacy-sell-cap picker patch failed: " + ex.GetType().Name + ": " + ex.Message);
			}
		}

		public static void FindMaxPostfix(PlayerInfo player, Entity building, Entity biz, InventoryModule target, Label item, Fixnum desiredQty, bool isPlayerBuying, CrewAssignment crew, ref Fixnum __result)
		{
			try
			{
				if (!IsEnabled() || isPlayerBuying || player?.IsHuman != true || __result > 0 || desiredQty <= 0)
				{
					return;
				}

				if (!BuildingAdvertisesHumanSellOffer(building, player.PID, item))
				{
					return;
				}

				Fixnum fallback = desiredQty.Floor();
				Fixnum targetQty = target?.data?.Get(item).qty ?? (Fixnum)0;
				if (targetQty.IsPositive && targetQty < fallback)
				{
					fallback = targetQty.Floor();
				}

				if (fallback <= 0)
				{
					return;
				}

				__result = fallback;
				LogSample("findmax", building, biz, item, 0, fallback.IntFloor());
			}
			catch (Exception ex)
			{
				AfterProhibitionLegacyShopStockHotfixPlugin.Log?.LogWarning("legacy-sell-cap max patch failed: " + ex.GetType().Name + ": " + ex.Message);
			}
		}

		private static bool IsEnabled()
		{
			return AfterProhibitionLegacyShopStockHotfixPlugin.EnableLegacySellOfferZeroCapFallback?.Value ?? false;
		}

		private static bool IsHumanVisit(VisitState visit)
		{
			return visit?.GetPlayer()?.IsHuman == true;
		}

		private static int FindFallbackSellCapFromVisit(VisitState visit, Label item)
		{
			InventoryModule vehicleInventory = visit?.vehicle?.components?.modules?.inventory;
			if (vehicleInventory == null)
			{
				return 0;
			}

			Fixnum playerQty = vehicleInventory.data.Get(item).qty;
			if (playerQty <= 0)
			{
				return 0;
			}

			int fallback = playerQty.Floor().IntFloor();
			InventoryModule buildingInventory = visit.building?.components?.modules?.inventory;
			Resource resource = Resource.Find(item);
			if (buildingInventory != null && resource != null)
			{
				int fit = buildingInventory.HowManyResourcesCanFit(resource);
				if (fit > 0)
				{
					fallback = Math.Min(fallback, fit);
				}
			}

			return Math.Max(0, fallback);
		}

		private static bool BuildingAdvertisesHumanSellOffer(Entity building, PlayerID pid, Label item)
		{
			try
			{
				IEnumerable<BuySellElement> offers = building?.components?.modules?.ProduceAllItemsPlayerCanBuyOrSell(pid, playerBuys: false, playerSells: true);
				if (offers == null)
				{
					return false;
				}

				foreach (BuySellElement offer in offers)
				{
					if (offer.item.consumed && offer.item.id == item)
					{
						return true;
					}
				}
			}
			catch
			{
				return false;
			}

			return false;
		}

		private static void LogSample(string path, Entity building, Entity biz, Label item, int oldCap, int newCap)
		{
			try
			{
				int limit = Math.Max(0, AfterProhibitionLegacyShopStockHotfixPlugin.SellCapSampleLogLimit?.Value ?? 0);
				if (LoggedSamples.Count >= limit)
				{
					return;
				}

				string key = path + "|" + (building?.Id.ToString() ?? "null") + "|" + item;
				if (!LoggedSamples.Add(key))
				{
					return;
				}

				AfterProhibitionLegacyShopStockHotfixPlugin.Log?.LogInfo(
					"legacy-sell-cap fallback path=" + path +
					" building=" + (building?.Id.ToString() ?? "(null)") +
					" biz=" + (biz?.Id.ToString() ?? "(null)") +
					" resource=" + item +
					" oldCap=" + oldCap +
					" newCap=" + newCap);
			}
			catch
			{
				// Logging should never affect the hotfix path.
			}
		}
	}

	internal static class LegacyCrewHireListNullGuardPatch
	{
		private static readonly HashSet<string> LoggedSamples = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		private static Type CardInfoType;
		private static FieldInfo CrewField;
		private static FieldInfo HirelingField;
		private static FieldInfo GotoLocationField;

		public static bool Prefix(object results, Trait filterTrait)
		{
			if (!(AfterProhibitionLegacyShopStockHotfixPlugin.EnableLegacyCrewHireListNullGuard?.Value ?? false))
			{
				return true;
			}

			IList list = results as IList;
			if (list == null)
			{
				return true;
			}

			try
			{
				EnsureReflection();
				if (CardInfoType == null || CrewField == null || HirelingField == null || GotoLocationField == null)
				{
					return true;
				}

				list.Clear();
				AddCrewInfos(list, living: true, filterTrait);
				AddCrewInfos(list, living: false, filterTrait);
				AddPotentialInfos(list, filterTrait);
				return false;
			}
			catch (Exception ex)
			{
				AfterProhibitionLegacyShopStockHotfixPlugin.Log?.LogWarning("legacy-crew-hire-list patch failed; falling back to vanilla: " + ex.GetType().Name + ": " + ex.Message);
				return true;
			}
		}

		private static void EnsureReflection()
		{
			if (CardInfoType != null)
			{
				return;
			}

			CardInfoType = AccessTools.TypeByName("Game.UI.Session.Crew.CrewInfoListDialog+CardInfo");
			if (CardInfoType == null)
			{
				return;
			}

			CrewField = AccessTools.Field(CardInfoType, "crew");
			HirelingField = AccessTools.Field(CardInfoType, "hireling");
			GotoLocationField = AccessTools.Field(CardInfoType, "gotoLocation");
		}

		private static void AddCrewInfos(IList results, bool living, Trait filterTrait)
		{
			List<object> cards = new List<object>();
			foreach (CrewAssignment crew in global::Game.Game.ctx.players.Human.crew.AllCrew)
			{
				if (crew.IsNotDead != living)
				{
					continue;
				}

				Entity peep = SafeGetCrewPeep(crew);
				if (!IsValidPeep(peep))
				{
					LogSample("crew-invalid", peep, EntityID.INVALID, null);
					continue;
				}

				if (!MatchesTrait(peep, filterTrait))
				{
					continue;
				}

				object card = Activator.CreateInstance(CardInfoType);
				CrewField.SetValue(card, crew);
				GotoLocationField.SetValue(card, SafeGetCrewTarget(crew));
				cards.Add(card);
			}

			cards.Sort(CompareCards);
			foreach (object card in cards)
			{
				results.Add(card);
			}
		}

		private static void AddPotentialInfos(IList results, Trait filterTrait)
		{
			PlayerInfo human = global::Game.Game.ctx.players.Human;
			Dictionary<EntityID, object> cardsByCandidate = new Dictionary<EntityID, object>(new EntityIDEqualityComparer());
			foreach (Node node in human.territory.GetAllKnownNodesExpensive())
			{
				if (node?.interesting == null)
				{
					continue;
				}

				foreach (EntityID buildingId in node.interesting)
				{
					Entity building = buildingId.FindEntity();
					if (building == null)
					{
						LogSample("building-missing", null, buildingId, null);
						continue;
					}

					if (!SafeIsScoped(human, building))
					{
						continue;
					}

					if (!SafeOwnerCanHire(human, buildingId))
					{
						continue;
					}

					Entity owner = BuildingUtil.FindOwnerOrManagerForAnyBuilding(building);
					if (owner == null)
					{
						LogSample("owner-missing", null, buildingId, null);
						continue;
					}

					EntityID candidateId = human.crew.CrewGrowth.GetBestCrewCandidateFrom(owner.Id);
					if (candidateId.IsNotValid)
					{
						LogSample("candidate-invalid", null, buildingId, owner);
						continue;
					}

					if (cardsByCandidate.ContainsKey(candidateId))
					{
						continue;
					}

					Entity hireling = candidateId.FindEntity();
					if (!IsValidPeep(hireling))
					{
						LogSample("candidate-missing", hireling, buildingId, owner);
						continue;
					}

					if (!MatchesTrait(hireling, filterTrait))
					{
						continue;
					}

					object card = Activator.CreateInstance(CardInfoType);
					HirelingField.SetValue(card, hireling);
					GotoLocationField.SetValue(card, building);
					cardsByCandidate.Add(candidateId, card);
				}
			}

			List<object> cards = new List<object>(cardsByCandidate.Values);
			cards.Sort(CompareCards);
			foreach (object card in cards)
			{
				results.Add(card);
			}
		}

		private static Entity SafeGetCrewPeep(CrewAssignment crew)
		{
			try
			{
				return crew.GetPeep();
			}
			catch
			{
				return null;
			}
		}

		private static Entity SafeGetCrewTarget(CrewAssignment crew)
		{
			try
			{
				return crew.GetTarget();
			}
			catch
			{
				return null;
			}
		}

		private static bool SafeIsScoped(PlayerInfo human, Entity building)
		{
			try
			{
				return human.territory.IsScoped(building);
			}
			catch
			{
				return false;
			}
		}

		private static bool SafeOwnerCanHire(PlayerInfo human, EntityID buildingId)
		{
			try
			{
				KBResult status = human.kb.GetStatusForBuilding(buildingId, PlayerKBQueryNames.OWNER_CAN_HIRE_CREW);
				return status.IsValid && status.passed;
			}
			catch
			{
				return false;
			}
		}

		private static bool IsValidPeep(Entity peep)
		{
			return peep?.data?.person != null;
		}

		private static bool MatchesTrait(Entity peep, Trait filterTrait)
		{
			try
			{
				return filterTrait == null || peep.data.person.traitIds.Contains(filterTrait.id);
			}
			catch
			{
				return false;
			}
		}

		private static int CompareCards(object left, object right)
		{
			return string.Compare(GetSortName(left), GetSortName(right), StringComparison.OrdinalIgnoreCase);
		}

		private static string GetSortName(object card)
		{
			Entity peep = HirelingField.GetValue(card) as Entity;
			if (peep == null)
			{
				CrewAssignment crew = (CrewAssignment)CrewField.GetValue(card);
				peep = SafeGetCrewPeep(crew);
			}

			return peep?.data?.person?.last ?? peep?.data?.person?.FullName ?? "";
		}

		private static void LogSample(string reason, Entity peep, EntityID buildingId, Entity owner)
		{
			try
			{
				int limit = Math.Max(0, AfterProhibitionLegacyShopStockHotfixPlugin.CrewHireListSampleLogLimit?.Value ?? 0);
				if (LoggedSamples.Count >= limit)
				{
					return;
				}

				string key = reason + "|" + buildingId + "|" + (peep?.Id.ToString() ?? "null") + "|" + (owner?.Id.ToString() ?? "null");
				if (!LoggedSamples.Add(key))
				{
					return;
				}

				AfterProhibitionLegacyShopStockHotfixPlugin.Log?.LogInfo(
					"legacy-crew-hire-list skipped reason=" + reason +
					" building=" + buildingId +
					" peep=" + (peep?.Id.ToString() ?? "(null)") +
					" owner=" + (owner?.Id.ToString() ?? "(null)"));
			}
			catch
			{
				// Logging should never affect the UI guard.
			}
		}
	}

	internal static class LegacyNpcPurchaseStockRefreshPatch
	{
		private static readonly Dictionary<string, int> LastRefreshDayBySource = new Dictionary<string, int>(StringComparer.Ordinal);
		private static readonly HashSet<string> LoggedSamples = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		private static MethodInfo ModulesComponentDoUpdateMethod;
		private static MethodInfo EconomyOwnsPurchaseStockRefreshMethod;
		private static bool LoggedEconomyDelegation;

		public static void UpdateBusinessModulesPostfix(bool initial)
		{
			if (!IsEnabled())
			{
				return;
			}

			Run(initial ? "business-update-initial" : "business-update", force: initial);
		}

		public static void PlayerInfoOnPlayerTurnStartedPostfix(PlayerInfo __instance)
		{
			if (__instance == null || !__instance.IsHuman || !IsEnabled())
			{
				return;
			}

			Run("human-turn-start", force: false);
		}

		private static bool IsEnabled()
		{
			return AfterProhibitionLegacyShopStockHotfixPlugin.EnableLegacyNpcPurchaseStockRefresh?.Value ?? false;
		}

		private static void Run(string source, bool force)
		{
			try
			{
				if (AfterProhibitionEconomyOwnsPurchaseStockRefresh())
				{
					return;
				}

				if (global::Game.Game.ctx?.entityman == null || global::Game.Game.ctx?.clock == null)
				{
					return;
				}

				SimTime now = global::Game.Game.ctx.clock.Now;
				string sourceKey = string.IsNullOrEmpty(source) ? "unknown" : source;
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

				IEnumerable<Entity> buildings = global::Game.Game.ctx.entityman.GetCachedEntitiesBuildingsUnsafe();
				if (buildings == null)
				{
					return;
				}

				foreach (Entity building in buildings)
				{
					scanned++;
					if (!CanRefreshBusinessPurchaseStock(building, out ModulesComponent modules, out string skipReason))
					{
						skipped++;
						TrackSkip(skipReason, ref skippedHuman, ref skippedNoModule, ref skippedAlreadyStocked, ref skippedNoInventory);
						continue;
					}

					candidates++;
					if (TryForceBusinessModulesUpdate(modules, now, sourceKey) && HasPositivePlayerBuyOffer(modules))
					{
						refreshed++;
						LogSample(sourceKey, building, modules);
					}
					else
					{
						failed++;
					}
				}

				if (candidates > 0
					|| refreshed > 0
					|| failed > 0
					|| (AfterProhibitionLegacyShopStockHotfixPlugin.EnableLegacyNpcPurchaseStockRefreshSummaryWhenNoCandidates?.Value ?? false))
				{
					AfterProhibitionLegacyShopStockHotfixPlugin.Log?.LogInfo(
						"legacy-stock-refresh source=" + sourceKey +
						" scanned=" + scanned +
						" candidates=" + candidates +
						" refreshed=" + refreshed +
						" failed=" + failed +
						" skipped=" + skipped +
						" skippedHuman=" + skippedHuman +
						" skippedNoModule=" + skippedNoModule +
						" skippedAlreadyStocked=" + skippedAlreadyStocked +
						" skippedNoInventory=" + skippedNoInventory +
						" day=" + now.days);
				}
			}
			catch (Exception ex)
			{
				AfterProhibitionLegacyShopStockHotfixPlugin.Log?.LogWarning("legacy-stock-refresh failed source=" + source + " error=" + ex.GetType().Name + ":" + ex.Message);
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

			if (!HasInstalledPurchaseProducingModule(modules))
			{
				skipReason = "no-purchase-producing-module";
				return false;
			}

			if (HasPositivePlayerBuyOffer(modules))
			{
				skipReason = "already-stocked";
				return false;
			}

			return true;
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

		private static bool HasInstalledPurchaseProducingModule(ModulesComponent modules)
		{
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
						if (!item.consumed)
						{
							return true;
						}
					}
				}
			}
			catch
			{
			}

			return false;
		}

		private static bool HasPositivePlayerBuyOffer(ModulesComponent modules)
		{
			if (modules?.inventory == null)
			{
				return false;
			}

			try
			{
				foreach (BuySellElement element in modules.ProduceAllItemsPlayerCanBuyOrSell(PlayerID.HumanPlayer, playerBuys: true, playerSells: false))
				{
					if (element.qty.IsPositive)
					{
						return true;
					}
				}
			}
			catch
			{
			}

			return false;
		}

		private static bool TryForceBusinessModulesUpdate(ModulesComponent modules, SimTime now, string source)
		{
			if (modules == null)
			{
				return false;
			}

			try
			{
				MethodInfo method = ModulesComponentDoUpdateMethod ?? (ModulesComponentDoUpdateMethod = AccessTools.Method(typeof(ModulesComponent), "DoUpdate", new[]
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
				AfterProhibitionLegacyShopStockHotfixPlugin.Log?.LogWarning(
					"legacy-stock-refresh failed-business source=" + source +
					" building=" + (modules.entity?.Id.ToString() ?? "null") +
					" error=" + ex.GetType().Name + ":" + ex.Message);
				return false;
			}
		}

		private static bool AfterProhibitionEconomyOwnsPurchaseStockRefresh()
		{
			try
			{
				Type economyType = AccessTools.TypeByName("AfterProhibitionEconomy.AfterProhibitionEconomyPlugin");
				if (economyType == null)
				{
					return false;
				}

				EconomyOwnsPurchaseStockRefreshMethod = EconomyOwnsPurchaseStockRefreshMethod ?? AccessTools.Method(economyType, "OwnsPurchaseStockRefresh");
				if (EconomyOwnsPurchaseStockRefreshMethod == null)
				{
					return false;
				}

				object value = EconomyOwnsPurchaseStockRefreshMethod.Invoke(null, null);
				bool owns = value is bool enabled && enabled;
				if (owns && !LoggedEconomyDelegation)
				{
					LoggedEconomyDelegation = true;
					AfterProhibitionLegacyShopStockHotfixPlugin.Log?.LogInfo("legacy-stock-refresh delegated owner=afterprohibition.economy");
				}
				return owns;
			}
			catch
			{
				return false;
			}
		}

		private static void LogSample(string source, Entity building, ModulesComponent modules)
		{
			try
			{
				int limit = Math.Max(0, AfterProhibitionLegacyShopStockHotfixPlugin.StockRefreshSampleLogLimit?.Value ?? 0);
				if (LoggedSamples.Count >= limit)
				{
					return;
				}

				Entity biz = BuildingUtil.FindBizForBuilding(building);
				string key = (building?.Id.ToString() ?? "null") + "|" + (biz?.Id.ToString() ?? "null");
				if (!LoggedSamples.Add(key))
				{
					return;
				}

				AfterProhibitionLegacyShopStockHotfixPlugin.Log?.LogInfo(
					"legacy-stock-refreshed source=" + source +
					" building=" + (building?.Id.ToString() ?? "(null)") +
					" biz=" + (biz?.Id.ToString() ?? "(null)") +
					" template=" + (building?.config?.Template.String ?? "(null)") +
					" modules=" + FormatInstalledModuleIds(modules));
			}
			catch
			{
				// Logging should never affect the hotfix path.
			}
		}

		private static string FormatInstalledModuleIds(ModulesComponent modules)
		{
			List<IModule> slots = modules?.GetAllSlotsUnsafe();
			if (slots == null || slots.Count == 0)
			{
				return "";
			}

			List<string> ids = new List<string>();
			foreach (IModule slot in slots)
			{
				if (slot?.ModuleConfig?.Id.IsSet == true)
				{
					ids.Add(slot.ModuleConfig.Id.ToString());
				}
			}

			return string.Join(",", ids.ToArray());
		}
	}
}
