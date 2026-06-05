using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Game.Core;
using Game.Platform;
using Game.Services;
using Game.Session.Data;
using Game.Session.Sim;
using HarmonyLib;
using UnityEngine;

namespace GameplayTweaks
{
public partial class GameplayTweaksPlugin
{
	private static readonly Dictionary<Type, FieldInfo[]> SionSerializableFieldsByType = new Dictionary<Type, FieldInfo[]>();
	private const int DeferredModDataSaveQuietFrames = 45;
	private const int DeferredModDataSaveMaxWaitFrames = 240;
	private const int DeferredModDataSaveMaxSourceLogLength = 220;
	private const int ModDataBaseSaveFutureToleranceSeconds = 120;
	private static bool _v2CriticalTextRecoveryReadOnly;
	private static bool _v2CriticalTextRecoverySaveBlockLogged;
	private static string _v2CriticalTextRecoveryReason = string.Empty;
	private static string _lastModDataLoadSource = string.Empty;
	private static int _lastModDataLoadPactCount = -1;

	private static class SaveLoadPatch
	{
		public static void ApplyPatch(Harmony harmony)
		{

			try
			{
				int loadPatched = 0;
				int savePatched = 0;
				Type serviceType = typeof(SaveLoadService);
				MethodInfo saveMethod = serviceType.GetMethod("SaveGame", BindingFlags.Instance | BindingFlags.Public, null, new Type[2]
				{
					typeof(SaveFileMetadata),
					typeof(SaveFileContents)
				}, null);
				if (saveMethod != null)
				{
					harmony.Patch(saveMethod, postfix: new HarmonyMethod(typeof(SaveLoadPatch), nameof(SaveGameServicePostfix)));
					savePatched++;
				}
				MethodInfo loadMethod = serviceType.GetMethod("LoadGame", BindingFlags.Instance | BindingFlags.Public, null, new Type[2]
				{
					typeof(IPlatformSaveSlotDescriptor),
					typeof(SaveFileContents)
				}, null);
				if (loadMethod != null)
				{
					harmony.Patch(loadMethod, postfix: new HarmonyMethod(typeof(SaveLoadPatch), nameof(LoadGameServicePostfix)));
					loadPatched++;
				}
				Type legacyType = typeof(GameClock).Assembly.GetType("Game.Services.SaveManager");
				if (legacyType != null)
				{
					MethodInfo legacyLoad = legacyType.GetMethod("LoadGame", BindingFlags.Instance | BindingFlags.Public, null, new Type[1] { typeof(string) }, null);
					if (legacyLoad != null)
					{
						harmony.Patch(legacyLoad, postfix: new HarmonyMethod(typeof(SaveLoadPatch), nameof(LoadNamePostfix)));
						loadPatched++;
					}
					MethodInfo legacySave = legacyType.GetMethod("SaveGame", BindingFlags.Instance | BindingFlags.Public, null, new Type[1] { typeof(string) }, null);
					if (legacySave != null)
					{
						harmony.Patch(legacySave, postfix: new HarmonyMethod(typeof(SaveLoadPatch), nameof(SaveNamePostfix)));
						savePatched++;
					}
				}
				Debug.Log($"[GameplayTweaks] Save/Load enabled saveMethods={savePatched} loadMethods={loadPatched} serviceType={serviceType.FullName}");
			}
				catch (Exception ex)
				{
					Debug.LogWarning("[GameplayTweaks] SaveLoadPatch.ApplyPatch failed: " + ex.Message);
				}
		}

		private static void LoadGameServicePostfix(IPlatformSaveSlotDescriptor slot, ref IEnumerator __result)
		{
			string saveName = GetSaveNameFromSlot(slot);
			__result = LoadAfterGameRoutine(__result, saveName);
		}

		private static void SaveGameServicePostfix(SaveFileMetadata meta, ref IEnumerator __result)
		{
			string saveName = GetSaveNameFromMetadata(meta);
			__result = SaveAfterGameRoutine(__result, saveName);
		}

		private static IEnumerator LoadAfterGameRoutine(IEnumerator original, string saveName)
		{
			while (original != null && original.MoveNext())
			{
				yield return original.Current;
			}
			OnLoadComplete(saveName, "service-coroutine");
		}

		private static IEnumerator SaveAfterGameRoutine(IEnumerator original, string saveName)
		{
			while (original != null && original.MoveNext())
			{
				yield return original.Current;
			}
			OnSaveComplete(saveName, "service-coroutine");
		}

		private static void LoadNamePostfix(string saveName)
		{
			OnLoadComplete(saveName, "legacy-name");
		}

		private static void SaveNamePostfix(string saveName)
		{
			OnSaveComplete(saveName, "legacy-name");
		}

		private static void OnLoadComplete(string saveName, string source)
		{
			if (string.IsNullOrWhiteSpace(saveName))
			{
				Debug.LogWarning("[GameplayTweaks] Load skipped: empty save name source=" + source);
				return;
			}
			VerificationLog("TweaksSave", $"load-hook save={saveName} source={source}");
			ClearDeferredModDataSaveState();
			TurnUpdatePatch.ResetRuntime();
			TurnPerformanceDiagnosticsPatch.ClearRuntimeCheatPlayerSetupCache();
			RouteShopStagingState.ClearAll("load", source);
			LoadModData(saveName);
			DirtyCashEconomyCompatibilityPatch.RequestDeferredHumanTerritoryRefresh("load-postfix", 30, 1);
			ResetTransientPolicePortraitState("load-postfix");
			_lastGangTrackDay = -1;
			_lastGangRelationshipBuffReconcileDay = -1;
			ResetLoyaltyTurnSummary();
			_lastGlobalScavengeableVehicleScrubFrame = -1;
			_lastLowHappinessPromptGlobalDay = int.MinValue;
		}

		private static void OnSaveComplete(string saveName, string source)
		{
			if (string.IsNullOrWhiteSpace(saveName))
			{
				Debug.LogWarning("[GameplayTweaks] Save skipped: empty save name source=" + source);
				return;
			}
			VerificationLog("TweaksSave", $"save-hook save={saveName} source={source}");
			SetSavePaths(saveName);
			SaveModData();
		}

		private static string GetSaveNameFromMetadata(SaveFileMetadata meta)
		{
			return string.IsNullOrWhiteSpace(meta?.slotId) ? string.Empty : meta.slotId;
		}

		private static string GetSaveNameFromSlot(IPlatformSaveSlotDescriptor slot)
		{
			if (slot == null)
			{
				return string.Empty;
			}
			if (!string.IsNullOrWhiteSpace(slot.Directory))
			{
				return slot.Directory;
			}
			return GetSaveNameFromMetadata(slot.Metadata);
		}
	}

	internal static void SaveModData()
		{
		ClearDeferredModDataSaveState();
		if (ShouldBlockCriticalTextRecoverySaveOverwrite())
		{
			if (!_v2CriticalTextRecoverySaveBlockLogged)
			{
				_v2CriticalTextRecoverySaveBlockLogged = true;
				VerificationLog("TweaksSave", $"critical-recovery-save-blocked reason={_v2CriticalTextRecoveryReason} path={GetTweaksSavePathForLog()} loadedCrew={SaveData?.CrewStates?.Count ?? 0} loadedPacts={SaveData?.Pacts?.Count ?? 0} loadedBuffs={SaveData?.GangRelationshipBuffs?.Count ?? 0}");
			}
			return;
		}
		if (ShouldBlockSuspiciousEmptyPactSaveOverwrite())
		{
			return;
		}
		if (string.IsNullOrWhiteSpace(_currentTweaksSaveName)
			&& string.IsNullOrEmpty(_saveFilePath)
			&& string.IsNullOrEmpty(_legacySaveFilePath)
			&& string.IsNullOrEmpty(_v2SaveFilePath))
		{
			return;
		}
		if (ShouldUseV2SaveFormat())
		{
			if (SaveModDataV2())
			{
				return;
			}
			_v2SaveDisabledSaveName = _currentTweaksSaveName;
			Debug.LogWarning("[GameplayTweaks] V2 save failed; using legacy JSON fallback for this save slot until reload.");
			if (!string.IsNullOrEmpty(_legacySaveFilePath))
			{
				_saveFilePath = _legacySaveFilePath;
			}
		}
		try
		{
			System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();
			if (string.IsNullOrEmpty(_saveFilePath))
			{
				return;
			}
			StringBuilder stringBuilder = new StringBuilder();
			VerificationStats verificationStats = EnsureVerifyStats();
			GangMeetingMode gangMeetingMode = GetGangMeetingMode();
			bool flag3 = gangMeetingMode == GangMeetingMode.Auto;
			stringBuilder.Append("{\"NextPactId\":" + SaveData.NextPactId + ",\"PlayerPactId\":" + SaveData.PlayerPactId + ",\"PJI\":" + SaveData.PlayerJoinedPactIndex + ",\"LPJD\":" + SaveData.LastPactJoinDay + ",\"NAP\":" + SaveData.NeverAcceptPacts.ToString().ToLower() + ",\"RERM\":" + SaveData.RobberyPromptsEvadeRefuseMode.ToString().ToLower() + ",\"LOD\":" + CrewRelationshipHandlerPatch._lastOutingDay + ",\"GMB\":" + CrewRelationshipHandlerPatch._globalMayorBribeActive.ToString().ToLower() + ",\"GMBD\":" + CrewRelationshipHandlerPatch._globalMayorBribeExpireDay + ",\"PPWSD\":" + SaveData.PlayerPactWarStartDay + ",\"PPWTID\":" + SaveData.PlayerPactWarTargetId + ",\"PED\":" + SaveData.PactEpochDay + ",\"SCPR\":" + SaveData.SnitchCaseProgress.ToString(CultureInfo.InvariantCulture) + ",\"NSCD\":" + SaveData.NextSnitchCollectionDay + ",\"LSRD\":" + SaveData.LastSnitchRaidDay + ",\"GME\":" + flag3.ToString().ToLower() + ",\"GMM\":" + (int)gangMeetingMode + ",\"GMT\":" + SaveData.GangMeetingTier + ",\"GMI\":" + SaveData.GangMeetingIntervalDays + ",\"GOV\":" + SaveData.GangOpsDefaultsProfileVersion + ",\"V10\":{\"BLC\":" + verificationStats.BossLoyaltyChecks + ",\"BLV\":" + verificationStats.BossLoyaltyViolations + ",\"LCI\":" + verificationStats.LoyaltyCapInitCount + ",\"LDE\":" + verificationStats.LoyaltyDecayEvents + ",\"ZLD\":" + verificationStats.ZeroLoyaltyDefections + ",\"PLG\":" + verificationStats.PepTalkLoyaltyGainEvents + ",\"VLG\":" + verificationStats.VacationLoyaltyGainEvents + ",\"SIR\":" + verificationStats.SnitchIntakeRuns + ",\"SLE\":" + verificationStats.SnitchLeakEvents + ",\"SRT\":" + verificationStats.SnitchRaidTriggers + ",\"RUT\":" + verificationStats.RetainerUpkeepTicks + ",\"RTR\":" + verificationStats.RetainerTrialAssistRolls + ",\"RTW\":" + verificationStats.RetainerTrialAssistWins + "},\"PICD\":{");
			bool picdFirst = true;
			foreach (KeyValuePair<int, int> inviteCd in SaveData.PactInviteCooldowns)
			{
				if (!picdFirst)
				{
					stringBuilder.Append(",");
				}
				picdFirst = false;
				stringBuilder.Append("\"" + inviteCd.Key + "\":" + inviteCd.Value);
			}
			stringBuilder.Append("},\"NH\":{");
			NationalHeatState nationalHeat = SaveData.NationalHeat ?? new NationalHeatState();
			stringBuilder.Append($"\"A\":{nationalHeat.Active.ToString().ToLower()},\"L\":{nationalHeat.Level},\"LED\":{nationalHeat.LastEscalationDay},\"WE\":[");
			if (nationalHeat.WitnessEntries == null)
			{
				nationalHeat.WitnessEntries = new List<ImportantWitnessEntry>();
			}
			bool firstWitnessEntry = true;
			for (int witnessIndex = 0; witnessIndex < nationalHeat.WitnessEntries.Count; witnessIndex++)
			{
				ImportantWitnessEntry witnessEntry = nationalHeat.WitnessEntries[witnessIndex];
				if (witnessEntry == null)
				{
					continue;
				}
				if (!firstWitnessEntry)
				{
					stringBuilder.Append(",");
				}
				firstWitnessEntry = false;
				stringBuilder.Append("{");
				stringBuilder.Append($"\"CP\":{witnessEntry.CrewPeepId},\"ST\":{witnessEntry.SourceType},\"AD\":{witnessEntry.AddedDay},\"DD\":{witnessEntry.ArrestDueDay},");
				stringBuilder.Append($"\"AP\":{witnessEntry.ArrestProcessed.ToString().ToLower()},\"SB\":{witnessEntry.SentenceBoostApplied.ToString().ToLower()}");
				stringBuilder.Append("}");
			}
			stringBuilder.Append("]},\"CrewStates\":{");
			bool flag = true;
			foreach (KeyValuePair<long, CrewModState> crewState in SaveData.CrewStates)
			{
				if (!flag)
				{
					stringBuilder.Append(",");
				}
				flag = false;
				CrewModState value = crewState.Value;
				stringBuilder.Append($"\"{crewState.Key}\":{{\"SCP\":{value.StreetCreditProgress},\"SCL\":{value.StreetCreditLevel},");
				stringBuilder.Append($"\"WL\":{(int)value.WantedLevel},\"WP\":{value.WantedProgress},\"LHL\":{(int)value.LocalHeatLevel},\"LHP\":{value.LocalHeatProgress},\"LHD\":{value.LocalHeatLastRefreshDay},\"LHI\":{value.LocalHeatInitialized.ToString().ToLower()},\"MBA\":{value.MayorBribeActive.ToString().ToLower()},");
				stringBuilder.Append($"\"JBA\":{value.JudgeBribeActive.ToString().ToLower()},\"BER\":{value.BribeExpiresRaw},\"HV\":{value.HappinessValue},\"LV\":{value.LoyaltyValue},\"LCP\":{value.LoyaltyCap},\"LCI\":{value.LoyaltyCapInitialized.ToString().ToLower()},\"LHS\":{value.LowHappinessStreak},\"SW\":{value.SnitchWeight},\"SWI\":{value.SnitchWeightInitialized.ToString().ToLower()},\"SX\":{value.SnitchExposed.ToString().ToLower()},\"SLC\":{value.SnitchLeakCount},\"SLD\":{value.LastSnitchLeakDay},\"SDP\":{value.SnitchDisappearPending.ToString().ToLower()},\"SDD\":{value.SnitchDisappearDueDay},");
				stringBuilder.Append($"\"TU\":{value.TurnsUnhappy},\"OV\":{value.OnVacation.ToString().ToLower()},\"VRR\":{value.VacationReturnsRaw},\"OH\":{value.OnHideout.ToString().ToLower()},\"HRR\":{value.HideoutReturnsRaw},\"HULD\":{value.HideoutLastUpkeepDay},\"HMP\":{value.HideoutMissedPayments},\"HFR\":{value.HideoutForcedReturn.ToString().ToLower()},");
				stringBuilder.Append("\"IU\":" + value.IsUnderboss.ToString().ToLower() + ",\"ACB\":" + value.AwaitingChildBirth.ToString().ToLower() + ",");
				stringBuilder.Append($"\"LFKC\":{value.LastFutureKidsCount},\"LFS\":{value.LookingForSpouse.ToString().ToLower()},\"SSD\":{value.SpouseSearchStartDay},\"NSD\":{value.NextSpouseSearchDay},\"SRD\":{value.SpouseSearchResolveNotBeforeDay},\"LSDS\":{value.LastSpouseDatingSpend},\"SSTS\":{value.SpouseSearchTotalSpend},\"LBSC\":{value.LastBoozeSoldCount},\"PTD\":{value.LastManualPepTalkDay},\"OJE\":{value.OddJobsEnabled.ToString().ToLower()},\"LOJD\":{value.LastOddJobDay},\"TOJE\":{value.TotalOddJobEarnings},");
				stringBuilder.Append($"\"VP\":{value.VacationPending.ToString().ToLower()},\"VD\":{value.VacationDuration},\"HP\":{value.HideoutPending.ToString().ToLower()},\"HD\":{value.HideoutDuration},");
				stringBuilder.Append($"\"FAC\":{value.FedArrivalCountdown},\"FI\":{value.FedsIncoming.ToString().ToLower()},");
				stringBuilder.Append("\"HW\":" + value.HasWitness.ToString().ToLower() + ",\"WC\":" + value.WitnessCount + ",\"FWC\":" + value.FederalWitnessCount + ",\"WTS\":" + value.WitnessThreatenedSuccessfully.ToString().ToLower() + ",");
				stringBuilder.Append($"\"WTA\":{value.WitnessThreatAttempted.ToString().ToLower()},\"EJY\":{value.ExtraJailYears},");
				stringBuilder.Append($"\"LR\":{value.LawyerRetainer},\"LRC\":{value.LawyerRetainerConfirmed.ToString().ToLower()},\"LRD\":{value.LastRetainerDeductDay},\"CD\":{value.CaseDismissed.ToString().ToLower()},\"LBST\":{value.LastBoozeSellTurn},\"TBSS\":{value.TotalBoozeSoldStreet},\"TBSL\":{value.TotalBoozeSoldLifetime}}}");
			}
			stringBuilder.Append("},\"Pacts\":[");
			bool flag2 = true;
			foreach (AlliancePact pact in SaveData.Pacts)
			{
				if (!flag2)
				{
					stringBuilder.Append(",");
				}
				flag2 = false;
				stringBuilder.Append(string.Format("{{\"PI\":\"{0}\",\"PN\":\"{1}\",\"CI\":{2},", pact.PactId, EscapeJsonString(pact.PactName ?? ""), pact.ColorIndex));
				stringBuilder.Append($"\"LG\":{pact.LeaderGangId},\"CR\":{pact.ColorR},\"CG\":{pact.ColorG},\"CB\":{pact.ColorB},");
				stringBuilder.Append($"\"FD\":{pact.FormedDays},\"IP\":{pact.IsPending.ToString().ToLower()},\"PV\":{pact.PlayerInvited.ToString().ToLower()},\"PCC\":{pact.PlayerColorConfirmed.ToString().ToLower()},");
				stringBuilder.Append($"\"ER\":{pact.EarningRate.ToString(CultureInfo.InvariantCulture)},\"CCB\":{pact.CrewCapacityBonus},\"LVD\":{pact.LastVoteDay},\"LVT\":{pact.LastVoteType},\"PVT\":{ClampPactVoteChoice(pact.PlayerProposedVote, allowNone: true)},\"PVCD\":{pact.PendingVoteCycleDay},\"PVPS\":{pact.VotePromptShown.ToString().ToLower()},\"PVPA\":{pact.VotePromptAnswered.ToString().ToLower()},");
				stringBuilder.Append("\"MI\":[");
				for (int i = 0; i < pact.MemberIds.Count; i++)
				{
					if (i > 0)
					{
						stringBuilder.Append(",");
					}
					stringBuilder.Append(pact.MemberIds[i]);
				}
				stringBuilder.Append("],\"BH\":{");
				bool bhFirst = true;
				foreach (var bh in pact.BossHappiness)
				{
					if (!bhFirst) stringBuilder.Append(",");
					bhFirst = false;
					stringBuilder.Append($"\"{bh.Key}\":{bh.Value.ToString(CultureInfo.InvariantCulture)}");
				}
				stringBuilder.Append("},\"VPR\":{");
				bool vpFirst = true;
				if (pact.VotePreferences == null)
				{
					pact.VotePreferences = new Dictionary<int, int>();
				}
				foreach (KeyValuePair<int, int> pref in pact.VotePreferences)
				{
					if (!vpFirst) stringBuilder.Append(",");
					vpFirst = false;
					stringBuilder.Append($"\"{pref.Key}\":{ClampPactVoteChoice(pref.Value, allowNone: false)}");
				}
				stringBuilder.Append("}}");
			}
			stringBuilder.Append("],\"GV\":[");
			for (int j = 0; j < SaveData.GrapevineEvents.Count; j++)
			{
				if (j > 0)
				{
					stringBuilder.Append(",");
				}
				stringBuilder.Append("\"" + EscapeJsonString(SaveData.GrapevineEvents[j]) + "\"");
			}
			stringBuilder.AppendLine("]}");
			File.WriteAllText(_saveFilePath, stringBuilder.ToString());
			stopwatch.Stop();
			if (stopwatch.ElapsedMilliseconds >= 20)
			{
				Debug.Log($"[PERF][TweaksSave] layout={GetTweaksSavePathKind(_saveFilePath)} writer=legacy-json path={GetTweaksSavePathForLog()} ms={stopwatch.ElapsedMilliseconds} crew={SaveData.CrewStates.Count} pacts={SaveData.Pacts.Count} pactMembers={GetPactMemberCountForSaveLog()} buffs={SaveData.GangRelationshipBuffs.Count} grapevine={SaveData.GrapevineEvents.Count}");
			}
		}
		catch (Exception arg)
		{
			Debug.LogError($"[GameplayTweaks] Save failed: {arg}");
		}
	}

