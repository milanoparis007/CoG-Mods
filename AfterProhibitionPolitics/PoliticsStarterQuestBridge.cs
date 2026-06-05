using System;
using System.Collections.Generic;
using Game.Core;
using Game.Services;
using Game.Session.Board;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Player;
using Game.Session.Sim;
using Game.Session.Tutorial;
using HarmonyLib;
using SomaSim.Util;

namespace AfterProhibitionPolitics
{
	internal static class PoliticsStarterQuestBridge
	{
		private static int _lastNoValidPoliticianLogTurn = -1000;

		internal static void ApplyPatch(Harmony harmony)
		{
			if (harmony == null)
			{
				return;
			}

			try
			{
				harmony.Patch(
					AccessTools.Method(typeof(PoliticsManager), "OnSystemTurn"),
					postfix: new HarmonyMethod(typeof(PoliticsStarterQuestBridge), nameof(OnSystemTurn_Postfix)));
				harmony.Patch(
					AccessTools.Method(typeof(PoliticsManager), "IsTimeToTutorialize"),
					postfix: new HarmonyMethod(typeof(PoliticsStarterQuestBridge), nameof(IsTimeToTutorialize_Postfix)));
				harmony.Patch(
					AccessTools.Method(typeof(PlayerInfo), "OnPlayerTurnStarted"),
					postfix: new HarmonyMethod(typeof(PoliticsStarterQuestBridge), nameof(PlayerInfo_OnPlayerTurnStarted_Postfix)));
				AfterProhibitionPoliticsPlugin.Log?.LogInfo("starter-quest bridge patches applied targets=3");
			}
			catch (Exception ex)
			{
				AfterProhibitionPoliticsPlugin.Log?.LogWarning("starter-quest bridge patch failed error=" + ex.GetType().Name + ":" + ex.Message);
			}
		}

		private static void OnSystemTurn_Postfix(PoliticsManager __instance)
		{
			TryEnsureNewYorkPoliticsStarter(__instance, "politics-system-turn");
		}

		private static void IsTimeToTutorialize_Postfix(ref bool __result)
		{
			if (!AfterProhibitionPoliticsPlugin.OwnsPoliticalStarterQuest())
			{
				return;
			}

			if (__result && IsStarterQuestKnown())
			{
				__result = false;
				AfterProhibitionPoliticsPlugin.Log?.LogInfo("starter-quest tutorial-suppressed reason=quest-known");
			}
		}

		private static void PlayerInfo_OnPlayerTurnStarted_Postfix(PlayerInfo __instance)
		{
			if (__instance == null || !__instance.PID.IsHumanPlayer)
			{
				return;
			}

			TryEnsureNewYorkPoliticsStarter(global::Game.Game.ctx?.simman?.politics, "human-turn-start");
		}

		internal static bool TryEnsureNewYorkPoliticsStarter(PoliticsManager manager, string source)
		{
			if (!AfterProhibitionPoliticsPlugin.OwnsPoliticalStarterQuest())
			{
				return false;
			}

			try
			{
				string mapId = global::Game.Game.ctx?.session?.mapconfig?.id;
				if (!string.Equals(mapId, "new-york", StringComparison.OrdinalIgnoreCase))
				{
					return false;
				}
				if (manager == null)
				{
					return false;
				}
				if (!IsProcgenReady())
				{
					return false;
				}
				if (IsStarterQuestKnown())
				{
					return false;
				}

				EntityID politician = ResolveStarterPolitician(manager);
				if (politician.IsNotValid)
				{
					if (ShouldLogNoValidPoliticianSkip())
					{
						LogStarter("skip", source, "no-valid-politician", politician, QuestUUID.EMPTY);
					}
					return false;
				}

				string questId = TutorialManager.QUEST_NY_POLITICS_STARTER;
				QuestUUID uuid = global::Game.Game.ctx.quests.StartQuest(questId, politician, fromRequest: false);
				if (uuid == QuestUUID.EMPTY)
				{
					LogStarter("start-failed", source, "startquest-empty", politician, uuid);
					return false;
				}

				TryAddStarterTicker(manager);
				LogStarter("started", source, "granted", politician, uuid);
				return true;
			}
			catch (Exception ex)
			{
				AfterProhibitionPoliticsPlugin.Log?.LogWarning("starter-quest ensure-failed source=" + source + " error=" + ex.GetType().Name + ":" + ex.Message);
				return false;
			}
		}

		private static bool IsProcgenReady()
		{
			try
			{
				return global::Game.Game.ctx.clock.CurrentTurn >= 1
					&& global::Game.Game.ctx.clock.Now >= global::Game.Game.ctx.clock.LastDayOfProcGen;
			}
			catch
			{
				return false;
			}
		}

