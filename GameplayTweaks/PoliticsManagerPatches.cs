using System;
using System.Collections.Generic;
using System.Reflection;
using Game;
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

namespace GameplayTweaks
{
	/// <summary>
	/// Guards PoliticsManager.RunNewGamePoliticianSetup so we never call PickElement on an empty
	/// localPoliticians list (e.g. when mods reduce the NPC pool). Wards with no candidates are skipped.
	/// </summary>
	internal static class PoliticsManagerPatches
	{
		private static readonly FieldInfo PdataField = AccessTools.Field(typeof(PoliticsManager), "_pdata");
		private static bool? _afterProhibitionPoliticsOwnsStarterQuest;
		private static string _afterProhibitionPoliticsStarterReason = "not-checked";
		private static string _afterProhibitionPoliticsStarterVersion = "unknown";
		private static bool _starterDelegationLogged;
		private static bool _starterFallbackLogged;

		public static void ApplyPatches(Harmony harmony)
		{
			harmony.Patch(
				AccessTools.Method(typeof(PoliticsManager), "RunNewGamePoliticianSetup"),
				prefix: new HarmonyMethod(typeof(PoliticsManagerPatches), nameof(RunNewGamePoliticianSetup_Prefix)));
			harmony.Patch(
				AccessTools.Method(typeof(PoliticsManager), "OnSystemTurn"),
				postfix: new HarmonyMethod(typeof(PoliticsManagerPatches), nameof(OnSystemTurn_Postfix)));
			harmony.Patch(
				AccessTools.Method(typeof(PoliticsManager), "IsTimeToTutorialize"),
				postfix: new HarmonyMethod(typeof(PoliticsManagerPatches), nameof(IsTimeToTutorialize_Postfix)));
			harmony.Patch(
				AccessTools.Method(typeof(PlayerInfo), "OnPlayerTurnStarted"),
				postfix: new HarmonyMethod(typeof(PoliticsManagerPatches), nameof(PlayerInfo_OnPlayerTurnStarted_Postfix)));
		}

		private static bool RunNewGamePoliticianSetup_Prefix(PoliticsManager __instance)
		{
			GameplayTweaksPlugin.ResetRuntimeCampaignStateForNewGame("RunNewGamePoliticianSetup");
			var pdata = (PoliticsManagerPersistedData)PdataField.GetValue(__instance);
			if (pdata == null)
				return true; // let original run

			foreach (PlayerInfo item in Game.Game.ctx.players.all)
			{
				if (item.PID.IsHumanPlayer || item.IsJustGang)
					pdata.influence[item.PID] = new InfluenceWallet();
			}

			foreach (Ward ward in pdata.wards)
			{
				PrecinctID id = ward.id;
				ward.GenerateLocalPoliticians();
				List<EntityID> localPoliticians = ward.localPoliticians;
				if (localPoliticians == null || localPoliticians.Count == 0)
					continue;
				EntityID politician = pdata.rng.PickElement(localPoliticians);
				__instance.ElectPoliticianToWard(id, politician);
			}

			return false; // skip original
		}

		private static void OnSystemTurn_Postfix(PoliticsManager __instance)
		{
			try
			{
				if (ShouldDelegateStarterQuest("politics-system-turn"))
					return;
				EnsureNewYorkPoliticsStarter(__instance, "politics-system-turn");
			}
			catch (Exception ex)
			{
				GameplayTweaksPlugin.VerificationLog("PoliticsStarter", $"ensure-failed error={ex.GetType().Name}:{ex.Message}");
			}
		}

		private static void IsTimeToTutorialize_Postfix(ref bool __result)
		{
			if (ShouldDelegateStarterQuest("tutorialize-check"))
				return;
			if (__result && IsStarterQuestKnown())
				__result = false;
		}

		private static void PlayerInfo_OnPlayerTurnStarted_Postfix(PlayerInfo __instance)
		{
			if (__instance == null || !__instance.PID.IsHumanPlayer)
				return;
			long totalTicks = GameplayTweaksPlugin.StartPerfTimer();
			try
			{
				if (ShouldDelegateStarterQuest("human-turn-start"))
					return;
				EnsureNewYorkPoliticsStarter(Game.Game.ctx.simman.politics, "human-turn-start");
			}
			catch (Exception ex)
			{
				GameplayTweaksPlugin.VerificationLog("PoliticsStarter", $"human-turn-ensure-failed error={ex.GetType().Name}:{ex.Message}");
			}
			finally
			{
				GameplayTweaksPlugin.LogHumanTurnStartPhase("politics-starter:ensure", totalTicks);
			}
		}