	internal static void QueueDeferredModDataSave(string source, SimTime now, int delayFrames)
	{
		try
		{
			ClearDeferredModDataSaveState();
		}
		catch (Exception ex)
		{
			Debug.LogWarning("[GameplayTweaks] Deferred mod save queue failed: " + ex.GetType().Name + ":" + ex.Message);
		}
	}

	internal static void FlushDeferredModDataSave(string source)
	{
		try
		{
			if (!_deferredModDataSaveQueued)
			{
				return;
			}

			int frame = Time.frameCount;
			string waitReason = GetDeferredModDataSaveFlushWaitReason(frame);
			if (waitReason != null)
			{
				_deferredModDataSaveDeferrals++;
				if (_deferredModDataSaveDeferrals == 1 || _deferredModDataSaveDeferrals % 60 == 0)
				{
					Debug.Log("[PERF][TweaksSaveDeferred] wait source=" + (_deferredModDataSaveSource ?? "unknown") + " flushSource=" + (source ?? "unknown") + " reason=" + waitReason + " deferrals=" + _deferredModDataSaveDeferrals + " frame=" + frame + " earliest=" + _deferredModDataSaveEarliestFrame + " lastQueued=" + _deferredModDataSaveLastQueuedFrame + " sourceCount=" + _deferredModDataSaveSourceCount);
				}

				return;
			}

			int queuedDay = _deferredModDataSaveDay;
			int queuedTurn = _deferredModDataSaveTurn;
			string queuedSource = _deferredModDataSaveSource ?? "unknown";
			int deferrals = _deferredModDataSaveDeferrals;
			ClearDeferredModDataSaveState();

			long startTicks = System.Diagnostics.Stopwatch.GetTimestamp();
			SaveModData();
			long elapsedMs = (long)((System.Diagnostics.Stopwatch.GetTimestamp() - startTicks) * 1000.0 / System.Diagnostics.Stopwatch.Frequency);
			Debug.Log("[PERF][TweaksSaveDeferred] flushed source=" + queuedSource + " flushSource=" + (source ?? "unknown") + " ms=" + elapsedMs + " queuedDay=" + queuedDay + " queuedTurn=" + queuedTurn + " day=" + (global::Game.Game.ctx?.clock?.Now.days ?? -1) + " turn=" + (global::Game.Game.ctx?.clock?.CurrentTurn ?? -1) + " deferrals=" + deferrals + " frame=" + frame);
		}
		catch (Exception ex)
		{
			ClearDeferredModDataSaveState();
			Debug.LogWarning("[GameplayTweaks] Deferred mod save flush failed: " + ex.GetType().Name + ":" + ex.Message);
		}
	}

	private static string GetDeferredModDataSaveFlushWaitReason(int frame)
	{
		bool allowMaxWaitOverride = _deferredModDataSaveQueuedFrame >= 0 && frame - _deferredModDataSaveQueuedFrame >= DeferredModDataSaveMaxWaitFrames;
		if (frame < _deferredModDataSaveEarliestFrame && !allowMaxWaitOverride)
		{
			return "earliest";
		}
		if (ShouldDeferUiMaintenanceForMouseInput() && !allowMaxWaitOverride)
		{
			return "input";
		}

		try
		{
			if (global::Game.Game.ctx?.clock != null && !global::Game.Game.ctx.clock.CurrentPlayer.IsHumanPlayer)
			{
				return "nonhuman-turn";
			}
		}
		catch
		{
		}

		if (_deferredModDataSaveLastQueuedFrame >= 0
			&& frame - _deferredModDataSaveLastQueuedFrame < DeferredModDataSaveQuietFrames
			&& !allowMaxWaitOverride)
		{
			return "quiet-window";
		}

		return null;
	}

	private static string AppendDeferredModDataSaveSource(string existing, string source)
	{
		string nextSource = source ?? "unknown";
		if (string.IsNullOrEmpty(existing))
		{
			return nextSource;
		}

		if (existing.Length + nextSource.Length + 1 <= DeferredModDataSaveMaxSourceLogLength)
		{
			return existing + "+" + nextSource;
		}

		return existing.IndexOf("+...", StringComparison.Ordinal) >= 0
			? existing
			: existing + "+...";
	}

	private static void ClearDeferredModDataSaveState()
	{
		_deferredModDataSaveQueued = false;
		_deferredModDataSaveDay = int.MinValue;
		_deferredModDataSaveTurn = int.MinValue;
		_deferredModDataSaveQueuedFrame = -1;
		_deferredModDataSaveLastQueuedFrame = -1;
		_deferredModDataSaveEarliestFrame = -1;
		_deferredModDataSaveDeferrals = 0;
		_deferredModDataSaveSourceCount = 0;
		_deferredModDataSaveSource = null;
	}

	private static string EscapeJsonString(string s)
	{
		return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
	}

