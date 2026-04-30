using System;
using System.Collections.Generic;
using System.Reflection;
using Game.Core;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Player;
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
			"school-regular",
			"civic-school-large",
			"worship-regular",
			"worship-large"
		};

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
					AccessTools.Method(typeof(ConvoCallbacks), "ExecuteBuildingTakeover", new Type[]
					{
						typeof(ConvoButton)
					}),
					prefix: nameof(ExecuteBuildingTakeoverPrefix));
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
			}
		}

		private static void SpecialBizPurchaseButtonPostfix(VisitState visit, ConvoButtonState bstate, ref bool __result)
		{
			if (!__result)
			{
				return;
			}

			if (IsCivicNonpurchaseVisit(visit) || IsCivicNonpurchaseButtonState(bstate))
			{
				__result = false;
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
			return false;
		}

		private static bool IsCivicNonpurchaseButtonState(ConvoButtonState bstate)
		{
			ConvoDataTicketBuilding convoDataTicketBuilding = bstate.data as ConvoDataTicketBuilding;
			return IsCivicNonpurchaseBuilding(convoDataTicketBuilding?.takeover.FindBuilding());
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
			if (HasTemplate(GetTemplateString(biz), CivicBizTemplates))
			{
				return true;
			}

			return HasTemplate(GetTemplateString(building), CivicBuildingTemplates);
		}

		private static bool HasTemplate(string template, HashSet<string> knownTemplates)
		{
			return !string.IsNullOrEmpty(template) && !string.Equals(template, "(null)", StringComparison.Ordinal) && knownTemplates.Contains(template);
		}

		private static string GetTemplateString(Entity entity)
		{
			return entity?.config?.Template.String ?? "(null)";
		}
	}
}
}