		private static void EnsureNewYorkPoliticsStarter(PoliticsManager manager, string source)
		{
			string mapId = Game.Game.ctx?.session?.mapconfig?.id;
			if (!string.Equals(mapId, "new-york", StringComparison.OrdinalIgnoreCase))
				return;
			if (Game.Game.ctx.clock.CurrentTurn < 1 || Game.Game.ctx.clock.Now < Game.Game.ctx.clock.LastDayOfProcGen)
				return;

			string questId = TutorialManager.QUEST_NY_POLITICS_STARTER;
			if (IsStarterQuestKnown())
				return;

			EntityID politician = ResolveStarterPolitician(manager);
			if (politician.IsNotValid)
			{
				GameplayTweaksPlugin.VerificationLog("PoliticsStarter", $"skip source={source} reason=no-valid-politician turn={Game.Game.ctx.clock.CurrentTurn}");
				return;
			}

			QuestUUID uuid = Game.Game.ctx.quests.StartQuest(questId, politician, fromRequest: false);
			if (uuid == QuestUUID.EMPTY)
			{
				GameplayTweaksPlugin.VerificationLog("PoliticsStarter", $"start-failed source={source} politician={politician.id} turn={Game.Game.ctx.clock.CurrentTurn}");
				return;
			}

			string dateText = Loc.FormatDate(
				Game.Game.ctx.clock.GetFirstTurnOfMonthThisYear(manager.Settings.elections.campaignStartMonth),
				showyear: true);
			Game.Game.ctx.hud.tickers.AddTextTicker(
				TickerIcon.POLITICS,
				TickerTitle.POLITICS,
				Loc.Get("ui.tickers.politics.ny-first-quest", "date", dateText),
				TickerTarget.INVALID,
				TickerPersistType.PolTutorialPersist);
			GameplayTweaksPlugin.VerificationLog("PoliticsStarter", $"started source={source} quest={questId} uuid={uuid.value} politician={politician.id} turn={Game.Game.ctx.clock.CurrentTurn}");
		}

		private static bool IsStarterQuestKnown()
		{
			string questId = TutorialManager.QUEST_NY_POLITICS_STARTER;
			return Game.Game.ctx.quests.IsQuestActiveByID(questId, EntityID.INVALID) ||
				Game.Game.ctx.quests.IsQuestWaitingByID(questId, EntityID.INVALID) ||
				Game.Game.ctx.quests.IsQuestCompletedByID(questId, EntityID.INVALID);
		}

		private static EntityID ResolveStarterPolitician(PoliticsManager manager)
		{
			EntityID safehousePolitician = ResolveSafehouseWardPolitician(manager);
			if (safehousePolitician.IsValid)
				return safehousePolitician;

			var pdata = (PoliticsManagerPersistedData)PdataField.GetValue(manager);
			if (pdata?.wards == null)
				return EntityID.INVALID;

			foreach (Ward ward in pdata.wards)
			{
				if (IsValidPolitician(manager, ward.currentPolitician))
					return ward.currentPolitician;
			}

			foreach (Ward ward in pdata.wards)
			{
				if (ward.localPoliticians == null)
					continue;
				foreach (EntityID politician in ward.localPoliticians)
				{
					if (IsValidPolitician(manager, politician))
						return politician;
				}
			}

			return EntityID.INVALID;
		}

		private static EntityID ResolveSafehouseWardPolitician(PoliticsManager manager)
		{
			Entity safehouse = Game.Game.ctx.players.Human?.territory?.Safehouse.FindEntity();
			Ward ward = safehouse?.data?.board?.bead.nodeId.FindNode()?.precinctId.FindWard();
			if (ward != null && IsValidPolitician(manager, ward.currentPolitician))
				return ward.currentPolitician;
			return EntityID.INVALID;
		}

		private static bool IsValidPolitician(PoliticsManager manager, EntityID politician)
		{
			return politician.IsValid &&
				politician.FindEntity() != null &&
				manager.GetPoliticianData(politician) != null;
		}

		private static bool ShouldDelegateStarterQuest(string source)
		{
			if (_afterProhibitionPoliticsOwnsStarterQuest == null)
				_afterProhibitionPoliticsOwnsStarterQuest = TryGetAfterProhibitionPoliticsStarterOwner(out _afterProhibitionPoliticsStarterReason, out _afterProhibitionPoliticsStarterVersion);

			if (_afterProhibitionPoliticsOwnsStarterQuest == true)
			{
				if (!_starterDelegationLogged)
				{
					_starterDelegationLogged = true;
					GameplayTweaksPlugin.VerificationLog("PoliticsStarter", $"delegated owner=AfterProhibitionPolitics source={source} version={_afterProhibitionPoliticsStarterVersion}");
				}
				return true;
			}

			if (!_starterFallbackLogged)
			{
				_starterFallbackLogged = true;
				GameplayTweaksPlugin.VerificationLog("PoliticsStarter", $"fallback active reason={_afterProhibitionPoliticsStarterReason} source={source}");
			}
			return false;
		}

		private static bool TryGetAfterProhibitionPoliticsStarterOwner(out string reason, out string version)
		{
			reason = "bridge-missing";
			version = "unknown";
			try
			{
				Type pluginType = FindLoadedType("AfterProhibitionPolitics.AfterProhibitionPoliticsPlugin");
				if (pluginType == null)
					return false;

				MethodInfo versionMethod = pluginType.GetMethod("GetPoliticsBridgeVersion", BindingFlags.Static | BindingFlags.Public);
				object versionValue = versionMethod?.Invoke(null, null);
				if (versionValue is string versionString && !string.IsNullOrEmpty(versionString))
					version = versionString;

				MethodInfo ownerMethod = pluginType.GetMethod("OwnsPoliticalStarterQuest", BindingFlags.Static | BindingFlags.Public);
				if (ownerMethod == null)
				{
					reason = "bridge-method-missing";
					return false;
				}

				object ownerValue = ownerMethod.Invoke(null, null);
				if (ownerValue is bool ownsStarterQuest && ownsStarterQuest)
				{
					reason = "delegated";
					return true;
				}

				reason = "bridge-disabled";
				return false;
			}
			catch (Exception ex)
			{
				reason = "bridge-error-" + ex.GetType().Name;
				return false;
			}
		}

		private static Type FindLoadedType(string fullName)
		{
			foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
			{
				try
				{
					Type type = assembly.GetType(fullName, throwOnError: false);
					if (type != null)
						return type;
				}
				catch
				{
				}
			}

			return null;
		}
	}
}