	internal static void LoadModData(string saveName)
	{
		System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();
		string source = "none";
		try
		{
			SetSavePaths(saveName);
			SaveData = new ModSaveData();
			_legacyLoadPendingMigrationLog = false;
			_v2CriticalTextRecoveryReadOnly = false;
			_v2CriticalTextRecoverySaveBlockLogged = false;
			_v2CriticalTextRecoveryReason = string.Empty;
			if (LoadModDataV2(saveName))
			{
				source = "v2";
				EnsureSaveDataDefaults();
				RuntimePromptEarliestFrame = Time.frameCount + 120;
				NormalizeCrewStateAfterLoad();
				RefreshPactCache();
				PactColorUiPatch.RequestFullCrewPickRefresh("load-v2", 30);
				Debug.Log($"[GameplayTweaks] Loaded {SaveData.CrewStates.Count} crew states, {SaveData.Pacts.Count} pacts, {SaveData.GrapevineEvents.Count} grapevine events");
				return;
			}
			if (TryLoadLegacyJson(saveName))
			{
				source = "legacy";
				_legacyLoadPendingMigrationLog = true;
				EnsureSaveDataDefaults();
				RuntimePromptEarliestFrame = Time.frameCount + 120;
				NormalizeCrewStateAfterLoad();
				RefreshPactCache();
				PactColorUiPatch.RequestFullCrewPickRefresh("load-legacy", 30);
				Debug.Log($"[GameplayTweaks] Loaded {SaveData.CrewStates.Count} crew states, {SaveData.Pacts.Count} pacts, {SaveData.GrapevineEvents.Count} grapevine events");
				return;
			}
			_saveFilePath = _legacySaveFilePath;
			if (string.IsNullOrEmpty(_saveFilePath))
			{
				Debug.Log("[GameplayTweaks] No save file found, starting fresh");
				source = "none";
				RuntimePromptEarliestFrame = Time.frameCount + 120;
				return;
			}
			if (!File.Exists(_saveFilePath))
			{
				Debug.Log("[GameplayTweaks] No save file found, starting fresh");
				source = "none";
				RuntimePromptEarliestFrame = Time.frameCount + 120;
				return;
			}
			source = "legacy-slow";
			_legacyLoadPendingMigrationLog = true;
			string text = File.ReadAllText(_saveFilePath);
			SaveData.NextPactId = JInt(text, "NextPactId", 0);
			SaveData.PlayerPactId = JInt(text, "PlayerPactId", -1);
			SaveData.PlayerJoinedPactIndex = JInt(text, "PJI", -1);
			SaveData.LastPactJoinDay = JInt(text, "LPJD", -1);
			SaveData.NeverAcceptPacts = JBool(text, "NAP", d: false);
			SaveData.RobberyPromptsEvadeRefuseMode = JBool(text, "RERM", d: false);
			CrewRelationshipHandlerPatch._lastOutingDay = JInt(text, "LOD", -1);
			CrewRelationshipHandlerPatch._globalMayorBribeActive = JBool(text, "GMB", d: false);
			CrewRelationshipHandlerPatch._globalMayorBribeExpireDay = JInt(text, "GMBD", -1);
			SaveData.PlayerPactWarStartDay = JInt(text, "PPWSD", -1);
			SaveData.PlayerPactWarTargetId = JInt(text, "PPWTID", -1);
			SaveData.PactEpochDay = JInt(text, "PED", -1);
			SaveData.SnitchCaseProgress = Mathf.Clamp(JFloat(text, "SCPR", 0f), 0f, 100f);
			SaveData.NextSnitchCollectionDay = JInt(text, "NSCD", -1);
			SaveData.LastSnitchRaidDay = JInt(text, "LSRD", -1);
			RuntimePromptEarliestFrame = Time.frameCount + 120;
			bool flag3 = JBool(text, "GME", d: true);
			int d = JInt(text, "GMM", -1);
			SaveData.GangMeetingMode = ResolveGangMeetingModeFromLegacy(d, flag3, JRaw(text, "GMM") != null);
			SaveData.GangMeetingsEnabled = SaveData.GangMeetingMode == GangMeetingMode.Auto;
			SaveData.GangMeetingTier = Mathf.Clamp(JInt(text, "GMT", 0), 0, 2);
			SaveData.GangMeetingIntervalDays = Mathf.Max(1, JInt(text, "GMI", GANG_MEETING_INTERVAL_DAYS));
			SaveData.GangOpsDefaultsProfileVersion = JInt(text, "GOV", 0);
			VerificationStats verificationStats = EnsureVerifyStats();
			int v10Idx = text.IndexOf("\"V10\":{");
			if (v10Idx >= 0)
			{
				int v10Start = text.IndexOf('{', v10Idx + 6);
				int v10End = MatchBrace(text, v10Start);
				if (v10End > v10Start)
				{
					string v10 = text.Substring(v10Start, v10End - v10Start + 1);
					verificationStats.BossLoyaltyChecks = JInt(v10, "BLC", 0);
					verificationStats.BossLoyaltyViolations = JInt(v10, "BLV", 0);
					verificationStats.LoyaltyCapInitCount = JInt(v10, "LCI", 0);
					verificationStats.LoyaltyDecayEvents = JInt(v10, "LDE", 0);
					verificationStats.ZeroLoyaltyDefections = JInt(v10, "ZLD", 0);
					verificationStats.PepTalkLoyaltyGainEvents = JInt(v10, "PLG", 0);
					verificationStats.VacationLoyaltyGainEvents = JInt(v10, "VLG", 0);
					verificationStats.SnitchIntakeRuns = JInt(v10, "SIR", 0);
					verificationStats.SnitchLeakEvents = JInt(v10, "SLE", 0);
					verificationStats.SnitchRaidTriggers = JInt(v10, "SRT", 0);
					verificationStats.RetainerUpkeepTicks = JInt(v10, "RUT", 0);
					verificationStats.RetainerTrialAssistRolls = JInt(v10, "RTR", 0);
					verificationStats.RetainerTrialAssistWins = JInt(v10, "RTW", 0);
				}
			}
			int picdIdx = text.IndexOf("\"PICD\":{");
			if (picdIdx >= 0)
			{
				int picdStart = text.IndexOf('{', picdIdx + 7);
				int picdEnd = MatchBrace(text, picdStart);
				if (picdEnd > picdStart + 1)
				{
					string picdContent = text.Substring(picdStart + 1, picdEnd - picdStart - 1);
					int picdPos = 0;
					while (picdPos < picdContent.Length)
					{
						int q1 = picdContent.IndexOf('"', picdPos);
						if (q1 < 0) break;
						int q2 = picdContent.IndexOf('"', q1 + 1);
						if (q2 < 0) break;
						string idRaw = picdContent.Substring(q1 + 1, q2 - q1 - 1);
						int colon = picdContent.IndexOf(':', q2);
						if (colon < 0) break;
						int valEnd = picdContent.IndexOf(',', colon);
						if (valEnd < 0) valEnd = picdContent.Length;
						string dayRaw = picdContent.Substring(colon + 1, valEnd - colon - 1).Trim();
						if (int.TryParse(idRaw, out int gangId) && int.TryParse(dayRaw, out int lastDay))
						{
							SaveData.PactInviteCooldowns[gangId] = lastDay;
						}
						picdPos = valEnd + 1;
					}
				}
			}
			SaveData.NationalHeat = new NationalHeatState();
			int nhIdx = text.IndexOf("\"NH\":{");
			if (nhIdx >= 0)
			{
				int nhStart = text.IndexOf('{', nhIdx + 5);
				int nhEnd = MatchBrace(text, nhStart);
				if (nhEnd > nhStart)
				{
					string nhJson = text.Substring(nhStart, nhEnd - nhStart + 1);
					SaveData.NationalHeat.Active = JBool(nhJson, "A", d: false);
					SaveData.NationalHeat.Level = JInt(nhJson, "L", 0);
					SaveData.NationalHeat.LastEscalationDay = JInt(nhJson, "LED", -1);
					int weIdx = nhJson.IndexOf("\"WE\":[");
					if (weIdx >= 0)
					{
						int weStart = nhJson.IndexOf('[', weIdx);
						int weEnd = MatchBracket(nhJson, weStart);
						if (weEnd > weStart + 1)
						{
							string weContent = nhJson.Substring(weStart + 1, weEnd - weStart - 1);
							int wePos = 0;
							while (wePos < weContent.Length)
							{
								int eStart = weContent.IndexOf('{', wePos);
								if (eStart < 0)
								{
									break;
								}
								int eEnd = MatchBrace(weContent, eStart);
								if (eEnd < 0)
								{
									break;
								}
								string ej = weContent.Substring(eStart, eEnd - eStart + 1);
								ImportantWitnessEntry entry = new ImportantWitnessEntry();
								entry.CrewPeepId = JLong(ej, "CP", 0L);
								entry.SourceType = JInt(ej, "ST", 0);
								entry.AddedDay = JInt(ej, "AD", -1);
								entry.ArrestDueDay = JInt(ej, "DD", -1);
								entry.ArrestProcessed = JBool(ej, "AP", d: false);
								entry.SentenceBoostApplied = JBool(ej, "SB", d: false);
								SaveData.NationalHeat.WitnessEntries.Add(entry);
								wePos = eEnd + 1;
							}
						}
					}
				}
			}
			int num = text.IndexOf("\"CrewStates\":{");
			if (num >= 0)
			{
				int num2 = text.IndexOf('{', num + 13);
				int num3 = MatchBrace(text, num2);
				if (num3 > num2)
				{
					string text2 = text.Substring(num2 + 1, num3 - num2 - 1);
					int num4 = 0;
					while (num4 < text2.Length)
					{
						int num5 = text2.IndexOf('"', num4);
						if (num5 < 0)
						{
							break;
						}
						int num6 = text2.IndexOf('"', num5 + 1);
						if (num6 < 0)
						{
							break;
						}
						string s = text2.Substring(num5 + 1, num6 - num5 - 1);
						int num7 = text2.IndexOf('{', num6);
						if (num7 < 0)
						{
							break;
						}
						int num8 = MatchBrace(text2, num7);
						if (num8 < 0)
						{
							break;
						}
						string j = text2.Substring(num7, num8 - num7 + 1);
						CrewModState crewModState = new CrewModState();
						crewModState.StreetCreditProgress = JFloat(j, "SCP", 0f);
						crewModState.StreetCreditLevel = JInt(j, "SCL", 0);
						crewModState.WantedLevel = (WantedLevel)JInt(j, "WL", 0);
						crewModState.WantedProgress = JFloat(j, "WP", 0f);
						crewModState.LocalHeatLevel = (WantedLevel)JInt(j, "LHL", (int)crewModState.WantedLevel);
						crewModState.LocalHeatProgress = JFloat(j, "LHP", crewModState.WantedProgress);
						crewModState.LocalHeatLastRefreshDay = JInt(j, "LHD", -1);
						crewModState.LocalHeatInitialized = JBool(j, "LHI", d: crewModState.LocalHeatProgress > 0f || crewModState.LocalHeatLevel != WantedLevel.None);
						crewModState.MayorBribeActive = JBool(j, "MBA", d: false);
						crewModState.JudgeBribeActive = JBool(j, "JBA", d: false);
						crewModState.BribeExpiresRaw = JInt(j, "BER", 0);
						crewModState.HappinessValue = JFloat(j, "HV", 1f);
						crewModState.LoyaltyValue = JFloat(j, "LV", 0.5f);
						crewModState.LoyaltyValue = Mathf.Clamp01(crewModState.LoyaltyValue);
						crewModState.LoyaltyCap = Mathf.Clamp(JFloat(j, "LCP", 1f), 0.1f, 1f);
						crewModState.LoyaltyCapInitialized = JBool(j, "LCI", d: false);
						crewModState.LowHappinessStreak = Mathf.Max(0, JInt(j, "LHS", 0));
						crewModState.SnitchWeight = Mathf.Clamp(JFloat(j, "SW", 0f), 0f, 0.95f);
						crewModState.SnitchWeightInitialized = JBool(j, "SWI", d: false);
						crewModState.SnitchExposed = JBool(j, "SX", d: false);
						crewModState.SnitchLeakCount = Mathf.Max(0, JInt(j, "SLC", 0));
						crewModState.LastSnitchLeakDay = JInt(j, "SLD", -1);
						crewModState.SnitchDisappearPending = JBool(j, "SDP", d: false);
						crewModState.SnitchDisappearDueDay = JInt(j, "SDD", -1);
						crewModState.TurnsUnhappy = JInt(j, "TU", 0);
						crewModState.OnVacation = JBool(j, "OV", d: false);
						crewModState.VacationReturnsRaw = JInt(j, "VRR", 0);
						crewModState.OnHideout = JBool(j, "OH", d: false);
						crewModState.HideoutReturnsRaw = JInt(j, "HRR", 0);
						crewModState.HideoutLastUpkeepDay = JInt(j, "HULD", -1);
						crewModState.HideoutMissedPayments = Mathf.Max(0, JInt(j, "HMP", 0));
						crewModState.HideoutForcedReturn = JBool(j, "HFR", d: false);
						crewModState.IsUnderboss = JBool(j, "IU", d: false);
						crewModState.AwaitingChildBirth = JBool(j, "ACB", d: false);
						crewModState.LastFutureKidsCount = JInt(j, "LFKC", 0);
						crewModState.LookingForSpouse = JBool(j, "LFS", d: false);
						crewModState.SpouseSearchStartDay = JInt(j, "SSD", -1);
						crewModState.NextSpouseSearchDay = JInt(j, "NSD", -1);
						crewModState.SpouseSearchResolveNotBeforeDay = JInt(j, "SRD", -1);
						crewModState.LastSpouseDatingSpend = Mathf.Max(0, JInt(j, "LSDS", 0));
						crewModState.SpouseSearchTotalSpend = Mathf.Max(0, JInt(j, "SSTS", 0));
						crewModState.LastBoozeSoldCount = JInt(j, "LBSC", 0);
						crewModState.LastManualPepTalkDay = JInt(j, "PTD", -1);
						crewModState.OddJobsEnabled = JBool(j, "OJE", d: false);
						crewModState.LastOddJobDay = JInt(j, "LOJD", -1);
						crewModState.TotalOddJobEarnings = Mathf.Max(0, JInt(j, "TOJE", 0));
						crewModState.VacationPending = JBool(j, "VP", d: false);
						crewModState.VacationDuration = JInt(j, "VD", 0);
						crewModState.HideoutPending = JBool(j, "HP", d: false);
						crewModState.HideoutDuration = JInt(j, "HD", HIDEOUT_DURATION_DAYS);
						crewModState.HideoutDuration = Mathf.Max(1, crewModState.HideoutDuration);
						crewModState.FedArrivalCountdown = JInt(j, "FAC", 0);
						crewModState.FedsIncoming = JBool(j, "FI", d: false);
						crewModState.HasWitness = JBool(j, "HW", d: false);
						crewModState.WitnessCount = JInt(j, "WC", crewModState.HasWitness ? 1 : 0);
						crewModState.FederalWitnessCount = JInt(j, "FWC", 0);
						crewModState.FederalWitnessCount = Mathf.Max(0, crewModState.FederalWitnessCount);
						crewModState.WitnessCount = Mathf.Max(crewModState.WitnessCount, crewModState.FederalWitnessCount);
						if (crewModState.FederalWitnessCount > 0)
						{
							crewModState.HasWitness = true;
						}
						crewModState.WitnessThreatenedSuccessfully = JBool(j, "WTS", d: false);
						crewModState.WitnessThreatAttempted = JBool(j, "WTA", d: false);
						crewModState.ExtraJailYears = JInt(j, "EJY", 0);
						crewModState.LawyerRetainer = JInt(j, "LR", 0);
						crewModState.LawyerRetainerConfirmed = JBool(j, "LRC", d: false);
						crewModState.LastRetainerDeductDay = JInt(j, "LRD", -1);
						crewModState.CaseDismissed = JBool(j, "CD", d: false);
						crewModState.LastBoozeSellTurn = JInt(j, "LBST", 0);
						crewModState.TotalBoozeSoldStreet = JInt(j, "TBSS", 0);
						crewModState.TotalBoozeSoldLifetime = JInt(j, "TBSL", 0);
						SyncLocalHeatFromLegacyFields(crewModState);
						if (long.TryParse(s, out var result))
						{
							SaveData.CrewStates[result] = crewModState;
						}
						num4 = num8 + 1;
					}
				}
			}
			int num9 = text.IndexOf("\"Pacts\":[");
			if (num9 >= 0)
			{
				int num10 = text.IndexOf('[', num9);
				int num11 = MatchBracket(text, num10);
				if (num11 > num10)
				{
					string text3 = text.Substring(num10 + 1, num11 - num10 - 1);
					int num12 = 0;
					while (num12 < text3.Length)
					{
						int num13 = text3.IndexOf('{', num12);
						if (num13 < 0)
						{
							break;
						}
						int num14 = MatchBrace(text3, num13);
						if (num14 < 0)
						{
							break;
						}
						string text4 = text3.Substring(num13, num14 - num13 + 1);
						AlliancePact alliancePact = new AlliancePact();
						alliancePact.PactId = JStr(text4, "PI", "");
						alliancePact.PactName = JStr(text4, "PN", "");
						alliancePact.ColorIndex = JInt(text4, "CI", 0);
						alliancePact.LeaderGangId = JInt(text4, "LG", -1);
						alliancePact.ColorR = JFloat(text4, "CR", 1f);
						alliancePact.ColorG = JFloat(text4, "CG", 1f);
						alliancePact.ColorB = JFloat(text4, "CB", 1f);
						alliancePact.FormedDays = JInt(text4, "FD", 0);
						alliancePact.IsPending = JBool(text4, "IP", d: false);
						alliancePact.PlayerInvited = JBool(text4, "PV", d: false);
						alliancePact.PlayerColorConfirmed = JBool(text4, "PCC", d: true);
						alliancePact.EarningRate = JFloat(text4, "ER", 0.05f);
						alliancePact.CrewCapacityBonus = JInt(text4, "CCB", 0);
						alliancePact.LastVoteDay = JInt(text4, "LVD", -1);
						alliancePact.LastVoteType = JInt(text4, "LVT", 0);
						alliancePact.PlayerProposedVote = ClampPactVoteChoice(JInt(text4, "PVT", -1), allowNone: true);
						alliancePact.PendingVoteCycleDay = JInt(text4, "PVCD", -1);
						alliancePact.VotePromptShown = JBool(text4, "PVPS", d: false);
						alliancePact.VotePromptAnswered = JBool(text4, "PVPA", d: false);
						int num15 = text4.IndexOf("\"MI\":[");
						if (num15 >= 0)
						{
							int num16 = text4.IndexOf('[', num15);
							int num17 = text4.IndexOf(']', num16);
							if (num17 > num16 + 1)
							{
								string[] array = text4.Substring(num16 + 1, num17 - num16 - 1).Split(',');
								for (int i = 0; i < array.Length; i++)
								{
									if (int.TryParse(array[i].Trim(), out var result2))
									{
										alliancePact.MemberIds.Add(result2);
									}
								}
							}
						}
						// Load boss happiness
						int bhIdx = text4.IndexOf("\"BH\":{");
						if (bhIdx >= 0)
						{
							int bhStart = text4.IndexOf('{', bhIdx + 4);
							int bhEnd = MatchBrace(text4, bhStart);
							if (bhEnd > bhStart + 1)
							{
								string bhContent = text4.Substring(bhStart + 1, bhEnd - bhStart - 1);
								int bhPos = 0;
								while (bhPos < bhContent.Length)
								{
									int q1 = bhContent.IndexOf('"', bhPos);
									if (q1 < 0) break;
									int q2 = bhContent.IndexOf('"', q1 + 1);
									if (q2 < 0) break;
									string bhKey = bhContent.Substring(q1 + 1, q2 - q1 - 1);
									int colon = bhContent.IndexOf(':', q2);
									if (colon < 0) break;
									int valEnd = bhContent.IndexOf(',', colon);
									if (valEnd < 0) valEnd = bhContent.Length;
									string bhVal = bhContent.Substring(colon + 1, valEnd - colon - 1).Trim();
									if (int.TryParse(bhKey, out int bhGangId) && float.TryParse(bhVal, NumberStyles.Float, CultureInfo.InvariantCulture, out float bhHap))
									{
										alliancePact.BossHappiness[bhGangId] = bhHap;
									}
									bhPos = valEnd + 1;
								}
							}
						}
						int vprIdx = text4.IndexOf("\"VPR\":{");
						if (vprIdx >= 0)
						{
							int vprStart = text4.IndexOf('{', vprIdx + 5);
							int vprEnd = MatchBrace(text4, vprStart);
							if (vprEnd > vprStart + 1)
							{
								string vprContent = text4.Substring(vprStart + 1, vprEnd - vprStart - 1);
								int vprPos = 0;
								while (vprPos < vprContent.Length)
								{
									int q1 = vprContent.IndexOf('"', vprPos);
									if (q1 < 0) break;
									int q2 = vprContent.IndexOf('"', q1 + 1);
									if (q2 < 0) break;
									string voteKey = vprContent.Substring(q1 + 1, q2 - q1 - 1);
									int colon = vprContent.IndexOf(':', q2);
									if (colon < 0) break;
									int valEnd = vprContent.IndexOf(',', colon);
									if (valEnd < 0) valEnd = vprContent.Length;
									string voteVal = vprContent.Substring(colon + 1, valEnd - colon - 1).Trim();
									if (int.TryParse(voteKey, out int voteGangId) && int.TryParse(voteVal, out int voteType))
									{
										alliancePact.VotePreferences[voteGangId] = ClampPactVoteChoice(voteType, allowNone: false);
									}
									vprPos = valEnd + 1;
								}
							}
						}
						SaveData.Pacts.Add(alliancePact);
						num12 = num14 + 1;
					}
				}
			}
			int num18 = text.IndexOf("\"GV\":[");
			if (num18 >= 0)
			{
				int num19 = text.IndexOf('[', num18);
				int num20 = MatchBracket(text, num19);
				if (num20 > num19 + 1)
				{
					string text5 = text.Substring(num19 + 1, num20 - num19 - 1);
					int num21 = 0;
					while (num21 < text5.Length)
					{
						int num22 = text5.IndexOf('"', num21);
						if (num22 < 0)
						{
							break;
						}
						int num23 = text5.IndexOf('"', num22 + 1);
						if (num23 < 0)
						{
							break;
						}
						SaveData.GrapevineEvents.Add(text5.Substring(num22 + 1, num23 - num22 - 1));
						num21 = num23 + 1;
					}
				}
			}
			RefreshPactCache();
			Debug.Log($"[GameplayTweaks] Loaded {SaveData.CrewStates.Count} crew states, {SaveData.Pacts.Count} pacts, {SaveData.GrapevineEvents.Count} grapevine events");
		}
		catch (Exception arg)
		{
			Debug.LogError($"[GameplayTweaks] Load failed: {arg}");
			SaveData = new ModSaveData();
			source = "error";
		}
		finally
		{
			stopwatch.Stop();
			EnsureSaveDataDefaults();
			_lastModDataLoadSource = source;
			_lastModDataLoadPactCount = SaveData?.Pacts?.Count ?? 0;
			Debug.Log($"[PERF][TweaksLoad] source={source} path={GetTweaksSavePathForLog()} ms={stopwatch.ElapsedMilliseconds} crew={SaveData.CrewStates.Count} pacts={SaveData.Pacts.Count} pactMembers={GetPactMemberCountForSaveLog()} buffs={SaveData.GangRelationshipBuffs.Count} pactHeat={SaveData.PactWarHeat.Count} pactRevenge={SaveData.PactRevengeQueue.Count} independentHeat={SaveData.IndependentWarHeat.Count} independentRevenge={SaveData.IndependentRevengeQueue.Count} grapevine={SaveData.GrapevineEvents.Count}");
		}
	}

	private static bool SaveModDataV2()
	{
		System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();
		try
		{
			// Migration boundary: once legacy data is loaded, subsequent saves are persisted as V2 envelope.
			if (string.IsNullOrEmpty(_v2SaveFilePath) && !string.IsNullOrEmpty(_saveFilePath))
			{
				_v2SaveFilePath = _saveFilePath;
			}
			if (string.IsNullOrEmpty(_v2SaveFilePath))
			{
				return false;
			}
			EnsureSaveDataDefaults();
			TweaksSaveEnvelopeV2 tweaksSaveEnvelopeV = new TweaksSaveEnvelopeV2();
			tweaksSaveEnvelopeV.Version = TweaksSaveVersionCurrent;
			tweaksSaveEnvelopeV.Data = SaveData;
			tweaksSaveEnvelopeV.LastOutingDay = CrewRelationshipHandlerPatch._lastOutingDay;
			tweaksSaveEnvelopeV.GlobalMayorBribeActive = CrewRelationshipHandlerPatch._globalMayorBribeActive;
			tweaksSaveEnvelopeV.GlobalMayorBribeExpireDay = CrewRelationshipHandlerPatch._globalMayorBribeExpireDay;
			long serializeTicks = stopwatch.ElapsedMilliseconds;
			string contents = SerializeTweaksSaveEnvelopeV2(tweaksSaveEnvelopeV);
			long serializedMs = stopwatch.ElapsedMilliseconds - serializeTicks;
			long writeTicks = stopwatch.ElapsedMilliseconds;
			bool dataWritten = WriteTextAtomicIfChanged(_v2SaveFilePath, contents);
			long dataWriteMs = stopwatch.ElapsedMilliseconds - writeTicks;
			long manifestTicks = stopwatch.ElapsedMilliseconds;
			bool manifestWritten = WriteTweaksSaveManifest();
			long manifestMs = stopwatch.ElapsedMilliseconds - manifestTicks;
			stopwatch.Stop();
			Debug.Log($"[PERF][TweaksSave] layout={GetTweaksSavePathKind(_v2SaveFilePath)} writer=fields-only-sion path={GetTweaksSavePathForLog()} manifest={(_v2ManifestFilePath ?? string.Empty)} ms={stopwatch.ElapsedMilliseconds} serializeMs={serializedMs} dataWriteMs={dataWriteMs} manifestMs={manifestMs} dataWritten={dataWritten} manifestWritten={manifestWritten} crew={SaveData.CrewStates.Count} pacts={SaveData.Pacts.Count} pactMembers={GetPactMemberCountForSaveLog()} buffs={SaveData.GangRelationshipBuffs.Count} pactHeat={SaveData.PactWarHeat.Count} pactRevenge={SaveData.PactRevengeQueue.Count} independentHeat={SaveData.IndependentWarHeat.Count} independentRevenge={SaveData.IndependentRevengeQueue.Count}");
			if (_legacyLoadPendingMigrationLog)
			{
				Debug.Log("[PERF][TweaksLoad] migration legacy-to-moddata-v2 completed");
				_legacyLoadPendingMigrationLog = false;
			}
			return true;
		}
		catch (Exception arg)
		{
			Debug.LogError($"[GameplayTweaks] Save failed: {arg}");
			return false;
		}
	}

