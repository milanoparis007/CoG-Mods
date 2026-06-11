using System;
using System.Collections.Generic;
using System.Reflection;
using Game.Core;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Player;
using Game.UI.Session;
using Game.UI.Session.Convo;
using HarmonyLib;
using UnityEngine;

namespace GameplayTweaks
{
public partial class GameplayTweaksPlugin
{
	internal static class CivicNonpurchaseFallbackPatch
	{
		private static readonly HashSet<string> CivicBizTemplates = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
		{
			"biz-bank",
			"biz-neighborhood-bank",
			"biz-downtown-bank",
			"biz-downtown-bank-new-york",
			"school-medium",
			"school-large-sub",
			"worship-church-regular",
			"worship-church-large"
		};

		private static readonly HashSet<string> CivicBuildingTemplates = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
		{
			"com-medium-philly-bank",
			"com-large-downtown-bank",
			"civic-school-medium",
			"school-regular",
			"school-large",
			"civic-school-large",
			"worship-regular",
			"worship-large"
		};

		private static readonly HashSet<string> LoggedEconomyCivicAccessKeys = new HashSet<string>(StringComparer.Ordinal);
		private static readonly HashSet<string> LoggedTicketBuildingGuardKeys = new HashSet<string>(StringComparer.Ordinal);
		private static readonly PropertyInfo ConvoCallbacksVisitProperty =
			typeof(ConvoCallbacks).GetProperty("Visit", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
		private static MethodInfo _afterProhibitionEconomyCivicPurchaseAccessSummaryMethod;

		public static void ApplyPatch(Harmony harmony)
		{
			try
			{
				Patch(harmony,
					AccessTools.Method(typeof(PlayerTerritory), nameof(PlayerTerritory.FindPotentialBuildingForTakeover), new Type[]
					{
						typeof(VisitState),
						typeof(bool)
					}),
					postfix: nameof(FindPotentialBuildingForTakeoverPostfix));
				Patch(harmony,
					AccessTools.Method(typeof(PlayerTerritory), nameof(PlayerTerritory.FindTakeoverData), new Type[]
					{
						typeof(VisitState),
						typeof(EntityID),
						typeof(EntityID)
					}),
					postfix: nameof(FindTakeoverDataPostfix));
				Patch(harmony,
					AccessTools.Method(typeof(PlayerTerritory), nameof(PlayerTerritory.PerformTakeover), new Type[]
					{
						typeof(CrewAssignment),
						typeof(PlayerTerritory.TakeoverData)
					}),
					prefix: nameof(PerformTakeoverPrefix));
				Patch(harmony,
					AccessTools.Method(typeof(CheckPotentialBizBuilding), nameof(CheckPotentialBizBuilding.DoesPass), new Type[]
					{
						typeof(VisitState)
					}),
					postfix: nameof(CheckPotentialBizBuildingPostfix));
				Patch(harmony,
					AccessTools.Method(typeof(SpecialBizPurchasePip), nameof(SpecialBizPurchasePip.DoesPass), new Type[]
					{
						typeof(VisitState)
					}),
					postfix: nameof(SpecialBizPurchasePipPostfix));
				Patch(harmony,
					AccessTools.Method(typeof(SpecialBizPurchaseButton), nameof(SpecialBizPurchaseButton.DoesPass), new Type[]
					{
						typeof(VisitState),
						typeof(ConvoButtonState)
					}),
					postfix: nameof(SpecialBizPurchaseButtonPostfix));
				Patch(harmony,
					AccessTools.Method(typeof(CheckCanBuySell), nameof(CheckCanBuySell.CheckAvailability), new Type[]
					{
						typeof(VisitState)
					}),
					postfix: nameof(CheckCanBuySellAvailabilityPostfix));
				Patch(harmony,
					AccessTools.Method(typeof(ConvoCallbacks), "ExecuteBuildingTakeover", new Type[]
					{
						typeof(ConvoButton)
					}),
					prefix: nameof(ExecuteBuildingTakeoverPrefix));
				Patch(harmony,
					AccessTools.Method(typeof(ConvoCallbacks), "StoreTicketBuildingChoice", new Type[]
					{
						typeof(ConvoButton)
					}),
					prefix: nameof(StoreTicketBuildingChoicePrefix));
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] Civic nonpurchase fallback patch setup failed: " + ex.Message);
			}
		}

