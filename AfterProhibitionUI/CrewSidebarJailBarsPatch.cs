using System;
using System.Reflection;
using Game.Core;
using Game.Session.Player;
using Game.UI.Session.Crew;
using HarmonyLib;
using UnityEngine;

namespace AfterProhibitionUI
{
	internal static class CrewSidebarJailBarsPatch
	{
		private static MethodInfo _gameplayTweaksIsCrewCurrentlyJailedMethod;

		public static int ApplyPatch(Harmony harmony)
		{
			try
			{
				MethodInfo refreshPanel = AccessTools.Method(typeof(CrewCardContext), "RefreshPanel");
				if (refreshPanel == null)
				{
					AfterProhibitionUIPlugin.Log?.LogWarning("CrewSidebarJailBarsPatch: RefreshPanel not found");
					return 0;
				}

				harmony.Patch(refreshPanel, postfix: new HarmonyMethod(typeof(CrewSidebarJailBarsPatch), nameof(RefreshPanelPostfix)));
				AfterProhibitionUIPlugin.Log?.LogInfo("Jail crew sidebar jail-bars patch applied");
				return 1;
			}
			catch (Exception ex)
			{
				AfterProhibitionUIPlugin.Log?.LogWarning("CrewSidebarJailBarsPatch failed: " + ex.Message);
				return 0;
			}
		}

		private static void RefreshPanelPostfix(CrewCardContext __instance)
		{
			try
			{
				if (__instance == null || __instance.data == null)
				{
					return;
				}

				CrewAssignment crew = __instance.data.crew;
				if (!crew.peepId.IsValid || !IsCrewCurrentlyJailed(crew.peepId))
				{
					return;
				}

				Transform bars = __instance.card?.transform.Find("Info/Panel/First/Bars");
				if (bars != null)
				{
					bars.gameObject.SetActive(true);
				}
			}
			catch
			{
			}
		}

		private static bool IsCrewCurrentlyJailed(EntityID peepId)
		{
			if (peepId.IsNotValid)
			{
				return false;
			}

			if (TryReadGameplayTweaksJailState(peepId, out bool isJailed))
			{
				return isJailed;
			}

			return TryReadVanillaCopTrackerJailState(peepId);
		}

		private static bool TryReadGameplayTweaksJailState(EntityID peepId, out bool isJailed)
		{
			isJailed = false;
			try
			{
				if (_gameplayTweaksIsCrewCurrentlyJailedMethod == null)
				{
					Type gameplayTweaksType = AccessTools.TypeByName("GameplayTweaks.GameplayTweaksPlugin");
					_gameplayTweaksIsCrewCurrentlyJailedMethod = gameplayTweaksType?.GetMethod(
						"IsCrewCurrentlyJailed",
						BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public,
						null,
						new[] { typeof(EntityID) },
						null);
				}

				if (_gameplayTweaksIsCrewCurrentlyJailedMethod == null)
				{
					return false;
				}

				object result = _gameplayTweaksIsCrewCurrentlyJailedMethod.Invoke(null, new object[] { peepId });
				if (result is bool value)
				{
					isJailed = value;
					return true;
				}
			}
			catch
			{
			}

			return false;
		}

		private static bool TryReadVanillaCopTrackerJailState(EntityID peepId)
		{
			try
			{
				object ctx = global::Game.Game.ctx;
				object simman = ctx?.GetType().GetField("simman", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(ctx);
				object cops = simman?.GetType().GetField("cops", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(simman);
				MethodInfo isArrestedOrImprisoned = cops?.GetType().GetMethod(
					"IsArrestedOrImprisoned",
					BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
					null,
					new[] { typeof(EntityID) },
					null);

				return isArrestedOrImprisoned != null
					&& isArrestedOrImprisoned.Invoke(cops, new object[] { peepId }) is bool value
					&& value;
			}
			catch
			{
				return false;
			}
		}
	}
}