	private static string SerializeTweaksSaveEnvelopeV2(TweaksSaveEnvelopeV2 envelope)
	{
		StringBuilder builder = new StringBuilder(65536);
		WriteSionValue(builder, envelope, 0);
		builder.AppendLine();
		return builder.ToString();
	}

	private static void WriteSionValue(StringBuilder builder, object value, int indent)
	{
		if (value == null)
		{
			builder.Append("{}");
			return;
		}

		Type type = value.GetType();
		if (type == typeof(string))
		{
			WriteSionString(builder, (string)value);
			return;
		}
		if (type == typeof(bool))
		{
			builder.Append((bool)value ? "#true" : "#false");
			return;
		}
		if (type.IsEnum)
		{
			builder.Append(Convert.ToInt32(value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture));
			return;
		}
		if (IsSionNumberType(type))
		{
			builder.Append(Convert.ToString(value, CultureInfo.InvariantCulture));
			return;
		}
		if (value is IDictionary dictionary)
		{
			WriteSionDictionary(builder, dictionary, indent);
			return;
		}
		if (value is IEnumerable enumerable && !(value is string))
		{
			WriteSionEnumerable(builder, enumerable, indent);
			return;
		}

		WriteSionObject(builder, value, indent);
	}

	private static bool IsSionNumberType(Type type)
	{
		type = Nullable.GetUnderlyingType(type) ?? type;
		return type == typeof(byte)
			|| type == typeof(sbyte)
			|| type == typeof(short)
			|| type == typeof(ushort)
			|| type == typeof(int)
			|| type == typeof(uint)
			|| type == typeof(long)
			|| type == typeof(ulong)
			|| type == typeof(float)
			|| type == typeof(double)
			|| type == typeof(decimal);
	}

	private static void WriteSionObject(StringBuilder builder, object value, int indent)
	{
		builder.Append("{");
		bool wroteAny = false;
		foreach (FieldInfo field in GetSionSerializableFields(value.GetType()))
		{
			object fieldValue = field.GetValue(value);
			if (fieldValue == null)
			{
				continue;
			}

			builder.AppendLine();
			AppendSionIndent(builder, indent + 1);
			builder.Append(field.Name).Append(' ');
			WriteSionValue(builder, fieldValue, indent + 1);
			wroteAny = true;
		}
		if (wroteAny)
		{
			builder.AppendLine();
			AppendSionIndent(builder, indent);
		}
		builder.Append("}");
	}

	private static FieldInfo[] GetSionSerializableFields(Type type)
	{
		if (type == null)
		{
			return new FieldInfo[0];
		}
		if (SionSerializableFieldsByType.TryGetValue(type, out FieldInfo[] cachedFields))
		{
			return cachedFields;
		}

		FieldInfo[] fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public);
		List<FieldInfo> serializableFields = new List<FieldInfo>(fields.Length);
		for (int i = 0; i < fields.Length; i++)
		{
			FieldInfo field = fields[i];
			if (!field.IsStatic && !field.IsNotSerialized)
			{
				serializableFields.Add(field);
			}
		}

		FieldInfo[] result = serializableFields.ToArray();
		SionSerializableFieldsByType[type] = result;
		return result;
	}

	private static void WriteSionDictionary(StringBuilder builder, IDictionary dictionary, int indent)
	{
		builder.Append("{");
		bool wroteAny = false;
		foreach (DictionaryEntry entry in dictionary)
		{
			if (entry.Key == null || entry.Value == null)
			{
				continue;
			}

			builder.AppendLine();
			AppendSionIndent(builder, indent + 1);
			WriteSionDictionaryKey(builder, entry.Key);
			builder.Append(' ');
			WriteSionValue(builder, entry.Value, indent + 1);
			wroteAny = true;
		}
		if (wroteAny)
		{
			builder.AppendLine();
			AppendSionIndent(builder, indent);
		}
		builder.Append("}");
	}

	private static void WriteSionEnumerable(StringBuilder builder, IEnumerable enumerable, int indent)
	{
		builder.Append("[");
		bool wroteAny = false;
		foreach (object item in enumerable)
		{
			if (item == null)
			{
				continue;
			}

			builder.AppendLine();
			AppendSionIndent(builder, indent + 1);
			WriteSionValue(builder, item, indent + 1);
			wroteAny = true;
		}
		if (wroteAny)
		{
			builder.AppendLine();
			AppendSionIndent(builder, indent);
		}
		builder.Append("]");
	}

	private static void WriteSionDictionaryKey(StringBuilder builder, object key)
	{
		if (key == null)
		{
			WriteSionString(builder, string.Empty);
			return;
		}

		Type keyType = key.GetType();
		if (keyType.IsEnum || IsSionNumberType(keyType))
		{
			builder.Append(Convert.ToString(key, CultureInfo.InvariantCulture));
			return;
		}

		WriteSionString(builder, Convert.ToString(key, CultureInfo.InvariantCulture) ?? string.Empty);
	}

	private static void WriteSionString(StringBuilder builder, string value)
	{
		builder.Append('"');
		if (!string.IsNullOrEmpty(value))
		{
			for (int i = 0; i < value.Length; i++)
			{
				char ch = value[i];
				switch (ch)
				{
					case '\\':
						builder.Append("\\\\");
						break;
					case '"':
						builder.Append("\\\"");
						break;
					case '\r':
						builder.Append("\\r");
						break;
					case '\n':
						builder.Append("\\n");
						break;
					case '\t':
						builder.Append("\\t");
						break;
					default:
						builder.Append(ch);
						break;
				}
			}
		}
		builder.Append('"');
	}

	private static void AppendSionIndent(StringBuilder builder, int indent)
	{
		builder.Append(' ', Math.Max(0, indent) * 2);
	}

	private static bool ShouldUseV2SaveFormat()
	{
		return string.IsNullOrEmpty(_v2SaveDisabledSaveName)
			|| !string.Equals(_v2SaveDisabledSaveName, _currentTweaksSaveName, StringComparison.Ordinal);
	}

	private static bool LoadModDataV2(string saveName)
	{
		try
		{
			// Preferred path: V2 envelope is authoritative when present.
			if (string.IsNullOrEmpty(_v2SaveFilePath))
			{
				SetSavePaths(saveName);
			}
			_v2SaveFilePath = ResolveExistingTweaksSavePath(saveName, "_tweaks_v2.sim", _v2SaveFilePath);
			if (!string.IsNullOrEmpty(_v2SaveFilePath))
			{
				_saveFilePath = _v2SaveFilePath;
			}
			if (string.IsNullOrEmpty(_v2SaveFilePath) || !File.Exists(_v2SaveFilePath))
			{
				return false;
			}
			if (IsTweaksModDataNewerThanBaseSave(saveName, _v2SaveFilePath, out string freshnessReason))
			{
				VerificationLog("TweaksSave", "v2-freshness-warning-accepted " + freshnessReason);
			}
			Debug.Log($"[PERF][TweaksLoad] v2-path layout={GetTweaksSavePathKind(_v2SaveFilePath)} path={_v2SaveFilePath}");
			string text = File.ReadAllText(_v2SaveFilePath, Encoding.UTF8);
			if (string.IsNullOrWhiteSpace(text))
			{
				return false;
			}
			TweaksSaveEnvelopeV2 tweaksSaveEnvelopeV = null;
			string genericLoadFailure = null;
			try
			{
				tweaksSaveEnvelopeV = FileUtil.DeserializeFromString<TweaksSaveEnvelopeV2>(text);
			}
			catch (Exception ex)
			{
				genericLoadFailure = ex.GetType().Name + ":" + ex.Message;
			}
			if ((genericLoadFailure != null || tweaksSaveEnvelopeV == null || tweaksSaveEnvelopeV.Data == null || ShouldRetryV2LoadWithFallbackReader(tweaksSaveEnvelopeV.Data, text))
				&& TryDeserializeTweaksSaveEnvelopeV2FromSion(text, out TweaksSaveEnvelopeV2 fallbackEnvelope, out string fallbackReason))
			{
				tweaksSaveEnvelopeV = fallbackEnvelope;
				string reason = genericLoadFailure == null ? fallbackReason : fallbackReason + ";generic=" + genericLoadFailure;
				if (fallbackReason.StartsWith("critical-text-recovery", StringComparison.Ordinal))
				{
					_v2CriticalTextRecoveryReadOnly = true;
					_v2CriticalTextRecoveryReason = reason;
				}
				VerificationLog(
					"TweaksSave",
					$"v2-fallback-reader path={GetTweaksSavePathForLog()} reason={reason} crew={tweaksSaveEnvelopeV.Data?.CrewStates?.Count ?? 0} pacts={tweaksSaveEnvelopeV.Data?.Pacts?.Count ?? 0} pactMembers={GetPactMemberCountForSaveLog(tweaksSaveEnvelopeV.Data)} buffs={tweaksSaveEnvelopeV.Data?.GangRelationshipBuffs?.Count ?? 0}");
			}
			if (tweaksSaveEnvelopeV == null || tweaksSaveEnvelopeV.Data == null)
			{
				if (!string.IsNullOrEmpty(genericLoadFailure))
				{
					Debug.LogWarning("[GameplayTweaks] V2 load failed, falling back to legacy JSON: " + genericLoadFailure);
				}
				return false;
			}
			SaveData = tweaksSaveEnvelopeV.Data ?? new ModSaveData();
			if (tweaksSaveEnvelopeV.Version < 3)
			{
				SaveData.GangMeetingMode = (SaveData.GangMeetingsEnabled ? GangMeetingMode.Auto : GangMeetingMode.Prompt);
			}
			TryRecoverEmptyPactsFromNearbyModData(saveName, _v2SaveFilePath, SaveData);
			CrewRelationshipHandlerPatch._lastOutingDay = tweaksSaveEnvelopeV.LastOutingDay;
			CrewRelationshipHandlerPatch._globalMayorBribeActive = tweaksSaveEnvelopeV.GlobalMayorBribeActive;
			CrewRelationshipHandlerPatch._globalMayorBribeExpireDay = tweaksSaveEnvelopeV.GlobalMayorBribeExpireDay;
			return true;
		}
		catch (Exception arg)
		{
			Debug.LogWarning($"[GameplayTweaks] V2 load failed, falling back to legacy JSON: {arg.Message}");
			return false;
		}
	}

	private static bool TryLoadLegacyJson(string saveName)
	{
		try
		{
			// Legacy fallback: accept historical JSON schema and normalize into current SaveData shape.
			if (string.IsNullOrEmpty(_legacySaveFilePath))
			{
				SetSavePaths(saveName);
			}
			_legacySaveFilePath = ResolveExistingTweaksSavePath(saveName, "_tweaks.json", _legacySaveFilePath);
			if (string.IsNullOrEmpty(_legacySaveFilePath) || !File.Exists(_legacySaveFilePath))
			{
				return false;
			}
			string text = File.ReadAllText(_legacySaveFilePath, Encoding.UTF8);
			Hashtable root = FileUtil.ParseAsHashtable(text, ResourceType.JSONFile);
			if (root == null)
			{
				return false;
			}
			LoadFromLegacyRoot(root);
			return true;
		}
		catch (Exception arg)
		{
			Debug.LogWarning($"[GameplayTweaks] Legacy JSON load failed: {arg.Message}");
			return false;
		}
	}

	private static bool TryRecoverEmptyPactsFromNearbyModData(string saveName, string currentPath, ModSaveData currentData)
	{
		try
		{
			if (currentData == null
				|| (currentData.Pacts != null && currentData.Pacts.Count > 0)
				|| string.IsNullOrWhiteSpace(saveName)
				|| string.IsNullOrWhiteSpace(currentPath))
			{
				return false;
			}
			if (!IsCurrentV2FileEmptyPacts(currentPath))
			{
				return false;
			}
			string currentFullPath = Path.GetFullPath(currentPath);
			string bestPath = null;
			ModSaveData bestData = null;
			DateTime bestWriteUtc = DateTime.MinValue;
			foreach (string candidatePath in EnumerateNearbyTweaksV2SaveFiles(saveName, currentFullPath))
			{
				try
				{
					if (string.IsNullOrWhiteSpace(candidatePath) || !File.Exists(candidatePath))
					{
						continue;
					}
					string candidateFullPath = Path.GetFullPath(candidatePath);
					if (string.Equals(candidateFullPath, currentFullPath, StringComparison.OrdinalIgnoreCase))
					{
						continue;
					}
					string text = File.ReadAllText(candidateFullPath, Encoding.UTF8);
					if (!HasNonEmptySionPactsBlock(text))
					{
						continue;
					}
					if (!TryDeserializeTweaksSaveEnvelopeV2FromSion(text, out TweaksSaveEnvelopeV2 envelope, out string reason)
						|| envelope?.Data?.Pacts == null
						|| envelope.Data.Pacts.Count == 0)
					{
						VerificationLog("TweaksSave", $"pact-recovery-candidate-skip path={candidateFullPath} reason={reason}");
						continue;
					}
					DateTime writeUtc = File.GetLastWriteTimeUtc(candidateFullPath);
					if (writeUtc <= bestWriteUtc)
					{
						continue;
					}
					bestWriteUtc = writeUtc;
					bestPath = candidateFullPath;
					bestData = envelope.Data;
				}
				catch (Exception candidateEx)
				{
					Debug.LogWarning("[GameplayTweaks] Pact recovery candidate check failed: " + candidateEx.Message);
				}
			}
			if (bestData == null || bestData.Pacts == null || bestData.Pacts.Count == 0)
			{
				VerificationLog("TweaksSave", $"pact-recovery-unavailable save={saveName} current={currentFullPath}");
				return false;
			}

			currentData.Pacts = CloneAlliancePacts(bestData.Pacts);
			currentData.NextPactId = Math.Max(currentData.NextPactId, bestData.NextPactId);
			currentData.PlayerPactId = bestData.PlayerPactId;
			currentData.PlayerJoinedPactIndex = bestData.PlayerJoinedPactIndex;
			currentData.LastPactJoinDay = bestData.LastPactJoinDay;
			currentData.PlayerPactWarStartDay = bestData.PlayerPactWarStartDay;
			currentData.PlayerPactWarTargetId = bestData.PlayerPactWarTargetId;
			currentData.PactEpochDay = Math.Max(currentData.PactEpochDay, bestData.PactEpochDay);
			if (bestData.PactAlliances != null && bestData.PactAlliances.Count > 0)
			{
				currentData.PactAlliances = bestData.PactAlliances.Select(CloneInterPactAlliance).Where(a => a != null).ToList();
			}
			if (bestData.PactAllianceVotes != null && bestData.PactAllianceVotes.Count > 0)
			{
				currentData.PactAllianceVotes = bestData.PactAllianceVotes.Select(CloneInterPactAllianceVote).Where(v => v != null).ToList();
			}
			currentData.NextPactAllianceId = Math.Max(currentData.NextPactAllianceId, bestData.NextPactAllianceId);
			currentData.NextPactAllianceVoteId = Math.Max(currentData.NextPactAllianceVoteId, bestData.NextPactAllianceVoteId);
			VerificationLog("TweaksSave", $"pact-recovery-applied save={saveName} source={bestPath} recoveredPacts={currentData.Pacts.Count} playerPact={currentData.PlayerPactId} nextPactId={currentData.NextPactId}");
			return true;
		}
		catch (Exception ex)
		{
			Debug.LogWarning("[GameplayTweaks] Pact recovery failed: " + ex.Message);
			return false;
		}
	}

	private static bool IsCurrentV2FileEmptyPacts(string path)
	{
		try
		{
			if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
			{
				return false;
			}
			string text = File.ReadAllText(path, Encoding.UTF8);
			if (!TryFindSionFieldBlock(text, nameof(ModSaveData.Pacts), '[', ']', out string pactsBlock))
			{
				return false;
			}
			return !HasNonEmptySionPactsBlock(text) && pactsBlock.IndexOf('{') < 0;
		}
		catch
		{
			return false;
		}
	}

