using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace AfterProhibitionUI
{
	public static class CrewPickAggroRefreshBridge
	{
		private static readonly HashSet<int> DirtyPids = new HashSet<int>();
		private static int _lastDeferredFrame = -1;
		private static MethodInfo _executeRefreshMethod;
		private static MethodInfo _getMouseButtonMethod;

		public static bool RequestAggroRefresh(int pid, string sourceTag)
		{
			if (pid < 0 || AfterProhibitionUIPlugin.EnableAggroUiRefreshBridge?.Value != true || AfterProhibitionUIPlugin.Instance == null)
			{
				return false;
			}

			if (!EnsureExecuteRefreshMethod())
			{
				return false;
			}

			if (DirtyPids.Add(pid))
			{
				AfterProhibitionUIPlugin.Log?.LogInfo("AggroUI dirty pid=" + pid + " source=" + (sourceTag ?? string.Empty) + " owner=AfterProhibitionUI");
			}

			return true;
		}

		public static bool FlushAggroRefreshes(string sourceTag)
		{
			if (AfterProhibitionUIPlugin.EnableAggroUiRefreshBridge?.Value != true || AfterProhibitionUIPlugin.Instance == null || DirtyPids.Count == 0)
			{
				return false;
			}

			if (!EnsureExecuteRefreshMethod())
			{
				return false;
			}

			if (IsMouseButtonHeld())
			{
				if (_lastDeferredFrame != Time.frameCount)
				{
					_lastDeferredFrame = Time.frameCount;
					AfterProhibitionUIPlugin.Log?.LogInfo("AggroUI flush deferred source=" + (sourceTag ?? string.Empty) + " count=" + DirtyPids.Count + " reason=mouse-held owner=AfterProhibitionUI");
				}
				return true;
			}

			int attempted = 0;
			int applied = 0;
			List<int> pids = new List<int>(DirtyPids);
			DirtyPids.Clear();
			foreach (int pid in pids)
			{
				attempted++;
				try
				{
					object result = _executeRefreshMethod.Invoke(null, new object[] { pid, sourceTag ?? string.Empty });
					string resultText = result as string;
					if (IsAppliedRefreshResult(resultText))
					{
						applied++;
					}
					if (ShouldLogRefreshResult(resultText))
					{
						AfterProhibitionUIPlugin.Log?.LogInfo("AggroUI " + resultText + " owner=AfterProhibitionUI");
					}
				}
				catch (Exception ex)
				{
					AfterProhibitionUIPlugin.Log?.LogWarning("AggroUI delegated refresh failed pid=" + pid + ": " + ex.Message);
				}
			}

			AfterProhibitionUIPlugin.Log?.LogInfo("AggroUI flush attempted=" + attempted + " applied=" + applied + " source=" + (sourceTag ?? string.Empty) + " owner=AfterProhibitionUI");
			return true;
		}

		private static bool IsAppliedRefreshResult(string resultText)
		{
			return !string.IsNullOrEmpty(resultText)
				&& !resultText.StartsWith("skipped=", StringComparison.Ordinal)
				&& !IsNoOpClearOnlyResult(resultText);
		}

		private static bool ShouldLogRefreshResult(string resultText)
		{
			return IsAppliedRefreshResult(resultText);
		}

		private static bool IsNoOpClearOnlyResult(string resultText)
		{
			return !string.IsNullOrEmpty(resultText)
				&& resultText.StartsWith("flush-clear-only ", StringComparison.Ordinal)
				&& resultText.IndexOf(" removed=0 ", StringComparison.Ordinal) >= 0;
		}

		private static bool EnsureExecuteRefreshMethod()
		{
			if (_executeRefreshMethod != null)
			{
				return true;
			}

			Type gameplayTweaksType = AccessTools.TypeByName("GameplayTweaks.GameplayTweaksPlugin");
			_executeRefreshMethod = gameplayTweaksType?.GetMethod(
				"ExecuteCrewPickAggroRefreshForExternalOwner",
				BindingFlags.Static | BindingFlags.Public,
				null,
				new[] { typeof(int), typeof(string) },
				null);
			return _executeRefreshMethod != null;
		}

		private static bool IsMouseButtonHeld()
		{
			try
			{
				if (_getMouseButtonMethod == null)
				{
					Type inputType = AccessTools.TypeByName("UnityEngine.Input")
						?? Type.GetType("UnityEngine.Input, UnityEngine.InputLegacyModule", throwOnError: false);
					_getMouseButtonMethod = inputType?.GetMethod(
						"GetMouseButton",
						BindingFlags.Static | BindingFlags.Public,
						null,
						new[] { typeof(int) },
						null);
				}

				return _getMouseButtonMethod != null
					&& _getMouseButtonMethod.Invoke(null, new object[] { 0 }) is bool held
					&& held;
			}
			catch
			{
				return false;
			}
		}
	}
}