		private static void Patch(Harmony harmony, MethodInfo method, string prefix = null, string postfix = null)
		{
			if (method == null)
			{
				return;
			}

			HarmonyMethod harmonyPrefix = string.IsNullOrEmpty(prefix) ? null : new HarmonyMethod(typeof(CivicNonpurchaseFallbackPatch), prefix);
			HarmonyMethod harmonyPostfix = string.IsNullOrEmpty(postfix) ? null : new HarmonyMethod(typeof(CivicNonpurchaseFallbackPatch), postfix);
			harmony.Patch(method, prefix: harmonyPrefix, postfix: harmonyPostfix);
		}

		private static void FindPotentialBuildingForTakeoverPostfix(VisitState visit, ref EntityID __result)
		{
			if (!__result.IsValid)
			{
				return;
			}

			Entity building = __result.FindEntity();
			if (IsCivicNonpurchaseVisit(visit) || IsCivicNonpurchaseBuilding(building))
			{
				__result = EntityID.INVALID;
			}
		}

		private static void FindTakeoverDataPostfix(VisitState visit, ref PlayerTerritory.TakeoverData __result)
		{
			if (!__result.IsValid)
			{
				__result = default(PlayerTerritory.TakeoverData);
				return;
			}

			if (!__result.buildingId.IsValid)
			{
				return;
			}

			Entity building = __result.buildingId.FindEntity();
			if (IsCivicNonpurchaseVisit(visit) || IsCivicNonpurchaseBuilding(building))
			{
				__result = default(PlayerTerritory.TakeoverData);
			}
		}

		private static bool PerformTakeoverPrefix(PlayerTerritory.TakeoverData td)
		{
			Entity building = td.FindBuilding();
			if (!IsCivicNonpurchaseBuilding(building))
			{
				return true;
			}

			Entity biz = BuildingUtil.FindBizForBuilding(building);
			VerificationLog("CivicNonpurchase", $"blocked perform-takeover building={GetTemplateString(building)} biz={GetTemplateString(biz)}");
			LogAfterProhibitionEconomyCivicPurchaseBoundary(building, "perform-takeover");
			return false;
		}

		private static void CheckPotentialBizBuildingPostfix(VisitState visit, ref bool __result)
		{
			if (__result && IsCivicNonpurchaseVisit(visit))
			{
				__result = false;
			}
		}

		private static void SpecialBizPurchasePipPostfix(VisitState visit, ref bool __result)
		{
			if (__result && IsCivicNonpurchaseVisit(visit))
			{
				__result = false;
				LogAfterProhibitionEconomyCivicPurchaseBoundary(visit?.building, "special-purchase-pip");
			}
		}

		private static void SpecialBizPurchaseButtonPostfix(VisitState visit, ConvoButtonState bstate, ref bool __result)
		{
			if (!__result)
			{
				return;
			}

			ConvoDataTicketBuilding data = bstate.data as ConvoDataTicketBuilding;
			if (data == null || !data.takeover.IsValid)
			{
				__result = false;
				return;
			}

			if (IsCivicNonpurchaseVisit(visit) || IsCivicNonpurchaseButtonState(bstate))
			{
				__result = false;
				LogAfterProhibitionEconomyCivicPurchaseBoundary(visit?.building ?? ResolveButtonStateBuilding(bstate), "special-purchase-button");
			}
		}

		private static bool ExecuteBuildingTakeoverPrefix(ConvoButton button)
		{
			ConvoDataTicketBuilding data = button?.GetData<ConvoDataTicketBuilding>();
			Entity building = data?.takeover.FindBuilding();
			if (!IsCivicNonpurchaseBuilding(building))
			{
				return true;
			}

			Entity biz = BuildingUtil.FindBizForBuilding(building);
			VerificationLog("CivicNonpurchase", $"blocked runtime takeover building={GetTemplateString(building)} biz={GetTemplateString(biz)}");
			LogAfterProhibitionEconomyCivicPurchaseBoundary(building, "runtime-takeover");
			return false;
		}