	private static IEnumerable<string> EnumerateNearbyTweaksV2SaveFiles(string saveName, string currentFullPath)
	{
		HashSet<string> yielded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		if (!string.IsNullOrWhiteSpace(currentFullPath))
		{
			string currentBackup = currentFullPath + ".pacts.bak";
			if (yielded.Add(currentBackup))
			{
				yield return currentBackup;
			}
		}
		foreach (string root in GetTweaksSaveRootCandidates())
		{
			if (string.IsNullOrWhiteSpace(root))
			{
				continue;
			}
			string modDataRoot = Path.Combine(root, "ModData");
			if (Directory.Exists(modDataRoot))
			{
				foreach (string candidate in Directory.GetDirectories(modDataRoot)
					.OrderByDescending(dir => dir, StringComparer.Ordinal))
				{
					string slotName = Path.GetFileName(candidate);
					if (string.Equals(slotName, saveName, StringComparison.Ordinal))
					{
						continue;
					}
					if (string.CompareOrdinal(slotName, saveName) > 0)
					{
						continue;
					}
					string file = Path.Combine(candidate, "GameplayTweaks_v2.sim");
					if (yielded.Add(file))
					{
						yield return file;
					}
					string backup = file + ".pacts.bak";
					if (yielded.Add(backup))
					{
						yield return backup;
					}
				}
			}
			string legacyPatternRoot = root;
			if (Directory.Exists(legacyPatternRoot))
			{
				foreach (string file in Directory.GetFiles(legacyPatternRoot, "*_tweaks_v2.sim"))
				{
					string legacyName = Path.GetFileNameWithoutExtension(file);
					if (legacyName != null && legacyName.EndsWith("_tweaks_v2", StringComparison.Ordinal))
					{
						legacyName = legacyName.Substring(0, legacyName.Length - "_tweaks_v2".Length);
					}
					if (string.IsNullOrWhiteSpace(legacyName)
						|| string.Equals(legacyName, saveName, StringComparison.Ordinal)
						|| string.CompareOrdinal(legacyName, saveName) > 0)
					{
						continue;
					}
					if (yielded.Add(file))
					{
						yield return file;
					}
					string backup = file + ".pacts.bak";
					if (yielded.Add(backup))
					{
						yield return backup;
					}
				}
			}
		}
	}

	private static List<AlliancePact> CloneAlliancePacts(IEnumerable<AlliancePact> source)
	{
		List<AlliancePact> result = new List<AlliancePact>();
		if (source == null)
		{
			return result;
		}
		foreach (AlliancePact pact in source)
		{
			if (pact == null)
			{
				continue;
			}
			result.Add(new AlliancePact
			{
				PactId = pact.PactId,
				PactName = pact.PactName,
				ColorIndex = pact.ColorIndex,
				LeaderGangId = pact.LeaderGangId,
				MemberIds = pact.MemberIds?.Where(id => id >= 0).Distinct().ToList() ?? new List<int>(),
				ColorR = pact.ColorR,
				ColorG = pact.ColorG,
				ColorB = pact.ColorB,
				FormedDays = pact.FormedDays,
				IsPending = pact.IsPending,
				PlayerInvited = pact.PlayerInvited,
				PlayerColorConfirmed = pact.PlayerColorConfirmed,
				EarningRate = pact.EarningRate,
				CrewCapacityBonus = pact.CrewCapacityBonus,
				LastVoteDay = pact.LastVoteDay,
				LastVoteType = pact.LastVoteType,
				PlayerProposedVote = pact.PlayerProposedVote,
				PendingVoteCycleDay = pact.PendingVoteCycleDay,
				VotePromptShown = pact.VotePromptShown,
				VotePromptAnswered = pact.VotePromptAnswered,
				BossHappiness = pact.BossHappiness != null ? new Dictionary<int, float>(pact.BossHappiness) : new Dictionary<int, float>(),
				VotePreferences = pact.VotePreferences != null ? new Dictionary<int, int>(pact.VotePreferences) : new Dictionary<int, int>()
			});
		}
		return result;
	}

	private static InterPactAlliance CloneInterPactAlliance(InterPactAlliance source)
	{
		if (source == null)
		{
			return null;
		}
		return new InterPactAlliance
		{
			AllianceId = source.AllianceId,
			LeftPactId = source.LeftPactId,
			RightPactId = source.RightPactId,
			CreatedDay = source.CreatedDay,
			Active = source.Active
		};
	}

	private static InterPactAllianceVote CloneInterPactAllianceVote(InterPactAllianceVote source)
	{
		if (source == null)
		{
			return null;
		}
		return new InterPactAllianceVote
		{
			VoteId = source.VoteId,
			SourcePactId = source.SourcePactId,
			TargetPactId = source.TargetPactId,
			AllianceId = source.AllianceId,
			IsRemoval = source.IsRemoval,
			ProposedDay = source.ProposedDay,
			ProposerGangId = source.ProposerGangId,
			InitiatedByHuman = source.InitiatedByHuman,
			Resolved = source.Resolved,
			ResolvedDay = source.ResolvedDay,
			VotesByGang = source.VotesByGang != null ? new Dictionary<int, bool>(source.VotesByGang) : new Dictionary<int, bool>()
		};
	}

	private static bool WriteTextAtomicIfChanged(string path, string contents)
	{
		if (string.IsNullOrEmpty(path))
		{
			return false;
		}

		TweaksSavedTextStamp stamp = CreateTweaksSavedTextStamp(contents);
		if (File.Exists(path)
			&& _lastTweaksSavedTextByPath.TryGetValue(path, out TweaksSavedTextStamp previous)
			&& previous.Length == stamp.Length
			&& previous.Hash == stamp.Hash)
		{
			return false;
		}

		CreateTweaksSaveBackupIfNeeded(path);
		WriteTextAtomic(path, contents);
		_lastTweaksSavedTextByPath[path] = stamp;
		return true;
	}

	private static void CreateTweaksSaveBackupIfNeeded(string path)
	{
		try
		{
			if (string.IsNullOrWhiteSpace(path)
				|| !File.Exists(path)
				|| !string.Equals(Path.GetFileName(path), "GameplayTweaks_v2.sim", StringComparison.OrdinalIgnoreCase))
			{
				return;
			}
			string existing = File.ReadAllText(path, Encoding.UTF8);
			if (!HasNonEmptySionPactsBlock(existing))
			{
				return;
			}
			string backupPath = path + ".pacts.bak";
			File.Copy(path, backupPath, overwrite: true);
			VerificationLog("TweaksSave", $"pact-backup-written path={backupPath}");
		}
		catch (Exception ex)
		{
			Debug.LogWarning("[GameplayTweaks] Pact backup write failed: " + ex.Message);
		}
	}

	private static TweaksSavedTextStamp CreateTweaksSavedTextStamp(string contents)
	{
		ulong hash = 14695981039346656037UL;
		if (contents != null)
		{
			for (int i = 0; i < contents.Length; i++)
			{
				hash ^= contents[i];
				hash *= 1099511628211UL;
			}
		}

		return new TweaksSavedTextStamp
		{
			Length = contents?.Length ?? 0,
			Hash = hash
		};
	}

	private static void WriteTextAtomic(string path, string contents)
	{
		string directoryName = Path.GetDirectoryName(path);
		if (!string.IsNullOrEmpty(directoryName) && !Directory.Exists(directoryName))
		{
			Directory.CreateDirectory(directoryName);
		}
		string text = path + ".tmp";
		File.WriteAllText(text, contents, Encoding.UTF8);
		try
		{
			if (File.Exists(path))
			{
				File.Replace(text, path, null);
			}
			else
			{
				File.Move(text, path);
			}
		}
		catch (Exception ex)
		{
			Debug.LogWarning("[GameplayTweaks] Atomic save replace failed, attempting fallback move: " + ex.Message);
			try
			{
				if (File.Exists(path))
				{
					File.Delete(path);
				}
			}
			catch (Exception deleteEx)
			{
				Debug.LogWarning("[GameplayTweaks] Atomic save fallback delete failed: " + deleteEx.Message);
			}
			File.Move(text, path);
		}
	}

	private static void EnsureSaveDataDefaults()
	{
		if (SaveData == null)
		{
			SaveData = new ModSaveData();
		}
		if (SaveData.CrewStates == null)
		{
			SaveData.CrewStates = new Dictionary<long, CrewModState>();
		}
		if (SaveData.Pacts == null)
		{
			SaveData.Pacts = new List<AlliancePact>();
		}
		foreach (AlliancePact pact in SaveData.Pacts.Where(p => p != null))
		{
			if (pact.MemberIds == null)
			{
				pact.MemberIds = new List<int>();
			}
			if (pact.BossHappiness == null)
			{
				pact.BossHappiness = new Dictionary<int, float>();
			}
			if (pact.VotePreferences == null)
			{
				pact.VotePreferences = new Dictionary<int, int>();
			}
			if (string.IsNullOrWhiteSpace(pact.PactId))
			{
				pact.PactId = "pact_recovered_" + pact.ColorIndex + "_" + pact.LeaderGangId;
			}
			pact.MemberIds = pact.MemberIds
				.Where(mid => mid >= 0 && mid != pact.LeaderGangId)
				.Distinct()
				.ToList();
			pact.PendingVoteCycleDay = Mathf.Max(-1, pact.PendingVoteCycleDay);
			if (pact.PendingVoteCycleDay < 0)
			{
				pact.VotePromptShown = false;
				pact.VotePromptAnswered = false;
			}
		}
		if (SaveData.PactJoinCooldowns == null)
		{
			SaveData.PactJoinCooldowns = new Dictionary<int, int>();
		}
		if (SaveData.PactInviteCooldowns == null)
		{
			SaveData.PactInviteCooldowns = new Dictionary<int, int>();
		}
		if (SaveData.GrapevineEvents == null)
		{
			SaveData.GrapevineEvents = new List<string>();
		}
		if (SaveData.NationalHeat == null)
		{
			SaveData.NationalHeat = new NationalHeatState();
		}
		if (SaveData.NationalHeat.WitnessEntries == null)
		{
			SaveData.NationalHeat.WitnessEntries = new List<ImportantWitnessEntry>();
		}
		if (SaveData.VerifyStats == null)
		{
			SaveData.VerifyStats = new VerificationStats();
		}
		EnsureGangOpsDefaultsSeeded();
		if (SaveData.PactOps == null)
		{
			SaveData.PactOps = BuildPactOpsDefaultsFromConfig();
		}
		NormalizePactOpsSettings(SaveData.PactOps);
		if (SaveData.PactWarHeat == null)
		{
			SaveData.PactWarHeat = new Dictionary<string, WarHeatEntry>();
		}
		if (SaveData.PactRevengeQueue == null)
		{
			SaveData.PactRevengeQueue = new Dictionary<string, RevengeEntry>();
		}
		if (SaveData.PactLastAutoProtectDayByGang == null)
		{
			SaveData.PactLastAutoProtectDayByGang = new Dictionary<int, int>();
		}
		if (SaveData.PactLastCoordAttackDayByGang == null)
		{
			SaveData.PactLastCoordAttackDayByGang = new Dictionary<int, int>();
		}
		if (SaveData.PactLastHireCycleDayByGang == null)
		{
			SaveData.PactLastHireCycleDayByGang = new Dictionary<int, int>();
		}
		if (SaveData.IndependentOps == null)
		{
			SaveData.IndependentOps = BuildGangOpsDefaultsFromConfig(GangOpsChannel.Independent);
		}
		NormalizePactOpsSettings(SaveData.IndependentOps);
		if (SaveData.IndependentWarHeat == null)
		{
			SaveData.IndependentWarHeat = new Dictionary<string, WarHeatEntry>();
		}
		if (SaveData.IndependentRevengeQueue == null)
		{
			SaveData.IndependentRevengeQueue = new Dictionary<string, RevengeEntry>();
		}
		if (SaveData.GangWarMediationLastAttemptDayByPair == null)
		{
			SaveData.GangWarMediationLastAttemptDayByPair = new Dictionary<string, int>();
		}
		if (SaveData.IndependentLastAutoProtectDayByGang == null)
		{
			SaveData.IndependentLastAutoProtectDayByGang = new Dictionary<int, int>();
		}
		if (SaveData.IndependentLastCoordAttackDayByGang == null)
		{
			SaveData.IndependentLastCoordAttackDayByGang = new Dictionary<int, int>();
		}
		if (SaveData.IndependentLastHireCycleDayByGang == null)
		{
			SaveData.IndependentLastHireCycleDayByGang = new Dictionary<int, int>();
		}
		if (SaveData.AiGangLastSnitchMeetingDayByGang == null)
		{
			SaveData.AiGangLastSnitchMeetingDayByGang = new Dictionary<int, int>();
		}
		if (SaveData.AiGangMayorBribeExpireDayByGang == null)
		{
			SaveData.AiGangMayorBribeExpireDayByGang = new Dictionary<int, int>();
		}
		if (SaveData.AiGangLastLegalActionDayByGang == null)
		{
			SaveData.AiGangLastLegalActionDayByGang = new Dictionary<int, int>();
		}
		if (SaveData.VehicleDriverByVehicleId == null)
		{
			SaveData.VehicleDriverByVehicleId = new Dictionary<long, long>();
		}
		if (SaveData.FrontRouteExpansionKeys == null)
		{
			SaveData.FrontRouteExpansionKeys = new List<string>();
		}
		if (SaveData.GangRelationshipBuffs == null)
		{
			SaveData.GangRelationshipBuffs = new List<GangRelationshipBuffState>();
		}
		if (SaveData.PactAlliances == null)
		{
			SaveData.PactAlliances = new List<InterPactAlliance>();
		}
		if (SaveData.PactAllianceVotes == null)
		{
			SaveData.PactAllianceVotes = new List<InterPactAllianceVote>();
		}
		SaveData.GangMeetingTier = Mathf.Clamp(SaveData.GangMeetingTier, 0, 2);
		SaveData.GangMeetingIntervalDays = GANG_MEETING_INTERVAL_DAYS;
		NormalizeGangMeetingModeState();
		NormalizeInterPactAllianceData();
	}

	private static bool IsTweaksModDataNewerThanBaseSave(string saveName, string modDataPath, out string reason)
	{
		reason = "none";
		try
		{
			if (string.IsNullOrWhiteSpace(saveName) || string.IsNullOrWhiteSpace(modDataPath) || !File.Exists(modDataPath))
			{
				return false;
			}

			if (!TryGetBaseSaveLastWriteUtc(saveName, out DateTime baseSaveUtc, out string baseSavePath))
			{
				return false;
			}

			DateTime modDataUtc = File.GetLastWriteTimeUtc(modDataPath);
			double deltaSeconds = (modDataUtc - baseSaveUtc).TotalSeconds;
			if (deltaSeconds <= ModDataBaseSaveFutureToleranceSeconds)
			{
				return false;
			}

			reason = "save=" + saveName
				+ " basePath=" + baseSavePath
				+ " baseUtc=" + baseSaveUtc.ToString("O", CultureInfo.InvariantCulture)
				+ " modPath=" + modDataPath
				+ " modUtc=" + modDataUtc.ToString("O", CultureInfo.InvariantCulture)
				+ " deltaSeconds=" + deltaSeconds.ToString("F0", CultureInfo.InvariantCulture);
			return true;
		}
		catch (Exception ex)
		{
			reason = "freshness-check-failed:" + ex.GetType().Name + ":" + ex.Message;
			return false;
		}
	}

	private static bool ShouldBlockCriticalTextRecoverySaveOverwrite()
	{
		if (!_v2CriticalTextRecoveryReadOnly || string.IsNullOrEmpty(_v2SaveFilePath) || !File.Exists(_v2SaveFilePath))
		{
			return false;
		}
		if ((SaveData?.CrewStates?.Count ?? 0) > 0 || (SaveData?.GangRelationshipBuffs?.Count ?? 0) > 0)
		{
			return false;
		}
		try
		{
			string text = File.ReadAllText(_v2SaveFilePath, Encoding.UTF8);
			return HasNonEmptySionPactsBlock(text)
				|| text.IndexOf("CrewStates {", StringComparison.Ordinal) >= 0
				|| text.IndexOf("GangRelationshipBuffs [", StringComparison.Ordinal) >= 0
				|| text.IndexOf("PactWarHeat {", StringComparison.Ordinal) >= 0
				|| text.IndexOf("IndependentWarHeat {", StringComparison.Ordinal) >= 0;
		}
		catch (Exception ex)
		{
			Debug.LogWarning("[GameplayTweaks] Critical recovery save-overwrite check failed; blocking save to preserve existing mod data. " + ex.Message);
			return true;
		}
	}

	private static bool ShouldBlockSuspiciousEmptyPactSaveOverwrite()
	{
		try
		{
			if (!string.Equals(_lastModDataLoadSource, "none", StringComparison.Ordinal)
				|| (SaveData?.Pacts?.Count ?? 0) > 0
				|| string.IsNullOrEmpty(_v2SaveFilePath)
				|| !File.Exists(_v2SaveFilePath))
			{
				return false;
			}
			string text = File.ReadAllText(_v2SaveFilePath, Encoding.UTF8);
			if (!HasNonEmptySionPactsBlock(text))
			{
				return false;
			}
			VerificationLog("TweaksSave", $"empty-pact-overwrite-blocked path={GetTweaksSavePathForLog()} lastLoad={_lastModDataLoadSource} lastLoadPacts={_lastModDataLoadPactCount} loadedCrew={SaveData?.CrewStates?.Count ?? 0} loadedPacts={SaveData?.Pacts?.Count ?? 0}");
			return true;
		}
		catch (Exception ex)
		{
			Debug.LogWarning("[GameplayTweaks] Empty pact overwrite guard failed: " + ex.Message);
			return false;
		}
	}

