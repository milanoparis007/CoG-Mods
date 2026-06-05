using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Configuration;
using Game.Core;
using Game.Session.Entities;
using Game.Session.Player;
using HarmonyLib;
using UnityEngine;

namespace AfterProhibitionUI
{
	public static class CrewInfoActionBridge
	{
		private static Type _gameplayTweaksType;
		private static readonly Dictionary<string, MethodInfo> MethodsByName = new Dictionary<string, MethodInfo>(StringComparer.Ordinal);
		private static readonly HashSet<string> MissingMethodLogs = new HashSet<string>(StringComparer.Ordinal);

		public static bool OpenCrewRelations(Entity peep)
		{
			return peep != null && InvokeVoid("OpenCrewRelationsFromExternalUi", peep);
		}

		public static bool OpenGangPacts()
		{
			return InvokeVoid("OpenGangPactsFromExternalUi");
		}

		public static bool OpenMyPact()
		{
			return InvokeVoid("OpenMyPactFromExternalUi");
		}

		public static bool OpenGrapevine()
		{
			return InvokeVoid("OpenGrapevineFromExternalUi");
		}

		public static bool OpenSafebox()
		{
			return InvokeVoid("OpenSafeboxFromExternalUi");
		}

		public static bool ApplyButtonIcon(GameObject button, string locKey, string fallbackText)
		{
			return button != null && InvokeVoid("ApplyCrewInfoButtonIconFromExternalUi", button, locKey, fallbackText);
		}

		public static bool CloseBossOnlyCrewMenus(bool keepMyPactMenus)
		{
			return InvokeBool("CloseBossOnlyCrewMenusFromExternalUi", keepMyPactMenus);
		}

		public static bool ForceCloseCrewMenus()
		{
			return InvokeVoid("ForceCloseCrewMenusFromExternalUi");
		}

		public static bool AnyCrewModMenuVisible()
		{
			return InvokeBool("AnyCrewModMenuVisibleFromExternalUi");
		}

		public static bool ShouldUseExternalSafeboxUi()
		{
			return InvokeBool("ShouldUseExternalSafeboxUI");
		}

		public static PlayerInfo GetHumanPlayer()
		{
			try
			{
				MethodInfo method = ResolveMethod("GetHumanPlayer", Array.Empty<object>());
				return method?.Invoke(null, null) as PlayerInfo;
			}
			catch (Exception ex)
			{
				AfterProhibitionUIPlugin.Log?.LogWarning("CrewInfoActionBridge failed method=GetHumanPlayer: " + ex.Message);
				return null;
			}
		}

		public static bool IsHumanBoss(Entity peep, PlayerInfo humanPlayer)
		{
			return peep != null && humanPlayer != null && InvokeBool("IsHumanBoss", peep, humanPlayer);
		}

		public static bool IsHumanBossOrUnderboss(Entity peep, PlayerInfo humanPlayer)
		{
			return peep != null && humanPlayer != null && InvokeBool("IsHumanBossOrUnderboss", peep, humanPlayer);
		}

		public static bool IsHumanPeepAtSafehouseCorner(PlayerInfo humanPlayer, Entity peep)
		{
			return humanPlayer != null && peep != null && InvokeBool("IsHumanPeepAtSafehouseCorner", humanPlayer, peep);
		}

		public static bool TryGetCrewAssignmentBlockReason(EntityID peepId, out string reason)
		{
			reason = null;
			if (!peepId.IsValid)
			{
				return false;
			}

			try
			{
				object[] args = { peepId, null };
				MethodInfo method = ResolveMethod("TryGetCrewAssignmentBlockReason", args);
				if (method == null)
				{
					return false;
				}

				object result = method.Invoke(null, args);
				reason = args.Length > 1 ? args[1] as string : null;
				return result is bool value && value && !string.IsNullOrWhiteSpace(reason);
			}
			catch (Exception ex)
			{
				AfterProhibitionUIPlugin.Log?.LogWarning("CrewInfoActionBridge failed method=TryGetCrewAssignmentBlockReason: " + ex.Message);
				return false;
			}
		}

		public static bool IsDirtyCashEnabled()
		{
			try
			{
				Type type = ResolveGameplayTweaksType();
				FieldInfo field = type?.GetField("EnableDirtyCash", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
				if (field?.GetValue(null) is ConfigEntry<bool> entry)
				{
					return entry.Value;
				}
			}
			catch
			{
			}

			return false;
		}

		public static bool IsAvailable()
		{
			return ResolveGameplayTweaksType() != null;
		}

		public static bool HasAction(string methodName, int parameterCount = 0)
		{
			if (string.IsNullOrEmpty(methodName))
			{
				return false;
			}

			try
			{
				Type type = ResolveGameplayTweaksType();
				MethodInfo method = type?.GetMethod(methodName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
				return method != null && method.GetParameters().Length == parameterCount;
			}
			catch
			{
				return false;
			}
		}

		private static bool InvokeVoid(string methodName, params object[] args)
		{
			try
			{
				MethodInfo method = ResolveMethod(methodName, args);
				if (method == null)
				{
					return false;
				}

				method.Invoke(null, args);
				return true;
			}
			catch (Exception ex)
			{
				AfterProhibitionUIPlugin.Log?.LogWarning("CrewInfoActionBridge failed method=" + methodName + ": " + ex.Message);
				return false;
			}
		}

		private static bool InvokeBool(string methodName, params object[] args)
		{
			try
			{
				MethodInfo method = ResolveMethod(methodName, args);
				if (method == null)
				{
					return false;
				}

				object result = method.Invoke(null, args);
				return result is bool value && value;
			}
			catch (Exception ex)
			{
				AfterProhibitionUIPlugin.Log?.LogWarning("CrewInfoActionBridge failed method=" + methodName + ": " + ex.Message);
				return false;
			}
		}

		private static MethodInfo ResolveMethod(string methodName, object[] args)
		{
			string cacheKey = methodName + ":" + (args?.Length ?? 0);
			if (MethodsByName.TryGetValue(cacheKey, out MethodInfo cached))
			{
				return cached;
			}

			Type type = ResolveGameplayTweaksType();
			if (type == null)
			{
				LogMissingOnce("type");
				return null;
			}

			MethodInfo method = type.GetMethod(methodName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
			if (method == null)
			{
				LogMissingOnce(methodName);
				return null;
			}

			MethodsByName[cacheKey] = method;
			return method;
		}

		private static Type ResolveGameplayTweaksType()
		{
			if (_gameplayTweaksType != null)
			{
				return _gameplayTweaksType;
			}

			_gameplayTweaksType = AccessTools.TypeByName("GameplayTweaks.GameplayTweaksPlugin");
			return _gameplayTweaksType;
		}

		private static void LogMissingOnce(string key)
		{
			if (MissingMethodLogs.Add(key))
			{
				AfterProhibitionUIPlugin.Log?.LogInfo("CrewInfoActionBridge unavailable key=" + key);
			}
		}
	}
}