		private static void CheckCanBuySellAvailabilityPostfix(CheckCanBuySell __instance, VisitState visit, ref bool __result)
		{
			LogAfterProhibitionEconomyCivicPurchaseAccessObservation(
				visit?.building,
				"buy-sell-availability",
				__instance?.playercan.ToString() ?? "unknown",
				__result);
		}

		private static bool StoreTicketBuildingChoicePrefix(ConvoCallbacks __instance, ref OnShowResult __result)
		{
			try
			{
				VisitState visit = ConvoCallbacksVisitProperty?.GetValue(__instance, null) as VisitState;
				PlayerInfo player = visit?.GetPlayer();
				if (player?.territory == null)
				{
					__result = OnShowResult.CONTINUE;
					LogTicketBuildingGuard("missing-player", visit, default(PlayerTerritory.TakeoverData), null, null);
					return false;
				}

				PlayerTerritory.TakeoverData takeover = player.territory.FindTakeoverData(visit);
				Entity building = takeover.FindBuilding();
				Entity candidate = takeover.FindCandidate();
				if (!takeover.IsValid || building == null || candidate == null || IsCivicNonpurchaseBuilding(building))
				{
					__result = OnShowResult.CONTINUE;
					LogTicketBuildingGuard("invalid-takeover", visit, takeover, building, candidate);
					return false;
				}

				__result = OnShowResult.SetButtonData(CreateTicketBuildingData(takeover, building, candidate));
				return false;
			}
			catch (Exception ex)
			{
				__result = OnShowResult.CONTINUE;
				Debug.LogWarning("[GameplayTweaks] StoreTicketBuildingChoice guard failed: " + ex.GetType().Name + ":" + ex.Message);
				return false;
			}
		}

		private static ConvoDataTicketBuilding CreateTicketBuildingData(PlayerTerritory.TakeoverData takeover, Entity building, Entity candidate)
		{
			try
			{
				return new ConvoDataTicketBuilding(takeover);
			}
			catch (Exception ex)
			{
				Entity biz = BuildingUtil.FindBizForBuilding(building);
				LogTicketBuildingGuard("data-constructor-fallback-" + ex.GetType().Name, null, takeover, building, candidate);
				return new ConvoDataTicketBuilding
				{
					takeover = takeover,
					name = candidate != null ? PersonInfoUtil.GeneratePeepName(candidate, showRank: false) : string.Empty,
					bizname = biz?.data.biz.bizname ?? string.Empty,
					sizeAndVert = string.Empty
				};
			}
		}

		private static bool IsCivicNonpurchaseButtonState(ConvoButtonState bstate)
		{
			return IsCivicNonpurchaseBuilding(ResolveButtonStateBuilding(bstate));
		}

		private static bool IsCivicNonpurchaseVisit(VisitState visit)
		{
			return IsCivicNonpurchaseBuilding(visit?.building);
		}

		private static bool IsCivicNonpurchaseBuilding(Entity building)
		{
			if (building == null)
			{
				return false;
			}

			Entity biz = BuildingUtil.FindBizForBuilding(building);
			if (IsKnownCivicNonpurchaseTemplate(GetTemplateString(biz), CivicBizTemplates))
			{
				return true;
			}

			return IsKnownCivicNonpurchaseTemplate(GetTemplateString(building), CivicBuildingTemplates);
		}

		private static bool IsKnownCivicNonpurchaseTemplate(string template, HashSet<string> knownTemplates)
		{
			if (string.IsNullOrEmpty(template) || string.Equals(template, "(null)", StringComparison.Ordinal))
			{
				return false;
			}

			if (knownTemplates.Contains(template))
			{
				return true;
			}

			return template.IndexOf("school", StringComparison.OrdinalIgnoreCase) >= 0
				|| template.IndexOf("worship", StringComparison.OrdinalIgnoreCase) >= 0
				|| template.IndexOf("church", StringComparison.OrdinalIgnoreCase) >= 0;
		}