	private static bool HasNonEmptySionPactsBlock(string text)
	{
		if (string.IsNullOrEmpty(text))
		{
			return false;
		}
		if (!TryFindSionFieldBlock(text, nameof(ModSaveData.Pacts), '[', ']', out string pactsBlock))
		{
			return false;
		}
		return pactsBlock.IndexOf(nameof(AlliancePact.PactId), StringComparison.Ordinal) >= 0
			|| pactsBlock.IndexOf(nameof(AlliancePact.LeaderGangId), StringComparison.Ordinal) >= 0
			|| pactsBlock.IndexOf(nameof(AlliancePact.MemberIds), StringComparison.Ordinal) >= 0;
	}

	private static bool ShouldRetryV2LoadWithFallbackReader(ModSaveData loadedData, string text)
	{
		if (loadedData == null || string.IsNullOrEmpty(text))
		{
			return true;
		}
		bool loadedEmpty = (loadedData.CrewStates == null || loadedData.CrewStates.Count == 0)
			&& (loadedData.Pacts == null || loadedData.Pacts.Count == 0)
			&& (loadedData.GangRelationshipBuffs == null || loadedData.GangRelationshipBuffs.Count == 0)
			&& (loadedData.PactWarHeat == null || loadedData.PactWarHeat.Count == 0)
			&& (loadedData.IndependentWarHeat == null || loadedData.IndependentWarHeat.Count == 0)
			&& (loadedData.GrapevineEvents == null || loadedData.GrapevineEvents.Count == 0);
		if (!loadedEmpty)
		{
			return false;
		}
		return text.IndexOf("Pacts [", StringComparison.Ordinal) >= 0
			|| text.IndexOf("CrewStates {", StringComparison.Ordinal) >= 0
			|| text.IndexOf("GangRelationshipBuffs [", StringComparison.Ordinal) >= 0
			|| text.IndexOf("PactWarHeat {", StringComparison.Ordinal) >= 0
			|| text.IndexOf("IndependentWarHeat {", StringComparison.Ordinal) >= 0;
	}

	private static bool TryDeserializeTweaksSaveEnvelopeV2FromSion(string text, out TweaksSaveEnvelopeV2 envelope, out string reason)
	{
		envelope = null;
		reason = "none";
		try
		{
			Hashtable root = FileUtil.ParseAsHashtable(text, ResourceType.SimFile);
			if (root == null)
			{
				reason = "parse-null";
				return false;
			}
			Hashtable dataTable = HTable(root, nameof(TweaksSaveEnvelopeV2.Data));
			if (dataTable == null)
			{
				reason = "missing-data";
				return false;
			}
			envelope = new TweaksSaveEnvelopeV2
			{
				Version = ToInt(HGet(root, nameof(TweaksSaveEnvelopeV2.Version)), TweaksSaveVersionCurrent),
				Data = ReadSionObject<ModSaveData>(dataTable),
				LastOutingDay = ToInt(HGet(root, nameof(TweaksSaveEnvelopeV2.LastOutingDay)), -1),
				GlobalMayorBribeActive = ToBool(HGet(root, nameof(TweaksSaveEnvelopeV2.GlobalMayorBribeActive)), d: false),
				GlobalMayorBribeExpireDay = ToInt(HGet(root, nameof(TweaksSaveEnvelopeV2.GlobalMayorBribeExpireDay)), -1)
			};
			if (envelope.Data == null)
			{
				reason = "data-null";
				return false;
			}
			reason = "sion-hashtable";
			return true;
		}
		catch (Exception ex)
		{
			if (TryRecoverTweaksSaveEnvelopeV2CriticalSections(text, out envelope, out string recoveryReason))
			{
				reason = recoveryReason + ";sion-parser=" + ex.GetType().Name + ":" + ex.Message;
				return true;
			}
			reason = ex.GetType().Name + ":" + ex.Message;
			envelope = null;
			return false;
		}
	}

	private static bool TryRecoverTweaksSaveEnvelopeV2CriticalSections(string text, out TweaksSaveEnvelopeV2 envelope, out string reason)
	{
		envelope = null;
		reason = "none";
		try
		{
			if (string.IsNullOrWhiteSpace(text) || !TryFindSionFieldBlock(text, nameof(TweaksSaveEnvelopeV2.Data), '{', '}', out string dataBlock))
			{
				reason = "missing-data-block";
				return false;
			}

			ModSaveData data = new ModSaveData
			{
				NextPactId = ReadSionInt(dataBlock, nameof(ModSaveData.NextPactId), 0),
				PlayerPactId = ReadSionInt(dataBlock, nameof(ModSaveData.PlayerPactId), -1),
				PlayerJoinedPactIndex = ReadSionInt(dataBlock, nameof(ModSaveData.PlayerJoinedPactIndex), -1),
				LastPactJoinDay = ReadSionInt(dataBlock, nameof(ModSaveData.LastPactJoinDay), -1),
				NeverAcceptPacts = ReadSionBool(dataBlock, nameof(ModSaveData.NeverAcceptPacts), d: false),
				RobberyPromptsEvadeRefuseMode = ReadSionBool(dataBlock, nameof(ModSaveData.RobberyPromptsEvadeRefuseMode), d: false),
				PlayerPactWarStartDay = ReadSionInt(dataBlock, nameof(ModSaveData.PlayerPactWarStartDay), -1),
				PlayerPactWarTargetId = ReadSionInt(dataBlock, nameof(ModSaveData.PlayerPactWarTargetId), -1),
				PactEpochDay = ReadSionInt(dataBlock, nameof(ModSaveData.PactEpochDay), -1),
				SnitchCaseProgress = ReadSionFloat(dataBlock, nameof(ModSaveData.SnitchCaseProgress), 0f),
				NextSnitchCollectionDay = ReadSionInt(dataBlock, nameof(ModSaveData.NextSnitchCollectionDay), -1),
				LastSnitchRaidDay = ReadSionInt(dataBlock, nameof(ModSaveData.LastSnitchRaidDay), -1),
				GangMeetingsEnabled = ReadSionBool(dataBlock, nameof(ModSaveData.GangMeetingsEnabled), d: true),
				GangMeetingMode = (GangMeetingMode)ReadSionInt(dataBlock, nameof(ModSaveData.GangMeetingMode), (int)GangMeetingMode.Auto),
				GangMeetingTier = ReadSionInt(dataBlock, nameof(ModSaveData.GangMeetingTier), 0),
				GangMeetingIntervalDays = ReadSionInt(dataBlock, nameof(ModSaveData.GangMeetingIntervalDays), GANG_MEETING_INTERVAL_DAYS),
				GangOpsDefaultsProfileVersion = ReadSionInt(dataBlock, nameof(ModSaveData.GangOpsDefaultsProfileVersion), 0)
			};

			if (TryFindSionFieldBlock(dataBlock, nameof(ModSaveData.Pacts), '[', ']', out string pactsBlock))
			{
				data.Pacts = ReadSionAlliancePacts(pactsBlock);
			}
			if (data.Pacts == null)
			{
				data.Pacts = new List<AlliancePact>();
			}
			if (data.Pacts.Count == 0)
			{
				reason = "critical-pacts-empty";
				return false;
			}

			envelope = new TweaksSaveEnvelopeV2
			{
				Version = ReadSionInt(text, nameof(TweaksSaveEnvelopeV2.Version), TweaksSaveVersionCurrent),
				Data = data,
				LastOutingDay = ReadSionInt(text, nameof(TweaksSaveEnvelopeV2.LastOutingDay), -1),
				GlobalMayorBribeActive = ReadSionBool(text, nameof(TweaksSaveEnvelopeV2.GlobalMayorBribeActive), d: false),
				GlobalMayorBribeExpireDay = ReadSionInt(text, nameof(TweaksSaveEnvelopeV2.GlobalMayorBribeExpireDay), -1)
			};
			reason = "critical-text-recovery";
			return true;
		}
		catch (Exception ex)
		{
			reason = ex.GetType().Name + ":" + ex.Message;
			envelope = null;
			return false;
		}
	}

	private static List<AlliancePact> ReadSionAlliancePacts(string pactsBlock)
	{
		List<AlliancePact> pacts = new List<AlliancePact>();
		if (string.IsNullOrEmpty(pactsBlock))
		{
			return pacts;
		}
		for (int i = 0; i < pactsBlock.Length; i++)
		{
			if (pactsBlock[i] != '{')
			{
				continue;
			}
			int close = MatchBrace(pactsBlock, i);
			if (close <= i)
			{
				break;
			}
			string block = pactsBlock.Substring(i, close - i + 1);
			AlliancePact pact = new AlliancePact
			{
				PactId = ReadSionString(block, nameof(AlliancePact.PactId), string.Empty),
				PactName = ReadSionString(block, nameof(AlliancePact.PactName), string.Empty),
				ColorIndex = ReadSionInt(block, nameof(AlliancePact.ColorIndex), 0),
				LeaderGangId = ReadSionInt(block, nameof(AlliancePact.LeaderGangId), -1),
				ColorR = ReadSionFloat(block, nameof(AlliancePact.ColorR), 1f),
				ColorG = ReadSionFloat(block, nameof(AlliancePact.ColorG), 1f),
				ColorB = ReadSionFloat(block, nameof(AlliancePact.ColorB), 1f),
				FormedDays = ReadSionInt(block, nameof(AlliancePact.FormedDays), 0),
				PlayerInvited = ReadSionBool(block, nameof(AlliancePact.PlayerInvited), d: false),
				IsPending = ReadSionBool(block, nameof(AlliancePact.IsPending), d: false),
				EarningRate = ReadSionFloat(block, nameof(AlliancePact.EarningRate), 0.05f),
				CrewCapacityBonus = ReadSionInt(block, nameof(AlliancePact.CrewCapacityBonus), 0),
				LastVoteDay = ReadSionInt(block, nameof(AlliancePact.LastVoteDay), -1),
				LastVoteType = ReadSionInt(block, nameof(AlliancePact.LastVoteType), 0),
				PlayerProposedVote = ClampPactVoteChoice(ReadSionInt(block, nameof(AlliancePact.PlayerProposedVote), -1), allowNone: true),
				PendingVoteCycleDay = ReadSionInt(block, nameof(AlliancePact.PendingVoteCycleDay), -1),
				VotePromptShown = ReadSionBool(block, nameof(AlliancePact.VotePromptShown), d: false),
				VotePromptAnswered = ReadSionBool(block, nameof(AlliancePact.VotePromptAnswered), d: false),
				PlayerColorConfirmed = ReadSionBool(block, nameof(AlliancePact.PlayerColorConfirmed), d: true)
			};
			if (TryFindSionFieldBlock(block, nameof(AlliancePact.MemberIds), '[', ']', out string membersBlock))
			{
				pact.MemberIds = ReadSionIntList(membersBlock)
					.Where(id => id >= 0 && id != pact.LeaderGangId)
					.Distinct()
					.ToList();
			}
			if (TryFindSionFieldBlock(block, nameof(AlliancePact.BossHappiness), '{', '}', out string happinessBlock))
			{
				pact.BossHappiness = ReadSionIntFloatDictionary(happinessBlock);
			}
			if (TryFindSionFieldBlock(block, nameof(AlliancePact.VotePreferences), '{', '}', out string votesBlock))
			{
				pact.VotePreferences = ReadSionIntIntDictionary(votesBlock);
			}
			if (!string.IsNullOrWhiteSpace(pact.PactId) || pact.LeaderGangId >= 0 || pact.MemberIds.Count > 0)
			{
				pacts.Add(pact);
			}
			i = close;
		}
		return pacts;
	}

	private static bool TryFindSionFieldBlock(string text, string fieldName, char openChar, char closeChar, out string block)
	{
		block = null;
		int fieldPos = FindSionFieldPosition(text, fieldName);
		if (fieldPos < 0)
		{
			return false;
		}
		int open = text.IndexOf(openChar, fieldPos + fieldName.Length);
		if (open < 0)
		{
			return false;
		}
		int close = openChar == '{' ? MatchBrace(text, open) : MatchBracket(text, open);
		if (close <= open || closeChar != text[close])
		{
			return false;
		}
		block = text.Substring(open, close - open + 1);
		return true;
	}

