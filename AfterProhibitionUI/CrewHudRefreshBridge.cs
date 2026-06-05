using System;
using System.Collections;
using System.Reflection;
using Game.UI.Session.Crew;

namespace AfterProhibitionUI
{
	public static class CrewHudRefreshBridge
	{
		private static bool _queued;
		private static bool _queuedRebuild;
		private static string _queuedSource;
		private static MethodInfo _recreateAllCardsMethod;
		private static MethodInfo _refreshContentsMethod;
		private static MethodInfo _forceRebuildLayoutMethod;

		public static bool RequestCrewHudRefresh(string sourceTag, bool rebuildCards)
		{
			if (AfterProhibitionUIPlugin.EnableCrewHudRefreshBridge?.Value != true || AfterProhibitionUIPlugin.Instance == null)
			{
				return false;
			}

			_queuedRebuild |= rebuildCards;
			_queuedSource = string.IsNullOrEmpty(sourceTag) ? "external" : sourceTag;
			if (_queued)
			{
				return true;
			}

			_queued = true;
			AfterProhibitionUIPlugin.StartDeferredWork(ApplyNextFrame());
			return true;
		}

		private static IEnumerator ApplyNextFrame()
		{
			yield return null;
			ApplyQueuedRefresh();
		}

		private static void ApplyQueuedRefresh()
		{
			bool rebuildCards = _queuedRebuild;
			string sourceTag = _queuedSource;
			_queued = false;
			_queuedRebuild = false;
			_queuedSource = null;

			try
			{
				CrewDialog crewHud = global::Game.Game.ctx?.hud?.crew;
				if (crewHud == null)
				{
					return;
				}

				if (rebuildCards)
				{
					EnsureMethods();
					_recreateAllCardsMethod?.Invoke(crewHud, null);
					_refreshContentsMethod?.Invoke(crewHud, null);
					_forceRebuildLayoutMethod?.Invoke(crewHud, null);
				}
				else
				{
					crewHud.RefreshCards();
				}

				AfterProhibitionUIPlugin.Log?.LogInfo("CrewHUD refresh rebuild=" + rebuildCards + " source=" + sourceTag + " owner=AfterProhibitionUI");
			}
			catch (Exception ex)
			{
				AfterProhibitionUIPlugin.Log?.LogWarning("CrewHUD refresh bridge failed: " + ex.Message);
			}
		}

		private static void EnsureMethods()
		{
			if (_recreateAllCardsMethod != null)
			{
				return;
			}

			_recreateAllCardsMethod = typeof(CrewDialog).GetMethod("RecreateAllCards", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
			_refreshContentsMethod = typeof(CrewDialog).GetMethod("RefreshContents", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
			_forceRebuildLayoutMethod = typeof(CrewDialog).GetMethod("ForceRebuildLayoutImmediate", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
		}
	}
}