		private static string GetTemplateString(Entity entity)
		{
			return entity?.config?.Template.String ?? "(null)";
		}

		private static Entity ResolveButtonStateBuilding(ConvoButtonState bstate)
		{
			ConvoDataTicketBuilding convoDataTicketBuilding = bstate.data as ConvoDataTicketBuilding;
			return convoDataTicketBuilding?.takeover.FindBuilding();
		}

		private static void LogTicketBuildingGuard(string reason, VisitState visit, PlayerTerritory.TakeoverData takeover, Entity building, Entity candidate)
		{
			try
			{
				string key = reason + "|"
					+ (visit?.building?.Id.id.ToString() ?? "0") + "|"
					+ takeover.buildingId.id + "|"
					+ takeover.candidateId.id;
				if (!LoggedTicketBuildingGuardKeys.Add(key))
				{
					return;
				}

				VerificationLog(
					"CivicNonpurchase",
					"ticket-building-guard reason=" + reason
					+ " visitBuilding=" + (visit?.building?.Id.id.ToString() ?? "0")
					+ " takeoverBuilding=" + takeover.buildingId.id
					+ " candidate=" + takeover.candidateId.id
					+ " buildingTemplate=" + GetTemplateString(building)
					+ " candidateNull=" + (candidate == null));
			}
			catch
			{
			}
		}

		private static void LogAfterProhibitionEconomyCivicPurchaseBoundary(Entity building, string source)
		{
			try
			{
				string summary = TryGetAfterProhibitionEconomyCivicPurchaseAccessSummary(building?.Id ?? EntityID.INVALID);
				if (!string.IsNullOrWhiteSpace(summary))
				{
					VerificationLog("EconomyCivic", $"source={source} takeoverGuard=True {summary}");
				}
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] Failed to log AfterProhibitionEconomy civic purchase boundary: " + ex.Message);
			}
		}

		private static void LogAfterProhibitionEconomyCivicPurchaseAccessObservation(Entity building, string source, string requirement, bool result)
		{
			try
			{
				string summary = TryGetAfterProhibitionEconomyCivicPurchaseAccessSummary(building?.Id ?? EntityID.INVALID);
				if (!IsAfterProhibitionEconomyCivicPurchaseCandidate(summary))
				{
					return;
				}

				string key = source + "|" + (building?.Id.id.ToString() ?? "0") + "|" + requirement + "|" + result;
				if (!LoggedEconomyCivicAccessKeys.Add(key))
				{
					return;
				}

				VerificationLog("EconomyCivic", $"source={source} takeoverGuard=False requirement={requirement} gameplayTweaksResult={result} {summary}");
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] Failed to log AfterProhibitionEconomy civic purchase access observation: " + ex.Message);
			}
		}

		private static bool IsAfterProhibitionEconomyCivicPurchaseCandidate(string summary)
		{
			return !string.IsNullOrWhiteSpace(summary)
				&& (summary.IndexOf("type=bank ", StringComparison.Ordinal) >= 0
					|| summary.IndexOf("type=warehouse ", StringComparison.Ordinal) >= 0);
		}

		private static string TryGetAfterProhibitionEconomyCivicPurchaseAccessSummary(EntityID buildingId)
		{
			Type economyType = AccessTools.TypeByName("AfterProhibitionEconomy.AfterProhibitionEconomyPlugin");
			if (economyType == null)
			{
				return string.Empty;
			}

			_afterProhibitionEconomyCivicPurchaseAccessSummaryMethod =
				_afterProhibitionEconomyCivicPurchaseAccessSummaryMethod ??
				AccessTools.Method(economyType, "GetCivicPurchaseAccessSummary");
			if (_afterProhibitionEconomyCivicPurchaseAccessSummaryMethod == null)
			{
				return string.Empty;
			}

			object summary = _afterProhibitionEconomyCivicPurchaseAccessSummaryMethod.Invoke(
				null,
				new object[]
				{
					buildingId
				});
			return summary as string ?? string.Empty;
		}
	}
}
}