	private static int FindSionFieldPosition(string text, string fieldName)
	{
		if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(fieldName))
		{
			return -1;
		}
		int index = 0;
		while (index < text.Length)
		{
			int lineEnd = text.IndexOf('\n', index);
			if (lineEnd < 0)
			{
				lineEnd = text.Length;
			}
			int pos = index;
			while (pos < lineEnd && char.IsWhiteSpace(text[pos]))
			{
				pos++;
			}
			if (pos + fieldName.Length <= lineEnd
				&& string.Compare(text, pos, fieldName, 0, fieldName.Length, StringComparison.Ordinal) == 0)
			{
				int after = pos + fieldName.Length;
				if (after >= lineEnd || char.IsWhiteSpace(text[after]) || text[after] == '{' || text[after] == '[')
				{
					return pos;
				}
			}
			index = lineEnd + 1;
		}
		return -1;
	}

	private static string ReadSionLineValue(string text, string fieldName)
	{
		int pos = FindSionFieldPosition(text, fieldName);
		if (pos < 0)
		{
			return null;
		}
		int valueStart = pos + fieldName.Length;
		int lineEnd = text.IndexOf('\n', valueStart);
		if (lineEnd < 0)
		{
			lineEnd = text.Length;
		}
		return text.Substring(valueStart, lineEnd - valueStart).Trim();
	}

	private static string ReadSionString(string text, string fieldName, string fallback)
	{
		string value = ReadSionLineValue(text, fieldName);
		if (string.IsNullOrEmpty(value))
		{
			return fallback;
		}
		value = value.Trim();
		if (value.Length < 2 || value[0] != '"')
		{
			return value;
		}
		StringBuilder builder = new StringBuilder(value.Length);
		for (int i = 1; i < value.Length; i++)
		{
			char c = value[i];
			if (c == '"')
			{
				return builder.ToString();
			}
			if (c == '\\' && i + 1 < value.Length)
			{
				char n = value[++i];
				builder.Append(n == 'n' ? '\n' : n == 'r' ? '\r' : n == 't' ? '\t' : n);
				continue;
			}
			builder.Append(c);
		}
		return builder.ToString();
	}

	private static int ReadSionInt(string text, string fieldName, int fallback)
	{
		return ToInt(ReadSionLineValue(text, fieldName), fallback);
	}

	private static float ReadSionFloat(string text, string fieldName, float fallback)
	{
		return ToFloat(ReadSionLineValue(text, fieldName), fallback);
	}

	private static bool ReadSionBool(string text, string fieldName, bool d)
	{
		return ToBool(ReadSionLineValue(text, fieldName), d);
	}

	private static List<int> ReadSionIntList(string block)
	{
		List<int> values = new List<int>();
		foreach (string rawLine in block.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
		{
			string line = rawLine.Trim();
			if (line.Length == 0 || line == "[" || line == "]")
			{
				continue;
			}
			if (int.TryParse(line, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
			{
				values.Add(value);
			}
		}
		return values;
	}

	private static Dictionary<int, float> ReadSionIntFloatDictionary(string block)
	{
		Dictionary<int, float> values = new Dictionary<int, float>();
		foreach (string rawLine in block.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
		{
			string line = rawLine.Trim();
			if (line.Length == 0 || line == "{" || line == "}")
			{
				continue;
			}
			string[] parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
			if (parts.Length >= 2
				&& int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int key)
				&& float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
			{
				values[key] = value;
			}
		}
		return values;
	}

	private static Dictionary<int, int> ReadSionIntIntDictionary(string block)
	{
		Dictionary<int, int> values = new Dictionary<int, int>();
		foreach (string rawLine in block.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
		{
			string line = rawLine.Trim();
			if (line.Length == 0 || line == "{" || line == "}")
			{
				continue;
			}
			string[] parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
			if (parts.Length >= 2
				&& int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int key)
				&& int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
			{
				values[key] = ClampPactVoteChoice(value, allowNone: false);
			}
		}
		return values;
	}

	private static T ReadSionObject<T>(Hashtable table) where T : new()
	{
		object value = ReadSionValue(table, typeof(T));
		return value is T typed ? typed : new T();
	}

	private static object ReadSionValue(object raw, Type targetType)
	{
		if (targetType == null)
		{
			return null;
		}
		if (raw == null)
		{
			return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;
		}
		Type nullableType = Nullable.GetUnderlyingType(targetType);
		if (nullableType != null)
		{
			return ReadSionValue(raw, nullableType);
		}
		if (targetType == typeof(string))
		{
			return ToStr(raw, string.Empty);
		}
		if (targetType == typeof(bool))
		{
			return ToBool(raw, d: false);
		}
		if (targetType == typeof(int))
		{
			return ToInt(raw, 0);
		}
		if (targetType == typeof(long))
		{
			return ToLong(raw, 0L);
		}
		if (targetType == typeof(float))
		{
			return ToFloat(raw, 0f);
		}
		if (targetType == typeof(double))
		{
			return Convert.ToDouble(raw, CultureInfo.InvariantCulture);
		}
		if (targetType.IsEnum)
		{
			return Enum.ToObject(targetType, ToInt(raw, 0));
		}
		if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(List<>))
		{
			return ReadSionList(raw as ArrayList, targetType);
		}
		if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(Dictionary<,>))
		{
			return ReadSionDictionary(raw as Hashtable, targetType);
		}
		if (raw is Hashtable table)
		{
			object instance = Activator.CreateInstance(targetType);
			foreach (FieldInfo field in GetSionSerializableFields(targetType))
			{
				if (field == null || field.IsInitOnly || !table.ContainsKey(field.Name))
				{
					continue;
				}
				try
				{
					field.SetValue(instance, ReadSionValue(table[field.Name], field.FieldType));
				}
				catch (Exception ex)
				{
					Debug.LogWarning("[GameplayTweaks] V2 fallback load skipped field " + targetType.Name + "." + field.Name + ": " + ex.Message);
				}
			}
			return instance;
		}
		return raw;
	}

	private static object ReadSionList(ArrayList rawList, Type listType)
	{
		object list = Activator.CreateInstance(listType);
		if (!(list is IList targetList) || rawList == null)
		{
			return list;
		}
		Type itemType = listType.GetGenericArguments()[0];
		foreach (object item in rawList)
		{
			targetList.Add(ReadSionValue(item, itemType));
		}
		return list;
	}

	private static object ReadSionDictionary(Hashtable rawDictionary, Type dictionaryType)
	{
		object dictionary = Activator.CreateInstance(dictionaryType);
		if (!(dictionary is IDictionary targetDictionary) || rawDictionary == null)
		{
			return dictionary;
		}
		Type[] args = dictionaryType.GetGenericArguments();
		Type keyType = args[0];
		Type valueType = args[1];
		foreach (DictionaryEntry entry in rawDictionary)
		{
			object key = ReadSionDictionaryKey(entry.Key, keyType);
			object value = ReadSionValue(entry.Value, valueType);
			if (key != null)
			{
				targetDictionary[key] = value;
			}
		}
		return dictionary;
	}

	private static object ReadSionDictionaryKey(object rawKey, Type keyType)
	{
		if (keyType == typeof(string))
		{
			return ToStr(rawKey, string.Empty);
		}
		if (keyType == typeof(int))
		{
			return ToInt(rawKey, 0);
		}
		if (keyType == typeof(long))
		{
			return ToLong(rawKey, 0L);
		}
		if (keyType.IsEnum)
		{
			return Enum.ToObject(keyType, ToInt(rawKey, 0));
		}
		return Convert.ChangeType(rawKey, keyType, CultureInfo.InvariantCulture);
	}

	private static bool TryGetBaseSaveLastWriteUtc(string saveName, out DateTime lastWriteUtc, out string sourcePath)
	{
		lastWriteUtc = DateTime.MinValue;
		sourcePath = string.Empty;
		string saveRoot = GetCitySaveRootPath();
		if (string.IsNullOrWhiteSpace(saveRoot) || string.IsNullOrWhiteSpace(saveName))
		{
			return false;
		}

		string slotRoot = Path.Combine(saveRoot, saveName);
		string[] candidates =
		{
			Path.Combine(slotRoot, "savedata.sim.zip"),
			Path.Combine(slotRoot, "savedata.sim"),
			Path.Combine(slotRoot, "metadata.sim"),
			Path.Combine(saveRoot, saveName + ".sim.zip"),
			Path.Combine(saveRoot, saveName + ".sim")
		};
		foreach (string candidate in candidates)
		{
			if (string.IsNullOrWhiteSpace(candidate) || !File.Exists(candidate))
			{
				continue;
			}

			DateTime candidateUtc = File.GetLastWriteTimeUtc(candidate);
			if (candidateUtc > lastWriteUtc)
			{
				lastWriteUtc = candidateUtc;
				sourcePath = candidate;
			}
		}

		return lastWriteUtc > DateTime.MinValue;
	}

	private static string ResolveExistingTweaksSavePath(string saveName, string suffix, string preferredPath)
	{
		if (string.IsNullOrEmpty(saveName) || string.IsNullOrEmpty(suffix))
		{
			return preferredPath;
		}
		foreach (string candidate in GetTweaksSaveFileCandidates(saveName, suffix, preferredPath))
		{
			if (!string.IsNullOrWhiteSpace(candidate) && File.Exists(candidate))
			{
				return candidate;
			}
		}
		return preferredPath;
	}

	private static IEnumerable<string> GetTweaksSaveFileCandidates(string saveName, string suffix, string preferredPath)
	{
		if (!string.IsNullOrWhiteSpace(preferredPath))
		{
			yield return preferredPath;
		}
		bool isV2 = string.Equals(suffix, "_tweaks_v2.sim", StringComparison.OrdinalIgnoreCase);
		foreach (string root in GetTweaksSaveRootCandidates())
		{
			if (string.IsNullOrWhiteSpace(root))
			{
				continue;
			}
			if (isV2)
			{
				string modDataRoot = GetTweaksModDataSaveRootPath(saveName, root);
				if (!string.IsNullOrWhiteSpace(modDataRoot))
				{
					yield return Path.Combine(modDataRoot, "GameplayTweaks_v2.sim");
				}
			}
			yield return Path.Combine(root, saveName + suffix);
		}
	}

	private static IEnumerable<string> GetTweaksSaveRootCandidates()
	{
		string persistentPath = Application.persistentDataPath;
		string primary = GetCitySaveRootPath();
		if (!string.IsNullOrWhiteSpace(primary))
		{
			yield return primary;
		}
		if (!string.IsNullOrWhiteSpace(persistentPath))
		{
			yield return Path.Combine(persistentPath, "Saves");
			yield return Path.Combine(persistentPath, "saves");
		}
	}

	private static string GetTweaksSavePathForLog()
	{
		if (!string.IsNullOrEmpty(_v2SaveFilePath))
		{
			return _v2SaveFilePath;
		}
		if (!string.IsNullOrEmpty(_saveFilePath))
		{
			return _saveFilePath;
		}
		return _legacySaveFilePath ?? string.Empty;
	}

	private static string GetTweaksSavePathKind(string path)
	{
		if (string.IsNullOrWhiteSpace(path))
		{
			return "none";
		}
		return path.IndexOf(Path.DirectorySeparatorChar + "ModData" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) >= 0
			? "moddata-v2"
			: "legacy-root";
	}

	private static bool WriteTweaksSaveManifest()
	{
		if (string.IsNullOrEmpty(_v2ManifestFilePath))
		{
			return false;
		}
		try
		{
			StringBuilder builder = new StringBuilder();
			builder.Append("{");
			builder.Append("\"Mod\":\"GameplayTweaks\",");
			builder.Append("\"Version\":").Append(TweaksSaveVersionCurrent).Append(",");
			builder.Append("\"SaveName\":\"").Append(EscapeJsonString(_currentTweaksSaveName ?? string.Empty)).Append("\",");
			builder.Append("\"Layout\":\"moddata-v2\",");
			builder.Append("\"DataFile\":\"GameplayTweaks_v2.sim\"");
			builder.Append("}");
			return WriteTextAtomicIfChanged(_v2ManifestFilePath, builder.ToString());
		}
		catch (Exception ex)
		{
			Debug.LogWarning("[GameplayTweaks] Manifest save failed: " + ex.Message);
			return false;
		}
	}

	private static int GetPactMemberCountForSaveLog()
	{
		return GetPactMemberCountForSaveLog(SaveData);
	}

	private static int GetPactMemberCountForSaveLog(ModSaveData data)
	{
		try
		{
			return data?.Pacts?
				.Where(p => p != null)
				.Sum(p => p.MemberIds == null
					? (p.LeaderGangId >= 0 ? 1 : 0)
					: p.MemberIds.Concat(p.LeaderGangId >= 0 ? new[] { p.LeaderGangId } : Array.Empty<int>()).Where(id => id >= 0).Distinct().Count()) ?? 0;
		}
		catch
		{
			return 0;
		}
	}

	private static void NormalizeCrewStateAfterLoad()
	{
		if (SaveData?.CrewStates == null)
		{
			return;
		}
		NormalizeHumanPoliticalBribeAfterLoad();
		foreach (KeyValuePair<long, CrewModState> crewState in SaveData.CrewStates)
		{
			CrewModState value = crewState.Value;
			if (value == null)
			{
				continue;
			}
			NormalizeStreetCreditStateAfterLoad(value, crewState.Key);
			value.FederalWitnessCount = Mathf.Max(0, value.FederalWitnessCount);
			value.WitnessCount = Mathf.Max(value.WitnessCount, value.FederalWitnessCount);
			if (value.FederalWitnessCount > 0)
			{
				value.HasWitness = true;
			}
			value.HideoutDuration = Mathf.Max(1, value.HideoutDuration);
			value.LoyaltyValue = Mathf.Clamp01(value.LoyaltyValue);
			value.LoyaltyCap = Mathf.Clamp(value.LoyaltyCap, 0.1f, 1f);
			value.SnitchWeight = Mathf.Clamp(value.SnitchWeight, 0f, 0.95f);
			value.SnitchLeakCount = Mathf.Max(0, value.SnitchLeakCount);
			value.HideoutMissedPayments = Mathf.Max(0, value.HideoutMissedPayments);
			try
			{
				NormalizeOddJobCooldownState(value, G.GetNow(), "load", EntityID.FromID(unchecked((ulong)crewState.Key)));
			}
			catch
			{
			}
			SyncLocalHeatFromLegacyFields(value);
		}
		ReconcileAllCrewJailStates("load");
		ReconcilePersistentGangRelationshipBuffs("load", force: true);
	}

	private static void NormalizeStreetCreditStateAfterLoad(CrewModState state, long crewKey)
	{
		if (state == null)
		{
			return;
		}
		float progress = Mathf.Max(0f, state.StreetCreditProgress);
		int carriedLevels = Mathf.FloorToInt(progress);
		if (carriedLevels > 0)
		{
			state.StreetCreditLevel = Mathf.Max(0, state.StreetCreditLevel) + carriedLevels;
			state.StreetCreditProgress = Mathf.Clamp01(progress - carriedLevels);
			VerificationLog("StreetCredit", $"load-normalized-no-grant peep={(ulong)crewKey} carriedLevels={carriedLevels} level={state.StreetCreditLevel} progress={state.StreetCreditProgress:0.000}");
			return;
		}
		state.StreetCreditLevel = Mathf.Max(0, state.StreetCreditLevel);
		state.StreetCreditProgress = Mathf.Clamp01(progress);
	}

	private static void NormalizeHumanPoliticalBribeAfterLoad()
	{
		try
		{
			if (!CrewRelationshipHandlerPatch._globalMayorBribeActive)
			{
				return;
			}
			int today = G.GetNow().days;
			if (CrewRelationshipHandlerPatch._globalMayorBribeExpireDay < 0 || CrewRelationshipHandlerPatch._globalMayorBribeExpireDay <= today)
			{
				int expireDay = CrewRelationshipHandlerPatch._globalMayorBribeExpireDay;
				CrewRelationshipHandlerPatch._globalMayorBribeActive = false;
				CrewRelationshipHandlerPatch._globalMayorBribeExpireDay = -1;
				VerificationLog("Political", $"load-cleared-stale-bribe today={today} expireDay={expireDay}");
			}
		}
		catch
		{
		}
	}

	private static object HGet(Hashtable table, string key)
	{
		if (table == null || string.IsNullOrEmpty(key) || !table.ContainsKey(key))
		{
			return null;
		}
		return table[key];
	}

	private static Hashtable HTable(Hashtable table, string key)
	{
		return HGet(table, key) as Hashtable;
	}

	private static ArrayList HArray(Hashtable table, string key)
	{
		return HGet(table, key) as ArrayList;
	}

	private static int ToInt(object value, int d)
	{
		if (value == null)
		{
			return d;
		}
		try
		{
			if (value is int num)
			{
				return num;
			}
			if (value is long num2)
			{
				return (int)num2;
			}
			if (value is double num3)
			{
				return (int)num3;
			}
			if (value is float num4)
			{
				return (int)num4;
			}
			if (value is string text && int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int result))
			{
				return result;
			}
			return Convert.ToInt32(value, CultureInfo.InvariantCulture);
		}
		catch
		{
			return d;
		}
	}

	private static long ToLong(object value, long d)
	{
		if (value == null)
		{
			return d;
		}
		try
		{
			if (value is long num)
			{
				return num;
			}
			if (value is int num2)
			{
				return num2;
			}
			if (value is double num3)
			{
				return (long)num3;
			}
			if (value is float num4)
			{
				return (long)num4;
			}
			if (value is string text && long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out long result))
			{
				return result;
			}
			return Convert.ToInt64(value, CultureInfo.InvariantCulture);
		}
		catch
		{
			return d;
		}
	}

	private static float ToFloat(object value, float d)
	{
		if (value == null)
		{
			return d;
		}
		try
		{
			if (value is float num)
			{
				return num;
			}
			if (value is double num2)
			{
				return (float)num2;
			}
			if (value is int num3)
			{
				return num3;
			}
			if (value is long num4)
			{
				return num4;
			}
			if (value is string text)
			{
				if (float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float result))
				{
					return result;
				}
				if (float.TryParse(text, out result))
				{
					return result;
				}
			}
			return Convert.ToSingle(value, CultureInfo.InvariantCulture);
		}
		catch
		{
			return d;
		}
	}

	private static bool ToBool(object value, bool d)
	{
		if (value == null)
		{
			return d;
		}
		try
		{
			if (value is bool flag)
			{
				return flag;
			}
			if (value is int num)
			{
				return num != 0;
			}
			if (value is long num2)
			{
				return num2 != 0;
			}
			if (value is double num3)
			{
				return Math.Abs(num3) > double.Epsilon;
			}
			if (value is float num4)
			{
				return Math.Abs(num4) > 0.0001f;
			}
			if (value is string text)
			{
				if (bool.TryParse(text, out bool result))
				{
					return result;
				}
				if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int result2))
				{
					return result2 != 0;
				}
			}
		}
		catch
		{
		}
		return d;
	}

	private static string ToStr(object value, string d)
	{
		if (value == null)
		{
			return d;
		}
		if (value is string text)
		{
			return text;
		}
		try
		{
			return Convert.ToString(value, CultureInfo.InvariantCulture) ?? d;
		}
		catch
		{
			return d;
		}
	}

	private static void LoadFromLegacyRoot(Hashtable root)
	{
		SaveData = new ModSaveData();
		SaveData.NextPactId = ToInt(HGet(root, "NextPactId"), 0);
		SaveData.PlayerPactId = ToInt(HGet(root, "PlayerPactId"), -1);
		SaveData.PlayerJoinedPactIndex = ToInt(HGet(root, "PJI"), -1);
		SaveData.LastPactJoinDay = ToInt(HGet(root, "LPJD"), -1);
		SaveData.NeverAcceptPacts = ToBool(HGet(root, "NAP"), d: false);
		SaveData.RobberyPromptsEvadeRefuseMode = ToBool(HGet(root, "RERM"), d: false);
		CrewRelationshipHandlerPatch._lastOutingDay = ToInt(HGet(root, "LOD"), -1);
		CrewRelationshipHandlerPatch._globalMayorBribeActive = ToBool(HGet(root, "GMB"), d: false);
		CrewRelationshipHandlerPatch._globalMayorBribeExpireDay = ToInt(HGet(root, "GMBD"), -1);
		SaveData.PlayerPactWarStartDay = ToInt(HGet(root, "PPWSD"), -1);
		SaveData.PlayerPactWarTargetId = ToInt(HGet(root, "PPWTID"), -1);
		SaveData.PactEpochDay = ToInt(HGet(root, "PED"), -1);
		SaveData.SnitchCaseProgress = Mathf.Clamp(ToFloat(HGet(root, "SCPR"), 0f), 0f, 100f);
		SaveData.NextSnitchCollectionDay = ToInt(HGet(root, "NSCD"), -1);
		SaveData.LastSnitchRaidDay = ToInt(HGet(root, "LSRD"), -1);
		bool d = ToBool(HGet(root, "GME"), d: true);
		object obj = HGet(root, "GMM");
		int explicitModeRaw = ToInt(obj, -1);
		SaveData.GangMeetingMode = ResolveGangMeetingModeFromLegacy(explicitModeRaw, d, obj != null);
		SaveData.GangMeetingsEnabled = SaveData.GangMeetingMode == GangMeetingMode.Auto;
		SaveData.GangMeetingTier = Mathf.Clamp(ToInt(HGet(root, "GMT"), 0), 0, 2);
		SaveData.GangMeetingIntervalDays = Mathf.Max(1, ToInt(HGet(root, "GMI"), GANG_MEETING_INTERVAL_DAYS));
		SaveData.GangOpsDefaultsProfileVersion = ToInt(HGet(root, "GOV"), 0);
		LoadLegacyVerificationStats(HTable(root, "V10"));
		LoadLegacyPactInviteCooldowns(HTable(root, "PICD"));
		LoadLegacyNationalHeat(HTable(root, "NH"));
		LoadLegacyCrewStates(HTable(root, "CrewStates"));
		LoadLegacyPacts(HArray(root, "Pacts"));
		LoadLegacyGrapevine(HArray(root, "GV"));
	}

	private static void LoadLegacyVerificationStats(Hashtable v10Table)
	{
		VerificationStats verificationStats = EnsureVerifyStats();
		if (v10Table == null)
		{
			return;
		}
		verificationStats.BossLoyaltyChecks = ToInt(HGet(v10Table, "BLC"), 0);
		verificationStats.BossLoyaltyViolations = ToInt(HGet(v10Table, "BLV"), 0);
		verificationStats.LoyaltyCapInitCount = ToInt(HGet(v10Table, "LCI"), 0);
		verificationStats.LoyaltyDecayEvents = ToInt(HGet(v10Table, "LDE"), 0);
		verificationStats.ZeroLoyaltyDefections = ToInt(HGet(v10Table, "ZLD"), 0);
		verificationStats.PepTalkLoyaltyGainEvents = ToInt(HGet(v10Table, "PLG"), 0);
		verificationStats.VacationLoyaltyGainEvents = ToInt(HGet(v10Table, "VLG"), 0);
		verificationStats.SnitchIntakeRuns = ToInt(HGet(v10Table, "SIR"), 0);
		verificationStats.SnitchLeakEvents = ToInt(HGet(v10Table, "SLE"), 0);
		verificationStats.SnitchRaidTriggers = ToInt(HGet(v10Table, "SRT"), 0);
		verificationStats.RetainerUpkeepTicks = ToInt(HGet(v10Table, "RUT"), 0);
		verificationStats.RetainerTrialAssistRolls = ToInt(HGet(v10Table, "RTR"), 0);
		verificationStats.RetainerTrialAssistWins = ToInt(HGet(v10Table, "RTW"), 0);
	}

	private static void LoadLegacyPactInviteCooldowns(Hashtable table)
	{
		if (table == null)
		{
			return;
		}
		foreach (DictionaryEntry item in table)
		{
			if (int.TryParse(ToStr(item.Key, ""), NumberStyles.Integer, CultureInfo.InvariantCulture, out int result))
			{
				SaveData.PactInviteCooldowns[result] = ToInt(item.Value, 0);
			}
		}
	}

	private static void LoadLegacyNationalHeat(Hashtable table)
	{
		SaveData.NationalHeat = new NationalHeatState();
		if (table == null)
		{
			return;
		}
		SaveData.NationalHeat.Active = ToBool(HGet(table, "A"), d: false);
		SaveData.NationalHeat.Level = ToInt(HGet(table, "L"), 0);
		SaveData.NationalHeat.LastEscalationDay = ToInt(HGet(table, "LED"), -1);
		ArrayList arrayList = HArray(table, "WE");
		if (arrayList == null)
		{
			return;
		}
		foreach (object item in arrayList)
		{
			Hashtable hashtable = item as Hashtable;
			if (hashtable == null)
			{
				continue;
			}
			ImportantWitnessEntry importantWitnessEntry = new ImportantWitnessEntry();
			importantWitnessEntry.CrewPeepId = ToLong(HGet(hashtable, "CP"), 0L);
			importantWitnessEntry.SourceType = ToInt(HGet(hashtable, "ST"), 0);
			importantWitnessEntry.AddedDay = ToInt(HGet(hashtable, "AD"), -1);
			importantWitnessEntry.ArrestDueDay = ToInt(HGet(hashtable, "DD"), -1);
			importantWitnessEntry.ArrestProcessed = ToBool(HGet(hashtable, "AP"), d: false);
			importantWitnessEntry.SentenceBoostApplied = ToBool(HGet(hashtable, "SB"), d: false);
			SaveData.NationalHeat.WitnessEntries.Add(importantWitnessEntry);
		}
	}

	private static void LoadLegacyCrewStates(Hashtable table)
	{
		if (table == null)
		{
			return;
		}
		foreach (DictionaryEntry item in table)
		{
			if (!(item.Value is Hashtable hashtable))
			{
				continue;
			}
			if (!long.TryParse(ToStr(item.Key, ""), NumberStyles.Integer, CultureInfo.InvariantCulture, out long result))
			{
				continue;
			}
			CrewModState crewModState = new CrewModState();
			crewModState.StreetCreditProgress = ToFloat(HGet(hashtable, "SCP"), 0f);
			crewModState.StreetCreditLevel = ToInt(HGet(hashtable, "SCL"), 0);
			crewModState.WantedLevel = (WantedLevel)ToInt(HGet(hashtable, "WL"), 0);
			crewModState.WantedProgress = ToFloat(HGet(hashtable, "WP"), 0f);
			crewModState.LocalHeatLevel = (WantedLevel)ToInt(HGet(hashtable, "LHL"), (int)crewModState.WantedLevel);
			crewModState.LocalHeatProgress = ToFloat(HGet(hashtable, "LHP"), crewModState.WantedProgress);
			crewModState.LocalHeatLastRefreshDay = ToInt(HGet(hashtable, "LHD"), -1);
			crewModState.LocalHeatInitialized = ToBool(HGet(hashtable, "LHI"), crewModState.LocalHeatProgress > 0f || crewModState.LocalHeatLevel != WantedLevel.None);
			crewModState.MayorBribeActive = ToBool(HGet(hashtable, "MBA"), d: false);
			crewModState.JudgeBribeActive = ToBool(HGet(hashtable, "JBA"), d: false);
			crewModState.BribeExpiresRaw = ToLong(HGet(hashtable, "BER"), 0L);
			crewModState.HappinessValue = ToFloat(HGet(hashtable, "HV"), 1f);
			crewModState.LoyaltyValue = Mathf.Clamp01(ToFloat(HGet(hashtable, "LV"), 0.5f));
			crewModState.LoyaltyCap = Mathf.Clamp(ToFloat(HGet(hashtable, "LCP"), 1f), 0.1f, 1f);
			crewModState.LoyaltyCapInitialized = ToBool(HGet(hashtable, "LCI"), d: false);
			crewModState.LowHappinessStreak = Mathf.Max(0, ToInt(HGet(hashtable, "LHS"), 0));
			crewModState.SnitchWeight = Mathf.Clamp(ToFloat(HGet(hashtable, "SW"), 0f), 0f, 0.95f);
			crewModState.SnitchWeightInitialized = ToBool(HGet(hashtable, "SWI"), d: false);
			crewModState.SnitchExposed = ToBool(HGet(hashtable, "SX"), d: false);
			crewModState.SnitchLeakCount = Mathf.Max(0, ToInt(HGet(hashtable, "SLC"), 0));
			crewModState.LastSnitchLeakDay = ToInt(HGet(hashtable, "SLD"), -1);
			crewModState.SnitchDisappearPending = ToBool(HGet(hashtable, "SDP"), d: false);
			crewModState.SnitchDisappearDueDay = ToInt(HGet(hashtable, "SDD"), -1);
			crewModState.TurnsUnhappy = ToInt(HGet(hashtable, "TU"), 0);
			crewModState.OnVacation = ToBool(HGet(hashtable, "OV"), d: false);
			crewModState.VacationReturnsRaw = ToLong(HGet(hashtable, "VRR"), 0L);
			crewModState.OnHideout = ToBool(HGet(hashtable, "OH"), d: false);
			crewModState.HideoutReturnsRaw = ToLong(HGet(hashtable, "HRR"), 0L);
			crewModState.HideoutLastUpkeepDay = ToInt(HGet(hashtable, "HULD"), -1);
			crewModState.HideoutMissedPayments = Mathf.Max(0, ToInt(HGet(hashtable, "HMP"), 0));
			crewModState.HideoutForcedReturn = ToBool(HGet(hashtable, "HFR"), d: false);
			crewModState.IsUnderboss = ToBool(HGet(hashtable, "IU"), d: false);
			crewModState.AwaitingChildBirth = ToBool(HGet(hashtable, "ACB"), d: false);
			crewModState.LastFutureKidsCount = ToInt(HGet(hashtable, "LFKC"), 0);
			crewModState.LookingForSpouse = ToBool(HGet(hashtable, "LFS"), d: false);
			crewModState.SpouseSearchStartDay = ToInt(HGet(hashtable, "SSD"), -1);
			crewModState.NextSpouseSearchDay = ToInt(HGet(hashtable, "NSD"), -1);
			crewModState.SpouseSearchResolveNotBeforeDay = ToInt(HGet(hashtable, "SRD"), -1);
			crewModState.LastSpouseDatingSpend = Mathf.Max(0, ToInt(HGet(hashtable, "LSDS"), 0));
			crewModState.SpouseSearchTotalSpend = Mathf.Max(0, ToInt(HGet(hashtable, "SSTS"), 0));
			crewModState.LastBoozeSoldCount = ToInt(HGet(hashtable, "LBSC"), 0);
			crewModState.LastManualPepTalkDay = ToInt(HGet(hashtable, "PTD"), -1);
			crewModState.OddJobsEnabled = ToBool(HGet(hashtable, "OJE"), d: false);
			crewModState.LastOddJobDay = ToInt(HGet(hashtable, "LOJD"), -1);
			crewModState.TotalOddJobEarnings = Mathf.Max(0, ToInt(HGet(hashtable, "TOJE"), 0));
			crewModState.VacationPending = ToBool(HGet(hashtable, "VP"), d: false);
			crewModState.VacationDuration = ToInt(HGet(hashtable, "VD"), 0);
			crewModState.HideoutPending = ToBool(HGet(hashtable, "HP"), d: false);
			crewModState.HideoutDuration = Mathf.Max(1, ToInt(HGet(hashtable, "HD"), HIDEOUT_DURATION_DAYS));
			crewModState.FedArrivalCountdown = ToInt(HGet(hashtable, "FAC"), 0);
			crewModState.FedsIncoming = ToBool(HGet(hashtable, "FI"), d: false);
			crewModState.HasWitness = ToBool(HGet(hashtable, "HW"), d: false);
			crewModState.WitnessCount = ToInt(HGet(hashtable, "WC"), crewModState.HasWitness ? 1 : 0);
			crewModState.FederalWitnessCount = Mathf.Max(0, ToInt(HGet(hashtable, "FWC"), 0));
			crewModState.WitnessThreatenedSuccessfully = ToBool(HGet(hashtable, "WTS"), d: false);
			crewModState.WitnessThreatAttempted = ToBool(HGet(hashtable, "WTA"), d: false);
			crewModState.ExtraJailYears = ToInt(HGet(hashtable, "EJY"), 0);
			crewModState.LawyerRetainer = ToInt(HGet(hashtable, "LR"), 0);
			crewModState.LawyerRetainerConfirmed = ToBool(HGet(hashtable, "LRC"), d: false);
			crewModState.LastRetainerDeductDay = ToInt(HGet(hashtable, "LRD"), -1);
			crewModState.CaseDismissed = ToBool(HGet(hashtable, "CD"), d: false);
			crewModState.LastBoozeSellTurn = ToInt(HGet(hashtable, "LBST"), 0);
			crewModState.TotalBoozeSoldStreet = ToInt(HGet(hashtable, "TBSS"), 0);
			crewModState.TotalBoozeSoldLifetime = ToInt(HGet(hashtable, "TBSL"), 0);
			SaveData.CrewStates[result] = crewModState;
		}
	}

	private static void LoadLegacyPacts(ArrayList pactsArray)
	{
		if (pactsArray == null)
		{
			return;
		}
		foreach (object item in pactsArray)
		{
			Hashtable hashtable = item as Hashtable;
			if (hashtable == null)
			{
				continue;
			}
			AlliancePact alliancePact = new AlliancePact();
			alliancePact.PactId = ToStr(HGet(hashtable, "PI"), "");
			alliancePact.PactName = ToStr(HGet(hashtable, "PN"), "");
			alliancePact.ColorIndex = ToInt(HGet(hashtable, "CI"), 0);
			alliancePact.LeaderGangId = ToInt(HGet(hashtable, "LG"), -1);
			alliancePact.ColorR = ToFloat(HGet(hashtable, "CR"), 1f);
			alliancePact.ColorG = ToFloat(HGet(hashtable, "CG"), 1f);
			alliancePact.ColorB = ToFloat(HGet(hashtable, "CB"), 1f);
			alliancePact.FormedDays = ToInt(HGet(hashtable, "FD"), 0);
			alliancePact.IsPending = ToBool(HGet(hashtable, "IP"), d: false);
			alliancePact.PlayerInvited = ToBool(HGet(hashtable, "PV"), d: false);
			alliancePact.PlayerColorConfirmed = ToBool(HGet(hashtable, "PCC"), d: true);
			alliancePact.EarningRate = ToFloat(HGet(hashtable, "ER"), 0.05f);
			alliancePact.CrewCapacityBonus = ToInt(HGet(hashtable, "CCB"), 0);
			alliancePact.LastVoteDay = ToInt(HGet(hashtable, "LVD"), -1);
			alliancePact.LastVoteType = ToInt(HGet(hashtable, "LVT"), 0);
			alliancePact.PlayerProposedVote = ClampPactVoteChoice(ToInt(HGet(hashtable, "PVT"), -1), allowNone: true);
			alliancePact.PendingVoteCycleDay = ToInt(HGet(hashtable, "PVCD"), -1);
			alliancePact.VotePromptShown = ToBool(HGet(hashtable, "PVPS"), d: false);
			alliancePact.VotePromptAnswered = ToBool(HGet(hashtable, "PVPA"), d: false);
			ArrayList arrayList = HArray(hashtable, "MI");
			if (arrayList != null)
			{
				foreach (object item2 in arrayList)
				{
					alliancePact.MemberIds.Add(ToInt(item2, -1));
				}
				alliancePact.MemberIds.RemoveAll((int x) => x < 0);
			}
			Hashtable hashtable2 = HTable(hashtable, "BH");
			if (hashtable2 != null)
			{
				foreach (DictionaryEntry item3 in hashtable2)
				{
					if (int.TryParse(ToStr(item3.Key, ""), NumberStyles.Integer, CultureInfo.InvariantCulture, out int result))
					{
						alliancePact.BossHappiness[result] = ToFloat(item3.Value, 1f);
					}
				}
			}
			Hashtable hashtable3 = HTable(hashtable, "VPR");
			if (hashtable3 != null)
			{
				foreach (DictionaryEntry item4 in hashtable3)
				{
					if (int.TryParse(ToStr(item4.Key, ""), NumberStyles.Integer, CultureInfo.InvariantCulture, out int result2))
					{
						alliancePact.VotePreferences[result2] = ClampPactVoteChoice(ToInt(item4.Value, 3), allowNone: false);
					}
				}
			}
			SaveData.Pacts.Add(alliancePact);
		}
	}

	private static void LoadLegacyGrapevine(ArrayList grapevineArray)
	{
		if (grapevineArray == null)
		{
			return;
		}
		foreach (object item in grapevineArray)
		{
			string text = ToStr(item, null);
			if (!string.IsNullOrEmpty(text))
			{
				SaveData.GrapevineEvents.Add(text);
			}
		}
	}

	private static int MatchBrace(string s, int open)
	{
		int num = 0;
		for (int i = open; i < s.Length; i++)
		{
			if (s[i] == '{')
			{
				num++;
			}
			else if (s[i] == '}')
			{
				num--;
				if (num == 0)
				{
					return i;
				}
			}
		}
		return -1;
	}

	private static int MatchBracket(string s, int open)
	{
		int num = 0;
		for (int i = open; i < s.Length; i++)
		{
			if (s[i] == '[')
			{
				num++;
			}
			else if (s[i] == ']')
			{
				num--;
				if (num == 0)
				{
					return i;
				}
			}
		}
		return -1;
	}

	private static string JRaw(string json, string key)
	{
		string text = "\"" + key + "\":";
		int num = json.IndexOf(text);
		if (num < 0)
		{
			return null;
		}
		int num2 = num + text.Length;
		int i;
		for (i = num2; i < json.Length && json[i] != ',' && json[i] != '}' && json[i] != ']'; i++)
		{
		}
		return json.Substring(num2, i - num2).Trim();
	}

	private static int JInt(string j, string k, int d)
	{
		string text = JRaw(j, k);
		if (text == null || !int.TryParse(text, out var result))
		{
			return d;
		}
		return result;
	}

	private static long JLong(string j, string k, long d)
	{
		string text = JRaw(j, k);
		if (text == null || !long.TryParse(text, out var result))
		{
			return d;
		}
		return result;
	}

	private static float JFloat(string j, string k, float d)
	{
		string text = JRaw(j, k);
		if (text == null || !float.TryParse(text, out var result))
		{
			return d;
		}
		return result;
	}

	private static bool JBool(string j, string k, bool d)
	{
		string text = JRaw(j, k);
		if (text == null)
		{
			return d;
		}
		return text == "true";
	}

	private static string JStr(string j, string k, string d)
	{
		string text = "\"" + k + "\":\"";
		int num = j.IndexOf(text);
		if (num < 0)
		{
			return d;
		}
		int num2 = num + text.Length;
		int num3 = j.IndexOf('"', num2);
		if (num3 <= num2)
		{
			return d;
		}
		return j.Substring(num2, num3 - num2);
	}

	

	}
}