		private static bool IsStarterQuestKnown()
		{
			try
			{
				string questId = TutorialManager.QUEST_NY_POLITICS_STARTER;
				return global::Game.Game.ctx.quests.IsQuestActiveByID(questId, EntityID.INVALID)
					|| global::Game.Game.ctx.quests.IsQuestWaitingByID(questId, EntityID.INVALID)
					|| global::Game.Game.ctx.quests.IsQuestCompletedByID(questId, EntityID.INVALID);
			}
			catch
			{
				return false;
			}
		}

		private static EntityID ResolveStarterPolitician(PoliticsManager manager)
		{
			EntityID safehousePolitician = ResolveSafehouseWardPolitician(manager);
			if (safehousePolitician.IsValid)
			{
				return safehousePolitician;
			}

			List<Ward> wards = SafeGetWards(manager);
			foreach (Ward ward in wards)
			{
				if (IsValidPolitician(manager, ward.currentPolitician))
				{
					return ward.currentPolitician;
				}
			}

			foreach (Ward ward in wards)
			{
				if (ward.localPoliticians == null)
				{
					continue;
				}
				foreach (EntityID politician in ward.localPoliticians)
				{
					if (IsValidPolitician(manager, politician))
					{
						return politician;
					}
				}
			}

			return EntityID.INVALID;
		}

		private static EntityID ResolveSafehouseWardPolitician(PoliticsManager manager)
		{
			try
			{
				Entity safehouse = global::Game.Game.ctx?.players?.Human?.territory?.Safehouse.FindEntity();
				Ward ward = safehouse?.data?.board?.bead.nodeId.FindNode()?.precinctId.FindWard();
				if (ward != null && IsValidPolitician(manager, ward.currentPolitician))
				{
					return ward.currentPolitician;
				}
			}
			catch
			{
			}

			return EntityID.INVALID;
		}

		private static List<Ward> SafeGetWards(PoliticsManager manager)
		{
			try
			{
				return manager?.GetWards() == null ? new List<Ward>() : new List<Ward>(manager.GetWards());
			}
			catch
			{
				return new List<Ward>();
			}
		}

		private static bool IsValidPolitician(PoliticsManager manager, EntityID politician)
		{
			try
			{
				return politician.IsValid
					&& politician.FindEntity() != null
					&& manager.GetPoliticianData(politician) != null;
			}
			catch
			{
				return false;
			}
		}

		private static void TryAddStarterTicker(PoliticsManager manager)
		{
			try
			{
				string dateText = Loc.FormatDate(
					global::Game.Game.ctx.clock.GetFirstTurnOfMonthThisYear(manager.Settings.elections.campaignStartMonth),
					showyear: true);
				global::Game.Game.ctx.hud.tickers.AddTextTicker(
					TickerIcon.POLITICS,
					TickerTitle.POLITICS,
					Loc.Get("ui.tickers.politics.ny-first-quest", "date", dateText),
					TickerTarget.INVALID,
					TickerPersistType.PolTutorialPersist);
			}
			catch (Exception ex)
			{
				AfterProhibitionPoliticsPlugin.Log?.LogWarning("starter-quest ticker-failed error=" + ex.GetType().Name + ":" + ex.Message);
			}
		}

		private static void LogStarter(string result, string source, string reason, EntityID politician, QuestUUID uuid)
		{
			AfterProhibitionPoliticsPlugin.Log?.LogInfo(
				"starter-quest result=" + result +
				" source=" + source +
				" reason=" + reason +
				" quest=" + TutorialManager.QUEST_NY_POLITICS_STARTER +
				" politician=" + (politician.IsValid ? politician.id.ToString() : "invalid") +
				" uuid=" + (uuid == QuestUUID.EMPTY ? "empty" : uuid.value.ToString()) +
				" turn=" + SafeCurrentTurn());
		}

		private static bool ShouldLogNoValidPoliticianSkip()
		{
			int turn = SafeCurrentTurn();
			if (turn < 0)
			{
				return false;
			}
			if (_lastNoValidPoliticianLogTurn < 0 || turn - _lastNoValidPoliticianLogTurn >= 10)
			{
				_lastNoValidPoliticianLogTurn = turn;
				return true;
			}

			return false;
		}

		private static int SafeCurrentTurn()
		{
			try
			{
				return global::Game.Game.ctx.clock.CurrentTurn;
			}
			catch
			{
				return -1;
			}
		}
	}
}
