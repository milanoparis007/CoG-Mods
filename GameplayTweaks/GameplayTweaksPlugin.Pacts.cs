using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Game.Core;
using Game.Session;
using Game.Session.Board;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Player;
using Game.Session.Player.AI;
using Game.Session.Player.Commands;
using Game.Session.Sim;
using Game.Session.Sim.Modules;
using Game.UI.Session;
using HarmonyLib;
using SomaSim.Util;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;
namespace GameplayTweaks
{
public partial class GameplayTweaksPlugin
{

	internal static class TurnUpdatePatch
	{
		private static class PactWarManager
		{
			private static MethodInfo _addAggroOnMethod;

			private static object[] _addArgs = new object[2];

			public static void ActivateWar(PlayerInfo attacker, PlayerInfo defender)
			{
				if (attacker == null || defender == null)
				{
					return;
				}
				if (attacker.PID.id == defender.PID.id)
				{
					return;
				}
				TryActivateOneWay(defender, attacker.PID);
				TryActivateOneWay(attacker, defender.PID);
				MarkCrewPickAggroDirty(attacker.PID, "pact-war-manager");
				MarkCrewPickAggroDirty(defender.PID, "pact-war-manager");
			}

			public static void ActivateWarAgainstAttackers(IEnumerable<PlayerInfo> pactMembers, IEnumerable<PlayerInfo> attackers)
			{
				if (pactMembers == null || attackers == null)
				{
					return;
				}
				List<PlayerInfo> attackerList = attackers.Where(a => a != null && a.crew != null && !a.crew.IsCrewDefeated).ToList();
				foreach (PlayerInfo pactMember in pactMembers)
				{
					if (pactMember == null || pactMember.crew == null || pactMember.crew.IsCrewDefeated)
					{
						continue;
					}
					foreach (PlayerInfo attacker in attackerList)
					{
						ActivateWar(attacker, pactMember);
					}
				}
			}

			private static void TryActivateOneWay(PlayerInfo owner, PlayerID targetPid)
			{
				try
				{
					object combat = owner.ai?.combat;
					if (combat == null)
					{
						return;
					}
					if (_addAggroOnMethod == null)
					{
						_addAggroOnMethod = combat.GetType().GetMethod("AddAggroOn", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
					}
					if (_addAggroOnMethod != null)
					{
						_addArgs[0] = targetPid;
						_addArgs[1] = (Fixnum)(-120);
						_addAggroOnMethod.Invoke(combat, _addArgs);
					}
				}
				catch
				{
				}
			}
		}

		private static MethodInfo _returnCrewMethod;

		private static MethodInfo _addCrewMethod;

		private static MethodInfo _removeCrewCompletelyMethod;

		private static MethodInfo _addToCrewMethod;

		private static readonly Dictionary<int, ulong> _lastKnownBossPeepByGang = new Dictionary<int, ulong>();

		private static int _lastBossTrackDay = -1;
		private static int _aiCrewTurnMaintenanceCursor;
		private static readonly Dictionary<int, int> _aiCrewTurnMaintenanceCrewCursorByGang = new Dictionary<int, int>();
		private static readonly AiCrewLevelupDiagnostics _aiCrewLevelupDiagnostics = new AiCrewLevelupDiagnostics();
		private static readonly Dictionary<ulong, int> _aiCrewLevelupThresholdStateLoggedByPeep = new Dictionary<ulong, int>();
		private static readonly Dictionary<string, AiRobberyPendingContact> _pendingAiHumanRobberyContacts = new Dictionary<string, AiRobberyPendingContact>(StringComparer.Ordinal);
		private static readonly Dictionary<string, int> _aiHumanRobberyDiagnosticCooldownUntilDay = new Dictionary<string, int>(StringComparer.Ordinal);
		private static readonly Dictionary<string, int> _aiHumanRobberySameNodeMissLogDayByKey = new Dictionary<string, int>(StringComparer.Ordinal);
		private static readonly Dictionary<string, int> _aiHumanRobberyApproachWarningDayByKey = new Dictionary<string, int>(StringComparer.Ordinal);
		private static readonly Dictionary<string, int> _aiGangRobberyCooldownUntilDayByPair = new Dictionary<string, int>(StringComparer.Ordinal);
		private static readonly Dictionary<string, int> _aiHumanRobberyPairCooldownUntilDay = new Dictionary<string, int>(StringComparer.Ordinal);
		private static readonly Dictionary<string, DeferredAiHumanRobberyResponse> _deferredAiHumanRobberyResponsesByKey = new Dictionary<string, DeferredAiHumanRobberyResponse>(StringComparer.Ordinal);
		private static readonly Dictionary<string, PendingAiHumanRobberyMeeting> _pendingAiHumanRobberyMeetingsByKey = new Dictionary<string, PendingAiHumanRobberyMeeting>(StringComparer.Ordinal);
		private static readonly HashSet<string> _activeAiHumanRobberyResponseKeys = new HashSet<string>(StringComparer.Ordinal);
		private static readonly Dictionary<string, int> _activeAiHumanRobberyResponseDayByKey = new Dictionary<string, int>(StringComparer.Ordinal);
		private static readonly HashSet<int> _activeAiHumanRobberyResponseHumanPids = new HashSet<int>();
		private static int _lastAiHumanRobberyContactScanDay = int.MinValue;
		private static int _lastAiHumanRobberyArrivalScanFrame = -1;
		private static readonly Dictionary<int, int> _lastAiHumanRobberyAiTurnScanDayByPid = new Dictionary<int, int>();
		private static int _lastAiHumanRobberyTrespassDiscoveryLogDay = int.MinValue;
		private const long HUMAN_TURN_PHASE_THRESHOLD_MS = 20;
		private const long HUMAN_TURN_DEFERRED_PHASE_THRESHOLD_MS = 8;
		private const long HUMAN_TURN_COMPACT_PHASE_THRESHOLD_MS = 80;
		private const int DEFERRED_ALLIANCE_PACT_GANG_OPS_INITIAL_GRACE_FRAMES = 20;
		private const int DEFERRED_ALLIANCE_PACT_GANG_OPS_INPUT_YIELD_LIMIT = 30;
		private const int DEFERRED_ALLIANCE_PACT_GANG_OPS_SAVE_DELAY_FRAMES = 18;
		private const int HUMAN_TURN_START_SAVE_DELAY_FRAMES = 48;
		private static int _deferredAlliancePactGangOpsDay = int.MinValue;
		private static bool _deferredAlliancePactGangOpsQueued;
		private static int _deferredAlliancePactGangOpsStage;
		private static int _deferredAlliancePactGangOpsEarliestFrame = -1;
		private static int _deferredAlliancePactGangOpsGraceDeferrals;
		private static int _deferredAlliancePactGangOpsInputDeferrals;
		private static long _deferredAlliancePactGangOpsStartedTicks;
		private static long _deferredAlliancePactGangOpsCpuMs;
		private static int _lastDeferredAlliancePactGangOpsCancelLogDay = int.MinValue;

		private sealed class AiCrewLevelupDiagnostics
		{
			internal int Checked;
			internal int AppliedCrew;
			internal int Grants;
			internal int MissingData;
			internal int HumanSkipped;
			internal int NoXp;
			internal int BelowThreshold;
			internal int NoNextThreshold;
			internal int NoAvailable;
			internal int InvalidSelection;
			internal int Failed;
			internal int MaxXp;
			internal int MaxNextXp;
			internal EntityID MaxXpPeep = EntityID.INVALID;

			internal void Reset()
			{
				Checked = 0;
				AppliedCrew = 0;
				Grants = 0;
				MissingData = 0;
				HumanSkipped = 0;
				NoXp = 0;
				BelowThreshold = 0;
				NoNextThreshold = 0;
				NoAvailable = 0;
				InvalidSelection = 0;
				Failed = 0;
				MaxXp = 0;
				MaxNextXp = 0;
				MaxXpPeep = EntityID.INVALID;
			}

			internal void TrackXp(Entity peep, int xp, int nextXp)
			{
				if (xp <= MaxXp)
				{
					return;
				}

				MaxXp = xp;
				MaxNextXp = nextXp;
				MaxXpPeep = peep?.Id ?? EntityID.INVALID;
			}
		}

		private static readonly FieldInfo PlayerSubmanagerPlayerField = AccessTools.Field(typeof(PlayerSubmanager), "_player");

		internal static void ResetRuntime()
		{
			_lastKnownBossPeepByGang.Clear();
			_lastBossTrackDay = -1;
			_aiCrewTurnMaintenanceCursor = 0;
			_aiCrewTurnMaintenanceCrewCursorByGang.Clear();
			_aiCrewLevelupDiagnostics.Reset();
			_aiCrewLevelupThresholdStateLoggedByPeep.Clear();
			_pendingAiHumanRobberyContacts.Clear();
			_aiHumanRobberyDiagnosticCooldownUntilDay.Clear();
			_aiHumanRobberySameNodeMissLogDayByKey.Clear();
			_aiGangRobberyCooldownUntilDayByPair.Clear();
			_aiHumanRobberyPairCooldownUntilDay.Clear();
			_deferredAiHumanRobberyResponsesByKey.Clear();
			_pendingAiHumanRobberyMeetingsByKey.Clear();
			_activeAiHumanRobberyResponseKeys.Clear();
			_activeAiHumanRobberyResponseDayByKey.Clear();
			_activeAiHumanRobberyResponseHumanPids.Clear();
			_lastAiHumanRobberyContactScanDay = int.MinValue;
			_lastAiHumanRobberyTrespassDiscoveryLogDay = int.MinValue;
			_deferredAlliancePactGangOpsDay = int.MinValue;
			_deferredAlliancePactGangOpsQueued = false;
			_deferredAlliancePactGangOpsStage = 0;
			_deferredAlliancePactGangOpsEarliestFrame = -1;
			_deferredAlliancePactGangOpsGraceDeferrals = 0;
			_deferredAlliancePactGangOpsInputDeferrals = 0;
			_deferredAlliancePactGangOpsStartedTicks = 0L;
			_deferredAlliancePactGangOpsCpuMs = 0L;
			_lastDeferredAlliancePactGangOpsCancelLogDay = int.MinValue;
			_lastAiHumanRobberyAiTurnScanDayByPid.Clear();
			ResetGrapevineRuntimeState();
		}

		public static void ApplyPatch(Harmony harmony)
		{

			try
			{
				MethodInfo method = typeof(PlayerCrew).GetMethod("OnPlayerTurnStarted", BindingFlags.Instance | BindingFlags.Public);
				if (method != null)
				{
					harmony.Patch((MethodBase)method, (HarmonyMethod)null, new HarmonyMethod(typeof(TurnUpdatePatch), "OnTurnPostfix", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
					Debug.Log("[GameplayTweaks] Turn update enabled");
				}
				MethodInfo aiTurnStartedMethod = typeof(PlayerAI).GetMethod("OnPlayerTurnStarted", BindingFlags.Instance | BindingFlags.Public);
				if (aiTurnStartedMethod != null)
				{
					harmony.Patch((MethodBase)aiTurnStartedMethod, (HarmonyMethod)null, new HarmonyMethod(typeof(TurnUpdatePatch), nameof(OnPlayerAiTurnStartedPostfix)), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
					Debug.Log("[GameplayTweaks] AI human robbery turn contact scan enabled");
				}
				MethodInfo finishActivePlayerTurnMethod = AccessTools.Method(typeof(AllPlayersManager), "FinishActivePlayerTurn");
				if (finishActivePlayerTurnMethod != null)
				{
					harmony.Patch(finishActivePlayerTurnMethod, prefix: new HarmonyMethod(typeof(TurnUpdatePatch), nameof(FinishActivePlayerTurnPrefix)));
				}
			}
			catch (Exception arg)
			{
				Debug.LogError($"[GameplayTweaks] TurnUpdatePatch failed: {arg}");
			}
		}

		private static void OnTurnPostfix(PlayerCrew __instance)
		{

			try
			{
				PlayerInfo humanPlayer = G.GetHumanPlayer();
				if (humanPlayer == null || __instance != humanPlayer.crew)
				{
					return;
				}
				SimTime now = G.GetNow();
				long humanTurnStartTicks = Stopwatch.GetTimestamp();
				long phaseStartTicks = humanTurnStartTicks;
				void MarkHumanTurnPhase(string phase)
				{
					LogHumanTurnPhase(phase, phaseStartTicks, now, humanPlayer);
					phaseStartTicks = Stopwatch.GetTimestamp();
				}
				ProcessHumanHideoutPrepass(humanPlayer, now);
				ProcessNationalHeatTurn(humanPlayer, now);
				ProcessMurderCaseTurn(humanPlayer, now);
				ReconcileAllCrewJailStates("turn");
				MarkHumanTurnPhase("prepass");
				HashSet<long> activeImportantWitnessCrewIds = BuildActiveImportantWitnessCrewIdSet(out int activeImportantWitnessEntries);
				bool hasAnyActiveImportantWitness = activeImportantWitnessEntries > 0;
				foreach (CrewAssignment item in __instance.GetLiving().ToList())
				{
					CrewAssignment current = item;
					if (GameplayTweaksPlugin.TryGetSafeFederalArrestPendingInfo(current.peepId, out GameplayTweaksPlugin.SafeFederalArrestPendingInfo pendingInfo))
					{
						GameplayTweaksPlugin.VerificationLog("Jail", $"safe-federal-turn-skip peep={current.peepId.id} source=turn-loop vehicle={pendingInfo?.VehicleId.id ?? 0} trialDay={pendingInfo?.TrialDate.days ?? -1}");
						continue;
					}
					Entity peep = current.GetPeep();
					if (peep != null)
					{
						ProcessCrewMemberTurn(peep, now, humanPlayer, activeImportantWitnessCrewIds, hasAnyActiveImportantWitness);
					}
				}
				MarkHumanTurnPhase("crew-member-turns");
				ProcessSnitchCaseTurn(humanPlayer, now);
				MarkHumanTurnPhase("snitch-case");
				int aiCrewRelationsProcessed = 0;
				int aiCrewRelationsDeferred = 0;
				ProcessAiCrewTurnMaintenance(now);
				ProcessWarStanceAiSitDownOffers(now);
				ReconcileRaidedGangSafehouseTerritory("pact-turn");
				EnsureHumanSafehouseTerritoryColorOwner("pact-turn", refreshColors: false);
				int lowRespectTerritoryClears = ReconcileLowRespectTerritoryOwnership("pact-turn", refreshColors: false);
				if (lowRespectTerritoryClears > 0)
				{
					DirtyCashEconomyCompatibilityPatch.RequestDeferredHumanTerritoryVisualOnlyRefresh("pact-turn-low-respect", delayFrames: 12, lightweight: true);
				}
				int days = now.days;
				if (days != _lastGangTrackDay)
				{
					_lastGangTrackDay = days;
					RefreshGangTracker();
					ReconcilePersistentGangRelationshipBuffs("pact-turn-day");
				}
				MarkHumanTurnPhase("ai-maintenance-territory");
				QueueDeferredAlliancePactGangOps(humanPlayer, now);
				MarkHumanTurnPhase("alliances-pacts-gangops-queued");
				RunAiHumanRobberyContactPhase(humanPlayer, now);
				MarkHumanTurnPhase("ai-robbery-contact");
				TryRunWeeklyGrapevinePulse(now, "human-turn-start");
				MarkHumanTurnPhase("grapevine-weekly-pulse");
				int days2 = now.days;
				int gangMeetingIntervalDays = GANG_MEETING_INTERVAL_DAYS;
				if (SaveData.GangMeetingIntervalDays != gangMeetingIntervalDays)
				{
					SaveData.GangMeetingIntervalDays = gangMeetingIntervalDays;
				}
				CrewRelationshipHandlerPatch._outingIntervalDays = gangMeetingIntervalDays;
				if (CrewRelationshipHandlerPatch._lastOutingDay < 0)
				{
					CrewRelationshipHandlerPatch._lastOutingDay = days2;
				}
				if (days2 - CrewRelationshipHandlerPatch._lastOutingDay >= gangMeetingIntervalDays)
				{
					CrewRelationshipHandlerPatch._lastOutingDay = days2;
					GangMeetingMode gangMeetingMode = GetGangMeetingMode();
					switch (gangMeetingMode)
					{
					case GangMeetingMode.Disabled:
						VerificationLog("GangMeeting", $"disabled day={days2}");
						break;
					case GangMeetingMode.Prompt:
						CrewOutingEvent.TryRunGangMeetingCycle(humanPlayer, showPrompt: true, "prompt");
						break;
					default:
						CrewOutingEvent.TryRunGangMeetingCycle(humanPlayer, showPrompt: false, "auto");
						break;
					}
				}
				MarkHumanTurnPhase("gang-meeting");
				foreach (PlayerInfo gang in G.GetAllPlayers())
				{
					if (gang == null)
					{
						continue;
					}
					if (gang.PID.IsHumanPlayer || !gang.IsJustGang || gang.crew == null || gang.crew.IsCrewDefeated || gang.crew.LivingCrewCount <= 0)
					{
						continue;
					}
					int lastMeetingDay = GetAiGangLastSnitchMeetingDay(gang.PID.id);
					if (lastMeetingDay >= 0 && days2 - lastMeetingDay < gangMeetingIntervalDays)
					{
						continue;
					}
					if (aiCrewRelationsProcessed >= AI_CREW_RELATIONS_MAINTENANCE_GANG_BUDGET_PER_TURN)
					{
						aiCrewRelationsDeferred++;
						continue;
					}
					try
					{
						SaveData.AiGangLastSnitchMeetingDayByGang[gang.PID.id] = days2;
						RunAIGangSnitchMeeting(gang, now);
						RunAiCrewRelationsDecisions(gang, now);
						RunRelationshipDrivenInternalCrewEvents(gang, now);
						aiCrewRelationsProcessed++;
					}
					catch (Exception ex4)
					{
						Debug.LogError($"[GameplayTweaks] AI gang snitch meeting failed for gang={gang.PID.id}: {ex4}");
					}
				}
				if (aiCrewRelationsDeferred > 0)
				{
					VerificationLog("AiCrewRelations", $"deferred gangs={aiCrewRelationsDeferred} processed={aiCrewRelationsProcessed} budget={AI_CREW_RELATIONS_MAINTENANCE_GANG_BUDGET_PER_TURN} day={days2}");
				}
				MarkHumanTurnPhase("ai-crew-relations");
				if (EnableDirtyCash.Value && !ShouldDeferDirtyCashRuntime())
				{
					try
					{
						DirtyCashPatches.ProcessLaundering();
					}
					catch (Exception arg2)
					{
						Debug.LogError($"[GameplayTweaks] Laundering failed: {arg2}");
					}
				}
				MarkHumanTurnPhase("dirty-cash");
				// CopWarSystem truce logic handled by CopKilling mod
				if (EnableAIAlliances.Value && days2 % 90 == 0 && !SaveData.NeverAcceptPacts && SaveData.PlayerJoinedPactIndex < 0 && !SaveData.Pacts.Any((AlliancePact p) => p.ColorIndex == PLAYER_PACT_SLOT_INDEX) && CalculateGangPower(humanPlayer) >= 30 && SharedRng.NextDouble() < 0.35)
				{
					List<AlliancePact> list = SaveData.Pacts.Where((AlliancePact p) => p.ColorIndex < AI_PACT_SLOT_COUNT && !p.IsPending && !GameplayTweaksPlugin.IsPactAtMemberCap(p)).ToList();
					if (list.Count > 0)
					{
						AlliancePact pact = list[SharedRng.Next(list.Count)];
						try
						{
							PactInvitationEvent.ShowInvitation(pact);
						}
						catch (Exception arg3)
						{
							Debug.LogError($"[GameplayTweaks] Pact invitation failed: {arg3}");
						}
					}
				}
				MarkHumanTurnPhase("pact-invitation");
				PactColorUiPatch.RequestFullCrewPickRefresh("human-turn", 5);
				MarkHumanTurnPhase("pact-ui-refresh");
				QueueDeferredModDataSave("human-turn-start", now, HUMAN_TURN_START_SAVE_DELAY_FRAMES);
				MarkHumanTurnPhase("save-mod-data-queued");
				LogHumanTurnPhase("total", humanTurnStartTicks, now, humanPlayer, 40);
			}
			catch (Exception arg4)
			{
				Debug.LogError($"[GameplayTweaks] OnTurnPostfix: {arg4}");
			}
		}

		private static void FinishActivePlayerTurnPrefix()
		{
			try
			{
				CancelDeferredAlliancePactGangOpsForNextTurn("finish-active-player-turn");
			}
			catch
			{
			}
		}

		private static void QueueDeferredAlliancePactGangOps(PlayerInfo humanPlayer, SimTime scheduledNow)
		{
			try
			{
				int scheduledDay = scheduledNow.days;
				if (_deferredAlliancePactGangOpsQueued && _deferredAlliancePactGangOpsDay == scheduledDay)
				{
					return;
				}

				_deferredAlliancePactGangOpsQueued = true;
				_deferredAlliancePactGangOpsDay = scheduledDay;
				_deferredAlliancePactGangOpsStage = 0;
				_deferredAlliancePactGangOpsEarliestFrame = Time.frameCount + DEFERRED_ALLIANCE_PACT_GANG_OPS_INITIAL_GRACE_FRAMES;
				_deferredAlliancePactGangOpsGraceDeferrals = 0;
				_deferredAlliancePactGangOpsInputDeferrals = 0;
				_deferredAlliancePactGangOpsStartedTicks = Stopwatch.GetTimestamp();
				_deferredAlliancePactGangOpsCpuMs = 0L;
				global::Game.TimerUtil.RunNextFrame(delegate
				{
					RunDeferredAlliancePactGangOpsStage(humanPlayer, scheduledDay, 0);
				});
			}
			catch (Exception ex)
			{
				_deferredAlliancePactGangOpsQueued = false;
				Debug.LogWarning("[GameplayTweaks] Deferred alliance/pact/gang-ops scheduling failed; running inline. " + ex.Message);
				RunDeferredAlliancePactGangOps(humanPlayer, scheduledNow.days);
			}
		}

		private static void RunDeferredAlliancePactGangOps(PlayerInfo humanPlayer, int scheduledDay)
		{
			long totalStartTicks = Stopwatch.GetTimestamp();
			try
			{
				_deferredAlliancePactGangOpsQueued = false;
				if (global::Game.Game.ctx == null || !global::Game.Game.ctx.IsInteractive)
				{
					return;
				}

				SimTime now = G.GetNow();
				if (now.days != scheduledDay)
				{
					VerificationLog("Perf", $"deferred alliance-pact-gangops skipped scheduledDay={scheduledDay} currentDay={now.days}");
					return;
				}

				PlayerInfo currentHuman = G.GetHumanPlayer();
				if (currentHuman != null)
				{
					humanPlayer = currentHuman;
				}

				long phaseStartTicks = Stopwatch.GetTimestamp();
				if (EnableAIAlliances.Value)
				{
					ProcessAIAlliances(now);
					LogHumanTurnPhase("deferred-ai-alliances", phaseStartTicks, now, humanPlayer, HUMAN_TURN_DEFERRED_PHASE_THRESHOLD_MS);
					phaseStartTicks = Stopwatch.GetTimestamp();
					ProcessPactVotes(now);
					ProcessAIInterPactAlliances(now);
					ProcessInterPactAllianceVotes(now);
					ShareLeaderEnemiesAcrossInterPactAlliances();
					LogHumanTurnPhase("deferred-pact-votes", phaseStartTicks, now, humanPlayer, HUMAN_TURN_DEFERRED_PHASE_THRESHOLD_MS);
					phaseStartTicks = Stopwatch.GetTimestamp();
					ProcessPactEarnings();
					EnforcePactPeace();
					CheckPlayerPactWarKick(now);
					LogHumanTurnPhase("deferred-pact-economy-peace", phaseStartTicks, now, humanPlayer, HUMAN_TURN_DEFERRED_PHASE_THRESHOLD_MS);
					phaseStartTicks = Stopwatch.GetTimestamp();
				}

				if (_lastPactOpsTurnDay != now.days)
				{
					_lastPactOpsTurnDay = now.days;
					RunGangOpsTurn(GangOpsChannel.Pact, humanPlayer, now);
					RunGangOpsTurn(GangOpsChannel.Independent, humanPlayer, now);
				}
				LogHumanTurnPhase("deferred-gangops", phaseStartTicks, now, humanPlayer, HUMAN_TURN_DEFERRED_PHASE_THRESHOLD_MS);
				phaseStartTicks = Stopwatch.GetTimestamp();

				if (ShouldDeferRetaliationWarReconciliation())
				{
					VerificationLog("Compat", $"retaliation war reconciliation deferred day={now.days} reason=external-gangwars");
				}
				else
				{
					EnforceGangWarFromRetaliationBuffs(humanPlayer);
				}
				GangWarsAdapterPatch.RunTurnAllianceProjection(now);
				GangWarsAdapterPatch.RunTurnPactAggroBoost(humanPlayer, now);
				FlushCrewPickAggroRefreshes("turn-reconcile-deferred");
				LogHumanTurnPhase("deferred-war-adapters-refresh", phaseStartTicks, now, humanPlayer, HUMAN_TURN_DEFERRED_PHASE_THRESHOLD_MS);

				PactColorUiPatch.RequestFullCrewPickRefresh("human-turn-deferred", 5);
				QueueDeferredModDataSave("deferred-alliance-pact-gangops", now, DEFERRED_ALLIANCE_PACT_GANG_OPS_SAVE_DELAY_FRAMES);
				LogHumanTurnPhase("deferred-alliances-pacts-gangops-wall-total", totalStartTicks, now, humanPlayer, HUMAN_TURN_DEFERRED_PHASE_THRESHOLD_MS);
			}
			catch (Exception ex)
			{
				Debug.LogError("[GameplayTweaks] Deferred alliance/pact/gang-ops failed: " + ex);
			}
			finally
			{
				_deferredAlliancePactGangOpsQueued = false;
			}
		}

		private static void RunDeferredAlliancePactGangOpsStage(PlayerInfo humanPlayer, int scheduledDay, int stage)
		{
			long phaseStartTicks = Stopwatch.GetTimestamp();
			try
			{
				if (!_deferredAlliancePactGangOpsQueued || _deferredAlliancePactGangOpsDay != scheduledDay)
				{
					return;
				}

				if (global::Game.Game.ctx == null || !global::Game.Game.ctx.IsInteractive)
				{
					_deferredAlliancePactGangOpsQueued = false;
					_deferredAlliancePactGangOpsCpuMs = 0L;
					return;
				}

				SimTime now = G.GetNow();
				if (now.days != scheduledDay)
				{
					_deferredAlliancePactGangOpsQueued = false;
					_deferredAlliancePactGangOpsInputDeferrals = 0;
					_deferredAlliancePactGangOpsCpuMs = 0L;
					VerificationLog("Perf", $"deferred alliance-pact-gangops staged skipped scheduledDay={scheduledDay} currentDay={now.days} stage={stage}");
					return;
				}

				if (ShouldYieldDeferredAlliancePactGangOpsStageForInput(stage, scheduledDay))
				{
					QueueDeferredAlliancePactGangOpsStage(humanPlayer, scheduledDay, stage);
					return;
				}

				PlayerInfo currentHuman = G.GetHumanPlayer();
				if (currentHuman != null)
				{
					humanPlayer = currentHuman;
				}

				if (stage == 0)
				{
					_deferredAlliancePactGangOpsStartedTicks = phaseStartTicks;
				}

				_deferredAlliancePactGangOpsStage = stage;
				switch (stage)
				{
					case 0:
						if (EnableAIAlliances.Value)
						{
							ProcessAIAlliances(now);
						}
						LogHumanTurnPhase("deferred-ai-alliances", phaseStartTicks, now, humanPlayer, HUMAN_TURN_DEFERRED_PHASE_THRESHOLD_MS);
						RecordDeferredAlliancePactGangOpsCpu(phaseStartTicks);
						QueueDeferredAlliancePactGangOpsStage(humanPlayer, scheduledDay, 1);
						return;
					case 1:
						if (EnableAIAlliances.Value)
						{
							ProcessPactVotes(now);
							ProcessAIInterPactAlliances(now);
							ProcessInterPactAllianceVotes(now);
							ShareLeaderEnemiesAcrossInterPactAlliances();
						}
						LogHumanTurnPhase("deferred-pact-votes", phaseStartTicks, now, humanPlayer, HUMAN_TURN_DEFERRED_PHASE_THRESHOLD_MS);
						RecordDeferredAlliancePactGangOpsCpu(phaseStartTicks);
						QueueDeferredAlliancePactGangOpsStage(humanPlayer, scheduledDay, 2);
						return;
					case 2:
						if (EnableAIAlliances.Value)
						{
							ProcessPactEarnings();
							EnforcePactPeace();
							CheckPlayerPactWarKick(now);
						}
						LogHumanTurnPhase("deferred-pact-economy-peace", phaseStartTicks, now, humanPlayer, HUMAN_TURN_DEFERRED_PHASE_THRESHOLD_MS);
						RecordDeferredAlliancePactGangOpsCpu(phaseStartTicks);
						QueueDeferredAlliancePactGangOpsStage(humanPlayer, scheduledDay, 3);
						return;
					case 3:
						if (_lastPactOpsTurnDay != now.days)
						{
							RunGangOpsTurn(GangOpsChannel.Pact, humanPlayer, now);
						}
						LogHumanTurnPhase("deferred-gangops-pact", phaseStartTicks, now, humanPlayer, HUMAN_TURN_DEFERRED_PHASE_THRESHOLD_MS);
						RecordDeferredAlliancePactGangOpsCpu(phaseStartTicks);
						QueueDeferredAlliancePactGangOpsStage(humanPlayer, scheduledDay, 4);
						return;
					case 4:
						if (_lastPactOpsTurnDay != now.days)
						{
							_lastPactOpsTurnDay = now.days;
							RunGangOpsTurn(GangOpsChannel.Independent, humanPlayer, now);
						}
						LogHumanTurnPhase("deferred-gangops-independent", phaseStartTicks, now, humanPlayer, HUMAN_TURN_DEFERRED_PHASE_THRESHOLD_MS);
						RecordDeferredAlliancePactGangOpsCpu(phaseStartTicks);
						QueueDeferredAlliancePactGangOpsStage(humanPlayer, scheduledDay, 5);
						return;
					default:
						if (ShouldDeferRetaliationWarReconciliation())
						{
							VerificationLog("Compat", $"retaliation war reconciliation deferred day={now.days} reason=external-gangwars");
						}
						else
						{
							EnforceGangWarFromRetaliationBuffs(humanPlayer);
						}
						GangWarsAdapterPatch.RunTurnAllianceProjection(now);
						GangWarsAdapterPatch.RunTurnPactAggroBoost(humanPlayer, now);
						FlushCrewPickAggroRefreshes("turn-reconcile-deferred");
						LogHumanTurnPhase("deferred-war-adapters-refresh", phaseStartTicks, now, humanPlayer, HUMAN_TURN_DEFERRED_PHASE_THRESHOLD_MS);
						PactColorUiPatch.RequestFullCrewPickRefresh("human-turn-deferred", 5);
						QueueDeferredModDataSave("deferred-alliance-pact-gangops", now, DEFERRED_ALLIANCE_PACT_GANG_OPS_SAVE_DELAY_FRAMES);
						RecordDeferredAlliancePactGangOpsCpu(phaseStartTicks);
						long totalStartTicks = _deferredAlliancePactGangOpsStartedTicks != 0L ? _deferredAlliancePactGangOpsStartedTicks : phaseStartTicks;
						LogHumanTurnPhase("deferred-alliances-pacts-gangops-wall-total", totalStartTicks, now, humanPlayer, HUMAN_TURN_DEFERRED_PHASE_THRESHOLD_MS);
						LogDeferredAlliancePactGangOpsCpuTotal(totalStartTicks, now, humanPlayer);
						_deferredAlliancePactGangOpsQueued = false;
						_deferredAlliancePactGangOpsStage = 0;
						_deferredAlliancePactGangOpsInputDeferrals = 0;
						_deferredAlliancePactGangOpsStartedTicks = 0L;
						_deferredAlliancePactGangOpsCpuMs = 0L;
						return;
				}
			}
			catch (Exception ex)
			{
				_deferredAlliancePactGangOpsQueued = false;
				_deferredAlliancePactGangOpsCpuMs = 0L;
				Debug.LogError("[GameplayTweaks] Deferred alliance/pact/gang-ops staged failed: " + ex);
			}
		}

		internal static void CancelDeferredAlliancePactGangOpsForNextTurn(string source)
		{
			if (!_deferredAlliancePactGangOpsQueued)
			{
				return;
			}

			int scheduledDay = _deferredAlliancePactGangOpsDay;
			int stage = _deferredAlliancePactGangOpsStage;
			int deferrals = _deferredAlliancePactGangOpsInputDeferrals;
			TryFlushCriticalGangOpsBeforeDeferredCancel(scheduledDay, stage, source);
			_deferredAlliancePactGangOpsQueued = false;
			_deferredAlliancePactGangOpsDay = int.MinValue;
			_deferredAlliancePactGangOpsStage = 0;
			_deferredAlliancePactGangOpsEarliestFrame = -1;
			_deferredAlliancePactGangOpsGraceDeferrals = 0;
			_deferredAlliancePactGangOpsInputDeferrals = 0;
			_deferredAlliancePactGangOpsStartedTicks = 0L;
			_deferredAlliancePactGangOpsCpuMs = 0L;
			if (_lastDeferredAlliancePactGangOpsCancelLogDay != scheduledDay)
			{
				_lastDeferredAlliancePactGangOpsCancelLogDay = scheduledDay;
				VerificationLog("Perf", $"deferred alliance-pact-gangops canceled-for-next-turn day={scheduledDay} stage={stage} deferrals={deferrals} source={source}");
			}
		}

		private static void TryFlushCriticalGangOpsBeforeDeferredCancel(int scheduledDay, int stage, string source)
		{
			try
			{
				if (scheduledDay == int.MinValue
					|| global::Game.Game.ctx == null
					|| !global::Game.Game.ctx.IsInteractive
					|| _lastPactOpsTurnDay == scheduledDay)
				{
					return;
				}

				SimTime now = G.GetNow();
				if (now.days != scheduledDay)
				{
					return;
				}

				PlayerInfo humanPlayer = G.GetHumanPlayer();
				bool ranPact = false;
				bool ranIndependent = false;
				long startTicks = Stopwatch.GetTimestamp();
				if (stage <= 2)
				{
					RunGangOpsTurn(GangOpsChannel.Pact, humanPlayer, now);
					ranPact = true;
				}
				if (stage <= 3)
				{
					_lastPactOpsTurnDay = scheduledDay;
					RunGangOpsTurn(GangOpsChannel.Independent, humanPlayer, now);
					ranIndependent = true;
				}
				long elapsedMs = GetElapsedMilliseconds(startTicks);
				if (ranPact || ranIndependent)
				{
					VerificationLog("Perf", $"deferred alliance-pact-gangops critical-flush-before-cancel day={scheduledDay} stage={stage} ranPact={ranPact} ranIndependent={ranIndependent} ms={elapsedMs} source={source}");
				}
			}
			catch (Exception ex)
			{
				VerificationLog("Perf", $"deferred alliance-pact-gangops critical-flush-failed day={scheduledDay} stage={stage} reason={ex.GetType().Name}:{ex.Message} source={source}");
			}
		}

		private static bool ShouldYieldDeferredAlliancePactGangOpsStageForInput(int stage, int scheduledDay)
		{
			try
			{
				if (Time.frameCount < _deferredAlliancePactGangOpsEarliestFrame)
				{
					_deferredAlliancePactGangOpsGraceDeferrals++;
					if (_deferredAlliancePactGangOpsGraceDeferrals == 1 || _deferredAlliancePactGangOpsGraceDeferrals % 30 == 0)
					{
						VerificationLog("Perf", $"deferred alliance-pact-gangops initial-grace day={scheduledDay} stage={stage} deferrals={_deferredAlliancePactGangOpsGraceDeferrals} frame={Time.frameCount} earliest={_deferredAlliancePactGangOpsEarliestFrame}");
					}
					return true;
				}

				if (!ShouldDeferUiMaintenanceForMouseInput())
				{
					_deferredAlliancePactGangOpsInputDeferrals = 0;
					return false;
				}

				if (_deferredAlliancePactGangOpsInputDeferrals >= DEFERRED_ALLIANCE_PACT_GANG_OPS_INPUT_YIELD_LIMIT)
				{
					VerificationLog("Perf", $"deferred alliance-pact-gangops input-yield-exhausted day={scheduledDay} stage={stage} deferrals={_deferredAlliancePactGangOpsInputDeferrals}");
					_deferredAlliancePactGangOpsInputDeferrals = 0;
					return false;
				}

				_deferredAlliancePactGangOpsInputDeferrals++;
				if (_deferredAlliancePactGangOpsInputDeferrals == 1 || _deferredAlliancePactGangOpsInputDeferrals % 10 == 0)
				{
					VerificationLog("Perf", $"deferred alliance-pact-gangops input-yield day={scheduledDay} stage={stage} deferrals={_deferredAlliancePactGangOpsInputDeferrals}");
				}
				return true;
			}
			catch
			{
				_deferredAlliancePactGangOpsInputDeferrals = 0;
				return false;
			}
		}

		private static void QueueDeferredAlliancePactGangOpsStage(PlayerInfo humanPlayer, int scheduledDay, int nextStage)
		{
			try
			{
				global::Game.TimerUtil.RunNextFrame(delegate
				{
					RunDeferredAlliancePactGangOpsStage(humanPlayer, scheduledDay, nextStage);
				});
			}
			catch
			{
				RunDeferredAlliancePactGangOpsStage(humanPlayer, scheduledDay, nextStage);
			}
		}

		private static void RecordDeferredAlliancePactGangOpsCpu(long startTicks)
		{
			_deferredAlliancePactGangOpsCpuMs += GetElapsedMilliseconds(startTicks);
		}

		private static void LogDeferredAlliancePactGangOpsCpuTotal(long wallStartTicks, SimTime now, PlayerInfo humanPlayer)
		{
			try
			{
				long cpuMs = _deferredAlliancePactGangOpsCpuMs;
				long wallMs = GetElapsedMilliseconds(wallStartTicks);
				long thresholdMs = GetHumanTurnPhaseLogThreshold(HUMAN_TURN_DEFERRED_PHASE_THRESHOLD_MS);
				if (cpuMs < thresholdMs && wallMs < thresholdMs)
				{
					return;
				}

				int totalPlayers = -1;
				try
				{
					totalPlayers = global::Game.Game.ctx?.players?.all?.Count ?? -1;
				}
				catch
				{
				}

				int turn = -1;
				try
				{
					turn = global::Game.Game.ctx?.clock?.CurrentTurn ?? -1;
				}
				catch
				{
				}

				Debug.Log($"[PERF][HumanTurnPhase] phase=deferred-alliances-pacts-gangops-cpu-total ms={cpuMs} wallMs={wallMs} day={now.days} year={now.YearsInt} turn={turn} pid={humanPlayer?.PID.id ?? -1} totalPlayers={totalPlayers}");
			}
			catch
			{
			}
		}

		private static void LogHumanTurnPhase(string phase, long startTicks, SimTime now, PlayerInfo humanPlayer, long thresholdMs = HUMAN_TURN_PHASE_THRESHOLD_MS)
		{
			try
			{
				long elapsedMs = GetElapsedMilliseconds(startTicks);
				if (elapsedMs < GetHumanTurnPhaseLogThreshold(thresholdMs))
				{
					return;
				}
				int totalPlayers = -1;
				try
				{
					totalPlayers = global::Game.Game.ctx?.players?.all?.Count ?? -1;
				}
				catch
				{
				}
				int turn = -1;
				try
				{
					turn = global::Game.Game.ctx?.clock?.CurrentTurn ?? -1;
				}
				catch
				{
				}
				Debug.Log($"[PERF][HumanTurnPhase] phase={phase} ms={elapsedMs} day={now.days} year={now.YearsInt} turn={turn} pid={humanPlayer?.PID.id ?? -1} totalPlayers={totalPlayers}");
			}
			catch
			{
			}
		}

		private static long GetHumanTurnPhaseLogThreshold(long thresholdMs)
		{
			try
			{
				return (GameplayTweaksPlugin.EnablePerformanceDiagnostics?.Value ?? false)
					? thresholdMs
					: Math.Max(thresholdMs, HUMAN_TURN_COMPACT_PHASE_THRESHOLD_MS);
			}
			catch
			{
				return Math.Max(thresholdMs, HUMAN_TURN_COMPACT_PHASE_THRESHOLD_MS);
			}
		}

		private static long GetElapsedMilliseconds(long startTicks)
		{
			return (Stopwatch.GetTimestamp() - startTicks) * 1000L / Stopwatch.Frequency;
		}

		// Tunable: AI crew-relations decisions (bribes / meetings)
		private const int AI_LEGAL_ACTION_COOLDOWN_DAYS = 14;  // Min days between expensive legal actions (mayor/judge) per gang
		private const int AI_MIN_CASH_RESERVE = 2000;         // Do not spend below this (clean+dirty) for bribes/meetings
		private const float AI_MORALE_MEETING_HAPPINESS_THRESHOLD = 0.45f;  // Run morale meeting when avg crew happiness below this
		private const int AI_CREW_TURN_MAINTENANCE_GANG_BUDGET_PER_TURN = 1;
		private const int AI_CREW_TURN_MAINTENANCE_CREW_BUDGET_PER_TURN = 2;
		private const long AI_CREW_TURN_MAINTENANCE_MS_BUDGET = 18L;
		private const int AI_CREW_LEVELUP_PRIORITY_SCAN_CREW_BUDGET_PER_TURN = 80;
		private const int AI_CREW_LEVELUP_PRIORITY_APPLY_BUDGET_PER_TURN = 4;
		private const int AI_CREW_LEVELUPS_MAX_PER_CREW_TURN = 12;
		private const int AI_CREW_RELATIONS_MAINTENANCE_GANG_BUDGET_PER_TURN = 6;

		// Tunable: relationship-driven internal crew events (AI-only)
		private const float AI_INTERNAL_EVENT_FAMILY_HAPPINESS_GAIN = 0.07f;
		private const float AI_INTERNAL_EVENT_FAMILY_LOYALTY_GAIN = 0.03f;
		private const int AI_INTERNAL_EVENT_TENSION_MEDIATION_CASH = 1200;
		private const float AI_INTERNAL_EVENT_TENSION_MEDIATION_HAPPINESS_GAIN = 0.08f;
		private const float AI_INTERNAL_EVENT_TENSION_HAPPINESS_PENALTY = 0.05f;
		private const float AI_INTERNAL_EVENT_TENSION_HAPPINESS_THRESHOLD = 0.4f;
		private const float AI_INTERNAL_EVENT_TENSION_LOYALTY_THRESHOLD = 0.35f;
		private const int AI_INTERNAL_EVENT_MIN_CASH_FOR_MEDIATION = 1500;
		private const int AI_INTERNAL_EVENT_MAX_FAMILY_PAIRS_PER_RUN = 2;
		private const int AI_SUPPORT_MIN_CLEAN_RESERVE = 250;
		private const int AI_SUPPORT_GIFT_COST = 50;
		private const int AI_SUPPORT_VACATION_COST = 1500;
		private const int AI_SUPPORT_LAWYER_RETAINER_MEDIUM = 1000;
		private const int AI_SUPPORT_LAWYER_RETAINER_HIGH = 2000;
		private const float AI_SUPPORT_PEPTALK_HAPPINESS_THRESHOLD = 0.65f;
		private const float AI_SUPPORT_PEPTALK_LOYALTY_RATIO_THRESHOLD = 0.8f;
		private const float AI_SUPPORT_GIFT_HAPPINESS_THRESHOLD = 0.5f;
		private const float AI_SUPPORT_GIFT_LOYALTY_RATIO_THRESHOLD = 0.65f;
		private const float AI_SUPPORT_VACATION_HAPPINESS_THRESHOLD = 0.25f;
		private const float AI_SUPPORT_VACATION_LOYALTY_RATIO_THRESHOLD = 0.5f;
		private const float CREW_PASSIVE_HAPPINESS_DECAY_HUMAN_LOW_CREDIT = 0.006f;
		private const float CREW_PASSIVE_HAPPINESS_DECAY_AI_LOW_CREDIT = 0.012f;
		private const int AI_CREW_RELATIONS_MAX_SUPPORT_TARGETS = 3;
		private const int AI_CREW_RELATIONS_MINOR_RETAINER = 500;
		private const int AI_CREW_RELATIONS_UNDERBOSS_MIN_CREW = 3;
		private const int AI_CREW_RELATIONS_SNITCH_DISAPPEAR_MIN_LEAKS = 2;
		private const float AI_CREW_RELATIONS_CRITICAL_LOYALTY_RATIO = 0.35f;
		private const float AI_CREW_RELATIONS_MORALE_MEETING_LOYALTY_THRESHOLD = 0.55f;
		private const int AI_PACT_TRADE_INTERVAL_DAYS = 21;
		private const int AI_NETWORK_TRADE_INTERVAL_DAYS = 42;
		private const int AI_NETWORK_TRADE_MAX_SUCCESS_COUNT = 1;
		private const int AI_NETWORK_TRADE_CANDIDATE_POOL_LIMIT = 24;
		private const int AI_NETWORK_TRADE_MAX_EVALUATED_PAIRS = 16;
		private const int AI_PACT_TRADE_MIN_CLEAN_RESERVE = 1200;
		private const int AI_PACT_TRADE_LAUNDER_CLEAN_COST = 1100;
		private const int AI_PACT_TRADE_LAUNDER_DIRTY_QTY = 1400;
		private const int AI_PACT_TRADE_LEISURE_COST = 1000;
		private const int AI_PACT_TRADE_LIQUOR_CLEAN_COST = 2500;
		private const int AI_PACT_TRADE_LIQUOR_QTY = 120;
		private const int AI_PACT_TRADE_DRUGS_CLEAN_COST = 8000;
		private const int AI_PACT_TRADE_DRUGS_QTY = 80;
		private const int AI_ROBBERY_MIN_CASH_RESERVE = 900;
		private const int AI_ROBBERY_LOW_CASH_MIN = 450;
		private const int AI_ROBBERY_CONTACT_LOW_CASH_MIN = 300;
		private const int AI_ROBBERY_LOW_CASH_MAX = 900;
		private const int AI_ROBBERY_HIGH_CASH_MIN = 900;
		private const int AI_ROBBERY_HIGH_CASH_MAX = 1800;
		private const float AI_ROBBERY_PLAYER_TARGET_CHANCE = 0.12f;
		private const bool AI_ROBBERY_HUMAN_CASH_MUTATION_ENABLED = false;
		private static readonly bool AI_ROBBERY_CONTACT_PLAYER_RESPONSE_ENABLED = true;
		private const bool AI_ROBBERY_CONTACT_VEHICLE_DEBIT_ENABLED = true;
		private const float AI_ROBBERY_CONTACT_VEHICLE_HEAT_GAIN = 2f;
		private const float AI_ROBBERY_CONTACT_REFUSE_HEAT_GAIN = 4f;
		private const float AI_ROBBERY_CONTACT_EVADE_HEAT_GAIN = 2f;
		private const float AI_GANG_ROBBERY_PRE_FRONT_HEAT_GAIN = 8f;
		private const float AI_GANG_ROBBERY_SUCCESS_LOW_HEAT_GAIN = 8f;
		private const float AI_GANG_ROBBERY_SUCCESS_HIGH_HEAT_GAIN = 12f;
		private const float AI_GANG_ROBBERY_FAILED_LOW_HEAT_GAIN = 16f;
		private const float AI_GANG_ROBBERY_FAILED_HIGH_HEAT_GAIN = 22f;
		private const float AI_ROBBERY_REFUSAL_ESCALATION_CHANCE = 0.2f;
		private const float AI_ROBBERY_NEARBY_WORLD_DISTANCE = 24f;
		private const float AI_ROBBERY_NEARBY_PROMPT_WORLD_DISTANCE = 14f;
		private const float AI_ROBBERY_ARRIVED_HOLD_WORLD_DISTANCE = AI_ROBBERY_NEARBY_WORLD_DISTANCE;
		private const float AI_ROBBERY_HUMAN_TERRITORY_MAX_CONTACT_DISTANCE = 14f;
		private const int AI_ROBBERY_DIAGNOSTIC_MAX_CANDIDATES_PER_RUN = 3;
		private const int AI_ROBBERY_MAX_CONTACTS_PER_SCAN = 1;
		private const int AI_ROBBERY_PENDING_CONTACT_EXPIRE_DAYS = 14;
		private const int AI_ROBBERY_MEETING_ROUTE_MAX_DAYS = 84;
		private const int AI_ROBBERY_MEETING_TARGET_COMMIT_REQUEUES = 1;
		private const float AI_ROBBERY_MEETING_TARGET_COMMIT_DISTANCE = 16f;
		private const int AI_ROBBERY_MEETING_TARGET_MOVE_WINDOW_RESETS = 2;
		private const int AI_ROBBERY_RESPONSE_LOCK_STALE_DAYS = 1;
	private const int AI_ROBBERY_COOLDOWN_DAYS_SIX_MONTHS = 180;
	private const int AI_ROBBERY_HUMAN_DIAGNOSTIC_COOLDOWN_DAYS = AI_ROBBERY_COOLDOWN_DAYS_SIX_MONTHS;
		private const int AI_ROBBERY_TRESPASS_GRACE_FAST_DAYS = 1;
		private const int AI_ROBBERY_TRESPASS_GRACE_DEFAULT_DAYS = 2;
		private const int AI_ROBBERY_TRESPASS_GRACE_SLOW_DAYS = 3;
		private const int AI_ROBBERY_TRESPASS_DISCOVERY_LOG_DAYS = 14;
	private const int AI_ROBBERY_HUMAN_NODE_DIAGNOSTIC_COOLDOWN_DAYS = AI_ROBBERY_COOLDOWN_DAYS_SIX_MONTHS;
	private const int AI_ROBBERY_HUMAN_TARGET_DIAGNOSTIC_COOLDOWN_DAYS = AI_ROBBERY_COOLDOWN_DAYS_SIX_MONTHS;
	private const int AI_ROBBERY_HUMAN_VEHICLE_DIAGNOSTIC_COOLDOWN_DAYS = AI_ROBBERY_COOLDOWN_DAYS_SIX_MONTHS;
		private const int AI_ROBBERY_GANG_PAIR_COOLDOWN_DAYS = AI_ROBBERY_COOLDOWN_DAYS_SIX_MONTHS;
		private const string AI_ROBBERY_TRESPASS_RELBUFF_ID = "relbuff-social-gang-trespass";
		private const float AI_ROBBERY_TERRITORY_EDGE_WORLD_DISTANCE = 30f;
		private const float AI_ROBBERY_TERRITORY_PRESSURE_MAX_CONTACT_DISTANCE = 40f;
		private const int AI_ROBBERY_TERRITORY_PRESSURE_POWER_MARGIN = 20;
		private const int AI_ROBBERY_SAME_NODE_MISS_LOG_COOLDOWN_DAYS = 21;
		private const int AI_ROBBERY_APPROACH_WARNING_COOLDOWN_DAYS = AI_ROBBERY_COOLDOWN_DAYS_SIX_MONTHS;
		private const int AI_ROBBERY_REFUSE_MIN_DAMAGE = 8;
		private const int AI_ROBBERY_REFUSE_MAX_DAMAGE = 22;
		private const int AI_ROBBERY_REFUSE_MIN_HEALTH_LEFT = 25;
		private static readonly Label AI_LEVELUP_HOODS = new Label("levelup-hoods");
		private static readonly Label AI_LEVELUP_HOODSGANG = new Label("levelup-hoodsgang");
		private static readonly string[] AI_PACT_TRADE_LIQUOR_LABELS = new string[8] { "home-brew", "moonshine", "cider", "brick-wine", "fake-beer", "fake-wine", "bathtub-gin", "sparkling-cider" };
		private static readonly string[] AI_PACT_TRADE_DRUG_LABELS = new string[7] { "cannabis", "cannabis-joints", "heroin", "opium", "opiumlow", "cocaine", "cocainelow" };

		private static void ProcessAiCrewTurnMaintenance(SimTime now)
		{
			try
			{
				List<PlayerInfo> gangs = G.GetAllPlayers()
					.Where(gang => gang != null
						&& !gang.PID.IsHumanPlayer
						&& gang.IsJustGang
						&& gang.crew != null
						&& !gang.crew.IsCrewDefeated)
					.OrderBy(gang => gang.PID.id)
					.ToList();
				if (gangs.Count == 0)
				{
					_aiCrewTurnMaintenanceCursor = 0;
					return;
				}

				int budget = Math.Max(1, AI_CREW_TURN_MAINTENANCE_GANG_BUDGET_PER_TURN);
				int crewBudget = Math.Max(1, AI_CREW_TURN_MAINTENANCE_CREW_BUDGET_PER_TURN);
				int start = _aiCrewTurnMaintenanceCursor % gangs.Count;
				int processedGangs = 0;
				int processedCrew = 0;
				int scannedGangs = 0;
				bool partialGang = false;
				bool budgetHit = false;
				bool timeBudgetHit = false;
				int nextGangCursor = start;
				_aiCrewLevelupDiagnostics.Reset();
				Stopwatch stopwatch = Stopwatch.StartNew();
				ProcessPriorityAiCrewLevelups(gangs, now, stopwatch, out int priorityScannedCrew, out int priorityEligibleCrew, out int priorityAppliedCrew, out int priorityGrants);
				for (int offset = 0; offset < gangs.Count && processedGangs < budget && processedCrew < crewBudget; offset++)
				{
					int gangIndex = (start + offset) % gangs.Count;
					PlayerInfo gang = gangs[gangIndex];
					scannedGangs++;
					int gangId = gang.PID.id;
					List<CrewAssignment> livingCrew = gang.crew.GetLiving().ToList();
					if (livingCrew.Count == 0)
					{
						_aiCrewTurnMaintenanceCrewCursorByGang.Remove(gangId);
						processedGangs++;
						nextGangCursor = (gangIndex + 1) % gangs.Count;
						continue;
					}

					int crewCursor = 0;
					if (_aiCrewTurnMaintenanceCrewCursorByGang.TryGetValue(gangId, out int savedCrewCursor))
					{
						crewCursor = Math.Max(0, savedCrewCursor) % livingCrew.Count;
					}

					int crewVisited = 0;
					while (crewVisited < livingCrew.Count && processedCrew < crewBudget)
					{
						CrewAssignment assignment = livingCrew[crewCursor];
						Entity aiPeep = assignment.GetPeep();
						if (aiPeep == null)
						{
							crewCursor = (crewCursor + 1) % livingCrew.Count;
							crewVisited++;
							continue;
						}

						ProcessCrewMemberTurn(aiPeep, now, gang);
						processedCrew++;
						crewCursor = (crewCursor + 1) % livingCrew.Count;
						crewVisited++;
						if (stopwatch.ElapsedMilliseconds >= AI_CREW_TURN_MAINTENANCE_MS_BUDGET)
						{
							timeBudgetHit = true;
							break;
						}
					}

					if (crewVisited >= livingCrew.Count)
					{
						_aiCrewTurnMaintenanceCrewCursorByGang.Remove(gangId);
						processedGangs++;
						nextGangCursor = (gangIndex + 1) % gangs.Count;
					}
					else
					{
						_aiCrewTurnMaintenanceCrewCursorByGang[gangId] = crewCursor;
						partialGang = true;
						nextGangCursor = gangIndex;
					}

					budgetHit = processedCrew >= crewBudget || processedGangs >= budget || timeBudgetHit;
					if (budgetHit)
					{
						break;
					}
				}
				stopwatch.Stop();
				_aiCrewTurnMaintenanceCursor = nextGangCursor % gangs.Count;
				int deferredGangs = Math.Max(0, gangs.Count - processedGangs);
				if (priorityScannedCrew > 0 && (priorityEligibleCrew > 0 || priorityAppliedCrew > 0 || stopwatch.ElapsedMilliseconds >= 20))
				{
					VerificationLog(
						"AiCrewLevelup",
						$"priority-scan scannedCrew={priorityScannedCrew} eligibleCrew={priorityEligibleCrew} appliedCrew={priorityAppliedCrew} grants={priorityGrants} applyBudget={AI_CREW_LEVELUP_PRIORITY_APPLY_BUDGET_PER_TURN} day={now.days} ms={stopwatch.ElapsedMilliseconds}");
				}
				if (partialGang || deferredGangs > 0 || stopwatch.ElapsedMilliseconds >= 20)
				{
					VerificationLog(
						"AiCrewTurn",
						$"maintenance processedGangs={processedGangs} scannedGangs={scannedGangs} deferredGangs={deferredGangs} partialGang={partialGang} processedCrew={processedCrew} totalGangs={gangs.Count} gangBudget={budget} crewBudget={crewBudget} budgetHit={budgetHit} timeBudgetHit={timeBudgetHit} cursor={_aiCrewTurnMaintenanceCursor} ms={stopwatch.ElapsedMilliseconds} day={now.days}");
				}
				if (processedCrew > 0)
				{
					VerificationLog(
						"AiCrewLevelup",
						$"summary checked={_aiCrewLevelupDiagnostics.Checked} appliedCrew={_aiCrewLevelupDiagnostics.AppliedCrew} grants={_aiCrewLevelupDiagnostics.Grants} missingData={_aiCrewLevelupDiagnostics.MissingData} humanSkipped={_aiCrewLevelupDiagnostics.HumanSkipped} noXp={_aiCrewLevelupDiagnostics.NoXp} belowThreshold={_aiCrewLevelupDiagnostics.BelowThreshold} noNextThreshold={_aiCrewLevelupDiagnostics.NoNextThreshold} noAvailable={_aiCrewLevelupDiagnostics.NoAvailable} invalidSelection={_aiCrewLevelupDiagnostics.InvalidSelection} failed={_aiCrewLevelupDiagnostics.Failed} maxXp={_aiCrewLevelupDiagnostics.MaxXp} maxNextXp={_aiCrewLevelupDiagnostics.MaxNextXp} maxXpPeep={_aiCrewLevelupDiagnostics.MaxXpPeep.id} processedCrew={processedCrew} day={now.days}");
				}
			}
			catch (Exception ex)
			{
				Debug.LogWarning($"[GameplayTweaks] AI crew turn maintenance failed: {ex.Message}");
			}
		}

		private static void OnPlayerAiTurnStartedPostfix(PlayerAI __instance)
		{
			try
			{
				if (!TryResolvePlayerAiOwner(__instance, out PlayerInfo actor) || actor == null || actor.PID.IsHumanPlayer)
				{
					return;
				}
				TryRunAiHumanRobberyContactScanForActor(actor, G.GetNow(), "ai-turn-contact");
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] AI turn robbery contact scan failed: " + ex.Message);
			}
		}

		private static bool TryResolvePlayerAiOwner(PlayerAI ai, out PlayerInfo player)
		{
			player = null;
			try
			{
				player = PlayerSubmanagerPlayerField?.GetValue(ai) as PlayerInfo;
				return player != null;
			}
			catch
			{
				return false;
			}
		}

		private static void ProcessPriorityAiCrewLevelups(List<PlayerInfo> gangs, SimTime now, Stopwatch stopwatch, out int scannedCrew, out int eligibleCrew, out int appliedCrew, out int grants)
		{
			scannedCrew = 0;
			eligibleCrew = 0;
			appliedCrew = 0;
			grants = 0;
			if (gangs == null || gangs.Count == 0)
			{
				return;
			}

			foreach (PlayerInfo gang in gangs)
			{
				if (gang?.crew == null)
				{
					continue;
				}
				if (stopwatch.ElapsedMilliseconds >= AI_CREW_TURN_MAINTENANCE_MS_BUDGET)
				{
					return;
				}

				List<CrewAssignment> livingCrew = gang.crew.GetLiving().ToList();
				foreach (CrewAssignment assignment in livingCrew)
				{
					if (scannedCrew >= AI_CREW_LEVELUP_PRIORITY_SCAN_CREW_BUDGET_PER_TURN)
					{
						return;
					}
					Entity peep = assignment.GetPeep();
					scannedCrew++;
					if (!IsAiCrewLevelupSpendableNow(gang, peep))
					{
						continue;
					}

					eligibleCrew++;
					if (appliedCrew >= AI_CREW_LEVELUP_PRIORITY_APPLY_BUDGET_PER_TURN)
					{
						continue;
					}

					int applied = TryApplyAiCrewLevelups(gang, peep, now, "priority-scan");
					if (applied > 0)
					{
						appliedCrew++;
						grants += applied;
					}
					if (stopwatch.ElapsedMilliseconds >= AI_CREW_TURN_MAINTENANCE_MS_BUDGET)
					{
						return;
					}
				}
			}
		}

		private static bool IsAiCrewLevelupSpendableNow(PlayerInfo gang, Entity peep)
		{
			if (gang?.crew == null || gang.PID.IsHumanPlayer || peep?.components?.agent == null || peep.data?.agent?.xp == null)
			{
				return false;
			}
			try
			{
				(int nextXP, string _) = peep.components.agent.FindXPForNextLevelup();
				return nextXP > 0 && peep.components.agent.GetXP() >= nextXP;
			}
			catch
			{
				return false;
			}
		}

		private static int TryApplyAiCrewLevelups(PlayerInfo gang, Entity peep, SimTime now, string source)
		{
			if (gang?.crew == null || peep?.components?.agent == null || peep.data?.agent == null)
			{
				_aiCrewLevelupDiagnostics.MissingData++;
				return 0;
			}
			if (gang.PID.IsHumanPlayer)
			{
				_aiCrewLevelupDiagnostics.HumanSkipped++;
				return 0;
			}
			try
			{
				AgentComponent agent = peep.components.agent;
				XP xp = peep.data.agent.xp;
				_aiCrewLevelupDiagnostics.Checked++;
				int currentXp = agent.GetXP();
				if (xp == null)
				{
					_aiCrewLevelupDiagnostics.MissingData++;
					return 0;
				}
				if (currentXp <= 0)
				{
					_aiCrewLevelupDiagnostics.NoXp++;
					return 0;
				}

				int grants = 0;
				int customHoods = 0;
				int customHoodsGang = 0;
				List<string> applied = null;
				string stopReason = "none";
				int lastNextXP = 0;
				while (grants < AI_CREW_LEVELUPS_MAX_PER_CREW_TURN)
				{
					(int nextXP, string _) = agent.FindXPForNextLevelup();
					currentXp = agent.GetXP();
					lastNextXP = nextXP;
					_aiCrewLevelupDiagnostics.TrackXp(peep, currentXp, nextXP);
					if (nextXP <= 0)
					{
						stopReason = "no-next-threshold";
						break;
					}
					if (currentXp < nextXP)
					{
						stopReason = "below-threshold";
						break;
					}

					List<LevelupDescription> available = agent.GetAvailableLevelups(explain: false)?.ToList();
					if (available == null || available.Count == 0)
					{
						stopReason = "no-available";
						break;
					}

					LevelupDescription selected = ChooseAiCrewLevelup(available);
					if (selected?.levelup == null || selected.nextLevel <= selected.currentLevel)
					{
						stopReason = "invalid-selection";
						break;
					}

					xp.lastThreshold++;
					xp.SetLevelupLevel(selected.levelup.id, selected.nextLevel);
					grants++;
					if (selected.levelup.id == AI_LEVELUP_HOODS)
					{
						customHoods++;
					}
					else if (selected.levelup.id == AI_LEVELUP_HOODSGANG)
					{
						customHoodsGang++;
					}
					if (applied == null)
					{
						applied = new List<string>();
					}
					if (applied.Count < 8)
					{
						applied.Add($"{selected.levelup.id}:{selected.currentLevel}->{selected.nextLevel}");
					}
				}

				if (grants <= 0)
				{
					if (string.Equals(stopReason, "no-next-threshold", StringComparison.Ordinal))
					{
						_aiCrewLevelupDiagnostics.NoNextThreshold++;
					}
					else if (string.Equals(stopReason, "below-threshold", StringComparison.Ordinal))
					{
						_aiCrewLevelupDiagnostics.BelowThreshold++;
						TryLogAiCrewLevelupThresholdState(gang, peep, xp, currentXp, lastNextXP, source, now);
					}
					else if (string.Equals(stopReason, "no-available", StringComparison.Ordinal))
					{
						_aiCrewLevelupDiagnostics.NoAvailable++;
					}
					else if (string.Equals(stopReason, "invalid-selection", StringComparison.Ordinal))
					{
						_aiCrewLevelupDiagnostics.InvalidSelection++;
					}
					if (!string.Equals(stopReason, "below-threshold", StringComparison.Ordinal))
					{
						VerificationLog(
							"AiCrewLevelup",
							$"skipped gang={gang.PID.id} peep={peep.Id.id} reason={stopReason} xp={currentXp} nextXp={lastNextXP} lastThreshold={xp.lastThreshold} source={source} day={now.days}");
					}
					return 0;
				}

				global::Game.Game.ctx?.events?.EnqueueOnce(new global::Game.Session.SessionEvent(global::Game.Session.SessionEventType.CrewLevelUpsChanged, peep.Id, gang.PID));
				string appliedText = applied == null || applied.Count == 0 ? "none" : string.Join(",", applied);
				_aiCrewLevelupDiagnostics.AppliedCrew++;
				_aiCrewLevelupDiagnostics.Grants += grants;
				VerificationLog(
					"AiCrewLevelup",
					$"applied gang={gang.PID.id} peep={peep.Id.id} grants={grants} hoods={customHoods} hoodsgang={customHoodsGang} xp={agent.GetXP()} lastThreshold={xp.lastThreshold} source={source} applied={appliedText} day={now.days}");
				return grants;
			}
			catch (Exception ex)
			{
				_aiCrewLevelupDiagnostics.Failed++;
				VerificationLog("AiCrewLevelup", $"failed gang={gang.PID.id} peep={peep.Id.id} reason={ex.GetType().Name} message={ex.Message}");
				return 0;
			}
		}

		private static void TryLogAiCrewLevelupThresholdState(PlayerInfo gang, Entity peep, XP xp, int currentXp, int nextXp, string source, SimTime now)
		{
			if (gang == null || peep == null || xp == null || !peep.Id.IsValid)
			{
				return;
			}
			if (currentXp < 40 && nextXp <= 40)
			{
				return;
			}

			int hoods = xp.GetLevelupLevel(AI_LEVELUP_HOODS);
			int hoodsGang = xp.GetLevelupLevel(AI_LEVELUP_HOODSGANG);
			int signature = unchecked((nextXp * 397) ^ xp.lastThreshold ^ (hoods << 8) ^ (hoodsGang << 16));
			if (_aiCrewLevelupThresholdStateLoggedByPeep.TryGetValue(peep.Id.id, out int previousSignature) && previousSignature == signature)
			{
				return;
			}

			_aiCrewLevelupThresholdStateLoggedByPeep[peep.Id.id] = signature;
			VerificationLog(
				"AiCrewLevelup",
				$"threshold-state gang={gang.PID.id} peep={peep.Id.id} xp={currentXp} nextXp={nextXp} lastThreshold={xp.lastThreshold} hoods={hoods} hoodsgang={hoodsGang} source={source} day={now.days}");
		}

		private static LevelupDescription ChooseAiCrewLevelup(List<LevelupDescription> available)
		{
			LevelupDescription hoodsGang = available
				.Where(desc => desc?.levelup != null && desc.levelup.id == AI_LEVELUP_HOODSGANG)
				.OrderByDescending(desc => desc.nextLevel)
				.FirstOrDefault();
			if (hoodsGang != null)
			{
				return hoodsGang;
			}

			LevelupDescription hoods = available
				.Where(desc => desc?.levelup != null && desc.levelup.id == AI_LEVELUP_HOODS)
				.OrderByDescending(desc => desc.nextLevel)
				.FirstOrDefault();
			if (hoods != null)
			{
				return hoods;
			}

			return available[SharedRng.Next(available.Count)];
		}

		private sealed class AiTradeActor
		{
			public PlayerInfo Gang;

			public AlliancePact Pact;

			public string ActorKey;

			public bool IsPactActor => Pact != null;
		}

		private sealed class AiTradeProposal
		{
			public string TradeKey;

			public AiTradeActor SellerActor;

			public AiTradeActor BuyerActor;

			public PlayerInfo Seller;

			public PlayerInfo Buyer;

			public float Score;
		}

		private sealed class AiRobberyCandidate
		{
			public PlayerInfo Robber;

			public CrewAssignment TargetCrew;

			public Node ContactNode;

			public float Distance;

			public bool SameNode;

			public string RobberNodeSource;

			public string TargetNodeSource;

			public bool DirectAggro;

			public bool BroadlyHostile;

			public bool EnemyTerritory;

			public bool HumanTerritory;

			public bool StrictTrespassNode;

			public bool TerritoryPressure;

			public bool RecentTrespassMemory;


			public int RobberPower;

			public int HumanPower;

			public int RobberLocalPower;

			public int HumanLocalPower;

			public float RarityChance;

			public float RarityRoll;

			public bool WouldAttempt;

			public int TrespassGraceDays;

			public string Reason;
		}

		private sealed class AiRobberyPendingContact
		{
			public int RobberPid;

			public long TargetCrewPeepId;

			public long TargetVehicleId;

			public short TargetNodeIndex;

			public int CreatedDay;

			public int ExpireDay;

			public int ContactDay;

			public int GraceDays;

			public string Source = string.Empty;

			public bool WarningShown;
		}

		private sealed class DeferredAiHumanRobberyResponse
		{
			public int RobberPid;

			public long TargetCrewPeepId;

			public long TargetVehicleId;

			public short TargetNodeIndex;

			public int EnactedDay;

			public int NotBeforeDay;

			public int ExpireDay;

			public int CreatedDay;

			public int LastPendingKeepLogDay = int.MinValue;

			public string Source = string.Empty;

			public string Mode = string.Empty;

			public float Distance;

			public bool SameNode;

			public string RobberNodeSource = string.Empty;

			public string TargetNodeSource = string.Empty;

			public bool DirectAggro;

			public bool BroadlyHostile;

			public bool EnemyTerritory;

			public bool HumanTerritory;

			public bool StrictTrespassNode;

			public bool TerritoryPressure;

			public bool RecentTrespassMemory;

			public int RobberPower;

			public int HumanPower;

			public int RobberLocalPower;

			public int HumanLocalPower;

			public int TrespassGraceDays;

			public string Reason = string.Empty;
		}

		private sealed class PendingAiHumanRobberyMeeting
		{
			public int RobberPid;

			public int HumanPid;

			public long TargetCrewPeepId;

			public long TargetVehicleId;

			public long MeetingPeepId;

			public long MeetingVehicleId;

			public short MeetingNodeIndex;

			public short MeetingCrewNodeIndex;

			public short RouteTargetNodeIndex;

			public short LastObservedNodeIndex;

			public int QueuedDay;

			public int NotBeforeDay;

			public int ExpireDay;

			public int CreatedDay;

			public int Priority;

			public int LastRouteIssuedDay;

			public int LastProgressDay;

			public int NoProgressChecks;

			public float LastDistanceToMeeting = -1f;

			public int DivergingRouteChecks;

			public string Source = string.Empty;

			public string Mode = string.Empty;

			public string Reason = string.Empty;

			public string MeetingCrewNodeSource = string.Empty;

			public bool RouteQueued;

			public bool Arrived;

			public int RouteRequeueCount;

			public bool FinalTargetRouteCommitted;

			public bool ActorReassignmentGranted;

			public int TargetMoveRouteWindowResets;

			public int LastRetainedLogDay = int.MinValue;
		}

		private sealed class AiRobberyResolutionPreview
		{
			public int SafehouseCash;

			public int VehicleCash;

			public int PeepCash;

			public int TotalCash;

			public int AvailableCash;

			public int Amount;

			public bool CanDebitSafehouse;

			public bool CanDebitCleanCash;

			public bool CanDebitVehicle;

			public bool CanDebitPeep;

			public string Reason;
		}

		/// <summary>Returns the set of living crew member EntityIDs for the given gang.</summary>
		internal static HashSet<EntityID> GetSameGangCrewEntityIds(PlayerInfo gang)
		{
			var set = new HashSet<EntityID>();
			if (gang?.crew == null)
			{
				return set;
			}
			foreach (CrewAssignment assignment in gang.crew.GetLiving())
			{
				Entity p = assignment.GetPeep();
				if (p != null && p.Id.IsValid)
				{
					set.Add(p.Id);
				}
			}
			return set;
		}

		/// <summary>Enumerates same-gang (peepA, peepB) pairs that have a family/spouse relationship. Each pair is yielded once (A-B, not also B-A).</summary>
		internal static IEnumerable<(Entity peepA, Entity peepB)> EnumerateInternalRelationshipPairs(PlayerInfo gang)
		{
			HashSet<EntityID> sameGangIds = GetSameGangCrewEntityIds(gang);
			if (sameGangIds.Count < 2)
			{
				yield break;
			}
			RelationshipTracker rels = G.GetRels();
			if (rels == null)
			{
				yield break;
			}
			var emitted = new HashSet<(ulong, ulong)>();
			foreach (EntityID memberId in sameGangIds)
			{
				Entity peepA = EntityIDExtensions.FindEntity(memberId);
				if (peepA == null)
				{
					continue;
				}
				RelationshipList listOrNull = rels.GetListOrNull(memberId);
				if (listOrNull?.data == null)
				{
					continue;
				}
				foreach (Relationship datum in listOrNull.data)
				{
					if ((int)datum.type != 20 && (int)datum.type != 50 && (int)datum.type != 10 && (int)datum.type != 30 && (int)datum.type != 40 && (int)datum.type != 90)
					{
						continue;
					}
					if (!sameGangIds.Contains(datum.to))
					{
						continue;
					}
					Entity peepB = EntityIDExtensions.FindEntity(datum.to);
					if (peepB == null || peepB.Id.id == peepA.Id.id)
					{
						continue;
					}
					ulong idMin = Math.Min(peepA.Id.id, peepB.Id.id);
					ulong idMax = Math.Max(peepA.Id.id, peepB.Id.id);
					if (emitted.Add((idMin, idMax)))
					{
						yield return (peepA, peepB);
					}
				}
			}
		}

		/// <summary>Run crew-relations decisions for an AI gang: bribes, threaten witness, optional morale meeting. Called on same cadence as snitch meeting.</summary>
		internal static void RunAiCrewRelationsDecisions(PlayerInfo gang, SimTime now)
		{
			if (gang == null || gang.PID.IsHumanPlayer || gang.crew == null || gang.crew.IsCrewDefeated || gang.crew.LivingCrewCount <= 0)
			{
				return;
			}
			ulong bossPeepId = GetBossPeepId(gang);
			if (bossPeepId == 0uL)
			{
				return;
			}
			Entity bossPeep = EntityIDExtensions.FindEntity(EntityID.FromID(bossPeepId));
			if (bossPeep == null)
			{
				return;
			}
			CrewModState bossState = GetOrCreateCrewState(bossPeep.Id);
			if (bossState == null)
			{
				return;
			}
			int clean = GetGangCleanCash(gang);
			int dirty = GetGangDirtyCash(gang);
			int totalCash = clean + dirty;
			int lastLegalDay = GetAiGangLastLegalActionDay(gang.PID.id);
			bool legalOnCooldown = lastLegalDay >= 0 && (now.days - lastLegalDay < AI_LEGAL_ACTION_COOLDOWN_DAYS);
			bool canSpend = totalCash >= AI_MIN_CASH_RESERVE;
			bool didExpensiveAction = false;

			// Priority 1: judge bribe (per-boss) when high risk and can afford
			if (!legalOnCooldown && canSpend && (bossState.LocalHeatLevel >= WantedLevel.Medium || bossState.FedsIncoming) && !bossState.JudgeBribeActive)
			{
				int judgeCost = GetBribeCost(bossState.LocalHeatLevel) * 2;
				if (clean >= judgeCost && TryBribeJudgeForBoss(gang, bossState))
				{
					SetAiGangLastLegalActionDay(gang.PID.id, now.days);
					didExpensiveAction = true;
				}
			}

			// Priority 2: mayor bribe (gang-wide) when high risk and judge not just taken
			if (!didExpensiveAction && !legalOnCooldown && canSpend && totalCash >= 10000 && !GetAiGangMayorBribeActive(gang.PID.id, now))
			{
				if ((bossState.LocalHeatLevel >= WantedLevel.High || bossState.FedsIncoming) && TryBribeMayorForGang(gang, now))
				{
					SetAiGangLastLegalActionDay(gang.PID.id, now.days);
					didExpensiveAction = true;
				}
			}

			// Priority 3: threaten witness (free)
			if (GetThreatenableWitnessCount(bossState) > 0)
			{
				TryThreatenWitnessForPeep(gang, bossPeep, bossState, now);
			}

			if (bossState.LocalHeatLevel >= WantedLevel.Medium || bossState.FedsIncoming || bossState.WitnessCount > 0)
			{
				int desiredRetainer = (bossState.FedsIncoming || bossState.LocalHeatLevel >= WantedLevel.High)
					? AI_SUPPORT_LAWYER_RETAINER_HIGH
					: AI_SUPPORT_LAWYER_RETAINER_MEDIUM;
				TryFundAiLawyerRetainer(gang, bossPeep, bossState, desiredRetainer, "boss-risk");
			}
			if (!didExpensiveAction
				&& (bossState.FedsIncoming || bossState.LocalHeatLevel >= WantedLevel.High || bossState.WitnessCount >= 2)
				&& !bossState.JudgeBribeActive
				&& !GetAiGangMayorBribeActive(gang.PID.id, now))
			{
				TryQueueAiHideout(gang, bossPeep, bossState, "boss-risk");
			}

			EnsureAiUnderbossAssigned(gang, bossPeepId);
			int relationActions = 0;
			relationActions += TryHandleAiExposedSnitches(gang, bossPeepId, now);
			relationActions += TryHandleAiCrewLegalRisk(gang, bossPeepId, now);

			foreach (var target in FindAiCrewSupportTargets(gang, AI_CREW_RELATIONS_MAX_SUPPORT_TARGETS))
			{
				Entity peep = target.peep;
				CrewModState state = target.state;
				if (peep == null || state == null || target.needScore <= 0.2f)
				{
					continue;
				}
				if (TryRunAiCrewSupportPackage(gang, peep, state))
				{
					relationActions++;
				}
			}

			// Priority 4: morale meeting if average happiness low and can afford (no snitch reveal; we already ran snitch meeting)
			float avgHappiness = 0f;
			float avgLoyaltyRatio = 0f;
			int count = 0;
			foreach (CrewAssignment living in gang.crew.GetLiving())
			{
				Entity p = living.GetPeep();
				if (p == null) continue;
				CrewModState s = GetOrCreateCrewState(p.Id);
				if (s != null)
				{
					avgHappiness += s.HappinessValue;
					avgLoyaltyRatio += GetAiCrewLoyaltyRatio(p, s, gang);
					count++;
				}
			}
			if (count > 0)
			{
				avgHappiness /= count;
				avgLoyaltyRatio /= count;
			}
			if ((avgHappiness < AI_MORALE_MEETING_HAPPINESS_THRESHOLD || avgLoyaltyRatio < AI_CREW_RELATIONS_MORALE_MEETING_LOYALTY_THRESHOLD) && canSpend)
			{
				CrewOutingEvent.TryExecuteGangMeetingEffectsForGang(gang, "ai", runSnitchReveal: false);
			}
			if (relationActions > 0
				|| avgHappiness < AI_MORALE_MEETING_HAPPINESS_THRESHOLD
				|| avgLoyaltyRatio < AI_CREW_RELATIONS_MORALE_MEETING_LOYALTY_THRESHOLD
				|| bossState.LocalHeatLevel >= WantedLevel.Medium
				|| bossState.FedsIncoming
				|| bossState.WitnessCount > 0)
			{
				VerificationLog("AiCrewRelations", $"summary gang={gang.PID.id} avgHappy={avgHappiness:0.000} avgLoyaltyRatio={avgLoyaltyRatio:0.000} actions={relationActions} cash={totalCash}");
			}
		}

		private static bool IsAiCrewUnavailableForRelations(Entity peep, CrewModState state)
		{
			return peep == null
				|| state == null
				|| state.InJail
				|| JailSystem.IsInJail(peep.Id)
				|| state.OnHideout
				|| state.HideoutPending
				|| state.OnVacation
				|| state.VacationPending;
		}

		private static void EnsureAiUnderbossAssigned(PlayerInfo gang, ulong bossPeepId)
		{
			if (gang?.crew == null || gang.crew.LivingCrewCount < AI_CREW_RELATIONS_UNDERBOSS_MIN_CREW)
			{
				return;
			}
			Entity bestPeep = null;
			CrewModState bestState = null;
			float bestScore = float.MinValue;
			foreach (CrewAssignment assignment in gang.crew.GetLiving())
			{
				Entity peep = assignment.GetPeep();
				if (peep == null || peep.Id.id == bossPeepId)
				{
					continue;
				}
				CrewModState state = GetOrCreateCrewState(peep.Id);
				if (state == null)
				{
					continue;
				}
				EnsureLoyaltyInitialized(peep, state, gang);
				if (state.IsUnderboss && !IsAiCrewUnavailableForRelations(peep, state))
				{
					return;
				}
				if (IsAiCrewUnavailableForRelations(peep, state))
				{
					continue;
				}
				float score = Mathf.Clamp01(state.LoyaltyValue) + Mathf.Clamp01(state.HappinessValue) * 0.35f + Mathf.Clamp01(state.StreetCreditProgress) * 0.15f;
				if (score > bestScore)
				{
					bestScore = score;
					bestPeep = peep;
					bestState = state;
				}
			}
			if (bestPeep == null || bestState == null)
			{
				return;
			}
			bestState.IsUnderboss = true;
			VerificationLog("AiCrewRelations", $"underboss-assigned gang={gang.PID.id} peep={bestPeep.Id.id} score={bestScore:0.000}");
		}

		private static int TryHandleAiExposedSnitches(PlayerInfo gang, ulong bossPeepId, SimTime now)
		{
			if (gang?.crew == null)
			{
				return 0;
			}
			int queued = 0;
			foreach (CrewAssignment assignment in gang.crew.GetLiving())
			{
				Entity peep = assignment.GetPeep();
				if (peep == null || peep.Id.id == bossPeepId)
				{
					continue;
				}
				CrewModState state = GetOrCreateCrewState(peep.Id);
				if (IsAiCrewUnavailableForRelations(peep, state) || !state.SnitchExposed || state.SnitchDisappearPending)
				{
					continue;
				}
				float loyaltyRatio = GetAiCrewLoyaltyRatio(peep, state, gang);
				if (state.SnitchLeakCount < AI_CREW_RELATIONS_SNITCH_DISAPPEAR_MIN_LEAKS && loyaltyRatio > AI_CREW_RELATIONS_CRITICAL_LOYALTY_RATIO)
				{
					continue;
				}
				state.SnitchDisappearPending = true;
				state.SnitchDisappearDueDay = now.days + 1;
				queued++;
				VerificationLog("AiCrewRelations", $"snitch-disappear-queued gang={gang.PID.id} peep={peep.Id.id} leaks={state.SnitchLeakCount} loyaltyRatio={loyaltyRatio:0.000} dueDay={state.SnitchDisappearDueDay}");
			}
			return queued;
		}

		private static int TryHandleAiCrewLegalRisk(PlayerInfo gang, ulong bossPeepId, SimTime now)
		{
			if (gang?.crew == null)
			{
				return 0;
			}
			int actions = 0;
			foreach (CrewAssignment assignment in gang.crew.GetLiving())
			{
				Entity peep = assignment.GetPeep();
				if (peep == null || peep.Id.id == bossPeepId)
				{
					continue;
				}
				CrewModState state = GetOrCreateCrewState(peep.Id);
				if (IsAiCrewUnavailableForRelations(peep, state))
				{
					continue;
				}
				if (GetThreatenableWitnessCount(state) > 0 && TryThreatenWitnessForPeep(gang, peep, state, now))
				{
					actions++;
				}
				bool highRisk = state.FedsIncoming || state.LocalHeatLevel >= WantedLevel.High || state.WitnessCount >= 2;
				bool mediumRisk = highRisk || state.LocalHeatLevel >= WantedLevel.Medium || state.WitnessCount > 0;
				if (mediumRisk && TryFundAiLawyerRetainer(gang, peep, state, highRisk ? AI_SUPPORT_LAWYER_RETAINER_MEDIUM : AI_CREW_RELATIONS_MINOR_RETAINER, "crew-risk"))
				{
					actions++;
				}
				if (highRisk && TryQueueAiHideout(gang, peep, state, "crew-risk"))
				{
					actions++;
				}
			}
			return actions;
		}

		private static bool TryRunAiCrewSupportPackage(PlayerInfo gang, Entity peep, CrewModState state)
		{
			if (gang == null || peep == null || state == null)
			{
				return false;
			}
			bool acted = false;
			float loyaltyRatio = GetAiCrewLoyaltyRatio(peep, state, gang);
			if (state.HappinessValue < AI_SUPPORT_PEPTALK_HAPPINESS_THRESHOLD || loyaltyRatio < AI_SUPPORT_PEPTALK_LOYALTY_RATIO_THRESHOLD)
			{
				acted |= TryRunAiPepTalk(gang, peep, state);
			}

			loyaltyRatio = GetAiCrewLoyaltyRatio(peep, state, gang);
			if (state.HappinessValue < AI_SUPPORT_GIFT_HAPPINESS_THRESHOLD
				|| loyaltyRatio < AI_SUPPORT_GIFT_LOYALTY_RATIO_THRESHOLD
				|| state.TurnsUnhappy >= 2
				|| state.LowHappinessStreak >= 3)
			{
				acted |= TryRunAiGift(gang, peep, state);
			}

			loyaltyRatio = GetAiCrewLoyaltyRatio(peep, state, gang);
			if (state.HappinessValue < AI_SUPPORT_VACATION_HAPPINESS_THRESHOLD
				|| (state.TurnsUnhappy >= 4 && loyaltyRatio < AI_SUPPORT_VACATION_LOYALTY_RATIO_THRESHOLD)
				|| (state.LowHappinessStreak >= 6 && loyaltyRatio < AI_SUPPORT_GIFT_LOYALTY_RATIO_THRESHOLD))
			{
				acted |= TryQueueAiVacation(gang, peep, state);
			}
			return acted;
		}

		private static bool TrySpendGangSafehouseCash(PlayerInfo gang, int amount)
		{
			if (gang == null || amount <= 0)
			{
				return false;
			}
			if (GetGangCleanCash(gang) - amount < AI_SUPPORT_MIN_CLEAN_RESERVE)
			{
				return false;
			}
			try
			{
				gang.finances.DoChangeMoneyOnSafehouse(new Price((Fixnum)(-amount)), (MoneyReason)1);
				return true;
			}
			catch
			{
				return false;
			}
		}

		private static float GetAiCrewLoyaltyRatio(Entity peep, CrewModState state, PlayerInfo gang)
		{
			if (state == null)
			{
				return 1f;
			}
			EnsureLoyaltyInitialized(peep, state, gang);
			float loyaltyCap = Mathf.Max(0.001f, state.LoyaltyCap);
			return Mathf.Clamp01(state.LoyaltyValue / loyaltyCap);
		}

		private static float GetAiCrewSupportNeedScore(Entity peep, CrewModState state, PlayerInfo gang)
		{
			if (peep == null || state == null)
			{
				return float.MinValue;
			}
			float happinessNeed = 1f - Mathf.Clamp01(state.HappinessValue);
			float loyaltyNeed = 1f - GetAiCrewLoyaltyRatio(peep, state, gang);
			float unhappyPressure = Mathf.Clamp(state.TurnsUnhappy * 0.05f, 0f, 0.25f);
			return happinessNeed * 0.65f + loyaltyNeed * 0.35f + unhappyPressure;
		}

		private static (Entity peep, CrewModState state, float needScore) FindAiCrewSupportTarget(PlayerInfo gang)
		{
			Entity bestPeep = null;
			CrewModState bestState = null;
			float bestNeed = float.MinValue;
			if (gang?.crew == null)
			{
				return (null, null, bestNeed);
			}
			foreach (CrewAssignment living in gang.crew.GetLiving())
			{
				Entity peep = living.GetPeep();
				if (peep == null)
				{
					continue;
				}
				CrewModState state = GetOrCreateCrewState(peep.Id);
				if (state == null
					|| state.InJail
					|| JailSystem.IsInJail(peep.Id)
					|| state.OnHideout
					|| state.HideoutPending
					|| state.OnVacation
					|| state.VacationPending)
				{
					continue;
				}
				float need = GetAiCrewSupportNeedScore(peep, state, gang);
				if (need > bestNeed)
				{
					bestNeed = need;
					bestPeep = peep;
					bestState = state;
				}
			}
			return (bestPeep, bestState, bestNeed);
		}

		private static List<(Entity peep, CrewModState state, float needScore)> FindAiCrewSupportTargets(PlayerInfo gang, int maxTargets)
		{
			List<(Entity peep, CrewModState state, float needScore)> targets = new List<(Entity, CrewModState, float)>();
			if (gang?.crew == null || maxTargets <= 0)
			{
				return targets;
			}
			foreach (CrewAssignment living in gang.crew.GetLiving())
			{
				Entity peep = living.GetPeep();
				if (peep == null)
				{
					continue;
				}
				CrewModState state = GetOrCreateCrewState(peep.Id);
				if (IsAiCrewUnavailableForRelations(peep, state))
				{
					continue;
				}
				float need = GetAiCrewSupportNeedScore(peep, state, gang);
				if (state.IsUnderboss)
				{
					need += 0.08f;
				}
				if (state.SnitchExposed)
				{
					need += 0.12f;
				}
				if (state.FedsIncoming || state.WitnessCount > 0 || state.LocalHeatLevel >= WantedLevel.Medium)
				{
					need += 0.08f;
				}
				if (need > 0.2f)
				{
					targets.Add((peep, state, need));
				}
			}
			return targets
				.OrderByDescending(t => t.needScore)
				.Take(maxTargets)
				.ToList();
		}

		private static bool TryRunAiPepTalk(PlayerInfo gang, Entity peep, CrewModState state)
		{
			if (gang == null || peep == null || state == null)
			{
				return false;
			}
			float happinessBefore = state.HappinessValue;
			state.HappinessValue = Mathf.Clamp01(state.HappinessValue + 0.05f);
			state.TurnsUnhappy = Math.Max(0, state.TurnsUnhappy - 1);
			TryRecoverLoyaltyTowardCap(peep, state, gang, "peptalk", 0.005f, 0.1f);
			VerificationLog("AiCrewRelations", $"pep-talk gang={gang.PID.id} peep={peep.Id.id} happinessBefore={happinessBefore:0.000} happinessAfter={state.HappinessValue:0.000}");
			return true;
		}

		private static bool TryRunAiGift(PlayerInfo gang, Entity peep, CrewModState state)
		{
			if (gang == null || peep == null || state == null || !TrySpendGangSafehouseCash(gang, AI_SUPPORT_GIFT_COST))
			{
				return false;
			}
			float happinessBefore = state.HappinessValue;
			state.HappinessValue = Mathf.Clamp01(state.HappinessValue + 0.2f);
			state.TurnsUnhappy = 0;
			TryRecoverLoyaltyTowardCap(peep, state, gang, "gift", 0.01f, 0.2f);
			VerificationLog("AiCrewRelations", $"gift gang={gang.PID.id} peep={peep.Id.id} happinessBefore={happinessBefore:0.000} happinessAfter={state.HappinessValue:0.000}");
			return true;
		}

		private static bool TryQueueAiVacation(PlayerInfo gang, Entity peep, CrewModState state)
		{
			if (gang == null
				|| peep == null
				|| state == null
				|| state.OnVacation
				|| state.VacationPending
				|| state.OnHideout
				|| state.HideoutPending
				|| state.InJail
				|| JailSystem.IsInJail(peep.Id)
				|| !TrySpendGangSafehouseCash(gang, AI_SUPPORT_VACATION_COST))
			{
				return false;
			}
			state.VacationPending = true;
			state.VacationDuration = 7;
			state.HappinessValue = 1f;
			state.TurnsUnhappy = 0;
			VerificationLog("AiCrewRelations", $"vacation gang={gang.PID.id} peep={peep.Id.id} duration={state.VacationDuration}");
			return true;
		}

		private static bool TryQueueAiHideout(PlayerInfo gang, Entity peep, CrewModState state, string reason)
		{
			if (gang == null
				|| peep == null
				|| state == null
				|| state.OnHideout
				|| state.HideoutPending
				|| state.OnVacation
				|| state.VacationPending
				|| state.InJail
				|| JailSystem.IsInJail(peep.Id))
			{
				return false;
			}
			state.HideoutPending = true;
			state.HideoutDuration = Mathf.Max(1, HIDEOUT_DURATION_DAYS);
			state.FedsIncoming = false;
			state.FedArrivalCountdown = 0;
			VerificationLog("Hideout", $"ai-requested peep={peep.Id.id} gang={gang.PID.id} duration={state.HideoutDuration} reason={reason}");
			return true;
		}

		private static bool TryFundAiLawyerRetainer(PlayerInfo gang, Entity peep, CrewModState state, int desiredRetainer, string reason)
		{
			if (gang == null || peep == null || state == null)
			{
				return false;
			}
			if (state.LawyerRetainer >= desiredRetainer)
			{
				state.LawyerRetainerConfirmed = true;
				return false;
			}
			int payment = desiredRetainer - state.LawyerRetainer;
			if (payment <= 0 || !TrySpendGangSafehouseCash(gang, payment))
			{
				return false;
			}
			JailSystem.PayLawyerRetainer(peep.Id, payment);
			state.LawyerRetainerConfirmed = true;
			VerificationLog("AiCrewRelations", $"retainer gang={gang.PID.id} peep={peep.Id.id} amount={payment} total={state.LawyerRetainer} reason={reason}");
			return true;
		}

		/// <summary>Run relationship-driven internal crew events for an AI gang (family boost, tension/mediation). Called on same 28-day cadence as snitch meeting. AI-only.</summary>
		internal static void RunRelationshipDrivenInternalCrewEvents(PlayerInfo gang, SimTime now)
		{
			if (gang == null || gang.PID.IsHumanPlayer || gang.crew == null || gang.crew.IsCrewDefeated || gang.crew.LivingCrewCount <= 0)
			{
				return;
			}
			// Positive: family/spouse in same crew -> small happiness + loyalty boost for 1–2 pairs per run
			List<(Entity peepA, Entity peepB)> familyPairs = EnumerateInternalRelationshipPairs(gang).ToList();
			if (familyPairs.Count > 0)
			{
				int toTake = Math.Min(AI_INTERNAL_EVENT_MAX_FAMILY_PAIRS_PER_RUN, familyPairs.Count);
				for (int i = 0; i < toTake; i++)
				{
					int idx = familyPairs.Count > 1 ? SharedRng.Next(familyPairs.Count) : 0;
					var pair = familyPairs[idx];
					familyPairs.RemoveAt(idx);
					CrewModState stateA = GetOrCreateCrewState(pair.peepA.Id);
					CrewModState stateB = GetOrCreateCrewState(pair.peepB.Id);
					if (stateA != null)
					{
						stateA.HappinessValue = Mathf.Clamp01(stateA.HappinessValue + AI_INTERNAL_EVENT_FAMILY_HAPPINESS_GAIN);
						stateA.LoyaltyValue = Mathf.Clamp(stateA.LoyaltyValue + AI_INTERNAL_EVENT_FAMILY_LOYALTY_GAIN, 0f, stateA.LoyaltyCap);
					}
					if (stateB != null)
					{
						stateB.HappinessValue = Mathf.Clamp01(stateB.HappinessValue + AI_INTERNAL_EVENT_FAMILY_HAPPINESS_GAIN);
						stateB.LoyaltyValue = Mathf.Clamp(stateB.LoyaltyValue + AI_INTERNAL_EVENT_FAMILY_LOYALTY_GAIN, 0f, stateB.LoyaltyCap);
					}
					Debug.Log($"[GameplayTweaks] AI internal event: family-in-crew boost gang={gang.PID.id} pair={pair.peepA.Id.id},{pair.peepB.Id.id}");
				}
			}
			// Tension: two crew with low happiness/loyalty -> mediation (spend cash, boost both) or penalty
			List<(Entity peep, CrewModState state)> tenseCrew = new List<(Entity, CrewModState)>();
			foreach (CrewAssignment assignment in gang.crew.GetLiving())
			{
				Entity p = assignment.GetPeep();
				if (p == null) continue;
				CrewModState s = GetOrCreateCrewState(p.Id);
				if (s == null) continue;
				if (s.HappinessValue < AI_INTERNAL_EVENT_TENSION_HAPPINESS_THRESHOLD || s.LoyaltyValue < AI_INTERNAL_EVENT_TENSION_LOYALTY_THRESHOLD)
				{
					tenseCrew.Add((p, s));
				}
			}
			if (tenseCrew.Count >= 2)
			{
				int aIdx = SharedRng.Next(tenseCrew.Count);
				int bIdx = SharedRng.Next(tenseCrew.Count);
				while (bIdx == aIdx && tenseCrew.Count > 1)
				{
					bIdx = SharedRng.Next(tenseCrew.Count);
				}
				var (peepA, stateA) = tenseCrew[aIdx];
				var (peepB, stateB) = tenseCrew[bIdx];
				int clean = GetGangCleanCash(gang);
				if (clean >= AI_INTERNAL_EVENT_MIN_CASH_FOR_MEDIATION)
				{
					int spend = Math.Min(AI_INTERNAL_EVENT_TENSION_MEDIATION_CASH, clean);
					try
					{
						gang.finances.DoChangeMoneyOnSafehouse(new Price((Fixnum)(-spend)), (MoneyReason)1);
						if (stateA != null)
						{
							stateA.HappinessValue = Mathf.Clamp01(stateA.HappinessValue + AI_INTERNAL_EVENT_TENSION_MEDIATION_HAPPINESS_GAIN);
						}
						if (stateB != null)
						{
							stateB.HappinessValue = Mathf.Clamp01(stateB.HappinessValue + AI_INTERNAL_EVENT_TENSION_MEDIATION_HAPPINESS_GAIN);
						}
						Debug.Log($"[GameplayTweaks] AI internal event: tension mediation gang={gang.PID.id} spent={spend}");
					}
					catch (Exception ex)
					{
						Debug.LogWarning("[GameplayTweaks] AI internal event mediation spend failed: " + ex.Message);
					}
				}
				else
				{
					// Apply small penalty to one member
					CrewModState victim = stateA;
					if (victim != null)
					{
						victim.HappinessValue = Mathf.Clamp01(victim.HappinessValue - AI_INTERNAL_EVENT_TENSION_HAPPINESS_PENALTY);
						Debug.Log($"[GameplayTweaks] AI internal event: tension penalty gang={gang.PID.id} peep={peepA.Id.id}");
					}
				}
			}
		}

		private static PlayerInfo ChooseDefectionTarget(PlayerInfo currentOwner, Entity peep, List<PlayerInfo> candidates)
		{
			if (currentOwner == null || candidates == null || candidates.Count == 0)
			{
				return null;
			}
			PlayerInfo human = G.GetHumanPlayer();
			PlayerInfo playerCandidate = candidates.FirstOrDefault(g => human != null && g.PID.id == human.PID.id);
			List<PlayerInfo> nonOwner = candidates.Where(g => g != null && g.PID.id != currentOwner.PID.id).ToList();
			if (nonOwner.Count == 0)
			{
				return null;
			}
			if (playerCandidate != null && !currentOwner.PID.IsHumanPlayer)
			{
				if (SharedRng.NextDouble() < 0.5)
				{
					return playerCandidate;
				}
			}
			return nonOwner[SharedRng.Next(nonOwner.Count)];
		}

		private static void ProcessHumanHideoutPrepass(PlayerInfo humanPlayer, SimTime now)
		{
			if (humanPlayer?.crew == null)
			{
				return;
			}

			foreach (CrewAssignment assignment in humanPlayer.crew.GetLiving().ToList())
			{
				Entity peep = assignment.GetPeep();
				if (peep == null)
				{
					continue;
				}

				CrewModState state = GetCrewStateOrNull(peep.Id);
				if (state == null || !state.HideoutPending || state.OnHideout)
				{
					continue;
				}

				VerificationLog("Hideout", $"hideout-prepass-started peep={peep.Id.id} day={now.days}");
				if (TryStartHideoutTransition(humanPlayer, peep, state, now, "hideout-prepass"))
				{
					VerificationLog("Hideout", $"hideout-prepass-complete peep={peep.Id.id} returnDay={state.HideoutReturns.days}");
				}
				else
				{
					VerificationLog("Hideout", $"hideout-prepass-failed peep={peep.Id.id} onHideout={state.OnHideout} pending={state.HideoutPending}");
				}
			}
		}

		private static bool TryStartHideoutTransition(PlayerInfo player, Entity peep, CrewModState state, SimTime now, string sourceTag)
		{
			if (player?.crew == null || peep == null || state == null || state.OnHideout || !state.HideoutPending)
			{
				return false;
			}
			if (IsCrewCurrentlyJailed(peep.Id))
			{
				ClearJailBlockedAwayStates(peep, state, sourceTag + "-jailed");
				return false;
			}

			try
			{
				CrewAssignment crewForPeep = player.crew.GetCrewForPeep(peep.Id);
				EntityID priorVehicleId = crewForPeep.IsValid && crewForPeep.IsInVehicle ? crewForPeep.VehicleID : EntityID.INVALID;
				if (!crewForPeep.IsValid || !player.crew.IsOnBoard(peep.Id))
				{
					return false;
				}
				if (priorVehicleId.IsValid)
				{
					MultiCrewVehicleHelper.ClearQueuedHumanVehicleTravelForPeep(player, peep.Id, "hideout-start");
					VerificationLog("Hideout", $"vehicle-detach-start peep={peep.Id.id} vehicle={priorVehicleId.id} source={sourceTag}");
				}
				player.crew.RemoveCrewFromBoard(crewForPeep, (PlayerCrewData.OffBoardReason)3, false, state.HideoutDuration);
				state.OnHideout = true;
				state.HideoutPending = false;
				state.HideoutReturns = now.IncrementDays(state.HideoutDuration);
				state.HideoutLastUpkeepDay = now.days;
				state.HideoutMissedPayments = 0;
				state.HideoutForcedReturn = false;
				state.FedsIncoming = false;
				state.FedArrivalCountdown = 0;
				if (priorVehicleId.IsValid)
				{
					MultiCrewVehicleHelper.HandleCrewDepartureFromGang(player, peep.Id, priorVehicleId, "hideout-start");
					VerificationLog("Hideout", $"vehicle-detach-finish peep={peep.Id.id} vehicle={priorVehicleId.id} source={sourceTag}");
				}
				VerificationLog("NationalHeat", $"Hideout started peep={peep.Id.id} returnDay={state.HideoutReturns.days} source={sourceTag}");
				return true;
			}
			catch (Exception arg)
			{
				Debug.LogError($"[GameplayTweaks] Hideout departure failed: {arg}");
				VerificationLog("Hideout", $"hideout-prepass-failed peep={peep.Id.id} ex={arg.GetType().Name} source={sourceTag}");
				return false;
			}
		}

		private static HashSet<long> BuildActiveImportantWitnessCrewIdSet(out int activeEntryCount)
		{
			activeEntryCount = 0;
			HashSet<long> result = new HashSet<long>();
			try
			{
				List<ImportantWitnessEntry> entries = SaveData.NationalHeat?.WitnessEntries;
				if (entries == null || entries.Count == 0)
				{
					return result;
				}

				for (int i = 0; i < entries.Count; i++)
				{
					ImportantWitnessEntry entry = entries[i];
					if (entry == null || entry.ArrestProcessed)
					{
						continue;
					}

					activeEntryCount++;
					if (entry.CrewPeepId >= 0)
					{
						result.Add(entry.CrewPeepId);
					}
				}
			}
			catch
			{
			}

			return result;
		}

		private static void ProcessCrewMemberTurn(Entity peep, SimTime now, PlayerInfo player, HashSet<long> activeImportantWitnessCrewIds = null, bool? hasAnyActiveImportantWitness = null)
		{

			if (!EnableCrewStats.Value)
			{
				return;
			}
			long crewTurnStartTicks = player?.PID.IsHumanPlayer == true ? Stopwatch.GetTimestamp() : 0L;
			CrewModState orCreateCrewState = GetOrCreateCrewState(peep.Id);
			if (orCreateCrewState == null)
			{
				return;
			}
			if (GameplayTweaksPlugin.TryGetSafeFederalArrestPendingInfo(peep.Id, out GameplayTweaksPlugin.SafeFederalArrestPendingInfo pendingInfo))
			{
				GameplayTweaksPlugin.VerificationLog("Jail", $"safe-federal-turn-skip peep={peep.Id.id} source=process-crew-turn vehicle={pendingInfo?.VehicleId.id ?? 0} trialDay={pendingInfo?.TrialDate.days ?? -1}");
				return;
			}
			ClearJailBlockedAwayStates(peep, orCreateCrewState, "turn-update");
			SyncLocalHeatFromLegacyFields(orCreateCrewState);
			EnsureLoyaltyInitialized(peep, orCreateCrewState, player);
			if (orCreateCrewState.SnitchDisappearPending)
			{
				if (now.days >= orCreateCrewState.SnitchDisappearDueDay)
				{
					if (TryExecuteSnitchDisappear(player, peep, orCreateCrewState, now, logGrapevine: true))
					{
						return;
					}
				}
				else if (orCreateCrewState.SnitchDisappearDueDay < 0)
				{
					orCreateCrewState.SnitchDisappearDueDay = now.days + 1;
				}
			}
			if (orCreateCrewState.HideoutPending && !orCreateCrewState.OnHideout)
			{
				TryStartHideoutTransition(player, peep, orCreateCrewState, now, "turn-update");
			}
			if (orCreateCrewState.OnHideout && now >= orCreateCrewState.HideoutReturns)
			{
				orCreateCrewState.OnHideout = false;
				orCreateCrewState.HideoutPending = false;
				try
				{
					CrewAssignment crewForPeep2 = player.crew.GetCrewForPeep(peep.Id);
					if (crewForPeep2.IsValid && !player.crew.IsOnBoard(peep.Id))
					{
						if (_returnCrewMethod == null)
						{
							_returnCrewMethod = typeof(PlayerCrew).GetMethod("ReturnCrewToBoard", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
							_addCrewMethod = typeof(PlayerCrew).GetMethod("AddCrewToBoard", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
						}
						if (_returnCrewMethod != null)
						{
							_returnCrewMethod.Invoke(player.crew, new object[1] { crewForPeep2 });
						}
						else if (_addCrewMethod != null)
						{
							_addCrewMethod.Invoke(player.crew, new object[1] { crewForPeep2 });
						}
					}
				}
				catch (Exception arg2)
				{
					Debug.LogError($"[GameplayTweaks] Hideout return failed for {peep.data.person.FullName}: {arg2}");
				}
				float hideoutReturnDetectionChance = ComputeHideoutReturnDetectionChance(orCreateCrewState);
				bool copsStillSearching = SharedRng.NextDouble() < (double)hideoutReturnDetectionChance;
				if (copsStillSearching)
				{
					ForceHardMaxLocalHeatAndFeds(peep.Id, 3, FED_SEARCH_TURNS);
					VerificationLog("Hideout", $"return chance={hideoutReturnDetectionChance:0.00} noticed=true misses={orCreateCrewState.HideoutMissedPayments} forced={orCreateCrewState.HideoutForcedReturn} peep={peep.Id.id} fedTurns={FED_SEARCH_TURNS}");
				}
				else
				{
					VerificationLog("Hideout", $"return chance={hideoutReturnDetectionChance:0.00} noticed=false misses={orCreateCrewState.HideoutMissedPayments} forced={orCreateCrewState.HideoutForcedReturn} peep={peep.Id.id}");
				}
				orCreateCrewState.HideoutLastUpkeepDay = -1;
				orCreateCrewState.HideoutMissedPayments = 0;
				orCreateCrewState.HideoutForcedReturn = false;
			}
			if (orCreateCrewState.OnHideout)
			{
				ProcessHideoutUpkeep(peep, orCreateCrewState, player, now);
				ApplyLocalHeatDecay(orCreateCrewState, now.days);
				orCreateCrewState.FedsIncoming = false;
				orCreateCrewState.FedArrivalCountdown = 0;
				SyncLegacyWantedFields(orCreateCrewState);
				return;
			}
			bool nationalHeatActiveForCrew = SaveData.NationalHeat != null && SaveData.NationalHeat.Active && (hasAnyActiveImportantWitness ?? CountActiveImportantWitnessEntries() > 0);
			if (nationalHeatActiveForCrew && !JailSystem.IsInJail(peep.Id))
			{
				RaiseLocalHeatFloor(peep.Id, LOCAL_HEAT_LOW_FLOOR, refreshDecayAnchor: true);
			}
			bool hasActiveImportantWitness = activeImportantWitnessCrewIds != null
				? activeImportantWitnessCrewIds.Contains((long)peep.Id.id)
				: HasActiveImportantWitnessForCrew(peep.Id);
			if (hasActiveImportantWitness && !JailSystem.IsInJail(peep.Id))
			{
				SetLocalHeatProgress(orCreateCrewState, 1f, now.days, refreshDecayAnchor: true);
				orCreateCrewState.HasWitness = true;
				orCreateCrewState.WitnessCount = Mathf.Max(orCreateCrewState.WitnessCount, 3);
				if (!orCreateCrewState.FedsIncoming)
				{
					orCreateCrewState.FedArrivalCountdown = FED_SEARCH_TURNS;
				}
				else if (orCreateCrewState.FedArrivalCountdown <= 0)
				{
					orCreateCrewState.FedArrivalCountdown = FED_SEARCH_TURNS;
				}
				orCreateCrewState.FedsIncoming = true;
			}
			else
			{
				ApplyLocalHeatDecay(orCreateCrewState, now.days);
			}
			ProcessCrewFamilyTurn(peep, orCreateCrewState, player, now);
			TryProcessRecurringOddJob(peep, orCreateCrewState, player, now, "turn-update");
			GameplayTweaksPlugin.ReconcileObservedBoozeStreetCredForCrew(player, peep, orCreateCrewState, now);
			ResolveStreetCreditProgressLevelUps(orCreateCrewState, player, peep.Id, "turn-update");
			if (!player.PID.IsHumanPlayer)
			{
				TryApplyAiCrewLevelups(player, peep, now, "turn-update");
			}
			if (orCreateCrewState.StreetCreditLevel < 2)
			{
				float beforePassiveHappiness = orCreateCrewState.HappinessValue;
				float loyaltyMul = GetLoyaltyHappinessPenaltyMultiplier(orCreateCrewState);
				float passiveDecay = (player.PID.IsHumanPlayer ? CREW_PASSIVE_HAPPINESS_DECAY_HUMAN_LOW_CREDIT : CREW_PASSIVE_HAPPINESS_DECAY_AI_LOW_CREDIT) * loyaltyMul;
				orCreateCrewState.HappinessValue = Mathf.Clamp01(orCreateCrewState.HappinessValue - passiveDecay);
				if (beforePassiveHappiness - orCreateCrewState.HappinessValue >= 0.004f)
				{
					VerificationLog("CrewHappiness", $"passive-decay player={player.PID.id} human={player.PID.IsHumanPlayer} peep={peep.Id.id} streetCredit={orCreateCrewState.StreetCreditLevel} before={beforePassiveHappiness:0.000} after={orCreateCrewState.HappinessValue:0.000} decay={passiveDecay:0.000} loyaltyMul={loyaltyMul:0.000}");
				}
			}
			if (orCreateCrewState.HappinessValue <= 0.25f)
			{
				orCreateCrewState.TurnsUnhappy++;
			}
			else
			{
				orCreateCrewState.TurnsUnhappy = Math.Max(0, orCreateCrewState.TurnsUnhappy - 1);
			}
			ApplyLoyaltyTurnUpdate(peep, orCreateCrewState, player, now);
			if (!IsHumanBoss(peep, player) && orCreateCrewState.LoyaltyValue <= 0f)
			{
				if (IsCrewCurrentlyJailed(peep.Id))
				{
					VerificationLog("Loyalty", $"Zero-loyalty defection skipped peep={peep.Id.id} reason=jailed");
				}
				else
				{
				EnsureVerifyStats().ZeroLoyaltyDefections++;
				try
				{
					if (_removeCrewCompletelyMethod == null)
					{
						_removeCrewCompletelyMethod = typeof(PlayerCrew).GetMethod("RemoveFromCrewCompletely", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
						_addToCrewMethod = typeof(PlayerCrew).GetMethod("AddToCrew", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
					}
					List<PlayerInfo> candidates = TrackedGangs
						.Where(g => g != null && g.crew != null && !g.crew.IsCrewDefeated && g.crew.LivingCrewCount > 0 && g.PID.id != player.PID.id)
						.ToList();
					PlayerInfo target = ChooseDefectionTarget(player, peep, candidates);
					bool humanCandidate = candidates.Any(g => g?.PID.IsHumanPlayer == true);
					VerificationLog("Loyalty", $"zero-loyalty-target peep={peep.Id.id} fromGang={player.PID.id} fromHuman={player.PID.IsHumanPlayer} candidates={candidates.Count} humanCandidate={humanCandidate} targetGang={target?.PID.id ?? -1} targetHuman={target?.PID.IsHumanPlayer == true} loyalty={orCreateCrewState.LoyaltyValue:0.000} happiness={orCreateCrewState.HappinessValue:0.000} unhappyStreak={orCreateCrewState.LowHappinessStreak}");
					string fullName = peep.data.person.FullName;
					CrewAssignment priorAssignment = player.crew.GetCrewForPeep(peep.Id);
					EntityID priorVehicleId = priorAssignment.IsValid && priorAssignment.IsInVehicle ? priorAssignment.VehicleID : EntityID.INVALID;
					if (target != null && target.PID.IsHumanPlayer)
					{
						CrewRelationshipHandlerPatch.ShowDefectionOffer(peep, player);
					}
					else if (target != null && _removeCrewCompletelyMethod != null)
					{
						PlayerSocial social = target.social;
						string targetName = (social != null ? social.PlayerGroupName : null) ?? "unknown";
						_removeCrewCompletelyMethod.Invoke(player.crew, new object[1] { peep });
						MultiCrewVehicleHelper.HandleCrewDepartureFromGang(player, peep.Id, priorVehicleId, "loyalty-defect");
						if (_addToCrewMethod != null)
						{
							_addToCrewMethod.Invoke(target.crew, new object[2] { peep, null });
							if (!target.PID.IsHumanPlayer)
							{
								MultiCrewVehicleHelper.TryNormalizeAiCrewAfterHire(target, peep.Id, target.territory?.GetHeadquartersNode(), allowVehicleCreateFallback: false, preserveCurrentVehicle: false, source: "loyalty-defect");
							}
							if (player.PID.IsHumanPlayer)
							{
								CrewRelationshipHandlerPatch.ShowCrewDepartureAlert(peep, target);
								Debug.Log($"[GameplayTweaks] {fullName} defected to {targetName} - loyalty hit zero!");
							}
							VerificationLog("Loyalty", $"Zero-loyalty defection peep={peep.Id.id} fromGang={player.PID.id} toGang={target.PID.id}");
						}
						else
						{
							if (player.PID.IsHumanPlayer)
							{
								CrewRelationshipHandlerPatch.ShowCrewDepartureAlert(peep, null);
								Debug.Log($"[GameplayTweaks] {fullName} left the crew (couldn't join another gang)");
							}
							VerificationLog("Loyalty", $"Zero-loyalty exit peep={peep.Id.id} fromGang={player.PID.id} without destination");
						}
					}
					else
					{
						if (_removeCrewCompletelyMethod != null)
						{
							_removeCrewCompletelyMethod.Invoke(player.crew, new object[1] { peep });
							MultiCrewVehicleHelper.HandleCrewDepartureFromGang(player, peep.Id, priorVehicleId, "loyalty-exit");
						}
						if (player.PID.IsHumanPlayer)
						{
							CrewRelationshipHandlerPatch.ShowCrewDepartureAlert(peep, null);
							Debug.Log($"[GameplayTweaks] {fullName} left the crew (no valid defection target)");
						}
						VerificationLog("Loyalty", $"Zero-loyalty exit peep={peep.Id.id} fromGang={player.PID.id} no-target");
					}
				}
				catch (Exception arg)
				{
					Debug.LogError($"[GameplayTweaks] Crew defection failed: {arg}");
				}
				}
			}
			if (orCreateCrewState.VacationPending && !orCreateCrewState.OnVacation)
			{
				if (IsCrewCurrentlyJailed(peep.Id))
				{
					ClearJailBlockedAwayStates(peep, orCreateCrewState, "vacation-pending-jailed");
				}
				else
				{
					try
					{
						CrewAssignment crewForPeep = player.crew.GetCrewForPeep(peep.Id);
						if (crewForPeep.IsValid && player.crew.IsOnBoard(peep.Id))
						{
							player.crew.RemoveCrewFromBoard(crewForPeep, (PlayerCrewData.OffBoardReason)3, false, orCreateCrewState.VacationDuration);
							orCreateCrewState.OnVacation = true;
							orCreateCrewState.VacationPending = false;
							orCreateCrewState.VacationReturns = now.IncrementDays(orCreateCrewState.VacationDuration);
							Debug.Log($"[GameplayTweaks] {peep.data.person.FullName} left for vacation, returns in {orCreateCrewState.VacationDuration} days");
						}
					}
					catch (Exception arg2)
					{
						Debug.LogError($"[GameplayTweaks] Vacation departure failed: {arg2}");
					}
				}
			}
			if (orCreateCrewState.OnVacation && now >= orCreateCrewState.VacationReturns)
			{
				orCreateCrewState.OnVacation = false;
				orCreateCrewState.VacationPending = false;
				TryRecoverLoyaltyTowardCap(peep, orCreateCrewState, player, "vacation", 0.03f, 0.5f);
				try
				{
					CrewAssignment crewForPeep2 = player.crew.GetCrewForPeep(peep.Id);
					if (crewForPeep2.IsValid && !player.crew.IsOnBoard(peep.Id))
					{
						if (_returnCrewMethod == null)
						{
							_returnCrewMethod = typeof(PlayerCrew).GetMethod("ReturnCrewToBoard", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
							_addCrewMethod = typeof(PlayerCrew).GetMethod("AddCrewToBoard", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
						}
						if (_returnCrewMethod != null)
						{
							_returnCrewMethod.Invoke(player.crew, new object[1] { crewForPeep2 });
							Debug.Log(("[GameplayTweaks] " + peep.data.person.FullName + " returned from vacation"));
						}
						else if (_addCrewMethod != null)
						{
							_addCrewMethod.Invoke(player.crew, new object[1] { crewForPeep2 });
							Debug.Log(("[GameplayTweaks] " + peep.data.person.FullName + " returned from vacation (fallback)"));
						}
					}
				}
				catch (Exception arg3)
				{
					Debug.LogError($"[GameplayTweaks] Vacation return failed for {peep.data.person.FullName}: {arg3}");
				}
			}
			if (player.PID.IsHumanPlayer)
			{
				CrewRelationshipHandlerPatch.ExpireHumanPoliticalBribeIfNeeded(now, logExpiration: true);
			}
			ApplyRetainerUpkeep(peep, orCreateCrewState, now);
			orCreateCrewState.LocalHeatLevel = HeatLevelFromProgress(orCreateCrewState.LocalHeatProgress);
			SyncLegacyWantedFields(orCreateCrewState);
			if (orCreateCrewState.WitnessThreatenedSuccessfully && orCreateCrewState.WitnessCount <= orCreateCrewState.FederalWitnessCount)
			{
				SetLocalHeatProgress(orCreateCrewState, Mathf.Max(0f, orCreateCrewState.LocalHeatProgress - 0.05f), now.days, refreshDecayAnchor: false);
				if (orCreateCrewState.LocalHeatProgress < 0.25f)
				{
					orCreateCrewState.LocalHeatLevel = WantedLevel.None;
					orCreateCrewState.WitnessThreatenedSuccessfully = false;
					orCreateCrewState.WitnessThreatAttempted = false;
					orCreateCrewState.WitnessCount = Mathf.Max(0, orCreateCrewState.FederalWitnessCount);
					SyncLegacyWantedFields(orCreateCrewState);
				}
			}
			bool globalMayorBribeActive = player.PID.IsHumanPlayer ? CrewRelationshipHandlerPatch.IsHumanPoliticalBribeActive() : GetAiGangMayorBribeActive(player.PID.id, now);
			bool nationalHeatActive = SaveData.NationalHeat != null && SaveData.NationalHeat.Active;
			if (!hasActiveImportantWitness && !nationalHeatActive && orCreateCrewState.FederalWitnessCount <= 0 && orCreateCrewState.LocalHeatLevel != WantedLevel.None)
			{
				float num = 0f;
				float num2 = (orCreateCrewState.WitnessThreatenedSuccessfully ? 0.1f : 0f);
				if (globalMayorBribeActive)
				{
					num += 0.15f + num2;
				}
				if (orCreateCrewState.JudgeBribeActive)
				{
					num += 0.25f + num2;
				}
				if (orCreateCrewState.LawyerRetainer >= 3000)
				{
					num += 0.3f;
				}
				else if (orCreateCrewState.LawyerRetainer >= 2000)
				{
					num += 0.2f;
				}
				else if (orCreateCrewState.LawyerRetainer >= 1000)
				{
					num += 0.1f;
				}
				if (num > 0f && SharedRng.NextDouble() < (double)num)
				{
					orCreateCrewState.CaseDismissed = true;
					SetLocalHeatProgress(orCreateCrewState, 0f, now.days, refreshDecayAnchor: true);
					orCreateCrewState.HasWitness = false;
					orCreateCrewState.WitnessCount = 0;
					orCreateCrewState.FederalWitnessCount = 0;
					orCreateCrewState.FedsIncoming = false;
					orCreateCrewState.FedArrivalCountdown = 0;
					orCreateCrewState.LawyerRetainer = 0;
					orCreateCrewState.JudgeBribeActive = false;
					orCreateCrewState.WitnessThreatAttempted = false;
					orCreateCrewState.WitnessThreatenedSuccessfully = false;
					Debug.Log(("[GameplayTweaks] Case dismissed for " + peep.data.person.FullName + "!"));
				}
			}
			if (orCreateCrewState.LocalHeatLevel == WantedLevel.High && !orCreateCrewState.FedsIncoming && !globalMayorBribeActive && !orCreateCrewState.JudgeBribeActive)
			{
				orCreateCrewState.FedsIncoming = true;
				orCreateCrewState.FedArrivalCountdown = FED_SEARCH_TURNS;
				Debug.Log($"[GameplayTweaks] Feds tracking {peep.data.person.FullName}, arrival in {orCreateCrewState.FedArrivalCountdown} days");
				VerificationLog(
					"Jail",
					$"federal-countdown-started peep={peep.Id.id} reason=local-heat-high localHeat={orCreateCrewState.LocalHeatLevel} progress={orCreateCrewState.LocalHeatProgress:F3} countdown={orCreateCrewState.FedArrivalCountdown} judgeBribe={orCreateCrewState.JudgeBribeActive} politicalBribe={globalMayorBribeActive}");
			}
			if (orCreateCrewState.FedsIncoming && !globalMayorBribeActive && !orCreateCrewState.JudgeBribeActive)
			{
				orCreateCrewState.FedArrivalCountdown--;
				if (orCreateCrewState.FedArrivalCountdown <= 0)
				{
					try
					{
						if (player.crew.IsOnBoard(peep.Id))
						{
							if (JailSystem.ArrestCrew(player, peep))
							{
								orCreateCrewState.FedsIncoming = false;
								SetLocalHeatProgress(orCreateCrewState, 0f, now.days, refreshDecayAnchor: true);
								orCreateCrewState.WitnessCount = 0;
								orCreateCrewState.HasWitness = false;
								orCreateCrewState.FederalWitnessCount = 0;
								Debug.Log(("[GameplayTweaks] " + peep.data.person.FullName + " arrested by feds (game system)!"));
							}
							else
							{
								CrewAssignment crewForPeep3 = player.crew.GetCrewForPeep(peep.Id);
								if (crewForPeep3.IsValid)
								{
									player.crew.RemoveCrewFromBoard(crewForPeep3, (PlayerCrewData.OffBoardReason)0, false, 7);
									orCreateCrewState.FedsIncoming = false;
									SetLocalHeatProgress(orCreateCrewState, 0f, now.days, refreshDecayAnchor: true);
									orCreateCrewState.WitnessCount = 0;
									orCreateCrewState.HasWitness = false;
									orCreateCrewState.FederalWitnessCount = 0;
									Debug.Log(("[GameplayTweaks] " + peep.data.person.FullName + " arrested by feds (fallback)!"));
								}
							}
						}
					}
					catch (Exception arg4)
					{
						Debug.LogError($"[GameplayTweaks] Fed arrest failed: {arg4}");
					}
				}
			}
			if (orCreateCrewState.MayorBribeActive || orCreateCrewState.JudgeBribeActive)
			{
				_ = orCreateCrewState.FedsIncoming;
			}
			if (orCreateCrewState.LocalHeatLevel == WantedLevel.None && orCreateCrewState.FedsIncoming)
			{
				orCreateCrewState.FedsIncoming = false;
				orCreateCrewState.FedArrivalCountdown = 0;
			}
			SyncLegacyWantedFields(orCreateCrewState);
			try
			{
				if (ShouldDeferNaturalCauseDeathsToExternalMod())
				{
					return;
				}
				SimTimeSpan age = peep.data.person.GetAge(now);
				float yearsFloat = age.YearsFloat;
				CrewAssignment crewForIndex2 = player.crew.GetCrewForIndex(0);
				if (crewForIndex2.IsValid && crewForIndex2.peepId == peep.Id)
				{
					return;
				}
				float num3 = GetNaturalCauseDeathChance(yearsFloat);
				if (num3 <= 0f)
				{
					return;
				}
				if (SharedRng.NextDouble() < (double)num3)
				{
					string fullName2 = peep.data.person.FullName;
					int num4 = (int)yearsFloat;
					MethodInfo method = ((object)player.crew).GetType().GetMethod("ProcessCrewMemberDeath", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
					if (method != null)
					{
						method.Invoke(player.crew, new object[1] { peep });
						VerificationLog("NaturalDeath", $"peep={peep.Id.id} age={yearsFloat:0.0} chance={num3:0.000000} authority=gameplaytweaks");
						VerificationLog("DeathAttribution", $"source=natural-gameplaytweaks peep={peep.Id.id} age={yearsFloat:0.0}");
						RecordDeathSource(peep.Id, "natural-gameplaytweaks");
					}
					LogGrapevine($"DEATH: {fullName2} passed away at age {num4} (natural causes)");
				}
			}
			catch
			{
			}
			if (crewTurnStartTicks != 0L)
			{
				LogSlowCrewMemberTurn(peep, orCreateCrewState, now, crewTurnStartTicks);
			}
		}

		private static void LogSlowCrewMemberTurn(Entity peep, CrewModState state, SimTime now, long startTicks)
		{
			try
			{
				long elapsedMs = GetElapsedMilliseconds(startTicks);
				if (elapsedMs < 12L)
				{
					return;
				}

				Debug.Log("[PERF][HumanTurnPhase] phase=crew-member-detail ms=" + elapsedMs
					+ " peep=" + (peep != null && !peep.Id.IsNotValid ? peep.Id.id.ToString(CultureInfo.InvariantCulture) : "-1")
					+ " onHideout=" + (state != null && state.OnHideout)
					+ " vacation=" + (state != null && (state.OnVacation || state.VacationPending))
					+ " fedsIncoming=" + (state != null && state.FedsIncoming)
					+ " localHeat=" + (state != null ? state.LocalHeatLevel.ToString() : "none")
					+ " witness=" + (state != null && (state.HasWitness || state.FederalWitnessCount > 0))
					+ " loyalty=" + (state != null ? state.LoyaltyValue.ToString("0.000", CultureInfo.InvariantCulture) : "n/a")
					+ " day=" + now.days
					+ " year=" + now.YearsInt
					+ " turn=" + (Game.Game.ctx?.clock?.CurrentTurn ?? -1)
					+ " pid=" + (Game.Game.ctx?.clock?.CurrentPlayer.id ?? -1)
					+ " totalPlayers=" + (Game.Game.ctx?.players?.all?.Count ?? -1));
			}
			catch
			{
			}
		}

		internal static ulong GetBossPeepId(PlayerInfo player)
		{
			if (player == null || player.crew == null || player.crew.IsCrewDefeated)
			{
				return 0uL;
			}
			if (player.social != null && !player.social.PlayerPeepId.IsNotValid)
			{
				return player.social.PlayerPeepId.id;
			}
			try
			{
				CrewAssignment boss = player.crew.GetCrewForIndex(0);
				if (boss.IsValid && !boss.peepId.IsNotValid)
				{
					return boss.peepId.id;
				}
			}
			catch
			{
			}
			return 0uL;
		}

		private static List<PlayerInfo> FindLikelyAttackers(PlayerInfo victim, int victimGangId, List<PlayerInfo> allPlayers)
		{
			HashSet<PlayerID> attackers = new HashSet<PlayerID>();
			int nowDay = G.GetNow().days;
			foreach (GangOpsChannel channel in new[] { GangOpsChannel.Pact, GangOpsChannel.Independent })
			{
				foreach (WarHeatEntry entry in GetWarHeatStore(channel).Values)
				{
					if (entry == null || entry.Heat <= 0f)
					{
						continue;
					}
					if (entry.LastUpdatedDay >= 0 && nowDay >= 0 && nowDay - entry.LastUpdatedDay > 35)
					{
						continue;
					}
					int candidatePid = -1;
					if (entry.AttackerPid == victimGangId)
					{
						candidatePid = entry.DefenderPid;
					}
					else if (entry.DefenderPid == victimGangId)
					{
						candidatePid = entry.AttackerPid;
					}
					if (candidatePid >= 0 && candidatePid != victimGangId)
					{
						PlayerInfo candidate = allPlayers.FirstOrDefault(p => p != null && p.PID.id == candidatePid);
						if (candidate != null && candidate.crew != null && !candidate.crew.IsCrewDefeated)
						{
							attackers.Add(candidate.PID);
						}
					}
				}
			}
			if (attackers.Count > 0)
			{
				VerificationLog("PactRetaliation", $"Likely attackers resolved from recent WarHeat victim={victimGangId} attackers={string.Join(",", attackers.Select(pid => pid.id.ToString(CultureInfo.InvariantCulture)).ToArray())}");
				return attackers.Select(pid => allPlayers.FirstOrDefault(p => p.PID.id == pid.id)).Where(p => p != null).Distinct().ToList();
			}
			foreach (PlayerInfo possibleAttacker in allPlayers)
			{
				if (possibleAttacker == null || possibleAttacker.crew.IsCrewDefeated || possibleAttacker.PID.id == victimGangId)
				{
					continue;
				}
				try
				{
					object attackerCombat = possibleAttacker.ai?.combat;
					if (attackerCombat == null || victim == null)
					{
						continue;
					}
					MethodInfo isAggro = attackerCombat.GetType().GetMethod("IsAggroAnyType", BindingFlags.Instance | BindingFlags.Public);
					if (isAggro != null && (bool)isAggro.Invoke(attackerCombat, new object[] { victim.PID }))
					{
						attackers.Add(possibleAttacker.PID);
					}
				}
				catch
				{
				}
			}
			if (attackers.Count > 0)
			{
				VerificationLog("PactRetaliation", $"Likely attackers resolved from active aggro fallback victim={victimGangId} attackers={string.Join(",", attackers.Select(pid => pid.id.ToString(CultureInfo.InvariantCulture)).ToArray())}");
			}
			return attackers.Select(pid => allPlayers.FirstOrDefault(p => p.PID.id == pid.id)).Where(p => p != null).Distinct().ToList();
		}

		private static void TriggerPactRetaliation(AlliancePact pact, List<int> allPactMembers, int victimGangId, string victimName, bool leaderWasKilled, List<PlayerInfo> allPlayers)
		{
			PlayerInfo victim = allPlayers.FirstOrDefault(p => p.PID.id == victimGangId);
			List<PlayerInfo> attackerPlayers = FindLikelyAttackers(victim, victimGangId, allPlayers);
			attackerPlayers = attackerPlayers.Where((PlayerInfo attacker) =>
			{
				if (attacker == null)
				{
					return false;
				}
				PlayerInfo playerById = G.FindPlayerById(victimGangId);
				return !ArePlayersProtectedByPactAlliance(playerById, attacker);
			}).ToList();
			if (attackerPlayers.Count == 0)
			{
				VerificationLog("PactRetaliation", $"No likely attacker found for pact boss death victim={victimGangId} leaderKilled={leaderWasKilled}; player blame skipped.");
			}
			foreach (PlayerInfo attackerPlayer in attackerPlayers)
			{
				if (attackerPlayer != null)
				{
					RegisterGangOpsHostileEvent(attackerPlayer.PID.id, victimGangId, lethal: true, 0L);
				}
			}
			ApplyPactBossDeathRelationshipPenalty(pact, allPactMembers, leaderWasKilled, victimGangId, attackerPlayers);
			List<PlayerInfo> survivingPactMembers = allPactMembers
				.Where(mid => mid != victimGangId)
				.Select(mid => allPlayers.FirstOrDefault(p => p.PID.id == mid))
				.Where(p => p != null && p.crew != null && !p.crew.IsCrewDefeated)
				.Distinct()
				.ToList();
			if (attackerPlayers.Count > 0 && survivingPactMembers.Count > 0)
			{
				foreach (PlayerInfo survivingPactMember in survivingPactMembers)
				{
					foreach (PlayerInfo attackerPlayer in attackerPlayers)
					{
						if (HasMutualTruce(survivingPactMember, attackerPlayer))
						{
							ClearWarBetweenPlayers(survivingPactMember, attackerPlayer);
							VerificationLog("PactRetaliation", $"Skipped boss retaliation due truce member={survivingPactMember.PID.id} attacker={attackerPlayer.PID.id}");
							continue;
						}
						ActivateWarBetweenPlayers(survivingPactMember, attackerPlayer);
						GangOpsChannel channel = ResolveGangOpsChannelForGang(survivingPactMember.PID.id);
						PactOpsSettings settings = EnsureGangOpsSettings(channel);
						float desiredHeat = Mathf.Max(GetWarHeatThresholdForCoordAttack(channel), settings.WarHeatKillGain);
						float currentHeat = GetWarHeat(channel, survivingPactMember.PID.id, attackerPlayer.PID.id);
						if (currentHeat < desiredHeat)
						{
							AddWarHeat(channel, survivingPactMember.PID.id, attackerPlayer.PID.id, desiredHeat - currentHeat, "pact-boss-retaliation");
						}
						QueueRevengeIfEligible(channel, survivingPactMember.PID.id, attackerPlayer.PID.id, 0L, G.GetNow().days, "pact-boss-retaliation");
						TryDispatchRuntimeGangAttack(survivingPactMember, attackerPlayer, Mathf.Clamp(survivingPactMember.crew?.LivingCrewCount ?? 1, 1, 4), "pact-boss-retaliation", out _);
					}
				}
				string targets = string.Join(", ", attackerPlayers.Select(a => a.PID.IsHumanPlayer ? "you" : (a.social?.PlayerGroupName ?? ("Gang#" + a.PID.id))).ToArray());
				if (leaderWasKilled)
				{
					LogGrapevine($"WAR: {pact.DisplayName} declared total war after leader {victimName} was killed by {targets}!");
				}
				else
				{
					LogGrapevine($"WAR: {pact.DisplayName} seeks revenge for {victimName} against {targets}!");
				}
			}
			if (leaderWasKilled)
			{
				ResetPactVotingStats(pact);
			}
		}

		private static void CommitBossSnapshot(Dictionary<int, ulong> currentBossByGang, int day)
		{
			_lastKnownBossPeepByGang.Clear();
			foreach (KeyValuePair<int, ulong> kv in currentBossByGang)
			{
				_lastKnownBossPeepByGang[kv.Key] = kv.Value;
			}
			_lastBossTrackDay = day;
		}

		private static void ProcessAIAlliances(SimTime now)
		{

			try
			{
				GameplayTweaksPlugin.NormalizePactSlots();
				List<PlayerInfo> source = G.GetAllPlayers().Where(p => p != null).ToList();
				Dictionary<int, PlayerInfo> playerById = new Dictionary<int, PlayerInfo>();
				List<PlayerInfo> list = new List<PlayerInfo>();
				foreach (PlayerInfo p in source)
				{
					playerById[p.PID.id] = p;
					if (p.IsJustGang && p.crew != null && !p.crew.IsCrewDefeated && !p.PID.IsHumanPlayer)
					{
						list.Add(p);
					}
				}
				List<AlliancePact> list2 = new List<AlliancePact>();
				Dictionary<AlliancePact, string> removalReasons = new Dictionary<AlliancePact, string>();
				int unlockedAIPactSlots = GetUnlockedAIPactSlots(now);
				Dictionary<int, ulong> currentBossByGang = new Dictionary<int, ulong>();
				foreach (PlayerInfo p in source)
				{
					if (p.IsJustGang && p.crew != null && !p.crew.IsCrewDefeated)
					{
						currentBossByGang[p.PID.id] = GetBossPeepId(p);
					}
				}
				bool hasBossSnapshot = _lastKnownBossPeepByGang.Count > 0 && _lastBossTrackDay >= 0;
				foreach (AlliancePact pact in SaveData.Pacts)
				{
					if (pact.ColorIndex < AI_PACT_SLOT_COUNT && pact.ColorIndex >= unlockedAIPactSlots)
					{
						list2.Add(pact);
						removalReasons[pact] = "locked-ai-slot";
						continue;
					}
					List<int> allPactMembers = new List<int>();
					if (pact.LeaderGangId >= 0)
					{
						allPactMembers.Add(pact.LeaderGangId);
					}
					allPactMembers.AddRange(pact.MemberIds);
					if (allPactMembers.Count == 0)
					{
						list2.Add(pact);
						removalReasons[pact] = "no-members";
						continue;
					}
					List<int> killedMembers = new List<int>();
					List<int> killedBossMembers = new List<int>();
					foreach (int mid in allPactMembers.Distinct())
					{
						playerById.TryGetValue(mid, out PlayerInfo memberP);
						if (memberP == null || memberP.crew == null || memberP.crew.IsCrewDefeated)
						{
							killedMembers.Add(mid);
							continue;
						}
						ulong prevBossId;
						ulong currentBossId = currentBossByGang.TryGetValue(mid, out var foundBossId) ? foundBossId : 0uL;
						if (hasBossSnapshot && _lastKnownBossPeepByGang.TryGetValue(mid, out prevBossId) && prevBossId > 0uL && currentBossId > 0uL && prevBossId != currentBossId)
						{
							Entity previousBoss = EntityIDExtensions.FindEntity(EntityID.FromID(prevBossId));
							if (previousBoss == null || previousBoss.data?.person == null || !previousBoss.data.person.IsAlive)
							{
								killedBossMembers.Add(mid);
							}
						}
					}
					foreach (int killedMemberId in killedMembers.Distinct().ToList())
					{
						playerById.TryGetValue(killedMemberId, out PlayerInfo killedMember);
						string killedName = killedMember?.social?.PlayerGroupName ?? ("Gang#" + killedMemberId);
						TriggerPactRetaliation(pact, allPactMembers, killedMemberId, killedName, pact.LeaderGangId == killedMemberId, source);
						GameplayTweaksPlugin.RemoveGangFromPactMembership(pact, killedMemberId, "pact-member-killed");
					}
					foreach (int bossKilledGangId in killedBossMembers.Distinct())
					{
						if (killedMembers.Contains(bossKilledGangId))
						{
							continue;
						}
						playerById.TryGetValue(bossKilledGangId, out PlayerInfo bossKilledGang);
						string killedName2 = bossKilledGang?.social?.PlayerGroupName ?? ("Gang#" + bossKilledGangId);
						TriggerPactRetaliation(pact, allPactMembers, bossKilledGangId, killedName2, pact.LeaderGangId == bossKilledGangId, source);
					}
					if (!list2.Contains(pact) && GetPactMemberGangIds(pact).Count <= 0)
					{
						list2.Add(pact);
						removalReasons[pact] = "empty-after-prune";
					}
				}
				foreach (AlliancePact item in list2)
				{
					if (SaveData.PlayerPactId >= 0 && item.PactId == $"pact_{SaveData.PlayerPactId}")
					{
						SaveData.PlayerPactId = -1;
					}
					if (SaveData.PlayerJoinedPactIndex == item.ColorIndex)
					{
						SaveData.PlayerJoinedPactIndex = -1;
					}
					VerificationLog("Pact", $"removed pact={item.PactId} leader={item.LeaderGangId} members={string.Join(",", (item.MemberIds ?? new List<int>()).ToArray())} reason={(removalReasons.TryGetValue(item, out string reason) ? reason : "unspecified")} day={now.days}");
					SaveData.Pacts.Remove(item);
				}
				if (list.Count < 2)
				{
					CommitBossSnapshot(currentBossByGang, now.days);
					return;
				}
				var list3 = (from g in list
					select new
					{
						Gang = g,
						Power = CalculateGangPower(g),
						Territory = GetGangTerritoryCount(g),
						StreetCred = GetGangStreetCredLevel(g)
					} into x
					orderby x.Power descending
					select x).ToList();
				int num = SaveData.Pacts.Count((AlliancePact p) => p.ColorIndex < AI_PACT_SLOT_COUNT);
				int maxAIPacts = GetUnlockedAIPactSlots(now);
				if (now.days % 7 == 0 && num < maxAIPacts)
				{
					int num2 = -1;
					HashSet<int> hashSet = new HashSet<int>(SaveData.Pacts.Select((AlliancePact p) => p.ColorIndex));
					for (int num3 = 0; num3 < AI_PACT_SLOT_COUNT; num3++)
					{
						if (!hashSet.Contains(num3))
						{
							num2 = num3;
							break;
						}
					}
					if (num2 < 0)
					{
						CommitBossSnapshot(currentBossByGang, now.days);
						return;
					}
					var list4 = list3.Where(x => GetPactForPlayer(x.Gang.PID) == null).ToList();
					if (list4.Count >= 2)
					{
						var leader = list4
							.Select(x => new
							{
								Entry = x,
								Score = x.Territory < AI_PACT_FOUNDER_MIN_TERRITORIES ? 0f : x.Power + x.StreetCred * 6f
							})
							.Where(x => x.Score > 0f)
							.OrderByDescending(x => x.Score)
							.Select(x => x.Entry)
							.FirstOrDefault();
						if (leader != null)
						{
							int leaderId = leader.Gang.PID.id;
							int leaderTerritory = leader.Territory;
							int leaderSc = leader.StreetCred;
							float leaderScore = leaderTerritory < AI_PACT_FOUNDER_MIN_TERRITORIES ? 0f : leader.Power + leader.StreetCred * 6f;
							var partnerCandidates = list4
								.Where(x => x.Gang.PID.id != leader.Gang.PID.id && x.Power > leader.Power / 3)
								.ToList();
							var anon = partnerCandidates
								.Select(x => new
								{
									Entry = x,
									Score = x.Power + GetInterGangRelationshipScore(leader.Gang, x.Gang) * 50f
								})
								.OrderByDescending(x => x.Score)
								.Select(x => x.Entry)
								.FirstOrDefault();
							if (anon != null)
							{
								int partnerId = anon.Gang.PID.id;
								int partnerTerritory = anon.Territory;
								int partnerSc = anon.StreetCred;
								float relScore = GetInterGangRelationshipScore(leader.Gang, anon.Gang);
								Debug.Log($"[GameplayTweaks] AI Alliance select founder={leaderId} terr={leaderTerritory} sc={leaderSc} score={leaderScore:0.0} partner={partnerId} terr={partnerTerritory} sc={partnerSc} rel={relScore:0.00}");
								AlliancePact alliancePact = new AlliancePact
								{
									PactId = $"pact_{SaveData.NextPactId++}",
									PactName = ModConstants.PACT_COLOR_NAMES[num2] + " Alliance",
									ColorIndex = num2,
									LeaderGangId = leader.Gang.PID.id,
									MemberIds = new List<int> { anon.Gang.PID.id },
									SharedColor = ModConstants.PACT_COLORS[num2],
									Formed = now
								};
								SaveData.Pacts.Add(alliancePact);
								ApplyPactJoinRelationshipBoost(alliancePact, leader.Gang.PID.id, leader.Gang.social?.PlayerGroupName ?? ("Gang#" + leader.Gang.PID.id));
								ApplyPactJoinRelationshipBoost(alliancePact, anon.Gang.PID.id, anon.Gang.social?.PlayerGroupName ?? ("Gang#" + anon.Gang.PID.id));
								VerificationLog("Pact", $"created-ai-pact slot={num2} unlocked={maxAIPacts} activeBefore={num} day={now.days} leader={leader.Gang.PID.id} partner={anon.Gang.PID.id} epoch={GetOrInitPactEpochDay(now)}");
								Debug.Log(("[GameplayTweaks] AI Alliance: " + leader.Gang.social.PlayerGroupName + " + " + anon.Gang.social.PlayerGroupName + " (" + alliancePact.PactName + ")"));
								LogGrapevine("PACT: " + leader.Gang.social.PlayerGroupName + " and " + anon.Gang.social.PlayerGroupName + " formed " + alliancePact.PactName);
							}
						}
					}
				}
				if (now.days % 20 == 0 && SaveData.Pacts.Count > 0)
				{
					var unaffiliated = list3.Where(x => GetPactForPlayer(x.Gang.PID) == null).ToList();
					foreach (AlliancePact existingPact in SaveData.Pacts.ToList())
					{
						if (!existingPact.IsActive || existingPact.ColorIndex >= AI_PACT_SLOT_COUNT || unaffiliated.Count == 0 || GameplayTweaksPlugin.IsPactAtMemberCap(existingPact))
							continue;
						if (SharedRng.NextDouble() < 0.35)
						{
							var recruit = unaffiliated[SharedRng.Next(unaffiliated.Count)];
							int recruitPower = recruit.Power;
							int leaderPower = 0;
							var leaderEntry = list3.FirstOrDefault(x => x.Gang.PID.id == existingPact.LeaderGangId);
							if (leaderEntry != null) leaderPower = leaderEntry.Power;
							if (recruitPower > leaderPower / 4)
							{
								if (recruit.Gang.PID.id != existingPact.LeaderGangId
									&& !existingPact.MemberIds.Contains(recruit.Gang.PID.id)
									&& GameplayTweaksPlugin.CanGangJoinPact(existingPact, recruit.Gang.PID.id))
								{
									existingPact.MemberIds.Add(recruit.Gang.PID.id);
									ApplyPactJoinRelationshipBoost(existingPact, recruit.Gang.PID.id, recruit.Gang.social?.PlayerGroupName ?? ("Gang#" + recruit.Gang.PID.id));
								}
								unaffiliated.Remove(recruit);
								string recruitName = recruit.Gang.social?.PlayerGroupName ?? "Unknown";
								Debug.Log($"[GameplayTweaks] {recruitName} joined {existingPact.DisplayName}");
								LogGrapevine("PACT: " + recruitName + " joined " + existingPact.DisplayName);
							}
						}
					}
				}
				if (now.days % AI_PACT_TRADE_INTERVAL_DAYS == 0 && SaveData.Pacts.Count > 0)
				{
					foreach (AlliancePact activePact in SaveData.Pacts.Where(p => p != null && p.IsActive && p.ColorIndex < AI_PACT_SLOT_COUNT).ToList())
					{
						TryRunAiPactTradeCycle(activePact, now);
					}
				}
				if (now.days % AI_NETWORK_TRADE_INTERVAL_DAYS == 0)
				{
					TryRunAiExternalTradeCycle(now);
				}
				if (now.days % 14 != 0 || list.Count < 2)
				{
					CommitBossSnapshot(currentBossByGang, now.days);
					return;
				}
				if (SharedRng.NextDouble() < 0.4)
				{
					var anon2 = list3[SharedRng.Next(Math.Min(4, list3.Count))];
					var anon3 = list3[SharedRng.Next(list3.Count)];
					if (anon2.Gang.PID.id != anon3.Gang.PID.id)
					{
						PlayerSocial social = anon2.Gang.social;
						string text = ((social != null) ? social.PlayerGroupName : null) ?? "Unknown";
						PlayerSocial social2 = anon3.Gang.social;
						string text2 = ((social2 != null) ? social2.PlayerGroupName : null) ?? "Unknown";
						LogGrapevine("DEATH: A member of " + text2 + " was killed by " + text);
					}
				}
				CommitBossSnapshot(currentBossByGang, now.days);
			}
			catch (Exception arg)
			{
				Debug.LogError($"[GameplayTweaks] ProcessAIAlliances: {arg}");
			}
		}

		private static void TryRunAiPactTradeCycle(AlliancePact pact, SimTime now)
		{
			try
			{
				if (pact == null || !pact.IsActive)
				{
					return;
				}
				List<AiTradeActor> list = GetPactMemberGangIds(pact)
					.Select(G.FindPlayerById)
					.Where(IsEligibleAiTradeGang)
					.Distinct()
					.Select(CreateGangTradeActor)
					.ToList();
				if (list.Count < 2)
				{
					VerificationLog("AIPactTrade", $"cadence day={now.days} pact={pact.PactId ?? "unknown"} actors={list.Count} result=insufficient-actors");
					return;
				}
				AiTradeProposal aiTradeProposal = null;
				int evaluatedPairs = 0;
				foreach (AiTradeActor item in list)
				{
					foreach (AiTradeActor item2 in list)
					{
						if (item == null || item2 == null || string.Equals(item.ActorKey, item2.ActorKey, StringComparison.Ordinal))
						{
							continue;
						}
						evaluatedPairs++;
						AiTradeProposal aiTradeProposal2 = EvaluateAiTradeProposal(item, item2, isInternalPactTrade: true);
						if (aiTradeProposal2 != null && (aiTradeProposal == null || aiTradeProposal2.Score > aiTradeProposal.Score))
						{
							aiTradeProposal = aiTradeProposal2;
						}
					}
				}
				if (aiTradeProposal == null)
				{
					VerificationLog("AIPactTrade", $"cadence day={now.days} pact={pact.PactId ?? "unknown"} actors={list.Count} evaluatedPairs={evaluatedPairs} candidates=0 result=no-candidate");
					return;
				}
				if (aiTradeProposal.Score < 0.48f)
				{
					VerificationLog("AIPactTrade", $"cadence day={now.days} pact={pact.PactId ?? "unknown"} actors={list.Count} evaluatedPairs={evaluatedPairs} best={aiTradeProposal.TradeKey} score={aiTradeProposal.Score:0.00} result=below-threshold");
					return;
				}
				double roll = SharedRng.NextDouble();
				if (roll > aiTradeProposal.Score)
				{
					VerificationLog("AIPactTrade", $"cadence day={now.days} pact={pact.PactId ?? "unknown"} actors={list.Count} evaluatedPairs={evaluatedPairs} best={aiTradeProposal.TradeKey} score={aiTradeProposal.Score:0.00} roll={roll:0.00} result=roll-miss");
					return;
				}
				if (TryExecuteAiTradeProposal(aiTradeProposal, now, pact, "AIPactTrade", externalNetwork: false))
				{
					VerificationLog("AIPactTrade", $"executed day={now.days} pact={pact.PactId} trade={aiTradeProposal.TradeKey} sellerActor={aiTradeProposal.SellerActor?.ActorKey} buyerActor={aiTradeProposal.BuyerActor?.ActorKey} sellerGang={aiTradeProposal.Seller?.PID.id ?? -1} buyerGang={aiTradeProposal.Buyer?.PID.id ?? -1} score={aiTradeProposal.Score:0.00}");
				}
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] TryRunAiPactTradeCycle failed: " + ex.Message);
			}
		}

		private static void TryRunAiExternalTradeCycle(SimTime now)
		{
			try
			{
				PlayerInfo humanPlayer = G.GetHumanPlayer();
				int humanGangId = humanPlayer?.PID.id ?? -1;
				List<AiTradeActor> list = BuildAiExternalTradeActors(humanGangId);
				if (list.Count < 2)
				{
					VerificationLog("AINetworkTrade", $"cadence day={now.days} actors={list.Count} result=insufficient-actors");
					return;
				}
				if (TryRunAiExternalTrespassRobberyCycle(now, list))
				{
					TryRunRareAiRobberyAgainstHuman(humanPlayer, now);
					return;
				}
				List<AiTradeProposal> list2 = new List<AiTradeProposal>(Math.Min(AI_NETWORK_TRADE_CANDIDATE_POOL_LIMIT, 8));
				int evaluatedPairs = 0;
				int attempts = 0;
				int maxEvaluatedPairs = Math.Min(AI_NETWORK_TRADE_MAX_EVALUATED_PAIRS, list.Count * Math.Max(0, list.Count - 1));
				int maxAttempts = Math.Max(maxEvaluatedPairs * 4, list.Count * 2);
				HashSet<string> evaluatedPairKeys = new HashSet<string>(StringComparer.Ordinal);
				while (evaluatedPairs < maxEvaluatedPairs && attempts < maxAttempts)
				{
					attempts++;
					int sellerIndex = SharedRng.Next(list.Count);
					int buyerIndex = SharedRng.Next(list.Count - 1);
					if (buyerIndex >= sellerIndex)
					{
						buyerIndex++;
					}
					AiTradeActor item = list[sellerIndex];
					AiTradeActor item2 = list[buyerIndex];
					if (ShouldSkipExternalTradePair(item, item2))
					{
						continue;
					}
					string pairKey = (item?.ActorKey ?? "?") + ">" + (item2?.ActorKey ?? "?");
					if (!evaluatedPairKeys.Add(pairKey))
					{
						continue;
					}
					evaluatedPairs++;
					AiTradeProposal aiTradeProposal = EvaluateAiTradeProposal(item, item2, isInternalPactTrade: false);
					if (aiTradeProposal != null && aiTradeProposal.Score >= 0.5f)
					{
						AddAiTradeCandidate(list2, aiTradeProposal, AI_NETWORK_TRADE_CANDIDATE_POOL_LIMIT);
					}
				}
				if (list2.Count == 0)
				{
					VerificationLog("AINetworkTrade", $"cadence day={now.days} actors={list.Count} evaluatedPairs={evaluatedPairs} attempts={attempts} candidates=0 result=no-candidate");
					TryRunRareAiRobberyAgainstHuman(humanPlayer, now);
					return;
				}
				HashSet<string> hashSet = new HashSet<string>(StringComparer.Ordinal);
				int num = 0;
				foreach (AiTradeProposal item3 in list2)
				{
					if (item3 == null || item3.Score < 0.5f)
					{
						continue;
					}
					if ((item3.SellerActor != null && hashSet.Contains(item3.SellerActor.ActorKey)) || (item3.BuyerActor != null && hashSet.Contains(item3.BuyerActor.ActorKey)))
					{
						continue;
					}
					if (SharedRng.NextDouble() > item3.Score)
					{
						continue;
					}
					if (!TryExecuteAiTradeProposal(item3, now, null, "AINetworkTrade", externalNetwork: true))
					{
						continue;
					}
					if (item3.SellerActor != null)
					{
						hashSet.Add(item3.SellerActor.ActorKey);
					}
					if (item3.BuyerActor != null)
					{
						hashSet.Add(item3.BuyerActor.ActorKey);
					}
					num++;
					if (num >= AI_NETWORK_TRADE_MAX_SUCCESS_COUNT)
					{
						break;
					}
				}
				VerificationLog("AINetworkTrade", $"cadence day={now.days} actors={list.Count} evaluatedPairs={evaluatedPairs} attempts={attempts} candidates={list2.Count} executed={num} result={(num > 0 ? "executed" : "no-execution")}");
				TryRunRareAiRobberyAgainstHuman(humanPlayer, now);
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] TryRunAiExternalTradeCycle failed: " + ex.Message);
			}
		}

		private static bool TryRunAiExternalTrespassRobberyCycle(SimTime now, List<AiTradeActor> actors)
		{
			try
			{
				if (actors == null || actors.Count < 2)
				{
					return false;
				}

				AiTradeProposal best = null;
				int scannedPairs = 0;
				int recentTrespassPairs = 0;
				int trespassPairs = 0;
				int adjacentPressurePairs = 0;
				int blockedGoodRelations = 0;
				int blockedProtected = 0;
				int blockedCash = 0;
				int blockedCooldown = 0;
				foreach (AiTradeActor robberActor in actors)
				{
					foreach (AiTradeActor victimActor in actors)
					{
						if (ShouldSkipExternalTradePair(robberActor, victimActor))
						{
							continue;
						}

						foreach (PlayerInfo robber in GetTradeActorGangCandidates(robberActor))
						{
							foreach (PlayerInfo victim in GetTradeActorGangCandidates(victimActor))
							{
								if (robber == null || victim == null || robber.PID.id == victim.PID.id || robber.PID.IsHumanPlayer || victim.PID.IsHumanPlayer)
								{
									continue;
								}

								scannedPairs++;
								if (ArePlayersProtectedByPactAlliance(robber, victim) || HasMutualTruce(robber, victim))
								{
									blockedProtected++;
									continue;
								}
								bool recentTrespassMemory = HasRecentGangTrespassMemory(victim, robber);
								bool territoryPressure = IsGangTrespassingOrPressuringGangTerritory(victim, robber, allowAdjacentPressure: true, out int trespassCrew, out int trespassNodes, out int adjacentCrew, out int adjacentNodes);
								if (!recentTrespassMemory && !territoryPressure)
								{
									continue;
								}

								if (recentTrespassMemory)
								{
									recentTrespassPairs++;
								}
								if (trespassCrew > 0)
								{
									trespassPairs++;
								}
								else
								{
									adjacentPressurePairs++;
								}
								int rel = GetInterGangRelationshipDisplayScore(robber, victim);
								float relationBias = GetSignedGangRelationshipBias(robber, victim);
								if (relationBias >= 0.2f || rel >= 20)
								{
									blockedGoodRelations++;
									continue;
								}
								int victimCash = GetGangCleanCash(victim);
								if (victimCash < AI_ROBBERY_LOW_CASH_MIN + AI_ROBBERY_MIN_CASH_RESERVE)
								{
									blockedCash++;
									continue;
								}
								if (TryGetAiGangRobberyPairCooldown(robber, victim, now, out _))
								{
									blockedCooldown++;
									continue;
								}
								if (IsAiPersonalityPeaceful(robber))
								{
									VerificationLog("AINetworkTrade", $"robbery-scan blocked reason=peaceful-personality day={now.days} robber={robber.PID.id} victim={victim.PID.id}");
									continue;
								}

								bool robberTraits = HasAnyGangBossTrait(robber, "trait-aggressive", "trait-vindictive", "trait-bold", "trait-cruel");
								bool aggressivePersonality = IsAiPersonalityAggressive(robber);
								bool expansionistPersonality = IsAiPersonalityExpansionist(robber);
								int powerEdge = CalculateGangPower(robber) - CalculateGangPower(victim);
								bool highValue = victimCash >= AI_ROBBERY_HIGH_CASH_MIN + AI_ROBBERY_MIN_CASH_RESERVE && (robberTraits || aggressivePersonality || powerEdge > 35);
								bool strictTrespass = trespassCrew > 0;
								float score = (strictTrespass ? 0.62f : (recentTrespassMemory ? 0.6f : 0.54f))
									+ (recentTrespassMemory ? 0.08f : 0f)
									+ Mathf.Clamp(trespassCrew * 0.04f + adjacentCrew * 0.025f, 0f, 0.16f)
									+ Mathf.Clamp(trespassNodes * 0.03f + adjacentNodes * 0.02f, 0f, 0.12f)
									+ Mathf.Clamp(powerEdge / 350f, -0.08f, 0.16f)
									+ (robberTraits ? 0.08f : 0f)
									+ (aggressivePersonality ? 0.12f : 0f)
									+ (expansionistPersonality ? 0.04f : 0f)
									+ (IsAggroWithoutTruceEitherWay(robber, victim) ? 0.06f : 0f)
									- (HasAnyGangBossTrait(robber, "trait-cautious") ? 0.05f : 0f);
								score = Mathf.Clamp(score, 0.5f, 0.95f);
								if (best == null || score > best.Score)
								{
									best = new AiTradeProposal
									{
										TradeKey = highValue ? "gang-robbery-high" : "gang-robbery-low",
										SellerActor = robberActor,
										BuyerActor = victimActor,
										Seller = robber,
										Buyer = victim,
										Score = score
									};
								}
							}
						}
					}
				}

				if (best == null)
				{
					VerificationLog("AINetworkTrade", $"robbery-scan day={now.days} scannedPairs={scannedPairs} recentTrespassPairs={recentTrespassPairs} trespassPairs={trespassPairs} adjacentPressurePairs={adjacentPressurePairs} eligible=0 blockedProtected={blockedProtected} blockedGoodRelations={blockedGoodRelations} blockedCash={blockedCash} blockedCooldown={blockedCooldown} result=no-candidate");
					return false;
				}
				if (SharedRng.NextDouble() > best.Score)
				{
					VerificationLog("AINetworkTrade", $"robbery-scan day={now.days} scannedPairs={scannedPairs} recentTrespassPairs={recentTrespassPairs} trespassPairs={trespassPairs} adjacentPressurePairs={adjacentPressurePairs} eligible=1 best={best.TradeKey} robber={best.Seller.PID.id} victim={best.Buyer.PID.id} score={best.Score:0.00} result=roll-miss");
					return false;
				}
				if (TryRunAiFrontClosureBeforeRobbery(best, now, "AINetworkTrade", externalNetwork: true))
				{
					VerificationLog("AINetworkTrade", $"robbery-scan day={now.days} scannedPairs={scannedPairs} recentTrespassPairs={recentTrespassPairs} trespassPairs={trespassPairs} adjacentPressurePairs={adjacentPressurePairs} eligible=1 best={best.TradeKey} robber={best.Seller.PID.id} victim={best.Buyer.PID.id} score={best.Score:0.00} result=front-closure-before-robbery");
					return true;
				}
				bool executed = TryExecuteAiTradeProposal(best, now, null, "AINetworkTrade", externalNetwork: true);
				VerificationLog("AINetworkTrade", $"robbery-scan day={now.days} scannedPairs={scannedPairs} recentTrespassPairs={recentTrespassPairs} trespassPairs={trespassPairs} adjacentPressurePairs={adjacentPressurePairs} eligible=1 best={best.TradeKey} robber={best.Seller.PID.id} victim={best.Buyer.PID.id} score={best.Score:0.00} result={(executed ? "executed" : "execute-failed")}");
				return executed;
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] TryRunAiExternalTrespassRobberyCycle failed: " + ex.Message);
				return false;
			}
		}

		private static void AddAiTradeCandidate(List<AiTradeProposal> candidates, AiTradeProposal proposal, int maxCandidates)
		{
			if (candidates == null || proposal == null || maxCandidates <= 0)
			{
				return;
			}
			int insertIndex = 0;
			while (insertIndex < candidates.Count && candidates[insertIndex] != null && candidates[insertIndex].Score >= proposal.Score)
			{
				insertIndex++;
			}
			if (insertIndex >= maxCandidates)
			{
				return;
			}
			candidates.Insert(insertIndex, proposal);
			if (candidates.Count > maxCandidates)
			{
				candidates.RemoveAt(candidates.Count - 1);
			}
		}

		private static List<AiTradeActor> BuildAiExternalTradeActors(int humanGangId)
		{
			AlliancePact pact = ((humanGangId >= 0) ? GetPactForPlayer(new PlayerID
			{
				id = (short)humanGangId
			}) : null);
			List<AiTradeActor> list = G.GetAllPlayers()
				.Where(gang => IsEligibleAiTradeGang(gang) && !IsGangInExcludedHumanPact(gang, pact))
				.Select(CreateGangTradeActor)
				.ToList();
			foreach (AlliancePact item in SaveData.Pacts.Where(p => p != null && p.IsActive))
			{
				if (pact != null && string.Equals(item.PactId, pact.PactId, StringComparison.Ordinal))
				{
					continue;
				}
				if (GetTradeActorGangCandidates(CreatePactTradeActor(item)).Count > 0)
				{
					list.Add(CreatePactTradeActor(item));
				}
			}
			return list;
		}

		private static AiTradeProposal EvaluateAiTradeProposal(AiTradeActor sellerActor, AiTradeActor buyerActor, bool isInternalPactTrade)
		{
			if (sellerActor == null || buyerActor == null || string.Equals(sellerActor.ActorKey, buyerActor.ActorKey, StringComparison.Ordinal))
			{
				return null;
			}
			List<PlayerInfo> tradeActorGangCandidates = GetTradeActorGangCandidates(sellerActor);
			List<PlayerInfo> tradeActorGangCandidates2 = GetTradeActorGangCandidates(buyerActor);
			if (tradeActorGangCandidates.Count == 0 || tradeActorGangCandidates2.Count == 0)
			{
				return null;
			}
			AiTradeProposal aiTradeProposal = null;
			float num = isInternalPactTrade ? 0.08f : 0f;
			void consider(string tradeKey, PlayerInfo seller, PlayerInfo buyer, float score)
			{
				if (seller == null || buyer == null || seller.PID.id == buyer.PID.id || score <= 0f)
				{
					return;
				}
				float num9 = Mathf.Clamp01(score);
				if (aiTradeProposal == null || num9 > aiTradeProposal.Score)
				{
					aiTradeProposal = new AiTradeProposal
					{
						TradeKey = tradeKey,
						SellerActor = sellerActor,
						BuyerActor = buyerActor,
						Seller = seller,
						Buyer = buyer,
						Score = num9
					};
				}
			}

			foreach (PlayerInfo item in tradeActorGangCandidates)
			{
				foreach (PlayerInfo item2 in tradeActorGangCandidates2)
				{
					if (item == null || item2 == null || item.PID.id == item2.PID.id)
					{
						continue;
					}
					int interGangRelationshipDisplayScore = GetInterGangRelationshipDisplayScore(item, item2);
					float signedGangRelationshipBias = GetSignedGangRelationshipBias(item, item2);
					float gangTradeDisposition = GetGangTradeDisposition(item);
					float gangTradeDisposition2 = GetGangTradeDisposition(item2);
					float num2 = gangTradeDisposition + gangTradeDisposition2;
					bool flag = HasAnyGangBossTrait(item, "trait-friendly", "trait-talkative");
					bool flag2 = HasAnyGangBossTrait(item2, "trait-friendly", "trait-talkative");
					bool flag3 = HasAnyGangBossTrait(item, "trait-intelligent", "trait-connected");
					bool flag4 = HasAnyGangBossTrait(item2, "trait-intelligent", "trait-connected");
					bool flag5 = HasAnyGangBossTrait(item, "trait-cautious");
					bool flag6 = HasAnyGangBossTrait(item2, "trait-cautious");
					bool flag7 = HasAnyGangBossTrait(item, "trait-aggressive");
					bool flag8 = HasAnyGangBossTrait(item2, "trait-aggressive");
					bool sellerPeacefulPersonality = IsAiPersonalityPeaceful(item);
					bool sellerAggressivePersonality = IsAiPersonalityAggressive(item);
					bool sellerExpansionistPersonality = IsAiPersonalityExpansionist(item);
					bool alreadyAggro = IsAggroWithoutTruceEitherWay(item, item2);
					if (!isInternalPactTrade && !ArePlayersProtectedByPactAlliance(item, item2) && !HasMutualTruce(item, item2))
					{
						int victimCleanCash = GetGangCleanCash(item2);
						bool robberTraits = flag7 || HasAnyGangBossTrait(item, "trait-vindictive", "trait-bold", "trait-cruel");
						int powerEdge = CalculateGangPower(item) - CalculateGangPower(item2);
						bool victimTrespassing = IsGangTrespassingOnGangTerritory(item2, item, out int trespassCrew, out int trespassNodes);
						bool recentTrespassMemory = HasRecentGangTrespassMemory(item2, item);
						bool goodRelations = signedGangRelationshipBias >= 0.2f || interGangRelationshipDisplayScore >= 20;
						if (victimCleanCash >= AI_ROBBERY_LOW_CASH_MIN + AI_ROBBERY_MIN_CASH_RESERVE
							&& !sellerPeacefulPersonality
							&& !goodRelations
							&& (victimTrespassing || recentTrespassMemory || interGangRelationshipDisplayScore <= -18)
							&& (victimTrespassing || recentTrespassMemory || robberTraits || sellerAggressivePersonality || alreadyAggro || powerEdge > -25))
						{
							float robberyScore = 0.42f
								+ Mathf.Clamp01((-interGangRelationshipDisplayScore - 15) / 80f) * 0.22f
								+ Mathf.Clamp(powerEdge / 300f, -0.08f, 0.16f)
								+ (robberTraits ? 0.08f : 0f)
								+ (sellerAggressivePersonality ? 0.12f : 0f)
								+ (sellerExpansionistPersonality ? 0.04f : 0f)
								+ (alreadyAggro ? 0.06f : 0f)
								+ (victimTrespassing ? 0.12f : 0f)
								+ (recentTrespassMemory ? 0.1f : 0f)
								+ Mathf.Clamp(trespassCrew * 0.02f, 0f, 0.08f)
								- (flag5 ? 0.04f : 0f)
								- (flag6 ? 0.03f : 0f);
							consider(victimCleanCash >= AI_ROBBERY_HIGH_CASH_MIN + AI_ROBBERY_MIN_CASH_RESERVE && (robberTraits || sellerAggressivePersonality || powerEdge > 35) ? "gang-robbery-high" : "gang-robbery-low", item, item2, robberyScore);
						}
					}
					if (alreadyAggro)
					{
						continue;
					}
					float num3 = num2;
					if (flag)
					{
						num3 += 0.1f;
					}
					if (flag2)
					{
						num3 += 0.1f;
					}
					if (GetGangCleanCash(item) >= AI_PACT_TRADE_LEISURE_COST + AI_PACT_TRADE_MIN_CLEAN_RESERVE
						&& interGangRelationshipDisplayScore >= (isInternalPactTrade ? -15 : -10)
						&& interGangRelationshipDisplayScore <= 25
						&& (num3 >= 0.08f || flag || flag2))
					{
						float num4 = (isInternalPactTrade ? 0.5f : 0.42f) + num + Mathf.Clamp01((num3 + 0.2f) * 0.38f) + Mathf.Clamp01(0.16f - Mathf.Abs(interGangRelationshipDisplayScore) / 100f) + ((flag || flag2) ? 0.06f : 0f) - ((flag7 || flag8) ? 0.03f : 0f);
						consider("gang-leisure", item, item2, num4);
					}
					int num5 = (isInternalPactTrade ? 0 : 5) + (flag5 ? 8 : 0) + (flag6 ? 8 : 0);
					if (GetGangDirtyCash(item) >= AI_PACT_TRADE_LAUNDER_DIRTY_QTY
						&& GetGangCleanCash(item2) >= AI_PACT_TRADE_LAUNDER_CLEAN_COST + AI_PACT_TRADE_MIN_CLEAN_RESERVE
						&& interGangRelationshipDisplayScore >= num5
						&& !HasAnyGangBossTrait(item, "trait-upright", "trait-religious"))
					{
						float num6 = (flag3 ? 0.08f : 0f) + (flag4 ? 0.05f : 0f) - ((flag5 || flag6) ? 0.04f : 0f);
						float num7 = (isInternalPactTrade ? 0.5f : 0.42f) + num + Mathf.Clamp01((signedGangRelationshipBias + 0.15f) * 0.3f) + Mathf.Clamp01((num2 + num6 + 0.15f) * 0.25f);
						consider("gang-money-in", item, item2, num7);
					}
					int num8 = (isInternalPactTrade ? -5 : 2) + (flag5 ? 6 : 0) + (flag6 ? 6 : 0);
					if (GetGangSafehouseResourceTotal(item, AI_PACT_TRADE_LIQUOR_LABELS) >= AI_PACT_TRADE_LIQUOR_QTY
						&& GetGangCleanCash(item2) >= AI_PACT_TRADE_LIQUOR_CLEAN_COST + AI_PACT_TRADE_MIN_CLEAN_RESERVE
						&& interGangRelationshipDisplayScore >= num8)
					{
						float num10 = (isInternalPactTrade ? 0.5f : 0.44f) + num + Mathf.Clamp01((signedGangRelationshipBias + 0.2f) * 0.28f) + Mathf.Clamp01((num2 + (flag7 ? 0.08f : 0f) + 0.12f) * 0.22f);
						consider("gang-trade-liquor-low", item, item2, num10);
					}
					int num11 = (isInternalPactTrade ? 10 : 18) + (flag5 ? 10 : 0) + (flag6 ? 10 : 0);
					if (GetGangSafehouseResourceTotal(item, AI_PACT_TRADE_DRUG_LABELS) >= AI_PACT_TRADE_DRUGS_QTY
						&& GetGangCleanCash(item2) >= AI_PACT_TRADE_DRUGS_CLEAN_COST + AI_PACT_TRADE_MIN_CLEAN_RESERVE
						&& interGangRelationshipDisplayScore >= num11
						&& !HasAnyGangBossTrait(item, "trait-upright", "trait-religious")
						&& (flag3 || flag4 || flag7 || flag8))
					{
						float num12 = (isInternalPactTrade ? 0.44f : 0.36f) + num + Mathf.Clamp01((signedGangRelationshipBias + 0.25f) * 0.32f) + Mathf.Clamp01((num2 + (flag3 ? 0.1f : 0f) + (flag7 ? 0.08f : 0f) + 0.08f) * 0.26f);
						consider("gang-trade-drugs-low", item, item2, num12);
					}
				}
			}
			return aiTradeProposal;
		}

		private static bool TryExecuteAiTradeProposal(AiTradeProposal proposal, SimTime now, AlliancePact pact, string verificationChannel, bool externalNetwork)
		{
			if (proposal?.Seller == null || proposal.Buyer == null)
			{
				return false;
			}
			switch (proposal.TradeKey)
			{
			case "gang-leisure":
				return TryExecuteAiGangLeisureTrade(proposal, now, pact, verificationChannel, externalNetwork);
			case "gang-money-in":
				return TryExecuteAiGangMoneyInTrade(proposal, now, pact, verificationChannel, externalNetwork);
			case "gang-trade-liquor-low":
				return TryExecuteAiGangResourceTrade(proposal, now, pact, verificationChannel, externalNetwork, proposal.TradeKey, AI_PACT_TRADE_LIQUOR_LABELS, AI_PACT_TRADE_LIQUOR_QTY, AI_PACT_TRADE_LIQUOR_CLEAN_COST, "relbuff-gangs-loot1-table-on-finish", "relbuff-gangs-loot2-buff", 0.05f);
			case "gang-trade-drugs-low":
				return TryExecuteAiGangResourceTrade(proposal, now, pact, verificationChannel, externalNetwork, proposal.TradeKey, AI_PACT_TRADE_DRUG_LABELS, AI_PACT_TRADE_DRUGS_QTY, AI_PACT_TRADE_DRUGS_CLEAN_COST, "relbuff-gangs-loot3-table-on-finish", "relbuff-gangs-loot3-buff", 0.07f);
			case "gang-robbery-low":
			case "gang-robbery-high":
				return TryExecuteAiGangRobbery(proposal, now, verificationChannel, externalNetwork);
			default:
				return false;
			}
		}

	private static bool TryExecuteAiGangLeisureTrade(AiTradeProposal proposal, SimTime now, AlliancePact pact, string verificationChannel, bool externalNetwork)
	{
		PlayerInfo seller = proposal?.Seller;
		PlayerInfo buyer = proposal?.Buyer;
			if (seller == null || buyer == null || !TryTransferCleanCashBetweenGangs(seller, buyer, AI_PACT_TRADE_LEISURE_COST, AI_PACT_TRADE_MIN_CLEAN_RESERVE))
			{
				return false;
		}
		bool logRelationshipBuffs = ShouldLogAiTradeRelationshipBuffs(seller, buyer);
		AddMutualRelationshipBuff(seller, buyer, "relbuff-gangs-leisure-table-on-finish", GetCrewPeepForPlayer(seller), GetCrewPeepForPlayer(buyer), logRelationshipBuffs);
		AddMutualRelationshipBuff(seller, buyer, "relbuff-gangs-leisure-buff", GetCrewPeepForPlayer(seller), GetCrewPeepForPlayer(buyer), logRelationshipBuffs);
		ApplyPactTradeRelationshipBuff(seller, buyer, logRelationshipBuffs);
		ApplyAiTradeMoodDelta(proposal, pact, 0.05f);
		AwardHumanPactTradeStreetCredit(seller, buyer, "pact-trade-leisure", 0.03f, 0.05f);
		LogGrapevine($"{GetAiTradeGrapevinePrefix(externalNetwork)}: {GetAiTradeActorDisplayName(proposal.SellerActor, seller)} picked up the tab for a leisure sitdown with {GetAiTradeActorDisplayName(proposal.BuyerActor, buyer)}.");
		VerificationLog(verificationChannel, $"type=gang-leisure day={now.days} sellerActor={proposal.SellerActor?.ActorKey} buyerActor={proposal.BuyerActor?.ActorKey} sellerGang={seller.PID.id} buyerGang={buyer.PID.id} cash={AI_PACT_TRADE_LEISURE_COST}");
			return true;
		}

		private static bool TryExecuteAiGangMoneyInTrade(AiTradeProposal proposal, SimTime now, AlliancePact pact, string verificationChannel, bool externalNetwork)
		{
			PlayerInfo seller = proposal?.Seller;
			PlayerInfo buyer = proposal?.Buyer;
			if (seller == null || buyer == null || !TryTransferCleanCashBetweenGangs(buyer, seller, AI_PACT_TRADE_LAUNDER_CLEAN_COST, AI_PACT_TRADE_MIN_CLEAN_RESERVE))
			{
				return false;
			}
			int num = RemoveGangDirtyCash(seller, AI_PACT_TRADE_LAUNDER_DIRTY_QTY);
			if (num < AI_PACT_TRADE_LAUNDER_DIRTY_QTY)
			{
				if (num > 0)
				{
					AddDirtyCashToGang(seller, num);
				}
				TryTransferCleanCashBetweenGangs(seller, buyer, AI_PACT_TRADE_LAUNDER_CLEAN_COST, 0);
				return false;
			}
			if (!AddDirtyCashToGang(buyer, num))
			{
				AddDirtyCashToGang(seller, num);
				TryTransferCleanCashBetweenGangs(seller, buyer, AI_PACT_TRADE_LAUNDER_CLEAN_COST, 0);
				return false;
		}
		bool logRelationshipBuffs = ShouldLogAiTradeRelationshipBuffs(seller, buyer);
		AddMutualRelationshipBuff(seller, buyer, "relbuff-gangs-loot5-table-on-finish", GetCrewPeepForPlayer(seller), GetCrewPeepForPlayer(buyer), logRelationshipBuffs);
		ApplyPactTradeRelationshipBuff(seller, buyer, logRelationshipBuffs);
		ApplyAiTradeMoodDelta(proposal, pact, 0.04f);
		AwardHumanPactTradeStreetCredit(seller, buyer, "pact-trade-laundering", 0.04f, 0.06f);
		LogGrapevine($"{GetAiTradeGrapevinePrefix(externalNetwork)}: {GetAiTradeActorDisplayName(proposal.BuyerActor, buyer)} bought dirty money from {GetAiTradeActorDisplayName(proposal.SellerActor, seller)}.");
		VerificationLog(verificationChannel, $"type=gang-money-in day={now.days} sellerActor={proposal.SellerActor?.ActorKey} buyerActor={proposal.BuyerActor?.ActorKey} sellerGang={seller.PID.id} buyerGang={buyer.PID.id} clean={AI_PACT_TRADE_LAUNDER_CLEAN_COST} dirty={num}");
			return true;
		}

	private static bool TryExecuteAiGangResourceTrade(AiTradeProposal proposal, SimTime now, AlliancePact pact, string verificationChannel, bool externalNetwork, string tradeKey, IReadOnlyList<string> labels, int requestedQty, int cleanCost, string finishBuffId, string flavorBuffId, float moodDelta)
		{
			PlayerInfo seller = proposal?.Seller;
			PlayerInfo buyer = proposal?.Buyer;
			if (seller == null || buyer == null || !TryTransferCleanCashBetweenGangs(buyer, seller, cleanCost, AI_PACT_TRADE_MIN_CLEAN_RESERVE))
			{
				return false;
			}
			if (!TryTransferGangSafehouseResources(seller, buyer, labels, requestedQty, out int moved, out string movedSummary))
			{
				TryTransferCleanCashBetweenGangs(seller, buyer, cleanCost, 0);
				return false;
		}
		bool logRelationshipBuffs = ShouldLogAiTradeRelationshipBuffs(seller, buyer);
		AddMutualRelationshipBuff(seller, buyer, finishBuffId, GetCrewPeepForPlayer(seller), GetCrewPeepForPlayer(buyer), logRelationshipBuffs);
		AddMutualRelationshipBuff(seller, buyer, flavorBuffId, GetCrewPeepForPlayer(seller), GetCrewPeepForPlayer(buyer), logRelationshipBuffs);
		ApplyPactTradeRelationshipBuff(seller, buyer, logRelationshipBuffs);
		ApplyAiTradeMoodDelta(proposal, pact, moodDelta);
		string text = string.Equals(tradeKey, "gang-trade-drugs-low", StringComparison.Ordinal) ? "drug" : "liquor";
		AwardHumanPactTradeStreetCredit(seller, buyer, string.Equals(tradeKey, "gang-trade-drugs-low", StringComparison.Ordinal) ? "pact-trade-drugs" : "pact-trade-liquor", 0.03f, 0.06f);
		LogGrapevine($"{GetAiTradeGrapevinePrefix(externalNetwork)}: {GetAiTradeActorDisplayName(proposal.BuyerActor, buyer)} stocked up on {text} from {GetAiTradeActorDisplayName(proposal.SellerActor, seller)}.");
		VerificationLog(verificationChannel, $"type={tradeKey} day={now.days} sellerActor={proposal.SellerActor?.ActorKey} buyerActor={proposal.BuyerActor?.ActorKey} sellerGang={seller.PID.id} buyerGang={buyer.PID.id} clean={cleanCost} moved={moved} summary={movedSummary}");
		return true;
	}

	private static bool TryRunAiFrontClosureBeforeRobbery(AiTradeProposal proposal, SimTime now, string verificationChannel, bool externalNetwork)
	{
		try
		{
			PlayerInfo robber = proposal?.Seller;
			PlayerInfo victim = proposal?.Buyer;
			if (robber == null
				|| victim == null
				|| robber.PID.id == victim.PID.id
				|| robber.PID.IsHumanPlayer
				|| victim.PID.IsHumanPlayer
				|| ArePlayersProtectedByPactAlliance(robber, victim)
				|| HasMutualTruce(robber, victim))
			{
				return false;
			}
			if (!TryFindRetaliationClosureTarget(robber, victim, out _, out _))
			{
				return false;
			}
			if (IsAiPersonalityPeaceful(robber))
			{
				VerificationLog(verificationChannel, $"type={proposal.TradeKey} phase=front-closure-before-robbery result=blocked-peaceful-personality day={now.days} robber={robber.PID.id} victim={victim.PID.id}");
				return false;
			}

			bool aggressivePersonality = IsAiPersonalityAggressive(robber);
			bool expansionistPersonality = IsAiPersonalityExpansionist(robber);
			float closureChance = expansionistPersonality ? 0.42f : (aggressivePersonality ? 0.14f : 0.28f);
			if (proposal.Score < 0.68f)
			{
				closureChance += expansionistPersonality ? 0.04f : 0.05f;
			}
			if (HasAnyGangBossTrait(robber, "trait-cautious", "trait-nervous", "trait-upright"))
			{
				closureChance += 0.04f;
			}
			if (CalculateGangPower(robber) < CalculateGangPower(victim) + 20)
			{
				closureChance += 0.04f;
			}
			closureChance = Mathf.Clamp(closureChance, aggressivePersonality ? 0.08f : 0.18f, expansionistPersonality ? 0.58f : 0.42f);
			double roll = SharedRng.NextDouble();
			if (roll >= closureChance)
			{
				VerificationLog(verificationChannel, $"type={proposal.TradeKey} phase=front-closure-before-robbery result=roll-miss day={now.days} robber={robber.PID.id} victim={victim.PID.id} chance={closureChance:0.00} roll={roll:0.00}");
				return false;
			}

			GangOpsChannel channel = ResolveGangOpsChannelForGang(robber.PID.id);
			string sourceTag = externalNetwork ? "ai-robbery-pre-front" : "pact-robbery-pre-front";
			if (!TryForceCloseRetaliationBusiness(channel, robber, victim, sourceTag, out string actionSummary))
			{
				VerificationLog(verificationChannel, $"type={proposal.TradeKey} phase=front-closure-before-robbery result=unavailable day={now.days} robber={robber.PID.id} victim={victim.PID.id} chance={closureChance:0.00} roll={roll:0.00}");
				return false;
			}

			AddAiRobberyWarHeatWithDiagnostic(
				channel,
				victim.PID.id,
				robber.PID.id,
				robber,
				victim,
				AI_GANG_ROBBERY_PRE_FRONT_HEAT_GAIN,
				sourceTag,
				"front-closure-before-robbery",
				proposal.TradeKey,
				cash: 0,
				success: false,
				highValue: string.Equals(proposal.TradeKey, "gang-robbery-high", StringComparison.Ordinal),
				externalNetwork: externalNetwork,
				direction: "victim-to-robber");
			RecordAiGangRobberyPairCooldown(robber, victim, now);
			LogGrapevine($"{(externalNetwork ? "ROBBERY" : "PACT")}: {GetGangDisplayName(robber.PID.id)} skipped the shakedown and leaned on one of {GetGangDisplayName(victim.PID.id)}'s fronts instead.");
			VerificationLog(verificationChannel, $"type={proposal.TradeKey} phase=front-closure-before-robbery result=closed day={now.days} robber={robber.PID.id} victim={victim.PID.id} action={actionSummary} chance={closureChance:0.00} roll={roll:0.00} cooldownDays={AI_ROBBERY_GANG_PAIR_COOLDOWN_DAYS}");
			return true;
		}
		catch (Exception ex)
		{
			Debug.LogWarning("[GameplayTweaks] TryRunAiFrontClosureBeforeRobbery failed: " + ex.Message);
			return false;
		}
	}

	private static void AddAiRobberyWarHeatWithDiagnostic(
		GangOpsChannel channel,
		int heatSourcePid,
		int heatTargetPid,
		PlayerInfo robber,
		PlayerInfo victim,
		float heatGain,
		string reason,
		string phase,
		string tradeKey,
		int cash,
		bool success,
		bool highValue,
		bool externalNetwork,
		string direction)
	{
		bool heatApplied = false;
		try
		{
			if (heatSourcePid < 0 || heatTargetPid < 0 || heatSourcePid == heatTargetPid || heatGain <= 0f)
			{
				return;
			}
			float directionalHeatBefore = GetWarHeat(heatSourcePid, heatTargetPid);
			WarStanceSnapshot pairBefore = ResolveWarStanceSnapshot(heatSourcePid, heatTargetPid);
			AddWarHeat(channel, heatSourcePid, heatTargetPid, heatGain, reason);
			heatApplied = true;
			float directionalHeatAfter = GetWarHeat(heatSourcePid, heatTargetPid);
			WarStanceSnapshot pairAfter = ResolveWarStanceSnapshot(heatSourcePid, heatTargetPid);
			VerificationLog(
				"WarStance",
				$"[WarStance][AiRobberyHeat] phase={phase} trade={tradeKey ?? "unknown"} channel={GetGangOpsChannelTag(channel)} robberPid={(robber != null ? robber.PID.id : -1)} victimPid={(victim != null ? victim.PID.id : -1)} victimHuman={victim != null && victim.PID.IsHumanPlayer} cash={cash} highValue={highValue} success={success} external={externalNetwork} heat=+{heatGain:0.0} reason={reason} direction={direction} heatSourcePid={heatSourcePid} heatTargetPid={heatTargetPid} directionalHeatBefore={directionalHeatBefore:0.0} directionalHeatAfter={directionalHeatAfter:0.0} effectiveHeatBefore={pairBefore.EffectiveHeat:0.0} effectiveHeatAfter={pairAfter.EffectiveHeat:0.0} stanceBefore={FormatWarWeaponStance(pairBefore.Stance)} stanceAfter={FormatWarWeaponStance(pairAfter.Stance)}");
		}
		catch (Exception ex)
		{
			Debug.LogWarning("[GameplayTweaks] AddAiRobberyWarHeatWithDiagnostic failed: " + ex.Message);
			if (!heatApplied)
			{
				AddWarHeat(channel, heatSourcePid, heatTargetPid, heatGain, reason);
			}
		}
	}

	private static bool TryExecuteAiGangRobbery(AiTradeProposal proposal, SimTime now, string verificationChannel, bool externalNetwork)
	{
		PlayerInfo robber = proposal?.Seller;
		PlayerInfo victim = proposal?.Buyer;
		if (robber == null || victim == null || robber.PID.id == victim.PID.id || robber.PID.IsHumanPlayer)
		{
			return false;
		}
		if (victim.PID.IsHumanPlayer && !AI_ROBBERY_HUMAN_CASH_MUTATION_ENABLED)
		{
			VerificationLog("AIPlayerRobbery", $"blocked phase=diagnostic-only reason=human-cash-mutation-disabled robber={robber.PID.id} victim={victim.PID.id} trade={proposal.TradeKey} day={now.days}");
			return false;
		}
		if (ArePlayersProtectedByPactAlliance(robber, victim) || HasMutualTruce(robber, victim))
		{
			return false;
		}
		if (IsAiPersonalityPeaceful(robber))
		{
			VerificationLog(verificationChannel, $"type={proposal.TradeKey} result=blocked reason=peaceful-personality day={now.days} robber={robber.PID.id} victim={victim.PID.id}");
			return false;
		}
		if (TryGetAiGangRobberyPairCooldown(robber, victim, now, out int pairCooldownUntilDay))
		{
			VerificationLog(verificationChannel, $"type={proposal.TradeKey} result=blocked reason=gang-robbery-cooldown day={now.days} robber={robber.PID.id} victim={victim.PID.id} untilDay={pairCooldownUntilDay} cooldownDays={AI_ROBBERY_GANG_PAIR_COOLDOWN_DAYS}");
			return false;
		}
		bool highValue = string.Equals(proposal.TradeKey, "gang-robbery-high", StringComparison.Ordinal);
		int cash = RollAiGangRobberyCash(victim, highValue);
		if (cash <= 0)
		{
			return false;
		}
		float successChance = CalculateAiGangRobberySuccessChance(robber, victim, highValue);
		bool success = SharedRng.NextDouble() < successChance;
		GangOpsChannel channel = ResolveGangOpsChannelForGang(victim.PID.IsHumanPlayer ? robber.PID.id : victim.PID.id);
		float heatGain = success
			? (highValue ? AI_GANG_ROBBERY_SUCCESS_HIGH_HEAT_GAIN : AI_GANG_ROBBERY_SUCCESS_LOW_HEAT_GAIN)
			: (highValue ? AI_GANG_ROBBERY_FAILED_HIGH_HEAT_GAIN : AI_GANG_ROBBERY_FAILED_LOW_HEAT_GAIN);
		string robberName = GetGangDisplayName(robber.PID.id);
		string victimName = victim.PID.IsHumanPlayer ? "your outfit" : GetGangDisplayName(victim.PID.id);

		if (success)
		{
			if (!TryTransferCleanCashBetweenGangs(victim, robber, cash, victim.PID.IsHumanPlayer ? 0 : AI_ROBBERY_MIN_CASH_RESERVE))
			{
				return false;
			}
			AddDirectedRelationshipBuff(victim, robber, "relbuff-gangs-robbery2-table-on-finish", GetCrewPeepForPlayer(victim), ShouldLogAiTradeRelationshipBuffs(victim, robber));
			AddAiRobberyWarHeatWithDiagnostic(
				channel,
				victim.PID.id,
				robber.PID.id,
				robber,
				victim,
				heatGain,
				victim.PID.IsHumanPlayer ? "ai-robbery-player-success" : "ai-robbery-success",
				"gang-robbery-success",
				proposal.TradeKey,
				cash,
				success: true,
				highValue: highValue,
				externalNetwork: externalNetwork,
				direction: "victim-to-robber");
			LogGrapevine($"{(externalNetwork ? "ROBBERY" : "PACT")}: {robberName} robbed {victimName} for ${cash}. {victimName} is angry, but no shots were fired.");
			RecordAiGangRobberyPairCooldown(robber, victim, now);
			VerificationLog(verificationChannel, $"type={proposal.TradeKey} result=success day={now.days} robber={robber.PID.id} victim={victim.PID.id} cash={cash} chance={successChance:0.00} heat={heatGain:0.0} cooldownDays={AI_ROBBERY_GANG_PAIR_COOLDOWN_DAYS}");
			return true;
		}

		AddDirectedRelationshipBuff(victim, robber, "relbuff-gangs-robbery1-buff", GetCrewPeepForPlayer(victim), ShouldLogAiTradeRelationshipBuffs(victim, robber));
		AddAiRobberyWarHeatWithDiagnostic(
			channel,
			victim.PID.id,
			robber.PID.id,
			robber,
			victim,
			heatGain,
			victim.PID.IsHumanPlayer ? "ai-robbery-player-failed" : "ai-robbery-failed",
			"gang-robbery-failed",
			proposal.TradeKey,
			cash,
			success: false,
			highValue: highValue,
			externalNetwork: externalNetwork,
			direction: "victim-to-robber");
		ActivateWarBetweenPlayers(victim, robber);
		bool dispatched = false;
		int dispatchedCount = 0;
		int approachCount = 0;
		bool retryQueued = false;
		if (victim.PID.IsHumanPlayer)
		{
			dispatched = TryDispatchRuntimeGangAttack(robber, victim, highValue ? 2 : 1, "ai-robbery-player-failed", GetCrewPeepForPlayer(victim), out dispatchedCount, out approachCount);
		}
		else
		{
			EntityID robberTargetPeep = GetCrewPeepForPlayer(robber);
			dispatched = TryTriggerRobberyFailureRetaliation(channel, victim, robber, highValue ? 2 : 1, "ai-robbery-failed", robberTargetPeep, out dispatchedCount, out string retaliationAction);
			if (!dispatched)
			{
				int retryDueDay = GetRuntimeGangAttackApproachRetryDueDay(now.days);
				retryQueued = QueueImmediateRevengeIfEligible(channel, victim.PID.id, robber.PID.id, robberTargetPeep.IsValid ? (long)robberTargetPeep.id : 0L, retryDueDay, "ai-robbery-failed-retaliation-retry");
				VerificationLog(verificationChannel, $"type={proposal.TradeKey} phase=failed-retaliation-retry day={now.days} robber={robber.PID.id} victim={victim.PID.id} action={retaliationAction} retryQueued={retryQueued} retryDueDay={retryDueDay}");
			}
		}
		string attackText = victim.PID.IsHumanPlayer ? $"{robberName} started shooting when the job went bad." : $"{victimName} struck back.";
		LogGrapevine($"{(externalNetwork ? "ROBBERY" : "PACT")}: {robberName} tried to rob {victimName}, but the job failed. {attackText}");
		RecordAiGangRobberyPairCooldown(robber, victim, now);
		VerificationLog(verificationChannel, $"type={proposal.TradeKey} result=failed day={now.days} robber={robber.PID.id} victim={victim.PID.id} chance={successChance:0.00} heat={heatGain:0.0} attack={dispatched} crews={dispatchedCount} approachCrews={approachCount} retryQueued={retryQueued} cooldownDays={AI_ROBBERY_GANG_PAIR_COOLDOWN_DAYS}");
		return true;
	}

	private static bool TryGetAiGangRobberyPairCooldown(PlayerInfo robber, PlayerInfo victim, SimTime now, out int untilDay)
	{
		untilDay = int.MinValue;
		string key = GetAiGangRobberyPairCooldownKey(robber, victim);
		if (string.IsNullOrEmpty(key))
		{
			return false;
		}
		if (!_aiGangRobberyCooldownUntilDayByPair.TryGetValue(key, out int candidateUntilDay))
		{
			return false;
		}
		if (candidateUntilDay >= now.days)
		{
			untilDay = candidateUntilDay;
			return true;
		}
		_aiGangRobberyCooldownUntilDayByPair.Remove(key);
		return false;
	}

	private static void RecordAiGangRobberyPairCooldown(PlayerInfo robber, PlayerInfo victim, SimTime now)
	{
		string key = GetAiGangRobberyPairCooldownKey(robber, victim);
		if (!string.IsNullOrEmpty(key))
		{
			_aiGangRobberyCooldownUntilDayByPair[key] = now.days + AI_ROBBERY_GANG_PAIR_COOLDOWN_DAYS;
		}
	}

	private static string GetAiGangRobberyPairCooldownKey(PlayerInfo robber, PlayerInfo victim)
	{
		if (robber == null || victim == null)
		{
			return string.Empty;
		}
		int first = Math.Min(robber.PID.id, victim.PID.id);
		int second = Math.Max(robber.PID.id, victim.PID.id);
		return "pair:" + first.ToString(CultureInfo.InvariantCulture) + ":" + second.ToString(CultureInfo.InvariantCulture);
	}

	private static int RollAiGangRobberyCash(PlayerInfo victim, bool highValue)
	{
		int clean = GetGangCleanCash(victim);
		int min = highValue ? AI_ROBBERY_HIGH_CASH_MIN : AI_ROBBERY_LOW_CASH_MIN;
		int max = highValue ? AI_ROBBERY_HIGH_CASH_MAX : AI_ROBBERY_LOW_CASH_MAX;
		int affordableMax = Mathf.Max(0, clean - (victim != null && victim.PID.IsHumanPlayer ? 0 : AI_ROBBERY_MIN_CASH_RESERVE));
		if (affordableMax < min)
		{
			return 0;
		}
		max = Math.Min(max, affordableMax);
		return SharedRng.Next(min / 50, max / 50 + 1) * 50;
	}

	private static float CalculateAiGangRobberySuccessChance(PlayerInfo robber, PlayerInfo victim, bool highValue)
	{
		float chance = highValue ? 0.58f : 0.72f;
		int robberPower = CalculateGangPower(robber);
		int victimPower = CalculateGangPower(victim);
		chance += Mathf.Clamp((robberPower - victimPower) / 280f, -0.2f, 0.18f);
		if (HasAnyGangBossTrait(robber, "trait-aggressive", "trait-vindictive", "trait-bold", "trait-confident"))
		{
			chance += 0.07f;
		}
		if (IsAiPersonalityAggressive(robber))
		{
			chance += 0.08f;
		}
		if (IsAiPersonalityExpansionist(robber))
		{
			chance += 0.03f;
		}
		if (HasAnyGangBossTrait(victim, "trait-cautious", "trait-connected", "trait-aggressive", "trait-vindictive"))
		{
			chance -= 0.08f;
		}
		if (victim?.PID.IsHumanPlayer == true)
		{
			chance -= 0.08f;
		}
		return Mathf.Clamp(chance, 0.2f, 0.9f);
	}

	private static void RunAiHumanRobberyContactPhase(PlayerInfo humanPlayer, SimTime now)
	{
		try
		{
			if (humanPlayer == null || _lastAiHumanRobberyContactScanDay == now.days)
			{
				return;
			}
			_lastAiHumanRobberyContactScanDay = now.days;
			ProcessPendingAiHumanRobberyContacts(humanPlayer, now);
			TryRunRareAiRobberyAgainstHuman(humanPlayer, now, "human-turn-contact", logAllCandidates: false);
		}
		catch (Exception ex)
		{
			Debug.LogWarning("[GameplayTweaks] RunAiHumanRobberyContactPhase failed: " + ex.Message);
		}
	}

	internal static void TryRunAiHumanRobberyContactScanNow(string source, bool logAllCandidates = false)
	{
		try
		{
			int frame = Time.frameCount;
			if (_lastAiHumanRobberyArrivalScanFrame == frame)
			{
				return;
			}
			PlayerInfo humanPlayer = G.GetHumanPlayer();
			if (humanPlayer == null || humanPlayer.crew == null || humanPlayer.crew.IsCrewDefeated)
			{
				return;
			}
			_lastAiHumanRobberyArrivalScanFrame = frame;
			SimTime now = G.GetNow();
			ProcessPendingAiHumanRobberyContacts(humanPlayer, now);
			TryRunRareAiRobberyAgainstHuman(humanPlayer, now, source ?? "human-contact-scan", logAllCandidates);
		}
		catch (Exception ex)
		{
			Debug.LogWarning("[GameplayTweaks] TryRunAiHumanRobberyContactScanNow failed: " + ex.Message);
		}
	}

	private static void TryRunAiHumanRobberyContactScanForActor(PlayerInfo robber, SimTime now, string source)
	{
		try
		{
			PlayerInfo humanPlayer = G.GetHumanPlayer();
			if (!IsEligibleAiTradeGang(robber) || humanPlayer == null || humanPlayer.crew == null || humanPlayer.crew.IsCrewDefeated)
			{
				return;
			}
			if (_lastAiHumanRobberyAiTurnScanDayByPid.TryGetValue(robber.PID.id, out int lastDay) && lastDay == now.days)
			{
				return;
			}
			_lastAiHumanRobberyAiTurnScanDayByPid[robber.PID.id] = now.days;
			ProcessPendingAiHumanRobberyContacts(humanPlayer, now);
			if (!TryBuildAiRobberyCandidateAgainstHuman(robber, humanPlayer, now, out AiRobberyCandidate candidate, out string blockReason, out bool hostile, out bool nearby))
			{
				if (hostile || nearby || ShouldLogAiHumanRobberyTrespassDiscovery(now, logAllCandidates: false))
				{
					VerificationLog("AIPlayerRobbery", $"blocked phase=ai-turn-contact source={source} robber={robber.PID.id} day={now.days} hostile={hostile} nearby={nearby} reason={blockReason}");
				}
				return;
			}

			AiRobberyResolutionPreview preview = BuildAiHumanRobberyResolutionPreview(humanPlayer, candidate);
			int minCash = GetAiHumanRobberyMinimumCash(candidate);
			bool cashBelowMinimum = preview.AvailableCash < minCash;
			VerificationLog(
				"AIPlayerRobbery",
				$"candidate phase=ai-turn-contact source={source} robber={candidate.Robber.PID.id} targetCrew={candidate.TargetCrew.peepId.id} vehicle={candidate.TargetCrew.VehicleID.id} node={candidate.ContactNode?.id ?? NodeID.INVALID} dist={candidate.Distance:0.0} sameNode={candidate.SameNode} enemyTerritory={candidate.EnemyTerritory} humanTerritory={candidate.HumanTerritory} strictTrespass={candidate.StrictTrespassNode} territoryPressure={candidate.TerritoryPressure} power={candidate.RobberPower}/{candidate.HumanPower} local={candidate.RobberLocalPower}/{candidate.HumanLocalPower} availableCash={preview.AvailableCash} vehicleCash={preview.VehicleCash} safehouseCash={preview.SafehouseCash} totalCash={preview.TotalCash} minCash={minCash} cashBelowMin={cashBelowMinimum} reason={candidate.Reason}");
			ProcessAiHumanRobberyCandidateContact(candidate, humanPlayer, now, source, cashBelowMinimum);
		}
		catch (Exception ex)
		{
			Debug.LogWarning("[GameplayTweaks] TryRunAiHumanRobberyContactScanForActor failed: " + ex.Message);
		}
	}

	private static void TryRunRareAiRobberyAgainstHuman(PlayerInfo humanPlayer, SimTime now, string source = "external-trade-cycle", bool logAllCandidates = true)
	{
		try
		{
			if (humanPlayer == null || humanPlayer.crew == null || humanPlayer.crew.IsCrewDefeated)
			{
				return;
			}

			AiRobberyResolutionPreview humanCashBasis = BuildAiHumanRobberyResolutionPreview(humanPlayer, null);
			// A no-candidate preview has no target vehicle, so vehicle-robbable cash is decided per candidate below.
			int humanCash = Math.Max(humanCashBasis.TotalCash, humanCashBasis.SafehouseCash);
			bool humanCashBelowMinimum = humanCash < AI_ROBBERY_LOW_CASH_MIN;
			bool allowContactCashProbe = IsAiHumanRobberyContactScanSource(source);
			if (humanCashBelowMinimum && logAllCandidates && !allowContactCashProbe)
			{
				VerificationLog("AIPlayerRobbery", $"blocked phase=eligibility-only source={source} reason=low-total-cash cash={humanCash} availableCash={humanCashBasis.AvailableCash} safehouseCash={humanCashBasis.SafehouseCash} vehicleCash={humanCashBasis.VehicleCash} totalCash={humanCashBasis.TotalCash} min={AI_ROBBERY_LOW_CASH_MIN} day={now.days}");
				return;
			}
			if (humanCashBelowMinimum && !allowContactCashProbe && ShouldLogAiHumanRobberyTrespassDiscovery(now, logAllCandidates))
			{
				_lastAiHumanRobberyTrespassDiscoveryLogDay = now.days;
				VerificationLog("AIPlayerRobbery", $"discovery phase=trespass-scan source={source} day={now.days} scanned=0 hostile=0 nearby=0 eligible=0 humanCash={humanCash} safehouseCash={humanCashBasis.SafehouseCash} totalCash={humanCashBasis.TotalCash} cashBelowMin=True blocked=low-total-cash:1");
				return;
			}

			List<PlayerInfo> gangs = G.GetAllPlayers()
				.Where(IsEligibleAiTradeGang)
				.ToList();
			if (gangs.Count == 0)
			{
				if (ShouldLogAiHumanRobberyTrespassDiscovery(now, logAllCandidates))
				{
					_lastAiHumanRobberyTrespassDiscoveryLogDay = now.days;
					VerificationLog("AIPlayerRobbery", $"discovery phase=trespass-scan source={source} day={now.days} scanned=0 hostile=0 nearby=0 eligible=0 humanCash={humanCash} safehouseCash={humanCashBasis.SafehouseCash} totalCash={humanCashBasis.TotalCash} cashBelowMin={humanCashBelowMinimum} blocked=no-eligible-gangs:1");
				}
				return;
			}

			List<AiRobberyCandidate> candidates = new List<AiRobberyCandidate>(AI_ROBBERY_DIAGNOSTIC_MAX_CANDIDATES_PER_RUN);
			Dictionary<string, int> blockedByReason = new Dictionary<string, int>(StringComparer.Ordinal);
			int hostileGangs = 0;
			int nearbyGangs = 0;
			foreach (PlayerInfo gang in gangs)
			{
				if (TryBuildAiRobberyCandidateAgainstHuman(gang, humanPlayer, now, out AiRobberyCandidate candidate, out string blockReason, out bool hostile, out bool nearby))
				{
					candidates.Add(candidate);
					continue;
				}
				if (hostile)
				{
					hostileGangs++;
				}
				if (nearby)
				{
					nearbyGangs++;
				}
				if (!string.IsNullOrWhiteSpace(blockReason))
				{
					blockedByReason.TryGetValue(blockReason, out int count);
					blockedByReason[blockReason] = count + 1;
				}
			}

			List<AiRobberyCandidate> orderedCandidates = candidates
				.OrderByDescending(c => c.WouldAttempt)
				.ThenBy(c => c.Distance)
				.ThenByDescending(c => c.RobberPower - c.HumanPower)
				.Take(AI_ROBBERY_DIAGNOSTIC_MAX_CANDIDATES_PER_RUN)
				.ToList();
			HashSet<AiRobberyCandidate> sameNodeMissCandidatesToLog = new HashSet<AiRobberyCandidate>();
			foreach (AiRobberyCandidate candidate in orderedCandidates)
			{
				if (!logAllCandidates && !candidate.WouldAttempt && ShouldLogAiHumanRobberySameNodeMiss(candidate, now, record: false))
				{
					sameNodeMissCandidatesToLog.Add(candidate);
				}
			}

			string blockedSummary = blockedByReason.Count == 0
				? "none"
				: string.Join(",", blockedByReason.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key).Take(4).Select(kv => kv.Key + ":" + kv.Value));
			bool shouldLogTrespassDiscovery = ShouldLogAiHumanRobberyTrespassDiscovery(now, logAllCandidates);
			bool shouldLogDiscovery = logAllCandidates || orderedCandidates.Any(candidate => candidate.WouldAttempt) || sameNodeMissCandidatesToLog.Count > 0 || shouldLogTrespassDiscovery;
			if (shouldLogDiscovery)
			{
				if (shouldLogTrespassDiscovery)
				{
					_lastAiHumanRobberyTrespassDiscoveryLogDay = now.days;
				}
				VerificationLog(
					"AIPlayerRobbery",
					$"discovery phase=trespass-scan source={source} day={now.days} scanned={gangs.Count} hostile={hostileGangs + candidates.Count} nearby={nearbyGangs + candidates.Count} eligible={candidates.Count} humanCash={humanCash} safehouseCash={humanCashBasis.SafehouseCash} totalCash={humanCashBasis.TotalCash} cashBelowMin={humanCashBelowMinimum} blocked={blockedSummary}");
				if (candidates.Count == 0
					&& (logAllCandidates || shouldLogTrespassDiscovery)
					&& blockedByReason.TryGetValue("no-human-trespass", out int noTrespassCount)
					&& noTrespassCount > 0)
				{
					LogAiHumanRobberyNoTrespassProbe(humanPlayer, gangs, now, source);
				}
			}

			int processedContacts = 0;
			foreach (AiRobberyCandidate candidate in orderedCandidates)
			{
				bool logSameNodeMiss = sameNodeMissCandidatesToLog.Contains(candidate) && ShouldLogAiHumanRobberySameNodeMiss(candidate, now, record: true);
				AiRobberyResolutionPreview candidateCashBasis = BuildAiHumanRobberyResolutionPreview(humanPlayer, candidate);
				int candidateMinCash = GetAiHumanRobberyMinimumCash(candidate);
				bool candidateCashBelowMinimum = candidateCashBasis.AvailableCash < candidateMinCash;
				if (logAllCandidates || candidate.WouldAttempt || logSameNodeMiss)
				{
					VerificationLog(
						"AIPlayerRobbery",
						$"candidate phase=eligibility-only source={source} robber={candidate.Robber.PID.id} targetCrew={candidate.TargetCrew.peepId.id} vehicle={candidate.TargetCrew.VehicleID.id} node={candidate.ContactNode?.id ?? NodeID.INVALID} dist={candidate.Distance:0.0} sameNode={candidate.SameNode} robberNodeSource={candidate.RobberNodeSource ?? "none"} targetNodeSource={candidate.TargetNodeSource ?? "none"} directAggro={candidate.DirectAggro} broadHostile={candidate.BroadlyHostile} enemyTerritory={candidate.EnemyTerritory} humanTerritory={candidate.HumanTerritory} strictTrespass={candidate.StrictTrespassNode} territoryPressure={candidate.TerritoryPressure} recentTrespass={candidate.RecentTrespassMemory} power={candidate.RobberPower}/{candidate.HumanPower} local={candidate.RobberLocalPower}/{candidate.HumanLocalPower} rarity={candidate.RarityRoll:0.00}/{candidate.RarityChance:0.00} wouldAttempt={candidate.WouldAttempt} cashBelowMin={candidateCashBelowMinimum} minCash={candidateMinCash} availableCash={candidateCashBasis.AvailableCash} safehouseCash={candidateCashBasis.SafehouseCash} vehicleCash={candidateCashBasis.VehicleCash} totalCash={candidateCashBasis.TotalCash} reason={candidate.Reason}");
				}
				if (logSameNodeMiss)
				{
					LogAiHumanRobberySameNodeMissPreview(candidate, humanPlayer, now, source);
				}
				if (TryGetAiHumanRobberyDiagnosticCooldown(candidate, now, out string cooldownKey, out int cooldownUntilDay))
				{
					if (logAllCandidates || candidate.WouldAttempt)
					{
						VerificationLog(
							"AIPlayerRobbery",
							$"blocked phase=contact-cooldown source={source} robber={candidate.Robber.PID.id} targetCrew={candidate.TargetCrew.peepId.id} vehicle={candidate.TargetCrew.VehicleID.id} node={candidate.ContactNode?.id ?? NodeID.INVALID} cooldown={cooldownKey} untilDay={cooldownUntilDay} result=skipped");
					}
					continue;
				}
				ProcessAiHumanRobberyCandidateContact(candidate, humanPlayer, now, source, candidateCashBelowMinimum);
				if (IsAiHumanRobberyImmediateContactCandidate(candidate)
					&& ++processedContacts >= AI_ROBBERY_MAX_CONTACTS_PER_SCAN)
				{
					break;
				}
			}
		}
		catch (Exception ex)
		{
			Debug.LogWarning("[GameplayTweaks] TryRunRareAiRobberyAgainstHuman failed: " + ex.Message);
		}
	}

	private static bool IsAiHumanRobberyContactScanSource(string source)
	{
		return !string.IsNullOrWhiteSpace(source)
			&& (source.StartsWith("human-", StringComparison.Ordinal)
				|| string.Equals(source, "ai-turn-contact", StringComparison.Ordinal)
				|| string.Equals(source, "pending-contact", StringComparison.Ordinal));
	}

	private static int GetAiHumanRobberyMinimumCash(AiRobberyCandidate candidate)
	{
		if (candidate != null
			&& (candidate.StrictTrespassNode
				|| candidate.TerritoryPressure
				|| candidate.EnemyTerritory
				|| candidate.HumanTerritory))
		{
			return AI_ROBBERY_CONTACT_LOW_CASH_MIN;
		}
		return AI_ROBBERY_LOW_CASH_MIN;
	}

	private static bool IsAiHumanRobberyImmediateContactCandidate(AiRobberyCandidate candidate)
	{
		return candidate != null
			&& candidate.WouldAttempt
			&& (candidate.StrictTrespassNode || candidate.HumanTerritory || candidate.TerritoryPressure);
	}

	private static bool ShouldLogAiHumanRobberyTrespassDiscovery(SimTime now, bool logAllCandidates)
	{
		return !logAllCandidates
			&& now.days - _lastAiHumanRobberyTrespassDiscoveryLogDay >= AI_ROBBERY_TRESPASS_DISCOVERY_LOG_DAYS;
	}

	private static void ProcessAiHumanRobberyCandidateContact(AiRobberyCandidate candidate, PlayerInfo humanPlayer, SimTime now, string source, bool humanCashBelowMinimum)
	{
		if (candidate == null || candidate.Robber == null || humanPlayer == null || !candidate.TargetCrew.IsValid || !candidate.WouldAttempt)
		{
			return;
		}
		if (TryGetAiHumanRobberyDiagnosticCooldown(candidate, now, out string cooldownKey, out int cooldownUntilDay))
		{
			VerificationLog(
				"AIPlayerRobbery",
				$"blocked phase=contact-cooldown source={source} robber={candidate.Robber.PID.id} targetCrew={candidate.TargetCrew.peepId.id} vehicle={candidate.TargetCrew.VehicleID.id} node={candidate.ContactNode?.id ?? NodeID.INVALID} cooldown={cooldownKey} untilDay={cooldownUntilDay} result=skipped");
			return;
		}
		string key = GetAiHumanRobberyContactKey(candidate.Robber.PID.id, candidate.TargetCrew.peepId.id);
		if (humanCashBelowMinimum)
		{
			LogAiHumanRobberyLowCashContactPreview(candidate, humanPlayer, now, source, candidate.SameNode ? "same-node-low-cash" : "contact-low-cash");
			return;
		}
		if (candidate.StrictTrespassNode || candidate.HumanTerritory || candidate.TerritoryPressure)
		{
			if (_pendingAiHumanRobberyContacts.Remove(key))
			{
				VerificationLog(
					"AIPlayerRobbery",
					$"cleared phase=trespass-direct source={source} robber={candidate.Robber.PID.id} targetCrew={candidate.TargetCrew.peepId.id} reason=strict-trespass-overrides-shadow");
			}
			string mode = candidate.StrictTrespassNode
				? (candidate.SameNode ? "trespass-same-node" : "trespass-direct")
				: (candidate.HumanTerritory
					? (candidate.SameNode ? "human-territory-same-node" : "human-territory-contact")
					: "territory-pressure-contact");
			VerificationLog(
				"AIPlayerRobbery",
				$"contact phase=trespass-direct source={source} robber={candidate.Robber.PID.id} targetCrew={candidate.TargetCrew.peepId.id} vehicle={candidate.TargetCrew.VehicleID.id} node={candidate.ContactNode?.id ?? NodeID.INVALID} dist={candidate.Distance:0.0} sameNode={candidate.SameNode} strictTrespass={candidate.StrictTrespassNode} humanTerritory={candidate.HumanTerritory} territoryPressure={candidate.TerritoryPressure} result=immediate");
			LogAiHumanRobberyResolutionPreview(candidate, humanPlayer, now, source, mode, -1);
			return;
		}
		if (_pendingAiHumanRobberyContacts.ContainsKey(key))
		{
			return;
		}
		int graceDays = Math.Max(AI_ROBBERY_TRESPASS_GRACE_FAST_DAYS, candidate.TrespassGraceDays);
		_pendingAiHumanRobberyContacts[key] = new AiRobberyPendingContact
		{
			RobberPid = candidate.Robber.PID.id,
			TargetCrewPeepId = (long)candidate.TargetCrew.peepId.id,
			TargetVehicleId = candidate.TargetCrew.VehicleID.IsValid ? (long)candidate.TargetCrew.VehicleID.id : 0L,
			TargetNodeIndex = candidate.ContactNode?.id.index ?? 0,
			CreatedDay = now.days,
			ContactDay = now.days + graceDays,
			GraceDays = graceDays,
			ExpireDay = now.days + AI_ROBBERY_PENDING_CONTACT_EXPIRE_DAYS,
			Source = source ?? string.Empty,
			WarningShown = false
		};
		bool warningShown = TryLogAiHumanRobberyApproachWarning(candidate, now, source);
		_pendingAiHumanRobberyContacts[key].WarningShown = warningShown;
		VerificationLog(
			"AIPlayerRobbery",
			$"pending phase=trespass-grace source={source} robber={candidate.Robber.PID.id} targetCrew={candidate.TargetCrew.peepId.id} vehicle={candidate.TargetCrew.VehicleID.id} node={candidate.ContactNode?.id ?? NodeID.INVALID} dist={candidate.Distance:0.0} sameNode={candidate.SameNode} graceDays={graceDays} contactDay={now.days + graceDays} expireDay={now.days + AI_ROBBERY_PENDING_CONTACT_EXPIRE_DAYS} warningShown={warningShown} result=shadowing");
	}

	private static bool TryLogAiHumanRobberyApproachWarning(AiRobberyCandidate candidate, SimTime now, string source)
	{
		if (candidate == null || candidate.Robber == null || !candidate.TargetCrew.IsValid)
		{
			return false;
		}
		string key = GetAiHumanRobberyContactKey(candidate.Robber.PID.id, candidate.TargetCrew.peepId.id) + ":warning";
		if (_aiHumanRobberyApproachWarningDayByKey.TryGetValue(key, out int lastDay)
			&& now.days - lastDay < AI_ROBBERY_APPROACH_WARNING_COOLDOWN_DAYS)
		{
			return false;
		}
		_aiHumanRobberyApproachWarningDayByKey[key] = now.days;
		string robberName = GetGangDisplayName(candidate.Robber.PID.id);
		string robberyReason = FormatAiHumanRobberyReasonForPlayer(candidate);
		LogGrapevine($"ROBBERY: {robberName} has been spotted shadowing one of your crews. {robberyReason} If they catch up, they may try to shake the crew down.");
		bool tickerShown = TryShowAiHumanRobberyIntentTicker(candidate, now, source, "approach-diagnostic");
		VerificationLog(
			"AIPlayerRobbery",
			$"warning phase=approach-diagnostic source={source} robber={candidate.Robber.PID.id} targetCrew={candidate.TargetCrew.peepId.id} vehicle={candidate.TargetCrew.VehicleID.id} node={candidate.ContactNode?.id ?? NodeID.INVALID} dist={candidate.Distance:0.0} reasonText=\"{robberyReason}\" tickerShown={tickerShown} cooldownDays={AI_ROBBERY_APPROACH_WARNING_COOLDOWN_DAYS} result=shown");
		return true;
	}

	private static bool TryLogAiHumanRobberyApproachClosed(AiRobberyPendingContact pending, SimTime now, string reason)
	{
		if (pending == null || !pending.WarningShown)
		{
			return false;
		}
		string robberName = GetGangDisplayName(pending.RobberPid);
		string outcome = string.Equals(reason, "timeout", StringComparison.Ordinal)
			? "lost track of your crew"
			: "backed off";
		LogGrapevine($"ROBBERY: {robberName} {outcome}. The crew is no longer being shadowed.");
		VerificationLog(
			"AIPlayerRobbery",
			$"warning-cleared phase=approach-diagnostic robber={pending.RobberPid} targetCrew={pending.TargetCrewPeepId} vehicle={pending.TargetVehicleId} node=NID_{pending.TargetNodeIndex} createdDay={pending.CreatedDay} day={now.days} reason={reason} result=closed");
		return true;
	}

	private static bool IsAiHumanRobberyPendingStillActive(AiRobberyPendingContact pending, PlayerInfo humanPlayer, out string reason)
	{
		reason = "inactive";
		if (pending == null || humanPlayer == null)
		{
			reason = "invalid";
			return false;
		}
		PlayerInfo robber = G.FindPlayerById(pending.RobberPid);
		if (!IsEligibleAiTradeGang(robber) || humanPlayer.crew == null)
		{
			reason = "invalid-gang";
			return false;
		}
		if (ArePlayersProtectedByPactAlliance(robber, humanPlayer))
		{
			reason = "pact-protected";
			return false;
		}
		if (HasMutualTruce(robber, humanPlayer))
		{
			reason = "truce";
			return false;
		}
		if (IsAiHumanRobberyBlockedByGoodRelations(robber, humanPlayer, out _))
		{
			reason = "friendly-relations";
			return false;
		}
		if (!TryFindAiRobberyTrespassContact(robber, humanPlayer, out CrewAssignment targetCrew, out Node contactNode, out _, out bool sameNode, out int robberLocalPower, out int humanLocalPower, out _, out _, out bool strictTrespassNode, out bool territoryPressure, out bool recentTrespassMemory, out reason))
		{
			return false;
		}
		if (!targetCrew.IsValid || (long)targetCrew.peepId.id != pending.TargetCrewPeepId)
		{
			reason = "target-changed";
			return false;
		}
		int robberPower = CalculateGangPower(robber);
		int humanPower = CalculateGangPower(humanPlayer);
		PlayerID owner = contactNode != null ? PlayerTerritory.GetNodeOwner(contactNode) : PlayerID.INVALID;
		bool humanTerritory = owner == humanPlayer.PID;
		bool pressureTrespass = territoryPressure && !humanTerritory;
		if (!strictTrespassNode && !pressureTrespass)
		{
			reason = territoryPressure
				? "territory-pressure-not-trespass"
				: (recentTrespassMemory ? "recent-trespass-not-current" : "not-trespassing");
			return false;
		}
		if (!IsAiHumanRobberyPhysicalTargetValid(humanPlayer, targetCrew, contactNode, out reason))
		{
			return false;
		}
		bool enemyTerritory = owner == robber.PID || pressureTrespass;
		if (!enemyTerritory)
		{
			reason = "not-trespassing";
			return false;
		}
		if (!PassesAiRobberyStrengthGate(enemyTerritory, robberPower, humanPower, robberLocalPower, humanLocalPower, out reason))
		{
			return false;
		}
		reason = sameNode ? "active-same-node" : "active-nearby";
		return true;
	}

	private static void ProcessPendingAiHumanRobberyContacts(PlayerInfo humanPlayer, SimTime now)
	{
		ClearStaleAiHumanRobberyResponseLocks(humanPlayer, now);
		if (_pendingAiHumanRobberyContacts.Count == 0)
		{
			return;
		}
		foreach (KeyValuePair<string, AiRobberyPendingContact> kv in _pendingAiHumanRobberyContacts.ToList())
		{
			AiRobberyPendingContact pending = kv.Value;
			if (pending == null || pending.ExpireDay < now.days)
			{
				_pendingAiHumanRobberyContacts.Remove(kv.Key);
				if (pending != null)
				{
					bool closureSuppressed = IsAiHumanRobberyPendingStillActive(pending, humanPlayer, out string closureReason);
					bool warningClosed = !closureSuppressed && TryLogAiHumanRobberyApproachClosed(pending, now, "timeout");
					VerificationLog("AIPlayerRobbery", $"expired phase=approach-diagnostic robber={pending.RobberPid} targetCrew={pending.TargetCrewPeepId} createdDay={pending.CreatedDay} expireDay={pending.ExpireDay} day={now.days} warningClosed={warningClosed} closureSuppressed={closureSuppressed} closureReason={closureReason} reason=timeout");
				}
				continue;
			}
			PlayerInfo robber = G.FindPlayerById(pending.RobberPid);
			if (!TryBuildAiRobberyCandidateAgainstHuman(robber, humanPlayer, now, out AiRobberyCandidate candidate, out string blockReason, out _, out _))
			{
				if (string.Equals(blockReason, "truce", StringComparison.Ordinal)
					|| string.Equals(blockReason, "pact-protected", StringComparison.Ordinal)
					|| string.Equals(blockReason, "friendly-relations", StringComparison.Ordinal)
					|| string.Equals(blockReason, "not-trespassing", StringComparison.Ordinal)
					|| string.Equals(blockReason, "territory-pressure-not-trespass", StringComparison.Ordinal)
					|| string.Equals(blockReason, "recent-trespass-not-current", StringComparison.Ordinal)
					|| string.Equals(blockReason, "target-not-in-vehicle", StringComparison.Ordinal)
					|| string.Equals(blockReason, "target-on-foot-not-at-node", StringComparison.Ordinal)
					|| string.Equals(blockReason, "target-not-onboard", StringComparison.Ordinal)
					|| string.Equals(blockReason, "target-vehicle-not-physical", StringComparison.Ordinal)
					|| string.Equals(blockReason, "target-vehicle-empty", StringComparison.Ordinal))
				{
					_pendingAiHumanRobberyContacts.Remove(kv.Key);
					bool warningClosed = TryLogAiHumanRobberyApproachClosed(pending, now, blockReason);
					VerificationLog("AIPlayerRobbery", $"cleared phase=approach-diagnostic robber={pending.RobberPid} targetCrew={pending.TargetCrewPeepId} reason={blockReason} day={now.days} warningClosed={warningClosed}");
				}
				continue;
			}
			if (now.days < pending.ContactDay)
			{
				continue;
			}
			if (HasActiveAiHumanRobberyResponseForHuman(humanPlayer))
			{
				VerificationLog("AIPlayerRobbery", $"deferred phase=response-popup source=pending-contact reason=human-response-active robber={pending.RobberPid} targetCrew={pending.TargetCrewPeepId} vehicle={pending.TargetVehicleId} day={now.days} result=pending-kept");
				continue;
			}

			AiRobberyResolutionPreview candidateCashBasis = BuildAiHumanRobberyResolutionPreview(humanPlayer, candidate);
			if (candidateCashBasis.AvailableCash < GetAiHumanRobberyMinimumCash(candidate))
			{
				_pendingAiHumanRobberyContacts.Remove(kv.Key);
				LogAiHumanRobberyLowCashContactPreview(candidate, humanPlayer, now, "pending-contact", candidate.SameNode ? "trespass-same-node" : "trespass-shadow");
				continue;
			}
			if (candidate.EnemyTerritory || candidate.HumanTerritory)
			{
				_pendingAiHumanRobberyContacts.Remove(kv.Key);
				LogAiHumanRobberyResolutionPreview(candidate, humanPlayer, now, "pending-contact", candidate.SameNode ? "trespass-same-node" : "trespass-shadow", pending.CreatedDay);
			}
		}
	}

	private static void LogAiHumanRobberyResolutionPreview(AiRobberyCandidate candidate, PlayerInfo humanPlayer, SimTime now, string source, string mode, int createdDay)
	{
		if (candidate == null || candidate.Robber == null || humanPlayer == null || !candidate.TargetCrew.IsValid)
		{
			return;
		}
		if (TryGetAiHumanRobberyDiagnosticCooldown(candidate, now, out string cooldownKey, out int cooldownUntilDay))
		{
			VerificationLog(
				"AIPlayerRobbery",
				$"blocked phase=resolution-preview source={source} mode={mode} reason=diagnostic-cooldown cooldown={cooldownKey} untilDay={cooldownUntilDay} robber={candidate.Robber.PID.id} targetCrew={candidate.TargetCrew.peepId.id} vehicle={candidate.TargetCrew.VehicleID.id} node={candidate.ContactNode?.id ?? NodeID.INVALID} day={now.days}");
			return;
		}

		AiRobberyResolutionPreview preview = BuildAiHumanRobberyResolutionPreview(humanPlayer, candidate);
		bool hasPromptValue = preview.AvailableCash >= GetAiHumanRobberyMinimumCash(candidate);
		if (hasPromptValue && HasActiveAiHumanRobberyResponseForHuman(humanPlayer))
		{
			VerificationLog(
				"AIPlayerRobbery",
				$"robbery-meeting-suppressed robber={candidate.Robber.PID.id} human={humanPlayer.PID.id} targetCrew={candidate.TargetCrew.peepId.id} vehicle={candidate.TargetCrew.VehicleID.id} node={candidate.ContactNode?.id ?? NodeID.INVALID} source={source} mode={mode} reason=human-response-active result=not-queued");
			return;
		}
		if (hasPromptValue)
		{
			RecordAiHumanRobberyMeetingPending(candidate, humanPlayer, now, source, mode, createdDay);
		}
		if (AI_ROBBERY_CONTACT_PLAYER_RESPONSE_ENABLED)
		{
			if (ShouldDeferAiHumanRobberyResponsePopup(source)
				&& QueueAiHumanRobberyResponseForNextHumanTurn(candidate, humanPlayer, now, source, mode, createdDay))
			{
				return;
			}
			if (TryShowAiHumanRobberyResponsePopup(candidate, humanPlayer, preview, now, source, mode, createdDay))
			{
				return;
			}
			RecordAiHumanRobberyDiagnosticCooldown(candidate, now);
			ClearPendingAiHumanRobberyMeeting(candidate.Robber, humanPlayer, "response-popup-ui-unavailable", source);
			string createdUnavailable = createdDay >= 0 ? $" createdDay={createdDay}" : string.Empty;
			VerificationLog(
				"AIPlayerRobbery",
				$"blocked phase=response-popup source={source} mode={mode} reason=ui-unavailable robber={candidate.Robber.PID.id} targetCrew={candidate.TargetCrew.peepId.id} vehicle={candidate.TargetCrew.VehicleID.id} node={candidate.ContactNode?.id ?? NodeID.INVALID}{createdUnavailable} cashPreview={preview.Amount} availableCash={preview.AvailableCash} safehouseCash={preview.SafehouseCash} vehicleCash={preview.VehicleCash} totalCash={preview.TotalCash} cooldownDays={GetAiHumanRobberyCooldownSummary()} result=no-debit");
			return;
		}
		if (AI_ROBBERY_CONTACT_VEHICLE_DEBIT_ENABLED
			&& TryResolveAiHumanVehicleRobberyContact(candidate, humanPlayer, preview, now, source, mode, createdDay))
		{
			return;
		}
		RecordAiHumanRobberyDiagnosticCooldown(candidate, now);
		string created = createdDay >= 0 ? $" createdDay={createdDay}" : string.Empty;
		VerificationLog(
			"AIPlayerRobbery",
			$"contact phase=contact-diagnostic source={source} mode={mode} robber={candidate.Robber.PID.id} targetCrew={candidate.TargetCrew.peepId.id} vehicle={candidate.TargetCrew.VehicleID.id} node={candidate.ContactNode?.id ?? NodeID.INVALID}{created} cashPreview={preview.Amount} availableCash={preview.AvailableCash} safehouseCash={preview.SafehouseCash} vehicleCash={preview.VehicleCash} peepCash={preview.PeepCash} totalCash={preview.TotalCash} canDebitSafehouse={preview.CanDebitSafehouse} canDebitCleanCash={preview.CanDebitCleanCash} canDebitVehicle={preview.CanDebitVehicle} canDebitPeep={preview.CanDebitPeep} cashReason={preview.Reason} cooldownDays={GetAiHumanRobberyCooldownSummary()} legacyMutationEnabled={AI_ROBBERY_HUMAN_CASH_MUTATION_ENABLED} vehicleDebitEnabled={AI_ROBBERY_CONTACT_VEHICLE_DEBIT_ENABLED} result=resolution-disabled");
	}

	private static bool ShouldDeferAiHumanRobberyResponsePopup(string source)
	{
		return string.IsNullOrWhiteSpace(source)
			|| !source.StartsWith("deferred-turn-start", StringComparison.Ordinal);
	}

	private static bool QueueAiHumanRobberyResponseForNextHumanTurn(AiRobberyCandidate candidate, PlayerInfo humanPlayer, SimTime now, string source, string mode, int createdDay)
	{
		if (candidate?.Robber == null || humanPlayer == null || !candidate.TargetCrew.IsValid)
		{
			return false;
		}
		string key = GetAiHumanRobberyDeferredResponseKey(candidate.Robber, humanPlayer);
		if (string.IsNullOrEmpty(key))
		{
			return false;
		}
		if (_deferredAiHumanRobberyResponsesByKey.TryGetValue(key, out DeferredAiHumanRobberyResponse existing))
		{
			if (!ShouldReplaceDeferredAiHumanRobberyResponse(existing, candidate, now, source))
			{
				int oldExpireDay = existing.ExpireDay;
				int desiredExpireDay = GetAiHumanRobberyDeferredResponseExpireDay(candidate.Robber.PID.id, humanPlayer.PID.id, Math.Max(existing.ExpireDay, now.days + AI_ROBBERY_PENDING_CONTACT_EXPIRE_DAYS));
				bool sameDeferredContact = existing.TargetCrewPeepId == (long)candidate.TargetCrew.peepId.id
					&& existing.TargetNodeIndex == (candidate.ContactNode?.id.index ?? 0)
					&& string.Equals(existing.Source ?? string.Empty, source ?? string.Empty, StringComparison.Ordinal)
					&& string.Equals(existing.Mode ?? string.Empty, mode ?? string.Empty, StringComparison.Ordinal);
				if (!sameDeferredContact || desiredExpireDay > existing.ExpireDay)
				{
					existing.ExpireDay = desiredExpireDay;
				}
				if (ShouldLogKeptDeferredAiHumanRobberyResponse(existing, now, oldExpireDay))
				{
					VerificationLog("AIPlayerRobbery", $"deferred phase=response-popup source={source} mode={mode} robber={candidate.Robber.PID.id} targetCrew={candidate.TargetCrew.peepId.id} vehicle={candidate.TargetCrew.VehicleID.id} node={candidate.ContactNode?.id ?? NodeID.INVALID} day={now.days} reason=pair-already-queued existingTarget={existing?.TargetCrewPeepId ?? 0L} existingNode=NID_{existing?.TargetNodeIndex ?? 0} existingSource={existing?.Source ?? string.Empty} expireDay={oldExpireDay}->{existing.ExpireDay} result=pending-kept");
				}
				return true;
			}
			VerificationLog("AIPlayerRobbery", $"deferred phase=response-popup source={source} mode={mode} robber={candidate.Robber.PID.id} targetCrew={candidate.TargetCrew.peepId.id} vehicle={candidate.TargetCrew.VehicleID.id} node={candidate.ContactNode?.id ?? NodeID.INVALID} day={now.days} reason=pair-queued-replaced oldTarget={existing?.TargetCrewPeepId ?? 0L} oldNode=NID_{existing?.TargetNodeIndex ?? 0} oldSource={existing?.Source ?? string.Empty} result=pending-updated");
		}
		if (HasActiveAiHumanRobberyResponseForHuman(humanPlayer))
		{
			VerificationLog("AIPlayerRobbery", $"deferred phase=response-popup source={source} mode={mode} robber={candidate.Robber.PID.id} targetCrew={candidate.TargetCrew.peepId.id} vehicle={candidate.TargetCrew.VehicleID.id} node={candidate.ContactNode?.id ?? NodeID.INVALID} day={now.days} reason=human-response-active result=queued-next-turn");
		}
		TryShowAiHumanRobberyIntentTicker(candidate, now, source, "queued-response");
		DeferredAiHumanRobberyResponse deferred = BuildDeferredAiHumanRobberyResponse(candidate, humanPlayer, now, source, mode, createdDay);
		_deferredAiHumanRobberyResponsesByKey[key] = deferred;
		VerificationLog(
			"AIPlayerRobbery",
			$"deferred phase=response-popup source={source} mode={mode} robber={candidate.Robber.PID.id} targetCrew={candidate.TargetCrew.peepId.id} vehicle={candidate.TargetCrew.VehicleID.id} node={candidate.ContactNode?.id ?? NodeID.INVALID} enactedDay={now.days} notBeforeDay={now.days + 1} expireDay={deferred.ExpireDay} result=queued-next-turn");
		return true;
	}

	private static DeferredAiHumanRobberyResponse BuildDeferredAiHumanRobberyResponse(AiRobberyCandidate candidate, PlayerInfo humanPlayer, SimTime now, string source, string mode, int createdDay)
	{
		return new DeferredAiHumanRobberyResponse
		{
			RobberPid = candidate.Robber.PID.id,
			TargetCrewPeepId = (long)candidate.TargetCrew.peepId.id,
			TargetVehicleId = candidate.TargetCrew.VehicleID.IsValid ? (long)candidate.TargetCrew.VehicleID.id : 0L,
			TargetNodeIndex = candidate.ContactNode?.id.index ?? 0,
			EnactedDay = now.days,
			NotBeforeDay = now.days + 1,
			ExpireDay = GetAiHumanRobberyDeferredResponseExpireDay(candidate.Robber.PID.id, humanPlayer?.PID.id ?? 0, now.days + AI_ROBBERY_PENDING_CONTACT_EXPIRE_DAYS),
			CreatedDay = createdDay,
			Source = source ?? string.Empty,
			Mode = mode ?? string.Empty,
			Distance = candidate.Distance,
			SameNode = candidate.SameNode,
			RobberNodeSource = candidate.RobberNodeSource ?? string.Empty,
			TargetNodeSource = candidate.TargetNodeSource ?? string.Empty,
			DirectAggro = candidate.DirectAggro,
			BroadlyHostile = candidate.BroadlyHostile,
			EnemyTerritory = candidate.EnemyTerritory,
			HumanTerritory = candidate.HumanTerritory,
			StrictTrespassNode = candidate.StrictTrespassNode,
			TerritoryPressure = candidate.TerritoryPressure,
			RecentTrespassMemory = candidate.RecentTrespassMemory,
			RobberPower = candidate.RobberPower,
			HumanPower = candidate.HumanPower,
			RobberLocalPower = candidate.RobberLocalPower,
			HumanLocalPower = candidate.HumanLocalPower,
			TrespassGraceDays = candidate.TrespassGraceDays,
			Reason = candidate.Reason ?? string.Empty
		};
	}

	private static bool RecordAiHumanRobberyMeetingPending(AiRobberyCandidate candidate, PlayerInfo humanPlayer, SimTime now, string source, string mode, int createdDay)
	{
		if (candidate?.Robber == null || humanPlayer == null || !candidate.TargetCrew.IsValid)
		{
			return false;
		}
		if (TryGetActiveConvoInitiative(candidate.Robber, out NodeID convoMeetingPoint, out string convoTopic, out bool convoNeedsPeep))
		{
			VerificationLog(
				"AIPlayerRobbery",
				$"robbery-meeting-skip-real-convo robber={candidate.Robber.PID.id} human={humanPlayer.PID.id} targetCrew={candidate.TargetCrew.peepId.id} source={source} mode={mode} convoTopic={convoTopic ?? string.Empty} convoMeetingNode={convoMeetingPoint} convoNeedsPeep={convoNeedsPeep} result=not-queued");
			return false;
		}
		string key = GetAiHumanRobberyMeetingKey(candidate.Robber, humanPlayer);
		if (string.IsNullOrEmpty(key))
		{
			return false;
		}
		if (!TrySelectAiHumanRobberyMeetingCrew(candidate, out CrewAssignment meetingCrew, out Node meetingCrewNode, out string meetingCrewNodeSource))
		{
			VerificationLog(
				"AIPlayerRobbery",
				$"robbery-meeting-capacity-blocked robber={candidate.Robber.PID.id} human={humanPlayer.PID.id} targetCrew={candidate.TargetCrew.peepId.id} vehicle={candidate.TargetCrew.VehicleID.id} node={candidate.ContactNode?.id ?? NodeID.INVALID} source={source} mode={mode} reason=no-meeting-crew result=not-queued");
			return false;
		}
		int priority = GetDeferredAiHumanRobberyPriority(candidate, source);
		int notBeforeDay = Math.Max(now.days + 1, createdDay >= 0 ? createdDay + 1 : now.days + 1);
		PendingAiHumanRobberyMeeting previousPending = null;
		if (_pendingAiHumanRobberyMeetingsByKey.TryGetValue(key, out PendingAiHumanRobberyMeeting existing))
		{
			bool sameTarget = existing.TargetCrewPeepId == (long)candidate.TargetCrew.peepId.id;
			bool sameNode = existing.MeetingNodeIndex == (candidate.ContactNode?.id.index ?? 0);
			bool sameActor = existing.MeetingPeepId == (long)meetingCrew.peepId.id;
			PlayerInfo robber = candidate.Robber;
			EntityID existingPeepId = existing.MeetingPeepId > 0L ? EntityID.FromID((ulong)existing.MeetingPeepId) : EntityID.INVALID;
			CrewAssignment existingCrew = existingPeepId.IsValid && robber?.crew != null
				? robber.crew.GetCrewForPeep(existingPeepId)
				: CrewAssignment.EMPTY;
			bool existingActorValid = existingCrew.IsValid
				&& !existingCrew.IsDead
				&& existingCrew.peepId.IsValid
				&& existingCrew.IsInVehicle
				&& existingCrew.VehicleID.IsValid
				&& (long)existingCrew.VehicleID.id == existing.MeetingVehicleId;
			bool routeCommitted = existing.RouteQueued || existing.Arrived || existing.LastRouteIssuedDay != int.MinValue;
			bool existingExpired = IsPendingAiHumanRobberyMeetingExpired(existing, now);
			previousPending = existingExpired ? null : existing;
			if (existingExpired)
			{
				VerificationLog(
					"AIPlayerRobbery",
					$"robbery-meeting-replace-expired robber={candidate.Robber.PID.id} human={humanPlayer.PID.id} oldMeetingPeep={existing.MeetingPeepId} oldMeetingVehicle={existing.MeetingVehicleId} oldTarget={existing.TargetCrewPeepId} oldMeetingNode=NID_{existing.MeetingNodeIndex} oldExpireDay={existing.ExpireDay} hardExpireDay={GetPendingAiHumanRobberyMeetingHardExpireDay(existing)} day={now.days} source={source} mode={mode} result=replace-expired");
			}
			if (!existingExpired && sameTarget && existingActorValid && routeCommitted)
			{
				int oldPriority = existing.Priority;
				int oldExpireDay = existing.ExpireDay;
				existing.Priority = Math.Max(existing.Priority, priority);
				existing.ExpireDay = ClampPendingAiHumanRobberyMeetingExpireDay(existing, Math.Max(existing.ExpireDay, now.days + AI_ROBBERY_PENDING_CONTACT_EXPIRE_DAYS));
				if (priority >= oldPriority)
				{
					existing.Source = source ?? existing.Source;
					existing.Mode = mode ?? existing.Mode;
					existing.Reason = candidate.Reason ?? existing.Reason;
					existing.CreatedDay = createdDay;
				}
				if (ShouldLogRetainedAiHumanRobberyMeeting(existing, now, oldPriority, oldExpireDay))
				{
					VerificationLog(
						"AIPlayerRobbery",
						$"robbery-meeting-retained robber={candidate.Robber.PID.id} human={humanPlayer.PID.id} meetingPeep={existing.MeetingPeepId} meetingVehicle={existing.MeetingVehicleId} targetCrew={existing.TargetCrewPeepId} meetingNode=NID_{existing.MeetingNodeIndex} source={source} mode={mode} existingSource={existing.Source} priority={priority}/{oldPriority}->{existing.Priority} actorLock=True routeLock=True proposedPeep={meetingCrew.peepId.id} proposedVehicle={meetingCrew.VehicleID.id} proposedNode={candidate.ContactNode?.id ?? NodeID.INVALID} sameNode={sameNode} routeQueued={existing.RouteQueued} arrived={existing.Arrived} result=pending-kept");
				}
				return true;
			}
			if (!existingExpired
				&& sameTarget
				&& routeCommitted
				&& !existingActorValid
				&& meetingCrew.IsValid
				&& meetingCrew.peepId.IsValid
				&& meetingCrew.VehicleID.IsValid)
			{
				int oldPriority = existing.Priority;
				long oldPeep = existing.MeetingPeepId;
				long oldVehicle = existing.MeetingVehicleId;
				existing.MeetingPeepId = (long)meetingCrew.peepId.id;
				existing.MeetingVehicleId = (long)meetingCrew.VehicleID.id;
				existing.Priority = Math.Max(existing.Priority, priority);
				existing.ExpireDay = ClampPendingAiHumanRobberyMeetingExpireDay(existing, Math.Max(existing.ExpireDay, now.days + AI_ROBBERY_PENDING_CONTACT_EXPIRE_DAYS));
				existing.MeetingCrewNodeIndex = meetingCrewNode?.id.index ?? existing.MeetingCrewNodeIndex;
				existing.LastObservedNodeIndex = meetingCrewNode?.id.index ?? existing.LastObservedNodeIndex;
				existing.RouteTargetNodeIndex = 0;
				existing.RouteQueued = false;
				existing.Arrived = false;
				existing.LastProgressDay = now.days;
				existing.Source = source ?? existing.Source;
				existing.Mode = mode ?? existing.Mode;
				existing.Reason = candidate.Reason ?? existing.Reason;
				existing.CreatedDay = createdDay;
				VerificationLog(
					"AIPlayerRobbery",
					$"robbery-meeting-retained robber={candidate.Robber.PID.id} human={humanPlayer.PID.id} meetingPeep={existing.MeetingPeepId} oldMeetingPeep={oldPeep} meetingVehicle={existing.MeetingVehicleId} oldMeetingVehicle={oldVehicle} targetCrew={existing.TargetCrewPeepId} meetingNode=NID_{existing.MeetingNodeIndex} source={source} mode={mode} existingSource={existing.Source} priority={priority}/{oldPriority}->{existing.Priority} actorRecovered=True vehicleRecovered=True vehicleChanged={oldVehicle != existing.MeetingVehicleId} routeLock=True proposedNode={candidate.ContactNode?.id ?? NodeID.INVALID} sameNode={sameNode} result=pending-kept");
				return true;
			}
			if (!existingExpired && sameTarget && sameNode && existingActorValid)
			{
				int oldPriority = existing.Priority;
				int oldExpireDay = existing.ExpireDay;
				existing.Priority = Math.Max(existing.Priority, priority);
				existing.ExpireDay = ClampPendingAiHumanRobberyMeetingExpireDay(existing, Math.Max(existing.ExpireDay, now.days + AI_ROBBERY_PENDING_CONTACT_EXPIRE_DAYS));
				if (priority >= oldPriority)
				{
					existing.Source = source ?? existing.Source;
					existing.Mode = mode ?? existing.Mode;
					existing.Reason = candidate.Reason ?? existing.Reason;
					existing.CreatedDay = createdDay;
				}
				if (ShouldLogRetainedAiHumanRobberyMeeting(existing, now, oldPriority, oldExpireDay))
				{
					VerificationLog(
						"AIPlayerRobbery",
						$"robbery-meeting-retained robber={candidate.Robber.PID.id} human={humanPlayer.PID.id} meetingPeep={existing.MeetingPeepId} meetingVehicle={existing.MeetingVehicleId} targetCrew={existing.TargetCrewPeepId} meetingNode=NID_{existing.MeetingNodeIndex} source={source} mode={mode} existingSource={existing.Source} priority={priority}/{oldPriority}->{existing.Priority} actorLock=True proposedPeep={meetingCrew.peepId.id} proposedVehicle={meetingCrew.VehicleID.id} result=pending-kept");
				}
				return true;
			}
			if (!existingExpired && existing.QueuedDay == now.days && priority <= existing.Priority && sameTarget && sameNode && sameActor)
			{
				if (ShouldLogRetainedAiHumanRobberyMeeting(existing, now, existing.Priority, existing.ExpireDay))
				{
					VerificationLog(
						"AIPlayerRobbery",
						$"robbery-meeting-retained robber={candidate.Robber.PID.id} human={humanPlayer.PID.id} meetingPeep={existing.MeetingPeepId} meetingVehicle={existing.MeetingVehicleId} targetCrew={existing.TargetCrewPeepId} meetingNode=NID_{existing.MeetingNodeIndex} source={source} mode={mode} existingSource={existing.Source} priority={priority}/{existing.Priority} result=pending-kept");
				}
				return true;
			}
			VerificationLog(
				"AIPlayerRobbery",
				$"robbery-meeting-updated robber={candidate.Robber.PID.id} human={humanPlayer.PID.id} oldMeetingPeep={existing.MeetingPeepId} oldMeetingVehicle={existing.MeetingVehicleId} oldTarget={existing.TargetCrewPeepId} oldMeetingNode=NID_{existing.MeetingNodeIndex} oldSource={existing.Source} newMeetingPeep={meetingCrew.peepId.id} newMeetingVehicle={meetingCrew.VehicleID.id} newTarget={candidate.TargetCrew.peepId.id} newMeetingNode={candidate.ContactNode?.id ?? NodeID.INVALID} source={source} mode={mode} priority={priority}/{existing.Priority} result=pending-updated");
		}
		PendingAiHumanRobberyMeeting pending = new PendingAiHumanRobberyMeeting
		{
			RobberPid = candidate.Robber.PID.id,
			HumanPid = humanPlayer.PID.id,
			TargetCrewPeepId = (long)candidate.TargetCrew.peepId.id,
			TargetVehicleId = candidate.TargetCrew.VehicleID.IsValid ? (long)candidate.TargetCrew.VehicleID.id : 0L,
			MeetingPeepId = (long)meetingCrew.peepId.id,
			MeetingVehicleId = meetingCrew.VehicleID.IsValid ? (long)meetingCrew.VehicleID.id : 0L,
			MeetingNodeIndex = candidate.ContactNode?.id.index ?? 0,
			MeetingCrewNodeIndex = meetingCrewNode?.id.index ?? 0,
			RouteTargetNodeIndex = previousPending?.RouteTargetNodeIndex ?? 0,
			LastObservedNodeIndex = previousPending?.LastObservedNodeIndex ?? (meetingCrewNode?.id.index ?? 0),
			QueuedDay = now.days,
			NotBeforeDay = notBeforeDay,
			ExpireDay = now.days + AI_ROBBERY_PENDING_CONTACT_EXPIRE_DAYS,
			CreatedDay = createdDay,
			Priority = priority,
			LastRouteIssuedDay = previousPending?.LastRouteIssuedDay ?? int.MinValue,
			LastProgressDay = previousPending?.LastProgressDay ?? now.days,
			NoProgressChecks = previousPending?.NoProgressChecks ?? 0,
			Source = source ?? string.Empty,
			Mode = mode ?? string.Empty,
			Reason = candidate.Reason ?? string.Empty,
			MeetingCrewNodeSource = meetingCrewNodeSource ?? string.Empty,
			RouteQueued = previousPending?.RouteQueued ?? false,
			Arrived = previousPending?.Arrived ?? false,
			RouteRequeueCount = previousPending?.RouteRequeueCount ?? 0,
			FinalTargetRouteCommitted = previousPending?.FinalTargetRouteCommitted ?? false,
			ActorReassignmentGranted = previousPending?.ActorReassignmentGranted ?? false,
			TargetMoveRouteWindowResets = previousPending?.TargetMoveRouteWindowResets ?? 0
		};
		_pendingAiHumanRobberyMeetingsByKey[key] = pending;
		VerificationLog(
			"AIPlayerRobbery",
			$"robbery-meeting-queued robber={pending.RobberPid} human={pending.HumanPid} meetingPeep={pending.MeetingPeepId} meetingVehicle={pending.MeetingVehicleId} meetingCrewNode=NID_{pending.MeetingCrewNodeIndex} meetingCrewSource={pending.MeetingCrewNodeSource} targetCrew={pending.TargetCrewPeepId} targetVehicle={pending.TargetVehicleId} meetingNode=NID_{pending.MeetingNodeIndex} queuedDay={pending.QueuedDay} notBeforeDay={pending.NotBeforeDay} expireDay={pending.ExpireDay} source={pending.Source} mode={pending.Mode} priority={pending.Priority} reason={pending.Reason} promptStillLegacy=True result=pending");
		TryQueuePendingAiHumanRobberyMeetingRoute(pending, candidate.Robber, now, "record:" + (source ?? "unknown"), allowRequeue: false);
		return true;
	}

	private static bool ShouldLogRetainedAiHumanRobberyMeeting(PendingAiHumanRobberyMeeting pending, SimTime now, int oldPriority, int oldExpireDay)
	{
		if (pending == null)
		{
			return false;
		}
		bool changed = pending.Priority != oldPriority || pending.ExpireDay != oldExpireDay;
		if (!changed && pending.LastRetainedLogDay == now.days)
		{
			return false;
		}
		pending.LastRetainedLogDay = now.days;
		return true;
	}

	private static bool TrySelectAiHumanRobberyMeetingCrew(AiRobberyCandidate candidate, out CrewAssignment meetingCrew, out Node meetingCrewNode, out string meetingCrewNodeSource)
	{
		meetingCrew = CrewAssignment.EMPTY;
		meetingCrewNode = null;
		meetingCrewNodeSource = "none";
		if (candidate?.Robber == null || candidate.ContactNode == null)
		{
			return false;
		}
		float bestDistance = float.MaxValue;
		foreach ((CrewAssignment crew, Node node, string source) entry in GetAiRobberyCrewNodes(candidate.Robber, allowSafehouseFallback: true))
		{
			if (!entry.crew.IsValid || entry.crew.IsDead || !entry.crew.peepId.IsValid || entry.node == null)
			{
				continue;
			}
			if (!TryResolveRuntimeFrontActionCrew(candidate.Robber, entry.crew, out CrewAssignment actionCrew, out _)
				|| !actionCrew.IsValid
				|| actionCrew.peepId.IsNotValid
				|| !actionCrew.IsInVehicle
				|| actionCrew.VehicleID.IsNotValid
				|| IsCrewAssignedToOtherPendingRetaliationAction(candidate.Robber.PID, actionCrew.peepId, EntityID.INVALID, out _)
				|| IsRetaliationConvoInitiativeCrewReserved(candidate.Robber, actionCrew, out _, out _, out _, out _)
				|| ShouldRuntimeGangAttackerRetreat(candidate.Robber, actionCrew, out _))
			{
				continue;
			}
			Node actionNode = entry.node;
			string actionSource = entry.source;
			if (actionCrew.peepId != entry.crew.peepId
				&& TryGetAiRobberyCrewNode(actionCrew, out Node resolvedActionNode, out string resolvedActionSource)
				&& resolvedActionNode != null)
			{
				actionNode = resolvedActionNode;
				actionSource = resolvedActionSource;
			}
			float distance = (actionNode.pos - candidate.ContactNode.pos).Magnitude;
			bool replaces = distance + 0.25f < bestDistance;
			if (!replaces && Math.Abs(distance - bestDistance) <= 0.25f && meetingCrew.IsValid)
			{
				bool entryIsBoss = IsRuntimeFrontActionBoss(candidate.Robber, actionCrew.GetPeep());
				bool selectedIsBoss = IsRuntimeFrontActionBoss(candidate.Robber, meetingCrew.GetPeep());
				replaces = !entryIsBoss && selectedIsBoss;
			}
			if (!replaces)
			{
				continue;
			}
			meetingCrew = actionCrew;
			meetingCrewNode = actionNode;
			meetingCrewNodeSource = actionSource ?? string.Empty;
			bestDistance = distance;
		}
		return meetingCrew.IsValid && meetingCrew.peepId.IsValid && meetingCrewNode != null;
	}

	private static bool IsPendingAiHumanRobberyMeetingActorValid(
		PendingAiHumanRobberyMeeting pending,
		PlayerInfo robber,
		out CrewAssignment meetingCrew,
		out Entity meetingPeep,
		out NodeID currentNodeId,
		out string currentNodeSource,
		out string reason)
	{
		meetingCrew = CrewAssignment.EMPTY;
		meetingPeep = null;
		currentNodeId = NodeID.INVALID;
		currentNodeSource = "none";
		reason = "invalid";
		if (pending == null)
		{
			reason = "missing-pending";
			return false;
		}
		if (robber?.crew == null || pending.MeetingPeepId <= 0L)
		{
			reason = "missing-robber-or-peep";
			return false;
		}
		EntityID meetingPeepId = EntityID.FromID((ulong)pending.MeetingPeepId);
		meetingCrew = meetingPeepId.IsValid ? robber.crew.GetCrewForPeep(meetingPeepId) : CrewAssignment.EMPTY;
		if (!meetingCrew.IsValid)
		{
			reason = "crew-missing";
			return false;
		}
		if (meetingCrew.IsDead)
		{
			reason = "crew-dead";
			return false;
		}
		meetingPeep = meetingCrew.GetPeep();
		if (meetingPeep?.data?.person?.IsAlive != true)
		{
			reason = "peep-dead";
			return false;
		}
		if (!meetingCrew.IsInVehicle || meetingCrew.VehicleID.IsNotValid)
		{
			reason = "no-vehicle";
			return false;
		}
		if (pending.MeetingVehicleId > 0L && (long)meetingCrew.VehicleID.id != pending.MeetingVehicleId)
		{
			reason = "vehicle-changed";
			return false;
		}
		if (!TryResolveRuntimeFrontActionCrewNodeQuiet(meetingCrew, meetingPeep, out currentNodeId, out currentNodeSource)
			|| currentNodeId.IsNotValid)
		{
			reason = "no-physical-node";
			return false;
		}
		reason = "valid";
		return true;
	}

	private static bool IsCrewReservedForOtherPendingAiHumanRobberyMeeting(PlayerInfo robber, CrewAssignment crew, PendingAiHumanRobberyMeeting allowedPending)
	{
		if (robber == null || !crew.IsValid || crew.peepId.IsNotValid)
		{
			return false;
		}
		long peepId = (long)crew.peepId.id;
		long vehicleId = crew.VehicleID.IsValid ? (long)crew.VehicleID.id : 0L;
		SimTime now = G.GetNow();
		foreach (PendingAiHumanRobberyMeeting pending in _pendingAiHumanRobberyMeetingsByKey.Values)
		{
			if (pending == null
				|| pending == allowedPending
				|| pending.RobberPid != robber.PID.id
				|| IsPendingAiHumanRobberyMeetingExpired(pending, now))
			{
				continue;
			}
			if (pending.MeetingPeepId == peepId || vehicleId > 0L && pending.MeetingVehicleId == vehicleId)
			{
				return true;
			}
		}
		return false;
	}

	private static bool TryReassignPendingAiHumanRobberyMeetingActor(PendingAiHumanRobberyMeeting pending, PlayerInfo robber, SimTime now, string source, string invalidReason)
	{
		if (pending == null || robber?.crew == null || pending.ActorReassignmentGranted || pending.MeetingNodeIndex <= 0)
		{
			return false;
		}
		Node meetingNode = new NodeID(pending.MeetingNodeIndex).FindNode();
		if (meetingNode?.id.IsValid != true)
		{
			return false;
		}
		CrewAssignment bestCrew = CrewAssignment.EMPTY;
		Node bestNode = null;
		string bestSource = "none";
		float bestDistance = float.MaxValue;
		long oldPeep = pending.MeetingPeepId;
		long oldVehicle = pending.MeetingVehicleId;
		foreach ((CrewAssignment crew, Node node, string source) entry in GetAiRobberyCrewNodes(robber, allowSafehouseFallback: true))
		{
			if (!entry.crew.IsValid || entry.crew.IsDead || !entry.crew.peepId.IsValid || entry.node == null)
			{
				continue;
			}
			if ((long)entry.crew.peepId.id == oldPeep)
			{
				continue;
			}
			if (!TryResolveRuntimeFrontActionCrew(robber, entry.crew, out CrewAssignment actionCrew, out _)
				|| !actionCrew.IsValid
				|| actionCrew.peepId.IsNotValid
				|| !actionCrew.IsInVehicle
				|| actionCrew.VehicleID.IsNotValid
				|| (long)actionCrew.peepId.id == oldPeep
				|| (oldVehicle > 0L && (long)actionCrew.VehicleID.id == oldVehicle)
				|| IsCrewAssignedToOtherPendingRetaliationAction(robber.PID, actionCrew.peepId, EntityID.INVALID, out _)
				|| IsCrewReservedForOtherPendingAiHumanRobberyMeeting(robber, actionCrew, pending)
				|| IsRetaliationConvoInitiativeCrewReserved(robber, actionCrew, out _, out _, out _, out _)
				|| ShouldRuntimeGangAttackerRetreat(robber, actionCrew, out _))
			{
				continue;
			}
			Entity peep = actionCrew.GetPeep();
			if (peep?.data?.person?.IsAlive != true)
			{
				continue;
			}
			Node actionNode = entry.node;
			string actionSource = entry.source;
			if (actionCrew.peepId != entry.crew.peepId
				&& TryGetAiRobberyCrewNode(actionCrew, out Node resolvedActionNode, out string resolvedActionSource)
				&& resolvedActionNode != null)
			{
				actionNode = resolvedActionNode;
				actionSource = resolvedActionSource;
			}
			float distance = (actionNode.pos - meetingNode.pos).Magnitude;
			bool replaces = distance + 0.25f < bestDistance;
			if (!replaces && Math.Abs(distance - bestDistance) <= 0.25f && bestCrew.IsValid)
			{
				bool entryIsBoss = IsRuntimeFrontActionBoss(robber, actionCrew.GetPeep());
				bool selectedIsBoss = IsRuntimeFrontActionBoss(robber, bestCrew.GetPeep());
				replaces = !entryIsBoss && selectedIsBoss;
			}
			if (!replaces)
			{
				continue;
			}
			bestCrew = actionCrew;
			bestNode = actionNode;
			bestSource = actionSource ?? string.Empty;
			bestDistance = distance;
		}
		if (!bestCrew.IsValid || bestCrew.peepId.IsNotValid || bestCrew.VehicleID.IsNotValid || bestNode == null)
		{
			return false;
		}
		pending.ActorReassignmentGranted = true;
		pending.MeetingPeepId = (long)bestCrew.peepId.id;
		pending.MeetingVehicleId = (long)bestCrew.VehicleID.id;
		pending.MeetingCrewNodeIndex = bestNode.id.index;
		pending.LastObservedNodeIndex = bestNode.id.index;
		pending.RouteTargetNodeIndex = 0;
		pending.RouteQueued = false;
		pending.Arrived = false;
		pending.NoProgressChecks = 0;
		pending.DivergingRouteChecks = 0;
		pending.LastDistanceToMeeting = bestDistance;
		pending.LastProgressDay = now.days;
		pending.MeetingCrewNodeSource = bestSource ?? string.Empty;
		TryExtendPendingAiHumanRobberyMeetingExpiry(pending, now, source, "actor-reassigned");
		VerificationLog(
			"AIPlayerRobbery",
			$"robbery-meeting-actor-reassigned robber={pending.RobberPid} human={pending.HumanPid} oldMeetingPeep={oldPeep} oldMeetingVehicle={oldVehicle} meetingPeep={pending.MeetingPeepId} meetingVehicle={pending.MeetingVehicleId} targetCrew={pending.TargetCrewPeepId} meetingCrewNode=NID_{pending.MeetingCrewNodeIndex} meetingCrewSource={pending.MeetingCrewNodeSource} meetingNode=NID_{pending.MeetingNodeIndex} queuedDay={pending.QueuedDay} expireDay={pending.ExpireDay} day={now.days} distance={bestDistance:0.0} invalidReason={invalidReason} source={source} result=reassigned");
		return true;
	}

	private static bool TryExtendPendingAiHumanRobberyMeetingExpiry(PendingAiHumanRobberyMeeting pending, SimTime now, string source, string reason)
	{
		if (pending == null)
		{
			return false;
		}
		int hardExpireDay = GetPendingAiHumanRobberyMeetingHardExpireDay(pending);
		if (now.days > hardExpireDay)
		{
			return false;
		}
		int oldExpireDay = pending.ExpireDay;
		int nextExpireDay = Math.Min(hardExpireDay, now.days + AI_ROBBERY_PENDING_CONTACT_EXPIRE_DAYS);
		if (nextExpireDay <= pending.ExpireDay)
		{
			return false;
		}
		pending.ExpireDay = nextExpireDay;
		VerificationLog(
			"AIPlayerRobbery",
			$"robbery-meeting-expiry-extended robber={pending.RobberPid} human={pending.HumanPid} meetingPeep={pending.MeetingPeepId} meetingVehicle={pending.MeetingVehicleId} meetingNode=NID_{pending.MeetingNodeIndex} queuedDay={pending.QueuedDay} oldExpireDay={oldExpireDay} expireDay={pending.ExpireDay} hardExpireDay={hardExpireDay} day={now.days} lastProgressDay={pending.LastProgressDay} routeQueued={pending.RouteQueued} requeues={pending.RouteRequeueCount} reason={reason} source={source} result=extended");
		return true;
	}

	private static int GetPendingAiHumanRobberyMeetingHardExpireDay(PendingAiHumanRobberyMeeting pending)
	{
		return pending == null ? int.MinValue : pending.QueuedDay + AI_ROBBERY_MEETING_ROUTE_MAX_DAYS;
	}

	private static int ClampPendingAiHumanRobberyMeetingExpireDay(PendingAiHumanRobberyMeeting pending, int requestedExpireDay)
	{
		int hardExpireDay = GetPendingAiHumanRobberyMeetingHardExpireDay(pending);
		return hardExpireDay > 0 ? Math.Min(hardExpireDay, requestedExpireDay) : requestedExpireDay;
	}

	private static int GetAiHumanRobberyDeferredResponseExpireDay(int robberPid, int humanPid, int requestedExpireDay)
	{
		if (TryGetPendingAiHumanRobberyMeeting(robberPid, humanPid, out PendingAiHumanRobberyMeeting pending))
		{
			return ClampPendingAiHumanRobberyMeetingExpireDay(pending, requestedExpireDay);
		}
		return requestedExpireDay;
	}

	private static bool IsPendingAiHumanRobberyMeetingExpired(PendingAiHumanRobberyMeeting pending, SimTime now)
	{
		return pending == null || pending.ExpireDay < now.days || now.days > GetPendingAiHumanRobberyMeetingHardExpireDay(pending);
	}

	private static bool TryQueuePendingAiHumanRobberyMeetingRoute(PendingAiHumanRobberyMeeting pending, PlayerInfo robber, SimTime now, string source, bool allowRequeue)
	{
		if (pending == null || robber?.crew == null || robber.commands == null || pending.MeetingPeepId <= 0L || pending.MeetingNodeIndex <= 0)
		{
			return false;
		}
		try
		{
			EntityID meetingPeepId = EntityID.FromID((ulong)pending.MeetingPeepId);
			CrewAssignment meetingCrew = robber.crew.GetCrewForPeep(meetingPeepId);
			Entity meetingPeep = meetingCrew.IsValid ? meetingCrew.GetPeep() : null;
			Node meetingNode = new NodeID(pending.MeetingNodeIndex).FindNode();
			if (!meetingCrew.IsValid || meetingCrew.IsDead || meetingPeep?.data?.person?.IsAlive != true || meetingNode?.id.IsValid != true)
			{
				VerificationLog("AIPlayerRobbery", $"robbery-meeting-route-blocked robber={pending.RobberPid} human={pending.HumanPid} meetingPeep={pending.MeetingPeepId} meetingVehicle={pending.MeetingVehicleId} meetingNode=NID_{pending.MeetingNodeIndex} source={source} reason=invalid-route-context actorDead={(meetingCrew.IsValid && meetingCrew.IsDead) || meetingPeep?.data?.person?.IsAlive == false} result=not-queued");
				return false;
			}
			if (TryGetActiveConvoInitiative(robber, out NodeID convoMeetingPoint, out string convoTopic, out bool convoNeedsPeep))
			{
				VerificationLog("AIPlayerRobbery", $"robbery-meeting-route-blocked robber={pending.RobberPid} human={pending.HumanPid} meetingPeep={pending.MeetingPeepId} meetingNode=NID_{pending.MeetingNodeIndex} source={source} reason=real-convo-active convoTopic={convoTopic} convoMeetingNode={convoMeetingPoint} convoNeedsPeep={convoNeedsPeep} result=not-queued");
				return false;
			}
			if (IsCrewAssignedToOtherPendingRetaliationAction(robber.PID, meetingCrew.peepId, EntityID.INVALID, out PendingRetaliationFrontTicker busyPending))
			{
				VerificationLog("AIPlayerRobbery", $"robbery-meeting-route-blocked robber={pending.RobberPid} human={pending.HumanPid} meetingPeep={pending.MeetingPeepId} meetingVehicle={pending.MeetingVehicleId} meetingNode=NID_{pending.MeetingNodeIndex} source={source} reason=pending-{GetPendingRetaliationActionKind(busyPending)} building={busyPending?.BuildingId.id ?? 0UL} result=not-queued");
				return false;
			}
			if (!TryResolveRuntimeFrontActionCrewNodeQuiet(meetingCrew, meetingPeep, out NodeID currentNodeId, out string currentNodeSource)
				|| currentNodeId.IsNotValid)
			{
				VerificationLog("AIPlayerRobbery", $"robbery-meeting-route-blocked robber={pending.RobberPid} human={pending.HumanPid} meetingPeep={pending.MeetingPeepId} meetingVehicle={pending.MeetingVehicleId} meetingNode=NID_{pending.MeetingNodeIndex} source={source} reason=no-physical-node result=not-queued");
				return false;
			}
			pending.MeetingCrewNodeIndex = currentNodeId.index;
			if (currentNodeId == meetingNode.id || meetingPeep.data?.agent?.nid == meetingNode.id)
			{
				bool routeArrivalHold = TryHoldPendingAiHumanRobberyMeetingActor(pending, robber, meetingCrew, currentNodeId, now, "arrived-exact", source);
				pending.Arrived = true;
				pending.RouteQueued = false;
				pending.RouteTargetNodeIndex = meetingNode.id.index;
				pending.LastObservedNodeIndex = meetingNode.id.index;
				pending.LastProgressDay = now.days;
				VerificationLog("AIPlayerRobbery", $"robbery-meeting-arrived robber={pending.RobberPid} human={pending.HumanPid} meetingPeep={pending.MeetingPeepId} meetingVehicle={pending.MeetingVehicleId} currentNode={currentNodeId} currentSource={currentNodeSource} meetingNode={meetingNode.id} queuedDay={pending.QueuedDay} day={now.days} requeues={pending.RouteRequeueCount} routeArrivalHold={routeArrivalHold} promptStillLegacy=True source={source} result=arrived");
				return true;
			}
			if (!allowRequeue && pending.RouteQueued)
			{
				return true;
			}
			Node currentNode = currentNodeId.FindNode();
			float currentDistanceToMeeting = currentNode != null ? (currentNode.pos - meetingNode.pos).Magnitude : -1f;
			bool finalMeetingRouteActive = pending.FinalTargetRouteCommitted
				|| (pending.RouteQueued
					&& pending.RouteTargetNodeIndex > 0
					&& pending.RouteTargetNodeIndex == meetingNode.id.index);
			if (finalMeetingRouteActive
				&& currentDistanceToMeeting >= 0f
				&& currentDistanceToMeeting <= AI_ROBBERY_NEARBY_PROMPT_WORLD_DISTANCE)
			{
				bool routeArrivalHold = TryHoldPendingAiHumanRobberyMeetingActor(pending, robber, meetingCrew, currentNodeId, now, "arrived-nearby", source);
				pending.Arrived = true;
				pending.RouteQueued = false;
				pending.RouteTargetNodeIndex = meetingNode.id.index;
				pending.LastObservedNodeIndex = currentNodeId.index;
				pending.LastDistanceToMeeting = currentDistanceToMeeting;
				pending.LastProgressDay = now.days;
				VerificationLog("AIPlayerRobbery", $"robbery-meeting-arrived robber={pending.RobberPid} human={pending.HumanPid} meetingPeep={pending.MeetingPeepId} meetingVehicle={pending.MeetingVehicleId} currentNode={currentNodeId} currentSource={currentNodeSource} meetingNode={meetingNode.id} distance={currentDistanceToMeeting:0.0} promptDistance={AI_ROBBERY_NEARBY_PROMPT_WORLD_DISTANCE:0.0} queuedDay={pending.QueuedDay} day={now.days} requeues={pending.RouteRequeueCount} routeArrivalHold={routeArrivalHold} promptStillLegacy=True source={source} result=arrived-nearby");
				return true;
			}
			bool reachedRouteSegment = allowRequeue
				&& pending.RouteQueued
				&& pending.RouteTargetNodeIndex > 0
				&& pending.RouteTargetNodeIndex != meetingNode.id.index
				&& currentNodeId.index == pending.RouteTargetNodeIndex;
			if (reachedRouteSegment)
			{
				pending.RouteQueued = false;
				pending.RouteTargetNodeIndex = 0;
				pending.NoProgressChecks = 0;
				pending.DivergingRouteChecks = 0;
				pending.LastDistanceToMeeting = currentDistanceToMeeting;
				pending.LastProgressDay = now.days;
				TryExtendPendingAiHumanRobberyMeetingExpiry(pending, now, source, "segment-complete");
				VerificationLog(
					"AIPlayerRobbery",
					$"robbery-meeting-route-segment-complete robber={pending.RobberPid} human={pending.HumanPid} meetingPeep={pending.MeetingPeepId} meetingVehicle={pending.MeetingVehicleId} currentNode={currentNodeId} currentSource={currentNodeSource} meetingNode={meetingNode.id} queuedDay={pending.QueuedDay} day={now.days} requeues={pending.RouteRequeueCount} source={source} result=queue-next-leg");
			}
			bool closeRangeTargetHandoff = allowRequeue
				&& pending.RouteQueued
				&& !reachedRouteSegment
				&& pending.RouteTargetNodeIndex > 0
				&& pending.RouteTargetNodeIndex != meetingNode.id.index
				&& currentDistanceToMeeting >= 0f
				&& currentDistanceToMeeting <= AI_ROBBERY_MEETING_TARGET_COMMIT_DISTANCE;
			if (closeRangeTargetHandoff)
			{
				short previousRouteTargetNodeIndex = pending.RouteTargetNodeIndex;
				pending.RouteQueued = false;
				pending.NoProgressChecks = 0;
				pending.DivergingRouteChecks = 0;
				pending.LastDistanceToMeeting = currentDistanceToMeeting;
				pending.LastProgressDay = now.days;
				TryExtendPendingAiHumanRobberyMeetingExpiry(pending, now, source, "close-range-target-handoff");
				VerificationLog(
					"AIPlayerRobbery",
					$"robbery-meeting-route-close-range-handoff robber={pending.RobberPid} human={pending.HumanPid} meetingPeep={pending.MeetingPeepId} meetingVehicle={pending.MeetingVehicleId} currentNode={currentNodeId} currentSource={currentNodeSource} previousRouteTargetNode=NID_{previousRouteTargetNodeIndex} meetingNode={meetingNode.id} distance={currentDistanceToMeeting:0.0} commitDistance={AI_ROBBERY_MEETING_TARGET_COMMIT_DISTANCE:0.0} queuedDay={pending.QueuedDay} day={now.days} requeues={pending.RouteRequeueCount} source={source} result=queue-target-leg");
			}
			if (allowRequeue
				&& pending.RouteQueued
				&& pending.RouteTargetNodeIndex > 0
				&& !reachedRouteSegment
				&& !closeRangeTargetHandoff
				&& robber.commands.PeepHasTask(meetingCrew.peepId))
			{
				bool progressed = pending.LastObservedNodeIndex > 0 && pending.LastObservedNodeIndex != currentNodeId.index;
				bool diverging = progressed
					&& pending.LastDistanceToMeeting >= 0f
					&& currentDistanceToMeeting > pending.LastDistanceToMeeting + 1f;
				if (progressed)
				{
					pending.NoProgressChecks = 0;
					pending.LastProgressDay = now.days;
					if (diverging)
					{
						pending.DivergingRouteChecks++;
					}
					else
					{
						pending.DivergingRouteChecks = 0;
						TryExtendPendingAiHumanRobberyMeetingExpiry(pending, now, source, "route-progress");
					}
				}
				else
				{
					pending.NoProgressChecks++;
					pending.DivergingRouteChecks = 0;
				}
				VerificationLog(
					"AIPlayerRobbery",
					$"robbery-meeting-route-progress robber={pending.RobberPid} human={pending.HumanPid} meetingPeep={pending.MeetingPeepId} meetingVehicle={pending.MeetingVehicleId} previousNode=NID_{pending.LastObservedNodeIndex} currentNode={currentNodeId} currentSource={currentNodeSource} routeTargetNode=NID_{pending.RouteTargetNodeIndex} meetingNode={meetingNode.id} progressed={progressed} diverging={diverging} distance={currentDistanceToMeeting:0.0} lastDistance={pending.LastDistanceToMeeting:0.0} divergingChecks={pending.DivergingRouteChecks} taskActive=True noProgressChecks={pending.NoProgressChecks} requeues={pending.RouteRequeueCount} source={source} result=route-active");
				pending.LastObservedNodeIndex = currentNodeId.index;
				pending.LastDistanceToMeeting = currentDistanceToMeeting;
				bool routeDiverged = pending.DivergingRouteChecks >= 2;
				if (routeDiverged)
				{
					pending.RouteQueued = false;
					pending.DivergingRouteChecks = 0;
					VerificationLog(
						"AIPlayerRobbery",
						$"robbery-meeting-route-diverged robber={pending.RobberPid} human={pending.HumanPid} meetingPeep={pending.MeetingPeepId} meetingVehicle={pending.MeetingVehicleId} currentNode={currentNodeId} currentSource={currentNodeSource} routeTargetNode=NID_{pending.RouteTargetNodeIndex} meetingNode={meetingNode.id} queuedDay={pending.QueuedDay} day={now.days} distance={currentDistanceToMeeting:0.0} requeues={pending.RouteRequeueCount} source={source} result=requeue-required");
				}
				if (!routeDiverged && pending.NoProgressChecks < 2)
				{
					return true;
				}
				if (!routeDiverged)
				{
					pending.RouteQueued = false;
					VerificationLog(
						"AIPlayerRobbery",
						$"robbery-meeting-route-stale robber={pending.RobberPid} human={pending.HumanPid} meetingPeep={pending.MeetingPeepId} meetingVehicle={pending.MeetingVehicleId} currentNode={currentNodeId} currentSource={currentNodeSource} routeTargetNode=NID_{pending.RouteTargetNodeIndex} meetingNode={meetingNode.id} queuedDay={pending.QueuedDay} day={now.days} noProgressChecks={pending.NoProgressChecks} requeues={pending.RouteRequeueCount} source={source} result=requeue-required");
				}
			}
			if (!TryBuildRuntimeFrontApproachRoute(robber.PID, meetingPeep, meetingNode, out PathData routePath, out string routeReason))
			{
				VerificationLog("AIPlayerRobbery", $"robbery-meeting-route-blocked robber={pending.RobberPid} human={pending.HumanPid} meetingPeep={pending.MeetingPeepId} meetingVehicle={pending.MeetingVehicleId} currentNode={currentNodeId} currentSource={currentNodeSource} meetingNode={meetingNode.id} source={source} reason={routeReason} result=not-queued");
				return false;
			}
			bool forceTargetRoute = allowRequeue
				&& (pending.FinalTargetRouteCommitted
					|| pending.RouteRequeueCount + 1 >= AI_ROBBERY_MEETING_TARGET_COMMIT_REQUEUES
					|| (currentDistanceToMeeting >= 0f && currentDistanceToMeeting <= AI_ROBBERY_MEETING_TARGET_COMMIT_DISTANCE));
			Node routeNode = ResolveFrontRouteCommandNode(meetingNode, routePath, allowSegmentTarget: !forceTargetRoute, out NodeID routeNodeId, out string routeGoal);
			if (routeNode?.id.IsValid != true)
			{
				routeNode = meetingNode;
				routeNodeId = meetingNode.id;
				routeGoal = "target-fallback";
			}
			bool routeAuthorityReset = false;
			bool routeIsFinalTarget = routeNodeId == meetingNode.id;
			if (forceTargetRoute || routeIsFinalTarget)
			{
				robber.commands.FlushQueue(meetingCrew.peepId, cancelActive: true);
				routeAuthorityReset = true;
			}
			robber.commands.AddCommandImmediate(new CommandGoto(robber.PID, meetingCrew.peepId, routeNode));
			if (allowRequeue)
			{
				pending.RouteRequeueCount++;
			}
			pending.RouteQueued = true;
			pending.Arrived = false;
			pending.RouteTargetNodeIndex = routeNodeId.index;
			if (routeIsFinalTarget)
			{
				pending.FinalTargetRouteCommitted = true;
			}
			pending.LastObservedNodeIndex = currentNodeId.index;
			pending.LastDistanceToMeeting = currentDistanceToMeeting;
			pending.DivergingRouteChecks = 0;
			pending.LastRouteIssuedDay = now.days;
			if (pending.LastProgressDay <= 0)
			{
				pending.LastProgressDay = now.days;
			}
			TryExtendPendingAiHumanRobberyMeetingExpiry(pending, now, source, allowRequeue ? "route-requeued" : "route-queued");
			VerificationLog(
				"AIPlayerRobbery",
				$"robbery-meeting-route-{(allowRequeue ? "requeued" : "queued")} robber={pending.RobberPid} human={pending.HumanPid} meetingPeep={pending.MeetingPeepId} meetingVehicle={pending.MeetingVehicleId} currentNode={currentNodeId} currentSource={currentNodeSource} routeTargetNode={routeNodeId} meetingNode={meetingNode.id} routeGoal={routeGoal} targetCommitted={pending.FinalTargetRouteCommitted} forceTargetRoute={forceTargetRoute} routeAuthorityReset={routeAuthorityReset} route={routeReason} cost={routePath.cost} queuedDay={pending.QueuedDay} day={now.days} requeues={pending.RouteRequeueCount} promptStillLegacy=True source={source} result=queued");
			return true;
		}
		catch (Exception ex)
		{
			VerificationLog("AIPlayerRobbery", $"robbery-meeting-route-failed robber={pending?.RobberPid ?? 0} human={pending?.HumanPid ?? 0} meetingPeep={pending?.MeetingPeepId ?? 0L} meetingNode=NID_{pending?.MeetingNodeIndex ?? 0} source={source} reason={ex.GetType().Name}:{ex.Message} result=not-queued");
			return false;
		}
	}

	private static void ServicePendingAiHumanRobberyMeetings(PlayerInfo humanPlayer, SimTime now, string source)
	{
		if (humanPlayer == null || _pendingAiHumanRobberyMeetingsByKey.Count == 0)
		{
			return;
		}
		foreach (KeyValuePair<string, PendingAiHumanRobberyMeeting> kv in _pendingAiHumanRobberyMeetingsByKey.ToList())
		{
			PendingAiHumanRobberyMeeting pending = kv.Value;
			if (pending == null || pending.HumanPid != humanPlayer.PID.id)
			{
				continue;
			}
			if (IsPendingAiHumanRobberyMeetingExpired(pending, now))
			{
				bool timeoutExtended = false;
				if (pending.Arrived && TryExtendPendingAiHumanRobberyMeetingExpiry(pending, now, source, "timeout-arrived"))
				{
					timeoutExtended = true;
					VerificationLog(
						"AIPlayerRobbery",
						$"robbery-meeting-timeout-deferred robber={pending.RobberPid} human={pending.HumanPid} meetingPeep={pending.MeetingPeepId} meetingVehicle={pending.MeetingVehicleId} meetingNode=NID_{pending.MeetingNodeIndex} queuedDay={pending.QueuedDay} expireDay={pending.ExpireDay} day={now.days} lastProgressDay={pending.LastProgressDay} requeues={pending.RouteRequeueCount} source={source} result=arrived-kept");
				}
				bool recentlyProgressed = !pending.Arrived
					&& pending.RouteQueued
					&& pending.LastProgressDay > 0
					&& pending.LastProgressDay >= now.days - AI_ROBBERY_PENDING_CONTACT_EXPIRE_DAYS;
				if (!timeoutExtended && recentlyProgressed && TryExtendPendingAiHumanRobberyMeetingExpiry(pending, now, source, "timeout-moving-route"))
				{
					timeoutExtended = true;
					VerificationLog(
						"AIPlayerRobbery",
						$"robbery-meeting-timeout-deferred robber={pending.RobberPid} human={pending.HumanPid} meetingPeep={pending.MeetingPeepId} meetingVehicle={pending.MeetingVehicleId} meetingNode=NID_{pending.MeetingNodeIndex} queuedDay={pending.QueuedDay} expireDay={pending.ExpireDay} day={now.days} lastProgressDay={pending.LastProgressDay} requeues={pending.RouteRequeueCount} source={source} result=moving-route-kept");
				}
				bool softExpiredBeforeHardWindow = pending.ExpireDay < now.days && now.days <= GetPendingAiHumanRobberyMeetingHardExpireDay(pending);
				bool staleRouteCheckPending = !pending.Arrived
					&& pending.RouteQueued
					&& pending.RouteTargetNodeIndex > 0
					&& pending.NoProgressChecks > 0;
				if (!timeoutExtended && softExpiredBeforeHardWindow && staleRouteCheckPending && TryExtendPendingAiHumanRobberyMeetingExpiry(pending, now, source, "timeout-stale-route-check"))
				{
					timeoutExtended = true;
					VerificationLog(
						"AIPlayerRobbery",
						$"robbery-meeting-timeout-deferred robber={pending.RobberPid} human={pending.HumanPid} meetingPeep={pending.MeetingPeepId} meetingVehicle={pending.MeetingVehicleId} meetingNode=NID_{pending.MeetingNodeIndex} queuedDay={pending.QueuedDay} expireDay={pending.ExpireDay} day={now.days} noProgressChecks={pending.NoProgressChecks} requeues={pending.RouteRequeueCount} source={source} result=stale-route-check-kept");
				}
				if (!timeoutExtended)
				{
					if (IsPendingAiHumanRobberyMeetingExpired(pending, now))
					{
						bool deferredRemoved = ClearDeferredAiHumanRobberyResponseForMeeting(pending, "meeting-route-expired", source);
						VerificationLog("AIPlayerRobbery", $"robbery-meeting-timeout robber={pending.RobberPid} human={pending.HumanPid} meetingPeep={pending.MeetingPeepId} meetingVehicle={pending.MeetingVehicleId} meetingNode=NID_{pending.MeetingNodeIndex} queuedDay={pending.QueuedDay} expireDay={pending.ExpireDay} hardExpireDay={GetPendingAiHumanRobberyMeetingHardExpireDay(pending)} day={now.days} requeues={pending.RouteRequeueCount} deferredRemoved={deferredRemoved} source={source} result=expired-cleared");
						ClearPendingAiHumanRobberyMeeting(pending.RobberPid, pending.HumanPid, "meeting-route-expired", source);
						continue;
					}
				}
			}
			PlayerInfo robber = G.FindPlayerById(pending.RobberPid);
			if (!IsPendingAiHumanRobberyMeetingActorValid(pending, robber, out CrewAssignment validMeetingCrew, out Entity validMeetingPeep, out NodeID validCurrentNodeId, out string validCurrentNodeSource, out string invalidActorReason))
			{
				if (TryReassignPendingAiHumanRobberyMeetingActor(pending, robber, now, source, invalidActorReason))
				{
					TryQueuePendingAiHumanRobberyMeetingRoute(pending, robber, now, source, allowRequeue: true);
					continue;
				}
				VerificationLog(
					"AIPlayerRobbery",
					$"robbery-meeting-actor-reassign-unavailable robber={pending.RobberPid} human={pending.HumanPid} meetingPeep={pending.MeetingPeepId} meetingVehicle={pending.MeetingVehicleId} targetCrew={pending.TargetCrewPeepId} meetingNode=NID_{pending.MeetingNodeIndex} queuedDay={pending.QueuedDay} expireDay={pending.ExpireDay} day={now.days} reassignmentGranted={pending.ActorReassignmentGranted} invalidReason={invalidActorReason} source={source} result=clear-required");
				ClearPendingAiHumanRobberyMeeting(pending.RobberPid, pending.HumanPid, "meeting-actor-invalid-" + invalidActorReason, source);
				continue;
			}
			if (pending.Arrived)
			{
				NodeID currentNodeId = validCurrentNodeId;
				string currentNodeSource = validCurrentNodeSource;
				Node meetingNode = new NodeID(pending.MeetingNodeIndex).FindNode();
				Node currentNode = currentNodeId.FindNode();
				float distance = currentNode != null && meetingNode != null
					? (currentNode.pos - meetingNode.pos).Magnitude
					: -1f;
				bool sameMeetingNode = currentNodeId.IsValid && currentNodeId.index == pending.MeetingNodeIndex;
				bool withinPromptDistance = distance >= 0f && distance <= AI_ROBBERY_NEARBY_PROMPT_WORLD_DISTANCE;
				bool withinArrivedHoldDistance = distance >= 0f && distance <= AI_ROBBERY_ARRIVED_HOLD_WORLD_DISTANCE;
				bool stillPresent = currentNodeId.IsValid
					&& (sameMeetingNode || withinPromptDistance || withinArrivedHoldDistance);
				if (stillPresent)
				{
					pending.LastObservedNodeIndex = currentNodeId.index;
					pending.LastDistanceToMeeting = distance;
					pending.LastProgressDay = now.days;
					bool routeArrivalHold = TryHoldPendingAiHumanRobberyMeetingActor(pending, robber, validMeetingCrew, currentNodeId, now, "arrived-held", source);
					if (!sameMeetingNode && !withinPromptDistance && withinArrivedHoldDistance)
					{
						VerificationLog(
							"AIPlayerRobbery",
							$"robbery-meeting-arrived-hold-grace robber={pending.RobberPid} human={pending.HumanPid} meetingPeep={pending.MeetingPeepId} meetingVehicle={pending.MeetingVehicleId} currentNode={currentNodeId} currentSource={currentNodeSource} meetingNode=NID_{pending.MeetingNodeIndex} distance={distance:0.0} holdDistance={AI_ROBBERY_ARRIVED_HOLD_WORLD_DISTANCE:0.0} promptDistance={AI_ROBBERY_NEARBY_PROMPT_WORLD_DISTANCE:0.0} queuedDay={pending.QueuedDay} day={now.days} requeues={pending.RouteRequeueCount} routeArrivalHold={routeArrivalHold} source={source} result=held-nearby");
					}
					TryExtendPendingAiHumanRobberyMeetingExpiry(pending, now, source, "arrived-held");
					continue;
				}
				pending.Arrived = false;
				pending.RouteQueued = false;
				pending.LastObservedNodeIndex = currentNodeId.IsValid ? currentNodeId.index : pending.LastObservedNodeIndex;
				VerificationLog(
					"AIPlayerRobbery",
					$"robbery-meeting-arrival-lost robber={pending.RobberPid} human={pending.HumanPid} meetingPeep={pending.MeetingPeepId} meetingVehicle={pending.MeetingVehicleId} currentNode={currentNodeId} currentSource={currentNodeSource} meetingNode=NID_{pending.MeetingNodeIndex} distance={distance:0.0} holdDistance={AI_ROBBERY_ARRIVED_HOLD_WORLD_DISTANCE:0.0} queuedDay={pending.QueuedDay} day={now.days} requeues={pending.RouteRequeueCount} source={source} result=route-required");
			}
			if (TryQueuePendingAiHumanRobberyMeetingRoute(pending, robber, now, source, allowRequeue: true))
			{
				continue;
			}
			pending.NoProgressChecks++;
			if (pending.NoProgressChecks >= 2)
			{
				VerificationLog("AIPlayerRobbery", $"robbery-meeting-timeout robber={pending.RobberPid} human={pending.HumanPid} meetingPeep={pending.MeetingPeepId} meetingVehicle={pending.MeetingVehicleId} meetingNode=NID_{pending.MeetingNodeIndex} queuedDay={pending.QueuedDay} day={now.days} noProgressChecks={pending.NoProgressChecks} requeues={pending.RouteRequeueCount} source={source} result=route-unavailable");
			}
		}
	}

	internal static int AddPendingAiHumanRobberyMeetingReservations(PlayerInfo robber, ISet<ulong> reservedVehicleIds)
	{
		if (robber == null || _pendingAiHumanRobberyMeetingsByKey.Count == 0)
		{
			return 0;
		}
		HashSet<long> meetings = new HashSet<long>();
		SimTime now = G.GetNow();
		foreach (PendingAiHumanRobberyMeeting pending in _pendingAiHumanRobberyMeetingsByKey.Values)
		{
			if (pending == null || pending.RobberPid != robber.PID.id || pending.MeetingPeepId <= 0L || IsPendingAiHumanRobberyMeetingExpired(pending, now))
			{
				continue;
			}
			meetings.Add(pending.MeetingPeepId);
			if (pending.MeetingVehicleId > 0L)
			{
				reservedVehicleIds?.Add((ulong)pending.MeetingVehicleId);
			}
		}
		return meetings.Count;
	}

	internal static bool IsCrewReservedForPendingAiHumanRobberyMeeting(PlayerInfo robber, CrewAssignment crew)
	{
		if (robber == null || !crew.IsValid || crew.peepId.IsNotValid)
		{
			return false;
		}
		long peepId = (long)crew.peepId.id;
		long vehicleId = crew.VehicleID.IsValid ? (long)crew.VehicleID.id : 0L;
		SimTime now = G.GetNow();
		return _pendingAiHumanRobberyMeetingsByKey.Values.Any(pending => pending != null
			&& pending.RobberPid == robber.PID.id
			&& !IsPendingAiHumanRobberyMeetingExpired(pending, now)
			&& (pending.MeetingPeepId == peepId || vehicleId > 0L && pending.MeetingVehicleId == vehicleId));
	}

	private static void ExtendArrivedPendingAiHumanRobberyMeetingDeferredResponses(PlayerInfo humanPlayer, SimTime now, string source)
	{
		if (humanPlayer == null || _pendingAiHumanRobberyMeetingsByKey.Count == 0 || _deferredAiHumanRobberyResponsesByKey.Count == 0)
		{
			return;
		}
		foreach (PendingAiHumanRobberyMeeting pending in _pendingAiHumanRobberyMeetingsByKey.Values.ToList())
		{
			if (pending == null || pending.HumanPid != humanPlayer.PID.id || !pending.Arrived)
			{
				continue;
			}
			string key = GetAiHumanRobberyDeferredResponseKey(pending.RobberPid, pending.HumanPid);
			if (string.IsNullOrEmpty(key) || !_deferredAiHumanRobberyResponsesByKey.TryGetValue(key, out DeferredAiHumanRobberyResponse deferred) || deferred == null)
			{
				continue;
			}
			int oldPendingExpireDay = pending.ExpireDay;
			TryExtendPendingAiHumanRobberyMeetingExpiry(pending, now, source, "arrived-response-wait");
			int oldDeferredExpireDay = deferred.ExpireDay;
			int targetExpireDay = Math.Max(deferred.ExpireDay, pending.ExpireDay);
			if (targetExpireDay > deferred.ExpireDay)
			{
				deferred.ExpireDay = targetExpireDay;
				VerificationLog(
					"AIPlayerRobbery",
					$"robbery-meeting-deferred-expiry-extended robber={deferred.RobberPid} human={humanPlayer.PID.id} meetingPeep={pending.MeetingPeepId} meetingVehicle={pending.MeetingVehicleId} targetCrew={deferred.TargetCrewPeepId} meetingNode=NID_{pending.MeetingNodeIndex} oldExpireDay={oldDeferredExpireDay} expireDay={deferred.ExpireDay} pendingExpireDay={oldPendingExpireDay}->{pending.ExpireDay} day={now.days} source={source} result=arrived-meeting-active");
			}
		}
	}

	private static bool TryGetPendingAiHumanRobberyMeeting(int robberPid, int humanPid, out PendingAiHumanRobberyMeeting pending)
	{
		pending = null;
		string key = GetAiHumanRobberyMeetingKey(robberPid, humanPid);
		return !string.IsNullOrEmpty(key)
			&& _pendingAiHumanRobberyMeetingsByKey.TryGetValue(key, out pending)
			&& pending != null;
	}

	private static void ClearPendingAiHumanRobberyMeeting(PlayerInfo robber, PlayerInfo humanPlayer, string reason, string source)
	{
		if (robber == null || humanPlayer == null)
		{
			return;
		}
		ClearPendingAiHumanRobberyMeeting(robber.PID.id, humanPlayer.PID.id, reason, source);
	}

	private static bool ClearDeferredAiHumanRobberyResponseForMeeting(PendingAiHumanRobberyMeeting pending, string reason, string source)
	{
		if (pending == null)
		{
			return false;
		}
		string deferredKey = GetAiHumanRobberyDeferredResponseKey(pending.RobberPid, pending.HumanPid);
		bool removed = !string.IsNullOrEmpty(deferredKey) && _deferredAiHumanRobberyResponsesByKey.Remove(deferredKey);
		if (removed)
		{
			VerificationLog(
				"AIPlayerRobbery",
				$"cleared phase=deferred-response robber={pending.RobberPid} human={pending.HumanPid} targetCrew={pending.TargetCrewPeepId} meetingPeep={pending.MeetingPeepId} meetingVehicle={pending.MeetingVehicleId} meetingNode=NID_{pending.MeetingNodeIndex} queuedDay={pending.QueuedDay} expireDay={pending.ExpireDay} source={source} reason={reason} result=meeting-cleared");
		}
		return removed;
	}

	private static void ClearPendingAiHumanRobberyMeeting(int robberPid, int humanPid, string reason, string source)
	{
		string key = GetAiHumanRobberyMeetingKey(robberPid, humanPid);
		if (string.IsNullOrEmpty(key) || !_pendingAiHumanRobberyMeetingsByKey.TryGetValue(key, out PendingAiHumanRobberyMeeting pending))
		{
			return;
		}
		NodeID currentNodeId = NodeID.INVALID;
		string currentNodeSource = "none";
		try
		{
			PlayerInfo robber = G.FindPlayerById(pending.RobberPid);
			EntityID meetingPeepId = pending.MeetingPeepId > 0L ? EntityID.FromID((ulong)pending.MeetingPeepId) : EntityID.INVALID;
			CrewAssignment meetingCrew = meetingPeepId.IsValid && robber?.crew != null
				? robber.crew.GetCrewForPeep(meetingPeepId)
				: CrewAssignment.EMPTY;
			if (meetingCrew.IsValid)
			{
				TryResolveRuntimeFrontActionCrewNodeQuiet(meetingCrew, meetingCrew.GetPeep(), out currentNodeId, out currentNodeSource);
			}
		}
		catch
		{
		}
		_pendingAiHumanRobberyMeetingsByKey.Remove(key);
		VerificationLog(
			"AIPlayerRobbery",
			$"robbery-meeting-cleared robber={pending.RobberPid} human={pending.HumanPid} meetingPeep={pending.MeetingPeepId} meetingVehicle={pending.MeetingVehicleId} targetCrew={pending.TargetCrewPeepId} currentNode={currentNodeId} currentSource={currentNodeSource} routeTargetNode=NID_{pending.RouteTargetNodeIndex} meetingNode=NID_{pending.MeetingNodeIndex} queuedDay={pending.QueuedDay} source={source} reason={reason} routeQueued={pending.RouteQueued} arrived={pending.Arrived} noProgressChecks={pending.NoProgressChecks} requeues={pending.RouteRequeueCount} result=cleared");
	}

	private static bool ShouldLogKeptDeferredAiHumanRobberyResponse(DeferredAiHumanRobberyResponse deferred, SimTime now, int oldExpireDay)
	{
		if (deferred == null)
		{
			return false;
		}
		bool changed = deferred.ExpireDay != oldExpireDay;
		if (!changed && deferred.LastPendingKeepLogDay == now.days)
		{
			return false;
		}
		deferred.LastPendingKeepLogDay = now.days;
		return true;
	}

	private static bool ShouldReplaceDeferredAiHumanRobberyResponse(DeferredAiHumanRobberyResponse existing, AiRobberyCandidate candidate, SimTime now, string source)
	{
		if (existing == null)
		{
			return true;
		}
		if (existing.EnactedDay != now.days)
		{
			return false;
		}
		int newPriority = GetDeferredAiHumanRobberyPriority(candidate, source);
		int existingPriority = GetDeferredAiHumanRobberyPriority(existing);
		if (newPriority > existingPriority)
		{
			return true;
		}
		if (newPriority < existingPriority)
		{
			return false;
		}
		if (candidate != null && candidate.Distance + 0.25f < existing.Distance)
		{
			return true;
		}
		bool sameTarget = candidate != null && candidate.TargetCrew.IsValid && (long)candidate.TargetCrew.peepId.id == existing.TargetCrewPeepId;
		bool sameNode = candidate?.ContactNode != null && candidate.ContactNode.id.index == existing.TargetNodeIndex;
		return (sameTarget || sameNode) && IsAiHumanRobberyTravelFinalizeSource(source) && !IsAiHumanRobberyTravelFinalizeSource(existing.Source);
	}

	private static int GetDeferredAiHumanRobberyPriority(AiRobberyCandidate candidate, string source)
	{
		if (candidate == null)
		{
			return 0;
		}
		int score = 0;
		if (candidate.SameNode)
		{
			score += 100;
		}
		if (candidate.StrictTrespassNode)
		{
			score += 80;
		}
		if (candidate.HumanTerritory)
		{
			score += 60;
		}
		if (candidate.EnemyTerritory)
		{
			score += 50;
		}
		if (candidate.TerritoryPressure)
		{
			score += 40;
		}
		if (candidate.DirectAggro)
		{
			score += 20;
		}
		if (IsAiHumanRobberyTravelFinalizeSource(source))
		{
			score += 5;
		}
		return score;
	}

	private static int GetDeferredAiHumanRobberyPriority(DeferredAiHumanRobberyResponse existing)
	{
		if (existing == null)
		{
			return 0;
		}
		int score = 0;
		if (existing.SameNode)
		{
			score += 100;
		}
		if (existing.StrictTrespassNode)
		{
			score += 80;
		}
		if (existing.HumanTerritory)
		{
			score += 60;
		}
		if (existing.EnemyTerritory)
		{
			score += 50;
		}
		if (existing.TerritoryPressure)
		{
			score += 40;
		}
		if (existing.DirectAggro)
		{
			score += 20;
		}
		if (IsAiHumanRobberyTravelFinalizeSource(existing.Source))
		{
			score += 5;
		}
		return score;
	}

	private static bool IsAiHumanRobberyTravelFinalizeSource(string source)
	{
		return !string.IsNullOrWhiteSpace(source)
			&& source.IndexOf("travel-finalize", StringComparison.OrdinalIgnoreCase) >= 0;
	}

	internal static void ProcessDeferredAiHumanRobberyResponsesOnHumanTurnStart(PlayerInfo humanPlayer, string sourceTag)
	{
		try
		{
			SimTime now = G.GetNow();
			ClearStaleAiHumanRobberyResponseLocks(humanPlayer, now);
			ServicePendingAiHumanRobberyMeetings(humanPlayer, now, sourceTag + "-meeting-route");
			ExtendArrivedPendingAiHumanRobberyMeetingDeferredResponses(humanPlayer, now, sourceTag);
			if (humanPlayer == null || _deferredAiHumanRobberyResponsesByKey.Count == 0 || HasActiveAiHumanRobberyResponseForHuman(humanPlayer))
			{
				return;
			}
			foreach (KeyValuePair<string, DeferredAiHumanRobberyResponse> kv in _deferredAiHumanRobberyResponsesByKey.OrderBy(item => item.Value?.EnactedDay ?? int.MaxValue).ToList())
			{
				DeferredAiHumanRobberyResponse deferred = kv.Value;
				if (deferred == null)
				{
					_deferredAiHumanRobberyResponsesByKey.Remove(kv.Key);
					continue;
				}
				if (now.days < deferred.NotBeforeDay)
				{
					continue;
				}
				if (TryGetPendingAiHumanRobberyMeeting(deferred.RobberPid, humanPlayer.PID.id, out PendingAiHumanRobberyMeeting expiryPendingMeeting)
					&& !expiryPendingMeeting.Arrived
					&& expiryPendingMeeting.ExpireDay >= now.days
					&& deferred.ExpireDay < expiryPendingMeeting.ExpireDay)
				{
					int oldExpireDay = deferred.ExpireDay;
					deferred.ExpireDay = expiryPendingMeeting.ExpireDay;
					VerificationLog(
						"AIPlayerRobbery",
						$"robbery-meeting-deferred-expiry-extended robber={deferred.RobberPid} human={humanPlayer.PID.id} meetingPeep={expiryPendingMeeting.MeetingPeepId} meetingVehicle={expiryPendingMeeting.MeetingVehicleId} targetCrew={deferred.TargetCrewPeepId} meetingNode=NID_{expiryPendingMeeting.MeetingNodeIndex} oldExpireDay={oldExpireDay} expireDay={deferred.ExpireDay} day={now.days} source={sourceTag} result=pending-meeting-active");
				}
				if (deferred.ExpireDay < now.days)
				{
					_deferredAiHumanRobberyResponsesByKey.Remove(kv.Key);
					ClearPendingAiHumanRobberyMeeting(deferred.RobberPid, humanPlayer.PID.id, "deferred-response-expired", sourceTag);
					VerificationLog("AIPlayerRobbery", $"expired phase=deferred-response robber={deferred.RobberPid} targetCrew={deferred.TargetCrewPeepId} enactedDay={deferred.EnactedDay} expireDay={deferred.ExpireDay} day={now.days} source={sourceTag}");
					continue;
				}
				PlayerInfo robber = G.FindPlayerById(deferred.RobberPid);
				if (!TryBuildAiRobberyCandidateAgainstHuman(robber, humanPlayer, now, out AiRobberyCandidate candidate, out string blockReason, out _, out _))
				{
					if (!TryBuildDeferredAiHumanRobberyCandidateSnapshot(deferred, robber, humanPlayer, now, blockReason, out candidate, out string snapshotReason))
					{
						_deferredAiHumanRobberyResponsesByKey.Remove(kv.Key);
						ClearPendingAiHumanRobberyMeeting(deferred.RobberPid, humanPlayer.PID.id, "deferred-response-cleared-" + snapshotReason, sourceTag);
						VerificationLog("AIPlayerRobbery", $"cleared phase=deferred-response robber={deferred.RobberPid} targetCrew={deferred.TargetCrewPeepId} enactedDay={deferred.EnactedDay} day={now.days} reason={snapshotReason} originalReason={blockReason} source={sourceTag}");
						continue;
					}
					VerificationLog("AIPlayerRobbery", $"restored phase=deferred-response robber={deferred.RobberPid} targetCrew={deferred.TargetCrewPeepId} enactedDay={deferred.EnactedDay} day={now.days} reason={blockReason} result=snapshot-valid source={sourceTag}");
				}
				if (!candidate.TargetCrew.IsValid || (long)candidate.TargetCrew.peepId.id != deferred.TargetCrewPeepId)
				{
					ulong newTarget = candidate.TargetCrew.IsValid ? candidate.TargetCrew.peepId.id : 0UL;
					if (!TryBuildDeferredAiHumanRobberyCandidateSnapshot(deferred, robber, humanPlayer, now, "target-changed", out candidate, out string snapshotReason))
					{
						_deferredAiHumanRobberyResponsesByKey.Remove(kv.Key);
						ClearPendingAiHumanRobberyMeeting(deferred.RobberPid, humanPlayer.PID.id, "deferred-response-cleared-" + snapshotReason, sourceTag);
						VerificationLog("AIPlayerRobbery", $"cleared phase=deferred-response robber={deferred.RobberPid} targetCrew={deferred.TargetCrewPeepId} newTarget={newTarget} enactedDay={deferred.EnactedDay} day={now.days} reason={snapshotReason} originalReason=target-changed source={sourceTag}");
						continue;
					}
					VerificationLog("AIPlayerRobbery", $"restored phase=deferred-response robber={deferred.RobberPid} targetCrew={deferred.TargetCrewPeepId} newTarget={newTarget} enactedDay={deferred.EnactedDay} day={now.days} reason=target-changed result=snapshot-valid source={sourceTag}");
				}
				AiRobberyResolutionPreview preview = BuildAiHumanRobberyResolutionPreview(humanPlayer, candidate);
				if (preview.AvailableCash < GetAiHumanRobberyMinimumCash(candidate))
				{
					_deferredAiHumanRobberyResponsesByKey.Remove(kv.Key);
					ClearPendingAiHumanRobberyMeeting(deferred.RobberPid, humanPlayer.PID.id, "deferred-response-low-cash", sourceTag);
					LogAiHumanRobberyLowCashContactPreview(candidate, humanPlayer, now, "deferred-turn-start:" + deferred.Source, deferred.Mode);
					continue;
				}
				bool meetingArrivalReady = false;
				if (TryGetPendingAiHumanRobberyMeeting(deferred.RobberPid, humanPlayer.PID.id, out PendingAiHumanRobberyMeeting pendingMeeting))
				{
					if (!pendingMeeting.Arrived)
					{
						if (IsPendingAiHumanRobberyMeetingExpired(pendingMeeting, now))
						{
							_deferredAiHumanRobberyResponsesByKey.Remove(kv.Key);
							VerificationLog("AIPlayerRobbery", $"robbery-meeting-prompt-cancelled robber={deferred.RobberPid} human={humanPlayer.PID.id} meetingPeep={pendingMeeting.MeetingPeepId} meetingVehicle={pendingMeeting.MeetingVehicleId} targetCrew={deferred.TargetCrewPeepId} currentNode=NID_{pendingMeeting.LastObservedNodeIndex} routeTargetNode=NID_{pendingMeeting.RouteTargetNodeIndex} meetingNode=NID_{pendingMeeting.MeetingNodeIndex} queuedDay={pendingMeeting.QueuedDay} expireDay={pendingMeeting.ExpireDay} day={now.days} source={sourceTag} reason=meeting-expired result=cancelled");
							ClearPendingAiHumanRobberyMeeting(deferred.RobberPid, humanPlayer.PID.id, "meeting-prompt-expired", sourceTag);
							continue;
						}
						deferred.NotBeforeDay = now.days + 1;
						deferred.ExpireDay = Math.Max(deferred.ExpireDay, pendingMeeting.ExpireDay);
						VerificationLog(
							"AIPlayerRobbery",
							$"robbery-meeting-prompt-deferred robber={deferred.RobberPid} human={humanPlayer.PID.id} meetingPeep={pendingMeeting.MeetingPeepId} meetingVehicle={pendingMeeting.MeetingVehicleId} targetCrew={deferred.TargetCrewPeepId} currentNode=NID_{pendingMeeting.LastObservedNodeIndex} routeTargetNode=NID_{pendingMeeting.RouteTargetNodeIndex} meetingNode=NID_{pendingMeeting.MeetingNodeIndex} queuedDay={pendingMeeting.QueuedDay} day={now.days} nextCheckDay={deferred.NotBeforeDay} expireDay={pendingMeeting.ExpireDay} routeQueued={pendingMeeting.RouteQueued} noProgressChecks={pendingMeeting.NoProgressChecks} requeues={pendingMeeting.RouteRequeueCount} source={sourceTag} result=waiting-for-arrival");
						return;
					}
					if (!EnsureArrivedAiHumanRobberyMeetingTargetInPromptRange(pendingMeeting, deferred, robber, humanPlayer, candidate.TargetCrew, candidate.ContactNode, candidate.TargetNodeSource, now, sourceTag))
					{
						return;
					}
					meetingArrivalReady = true;
					VerificationLog(
						"AIPlayerRobbery",
						$"robbery-meeting-prompt-ready robber={deferred.RobberPid} human={humanPlayer.PID.id} meetingPeep={pendingMeeting.MeetingPeepId} meetingVehicle={pendingMeeting.MeetingVehicleId} targetCrew={deferred.TargetCrewPeepId} meetingNode=NID_{pendingMeeting.MeetingNodeIndex} queuedDay={pendingMeeting.QueuedDay} day={now.days} requeues={pendingMeeting.RouteRequeueCount} source={sourceTag} result=arrival-confirmed");
					short currentPromptNodeIndex = candidate.ContactNode?.id.index ?? 0;
					if (pendingMeeting.MeetingNodeIndex > 0
						&& currentPromptNodeIndex != pendingMeeting.MeetingNodeIndex
						&& TryBuildDeferredAiHumanRobberyCandidateSnapshot(deferred, robber, humanPlayer, now, "target-changed", out AiRobberyCandidate meetingCandidate, out string meetingSnapshotReason))
					{
						candidate = meetingCandidate;
						preview = BuildAiHumanRobberyResolutionPreview(humanPlayer, candidate);
						VerificationLog(
							"AIPlayerRobbery",
							$"robbery-meeting-prompt-node-locked robber={deferred.RobberPid} human={humanPlayer.PID.id} meetingPeep={pendingMeeting.MeetingPeepId} meetingVehicle={pendingMeeting.MeetingVehicleId} targetCrew={deferred.TargetCrewPeepId} currentNode=NID_{currentPromptNodeIndex} meetingNode=NID_{pendingMeeting.MeetingNodeIndex} lockedNode={candidate.ContactNode?.id ?? NodeID.INVALID} snapshotReason={meetingSnapshotReason} day={now.days} source={sourceTag} result=meeting-snapshot");
						if (preview.AvailableCash < GetAiHumanRobberyMinimumCash(candidate))
						{
							_deferredAiHumanRobberyResponsesByKey.Remove(kv.Key);
							ClearPendingAiHumanRobberyMeeting(deferred.RobberPid, humanPlayer.PID.id, "deferred-response-low-cash-after-meeting-lock", sourceTag);
							LogAiHumanRobberyLowCashContactPreview(candidate, humanPlayer, now, "robbery-meeting-arrival:" + deferred.Source, deferred.Mode);
							continue;
						}
					}
					else if (pendingMeeting.MeetingNodeIndex > 0 && currentPromptNodeIndex != pendingMeeting.MeetingNodeIndex)
					{
						VerificationLog(
							"AIPlayerRobbery",
							$"robbery-meeting-prompt-node-lock-skipped robber={deferred.RobberPid} human={humanPlayer.PID.id} meetingPeep={pendingMeeting.MeetingPeepId} meetingVehicle={pendingMeeting.MeetingVehicleId} targetCrew={deferred.TargetCrewPeepId} currentNode=NID_{currentPromptNodeIndex} meetingNode=NID_{pendingMeeting.MeetingNodeIndex} day={now.days} source={sourceTag} result=snapshot-unavailable");
					}
				}
				string displaySource = (meetingArrivalReady ? "robbery-meeting-arrival:" : "deferred-turn-start:") + deferred.Source;
				if (TryShowAiHumanRobberyResponsePopup(candidate, humanPlayer, preview, now, displaySource, deferred.Mode, deferred.CreatedDay >= 0 ? deferred.CreatedDay : deferred.EnactedDay))
				{
					deferred.NotBeforeDay = now.days + 1;
					deferred.ExpireDay = Math.Max(deferred.ExpireDay, now.days + AI_ROBBERY_PENDING_CONTACT_EXPIRE_DAYS);
					if (meetingArrivalReady)
					{
						VerificationLog("AIPlayerRobbery", $"robbery-meeting-prompt-shown robber={deferred.RobberPid} human={humanPlayer.PID.id} targetCrew={deferred.TargetCrewPeepId} enactedDay={deferred.EnactedDay} day={now.days} nextCheckDay={deferred.NotBeforeDay} expireDay={deferred.ExpireDay} source={sourceTag} mode={deferred.Mode} result=shown-after-arrival-choice-pending");
					}
					VerificationLog("AIPlayerRobbery", $"shown phase=deferred-response robber={deferred.RobberPid} targetCrew={deferred.TargetCrewPeepId} enactedDay={deferred.EnactedDay} day={now.days} nextCheckDay={deferred.NotBeforeDay} expireDay={deferred.ExpireDay} source={sourceTag} mode={deferred.Mode} result=shown-choice-pending");
					return;
				}
				VerificationLog("AIPlayerRobbery", $"deferred phase=deferred-response robber={deferred.RobberPid} targetCrew={deferred.TargetCrewPeepId} enactedDay={deferred.EnactedDay} day={now.days} source={sourceTag} mode={deferred.Mode} result=ui-unavailable-kept");
				return;
			}
		}
		catch (Exception ex)
		{
			Debug.LogWarning("[GameplayTweaks] ProcessDeferredAiHumanRobberyResponsesOnHumanTurnStart failed: " + ex.Message);
		}
	}

	private static bool TryBuildDeferredAiHumanRobberyCandidateSnapshot(DeferredAiHumanRobberyResponse deferred, PlayerInfo robber, PlayerInfo humanPlayer, SimTime now, string blockReason, out AiRobberyCandidate candidate, out string reason)
	{
		candidate = null;
		reason = blockReason;
		if (deferred == null || robber == null || humanPlayer?.crew == null)
		{
			reason = "invalid";
			return false;
		}
		if (!IsDeferredAiHumanRobberySnapshotRecoverableReason(blockReason))
		{
			reason = string.IsNullOrEmpty(blockReason) ? "snapshot-not-recoverable" : blockReason;
			return false;
		}
		if (!IsEligibleAiTradeGang(robber))
		{
			reason = "invalid-robber";
			return false;
		}
		if (ArePlayersProtectedByPactAlliance(robber, humanPlayer))
		{
			reason = "pact-protected";
			return false;
		}
		if (HasMutualTruce(robber, humanPlayer))
		{
			reason = "truce";
			return false;
		}
		if (TryGetAiHumanRobberyPairCooldown(robber, humanPlayer, now, out _))
		{
			reason = "human-robbery-pair-cooldown";
			return false;
		}
		if (IsAiHumanRobberyBlockedByGoodRelations(robber, humanPlayer, out _))
		{
			reason = "friendly-relations";
			return false;
		}
		if (deferred.TargetCrewPeepId <= 0L)
		{
			reason = "target-missing";
			return false;
		}
		EntityID targetPeepId = EntityID.FromID((ulong)deferred.TargetCrewPeepId);
		CrewAssignment targetCrew = humanPlayer.crew.GetCrewForPeep(targetPeepId);
		if (!targetCrew.IsValid || targetCrew.GetPeep() == null)
		{
			reason = "target-missing";
			return false;
		}
		Node contactNode = null;
		if (deferred.TargetNodeIndex > 0)
		{
			contactNode = new NodeID(deferred.TargetNodeIndex).FindNode();
		}
		int robberPower = deferred.RobberPower > 0 ? deferred.RobberPower : CalculateGangPower(robber);
		int humanPower = deferred.HumanPower > 0 ? deferred.HumanPower : CalculateGangPower(humanPlayer);
		candidate = new AiRobberyCandidate
		{
			Robber = robber,
			TargetCrew = targetCrew,
			ContactNode = contactNode,
			Distance = deferred.Distance,
			SameNode = deferred.SameNode,
			RobberNodeSource = string.IsNullOrEmpty(deferred.RobberNodeSource) ? "deferred-snapshot" : deferred.RobberNodeSource,
			TargetNodeSource = string.IsNullOrEmpty(deferred.TargetNodeSource) ? "deferred-snapshot" : deferred.TargetNodeSource,
			DirectAggro = deferred.DirectAggro,
			BroadlyHostile = deferred.BroadlyHostile,
			EnemyTerritory = deferred.EnemyTerritory,
			HumanTerritory = deferred.HumanTerritory,
			StrictTrespassNode = deferred.StrictTrespassNode,
			TerritoryPressure = deferred.TerritoryPressure,
			RecentTrespassMemory = deferred.RecentTrespassMemory,
			RobberPower = robberPower,
			HumanPower = humanPower,
			RobberLocalPower = deferred.RobberLocalPower,
			HumanLocalPower = deferred.HumanLocalPower,
			RarityChance = 1f,
			RarityRoll = 0f,
			WouldAttempt = true,
			TrespassGraceDays = deferred.TrespassGraceDays,
			Reason = string.IsNullOrEmpty(deferred.Reason) ? "deferred-snapshot" : deferred.Reason
		};
		reason = "snapshot-valid";
		return true;
	}

	private static bool EnsureArrivedAiHumanRobberyMeetingTargetInPromptRange(PendingAiHumanRobberyMeeting pending, DeferredAiHumanRobberyResponse deferred, PlayerInfo robber, PlayerInfo humanPlayer, CrewAssignment targetCrew, Node fallbackTargetNode, string fallbackTargetNodeSource, SimTime now, string source)
	{
		try
		{
			if (pending == null || deferred == null || robber == null || humanPlayer == null || !pending.Arrived)
			{
				return true;
			}
			Node meetingNode = pending.MeetingNodeIndex > 0 ? new NodeID(pending.MeetingNodeIndex).FindNode() : null;
			if (meetingNode?.id.IsValid != true)
			{
				return true;
			}
			if (!TryGetAiHumanRobberyTargetPhysicalNode(targetCrew, out Node targetNode, out string targetNodeSource))
			{
				if (TryGetCurrentAiHumanRobberyMeetingTargetFallbackNode(fallbackTargetNode, fallbackTargetNodeSource, out Node fallbackNode, out string fallbackNodeSource))
				{
					float fallbackDistance = (fallbackNode.pos - meetingNode.pos).Magnitude;
					if (fallbackNode.id == meetingNode.id || fallbackDistance <= AI_ROBBERY_NEARBY_PROMPT_WORLD_DISTANCE)
					{
						VerificationLog(
							"AIPlayerRobbery",
							$"robbery-meeting-prompt-target-fallback robber={pending.RobberPid} human={pending.HumanPid} meetingPeep={pending.MeetingPeepId} meetingVehicle={pending.MeetingVehicleId} targetCrew={pending.TargetCrewPeepId} meetingNode={meetingNode.id} fallbackNode={fallbackNode.id} fallbackSource={fallbackNodeSource} physicalReason={targetNodeSource} distance={fallbackDistance:0.0} promptDistance={AI_ROBBERY_NEARBY_PROMPT_WORLD_DISTANCE:0.0} queuedDay={pending.QueuedDay} day={now.days} source={source} result=fallback-nearby");
						return true;
					}
					targetNode = fallbackNode;
					targetNodeSource = fallbackNodeSource + "/" + targetNodeSource;
				}
				else
				{
					int hardExpireDay = GetPendingAiHumanRobberyMeetingHardExpireDay(pending);
					if (pending.ExpireDay < now.days || now.days > hardExpireDay)
					{
						bool deferredRemoved = ClearDeferredAiHumanRobberyResponseForMeeting(pending, "meeting-target-node-unavailable-expired", source);
						VerificationLog(
							"AIPlayerRobbery",
							$"robbery-meeting-prompt-target-give-up robber={pending.RobberPid} human={pending.HumanPid} meetingPeep={pending.MeetingPeepId} meetingVehicle={pending.MeetingVehicleId} targetCrew={pending.TargetCrewPeepId} meetingNode={meetingNode.id} queuedDay={pending.QueuedDay} expireDay={pending.ExpireDay} hardExpireDay={hardExpireDay} day={now.days} source={source} reason={targetNodeSource} deferredRemoved={deferredRemoved} result=expired-cleared");
						ClearPendingAiHumanRobberyMeeting(pending.RobberPid, pending.HumanPid, "meeting-target-node-unavailable-expired", source);
						return false;
					}
					int nextExpireDay = ClampPendingAiHumanRobberyMeetingExpireDay(pending, Math.Max(pending.ExpireDay, now.days + AI_ROBBERY_PENDING_CONTACT_EXPIRE_DAYS));
					pending.ExpireDay = Math.Min(hardExpireDay, Math.Max(pending.ExpireDay, nextExpireDay));
					deferred.NotBeforeDay = now.days + 1;
					deferred.ExpireDay = Math.Min(hardExpireDay, Math.Max(deferred.ExpireDay, nextExpireDay));
					VerificationLog(
						"AIPlayerRobbery",
						$"robbery-meeting-prompt-target-deferred robber={pending.RobberPid} human={pending.HumanPid} meetingPeep={pending.MeetingPeepId} meetingVehicle={pending.MeetingVehicleId} targetCrew={pending.TargetCrewPeepId} meetingNode={meetingNode.id} queuedDay={pending.QueuedDay} day={now.days} nextCheckDay={deferred.NotBeforeDay} expireDay={deferred.ExpireDay} source={source} reason={targetNodeSource} result=target-node-unavailable");
					return false;
				}
			}
			float distance = (targetNode.pos - meetingNode.pos).Magnitude;
			if (targetNode.id == meetingNode.id || distance <= AI_ROBBERY_NEARBY_PROMPT_WORLD_DISTANCE)
			{
				return true;
			}

			short oldMeetingNodeIndex = pending.MeetingNodeIndex;
			short oldRouteTargetNodeIndex = pending.RouteTargetNodeIndex;
			int oldQueuedDay = pending.QueuedDay;
			int oldHardExpireDay = GetPendingAiHumanRobberyMeetingHardExpireDay(pending);
			int oldExpireDay = pending.ExpireDay;
			int oldDeferredExpireDay = deferred.ExpireDay;
			bool routeAuthorityReset = TryResetPendingAiHumanRobberyMeetingRouteAuthority(pending, robber, now, "target-moved", source, oldMeetingNodeIndex, targetNode.id.index);
			pending.MeetingNodeIndex = targetNode.id.index;
			pending.RouteTargetNodeIndex = 0;
			pending.RouteQueued = false;
			pending.Arrived = false;
			pending.FinalTargetRouteCommitted = false;
			pending.NoProgressChecks = 0;
			pending.DivergingRouteChecks = 0;
			pending.LastDistanceToMeeting = -1f;
			pending.LastProgressDay = now.days;
			pending.Source = source ?? pending.Source;

			deferred.TargetNodeIndex = targetNode.id.index;
			deferred.Distance = distance;
			deferred.SameNode = false;
			deferred.TargetNodeSource = targetNodeSource ?? string.Empty;
			deferred.NotBeforeDay = now.days + 1;
			bool routeWindowReset = false;
			if (pending.TargetMoveRouteWindowResets < AI_ROBBERY_MEETING_TARGET_MOVE_WINDOW_RESETS)
			{
				pending.TargetMoveRouteWindowResets++;
				pending.QueuedDay = now.days;
				pending.ExpireDay = Math.Max(pending.ExpireDay, now.days + AI_ROBBERY_PENDING_CONTACT_EXPIRE_DAYS);
				deferred.ExpireDay = Math.Max(deferred.ExpireDay, pending.ExpireDay);
				routeWindowReset = true;
				VerificationLog(
					"AIPlayerRobbery",
					$"robbery-meeting-target-moved-window-reset robber={pending.RobberPid} human={pending.HumanPid} meetingPeep={pending.MeetingPeepId} meetingVehicle={pending.MeetingVehicleId} targetCrew={pending.TargetCrewPeepId} oldMeetingNode=NID_{oldMeetingNodeIndex} targetNode={targetNode.id} oldQueuedDay={oldQueuedDay} queuedDay={pending.QueuedDay} oldHardExpireDay={oldHardExpireDay} hardExpireDay={GetPendingAiHumanRobberyMeetingHardExpireDay(pending)} oldExpireDay={oldExpireDay} oldDeferredExpireDay={oldDeferredExpireDay} resets={pending.TargetMoveRouteWindowResets}/{AI_ROBBERY_MEETING_TARGET_MOVE_WINDOW_RESETS} day={now.days} source={source} result=reset");
			}
			deferred.ExpireDay = ClampPendingAiHumanRobberyMeetingExpireDay(pending, Math.Max(deferred.ExpireDay, Math.Max(pending.ExpireDay, now.days + AI_ROBBERY_PENDING_CONTACT_EXPIRE_DAYS)));
			pending.ExpireDay = ClampPendingAiHumanRobberyMeetingExpireDay(pending, Math.Max(pending.ExpireDay, deferred.ExpireDay));

			bool routeQueued = TryQueuePendingAiHumanRobberyMeetingRoute(pending, robber, now, source + "-target-moved", allowRequeue: true);
			VerificationLog(
				"AIPlayerRobbery",
				$"robbery-meeting-prompt-target-moved robber={pending.RobberPid} human={pending.HumanPid} meetingPeep={pending.MeetingPeepId} meetingVehicle={pending.MeetingVehicleId} targetCrew={pending.TargetCrewPeepId} oldMeetingNode=NID_{oldMeetingNodeIndex} oldRouteTargetNode=NID_{oldRouteTargetNodeIndex} targetNode={targetNode.id} targetNodeSource={targetNodeSource} distance={distance:0.0} promptDistance={AI_ROBBERY_NEARBY_PROMPT_WORLD_DISTANCE:0.0} oldQueuedDay={oldQueuedDay} queuedDay={pending.QueuedDay} routeWindowReset={routeWindowReset} routeWindowResets={pending.TargetMoveRouteWindowResets}/{AI_ROBBERY_MEETING_TARGET_MOVE_WINDOW_RESETS} day={now.days} nextCheckDay={deferred.NotBeforeDay} expireDay={deferred.ExpireDay} routeQueued={routeQueued} routeAuthorityReset={routeAuthorityReset} source={source} result={(routeQueued ? "requeued-to-target" : "waiting-for-route")}");
			return false;
		}
		catch (Exception ex)
		{
			Debug.LogWarning("[GameplayTweaks] EnsureArrivedAiHumanRobberyMeetingTargetInPromptRange failed: " + ex.Message);
			return true;
		}
	}

	private static bool TryHoldPendingAiHumanRobberyMeetingActor(PendingAiHumanRobberyMeeting pending, PlayerInfo robber, CrewAssignment meetingCrew, NodeID currentNodeId, SimTime now, string reason, string source)
	{
		if (pending == null || robber?.commands == null || !meetingCrew.IsValid)
		{
			return false;
		}
		try
		{
			if (!robber.commands.PeepHasTask(meetingCrew.peepId))
			{
				return false;
			}
			robber.commands.FlushQueue(meetingCrew.peepId, cancelActive: true);
			VerificationLog(
				"AIPlayerRobbery",
				$"robbery-meeting-arrival-hold robber={pending.RobberPid} human={pending.HumanPid} meetingPeep={pending.MeetingPeepId} meetingVehicle={pending.MeetingVehicleId} currentNode={currentNodeId} meetingNode=NID_{pending.MeetingNodeIndex} routeTargetNode=NID_{pending.RouteTargetNodeIndex} queuedDay={pending.QueuedDay} day={now.days} reason={reason} source={source} result=held");
			return true;
		}
		catch (Exception ex)
		{
			VerificationLog("AIPlayerRobbery", $"robbery-meeting-arrival-hold-failed robber={pending.RobberPid} human={pending.HumanPid} meetingPeep={pending.MeetingPeepId} currentNode={currentNodeId} meetingNode=NID_{pending.MeetingNodeIndex} queuedDay={pending.QueuedDay} day={now.days} reason={reason} source={source} error={ex.GetType().Name}:{ex.Message} result=not-held");
			return false;
		}
	}

	private static bool TryResetPendingAiHumanRobberyMeetingRouteAuthority(PendingAiHumanRobberyMeeting pending, PlayerInfo robber, SimTime now, string reason, string source, short oldMeetingNodeIndex, short newMeetingNodeIndex)
	{
		if (pending == null || robber?.crew == null || robber.commands == null || pending.MeetingPeepId <= 0L)
		{
			return false;
		}
		try
		{
			EntityID meetingPeepId = EntityID.FromID((ulong)pending.MeetingPeepId);
			CrewAssignment meetingCrew = robber.crew.GetCrewForPeep(meetingPeepId);
			if (!meetingCrew.IsValid)
			{
				return false;
			}
			if (!robber.commands.PeepHasTask(meetingCrew.peepId))
			{
				return false;
			}
			robber.commands.FlushQueue(meetingCrew.peepId, cancelActive: true);
			VerificationLog(
				"AIPlayerRobbery",
				$"robbery-meeting-route-authority-reset robber={pending.RobberPid} human={pending.HumanPid} meetingPeep={pending.MeetingPeepId} meetingVehicle={pending.MeetingVehicleId} oldRouteTargetNode=NID_{pending.RouteTargetNodeIndex} oldMeetingNode=NID_{oldMeetingNodeIndex} newMeetingNode=NID_{newMeetingNodeIndex} queuedDay={pending.QueuedDay} day={now.days} reason={reason} source={source} result=reset");
			return true;
		}
		catch (Exception ex)
		{
			VerificationLog("AIPlayerRobbery", $"robbery-meeting-route-authority-reset-failed robber={pending.RobberPid} human={pending.HumanPid} meetingPeep={pending.MeetingPeepId} oldMeetingNode=NID_{oldMeetingNodeIndex} newMeetingNode=NID_{newMeetingNodeIndex} queuedDay={pending.QueuedDay} day={now.days} reason={reason} source={source} error={ex.GetType().Name}:{ex.Message} result=not-reset");
			return false;
		}
	}

	private static bool TryGetCurrentAiHumanRobberyMeetingTargetFallbackNode(Node fallbackTargetNode, string fallbackTargetNodeSource, out Node node, out string source)
	{
		node = null;
		source = "no-current-target-fallback";
		if (fallbackTargetNode?.id.IsValid != true)
		{
			return false;
		}
		string normalizedSource = fallbackTargetNodeSource ?? string.Empty;
		if (string.IsNullOrWhiteSpace(normalizedSource)
			|| string.Equals(normalizedSource, "none", StringComparison.OrdinalIgnoreCase)
			|| normalizedSource.IndexOf("deferred-snapshot", StringComparison.OrdinalIgnoreCase) >= 0)
		{
			source = string.IsNullOrWhiteSpace(normalizedSource) ? "target-fallback-source-empty" : "target-fallback-source-" + normalizedSource;
			return false;
		}
		node = fallbackTargetNode;
		source = "candidate-contact:" + normalizedSource;
		return true;
	}

	private static bool TryGetAiHumanRobberyTargetPhysicalNode(CrewAssignment targetCrew, out Node node, out string source)
	{
		node = null;
		source = "invalid-target";
		try
		{
			if (!targetCrew.IsValid || targetCrew.IsDead)
			{
				return false;
			}
			if (targetCrew.IsInVehicle && targetCrew.VehicleID.IsValid)
			{
				if (MultiCrewVehicleHelper.TryGetStrictPhysicalVehicleNode(targetCrew.VehicleID, out node, out source) && node != null)
				{
					source = string.IsNullOrWhiteSpace(source) ? "target-vehicle-physical" : source;
					return true;
				}
				source = "target-vehicle-not-physical";
				return false;
			}
			Entity peep = targetCrew.GetPeep();
			node = peep?.components?.agent?.GetNode();
			source = node != null ? "target-agent" : "target-agent-missing";
			return node != null;
		}
		catch (Exception ex)
		{
			source = "target-node-error-" + ex.GetType().Name;
			return false;
		}
	}

	private static bool IsDeferredAiHumanRobberySnapshotRecoverableReason(string blockReason)
	{
		return string.Equals(blockReason, "nearby-contact-too-far", StringComparison.Ordinal)
			|| string.Equals(blockReason, "human-territory-contact-too-far", StringComparison.Ordinal)
			|| string.Equals(blockReason, "territory-pressure-too-far", StringComparison.Ordinal)
			|| string.Equals(blockReason, "not-trespassing", StringComparison.Ordinal)
			|| string.Equals(blockReason, "recent-trespass-not-current", StringComparison.Ordinal)
			|| string.Equals(blockReason, "territory-pressure-not-trespass", StringComparison.Ordinal)
			|| string.Equals(blockReason, "target-changed", StringComparison.Ordinal);
	}

	private static bool TryShowAiHumanRobberyResponsePopup(AiRobberyCandidate candidate, PlayerInfo humanPlayer, AiRobberyResolutionPreview preview, SimTime now, string source, string mode, int createdDay)
	{
		if (candidate == null || candidate.Robber == null || humanPlayer == null || preview == null || preview.Amount <= 0 || (!preview.CanDebitVehicle && !preview.CanDebitPeep && !preview.CanDebitSafehouse && !preview.CanDebitCleanCash))
		{
			return false;
		}
		if (global::Game.Game.serv?.ui == null || !global::Game.Game.serv.ui.IsLoadingDone)
		{
			return false;
		}
		if (HasActiveAiHumanRobberyResponseForHuman(humanPlayer))
		{
			VerificationLog("AIPlayerRobbery", $"blocked phase=response-popup source={source} mode={mode} reason=human-response-active robber={candidate.Robber.PID.id} targetCrew={candidate.TargetCrew.peepId.id} vehicle={candidate.TargetCrew.VehicleID.id} node={candidate.ContactNode?.id ?? NodeID.INVALID} result=no-stack");
			return true;
		}
		string key = GetAiHumanRobberyContactKey(candidate.Robber.PID.id, candidate.TargetCrew.peepId.id) + ":response";
		if (_activeAiHumanRobberyResponseKeys.Contains(key))
		{
			VerificationLog("AIPlayerRobbery", $"blocked phase=response-popup source={source} mode={mode} reason=already-active robber={candidate.Robber.PID.id} targetCrew={candidate.TargetCrew.peepId.id} vehicle={candidate.TargetCrew.VehicleID.id} node={candidate.ContactNode?.id ?? NodeID.INVALID} result=no-duplicate");
			return true;
		}

		string robberName = GetGangDisplayName(candidate.Robber.PID.id);
		int favors = GetAiHumanRobberyFavorCount(humanPlayer, candidate.Robber, out EntityID favorPeepId);
		bool evadeRefuseMode = SaveData?.RobberyPromptsEvadeRefuseMode == true;
		string created = createdDay >= 0 ? $" createdDay={createdDay}" : string.Empty;
		string robberyReason = FormatAiHumanRobberyReasonForPlayer(candidate);
		TryShowAiHumanRobberyIntentTicker(candidate, now, source, "response-popup");
		if (evadeRefuseMode)
		{
			VerificationLog(
				"AIPlayerRobbery",
				$"auto phase=response-popup source={source} mode={mode} robber={candidate.Robber.PID.id} targetCrew={candidate.TargetCrew.peepId.id} vehicle={candidate.TargetCrew.VehicleID.id} node={candidate.ContactNode?.id ?? NodeID.INVALID}{created} cashPreview={preview.Amount} availableCash={preview.AvailableCash} safehouseCash={preview.SafehouseCash} vehicleCash={preview.VehicleCash} peepCash={preview.PeepCash} canDebitSafehouse={preview.CanDebitSafehouse} canDebitCleanCash={preview.CanDebitCleanCash} canDebitVehicle={preview.CanDebitVehicle} canDebitPeep={preview.CanDebitPeep} favors={favors} reasonText=\"{robberyReason}\" evadeRefuseMode=True cooldownDays={GetAiHumanRobberyCooldownSummary()} result=auto-evasion");
			try
			{
				ResolveAiHumanRobberyEvasion(candidate, humanPlayer, preview, now, source, mode + "-standing-order", createdDay);
			}
			finally
			{
				ClearAiHumanRobberyResponseAfterExplicitChoice(candidate, humanPlayer, "standing-order-evasion", source);
			}
			return true;
		}
		_activeAiHumanRobberyResponseKeys.Add(key);
		_activeAiHumanRobberyResponseDayByKey[key] = now.days;
		_activeAiHumanRobberyResponseHumanPids.Add(humanPlayer.PID.id);
		VerificationLog(
			"AIPlayerRobbery",
			$"prompt phase=response-popup source={source} mode={mode} robber={candidate.Robber.PID.id} targetCrew={candidate.TargetCrew.peepId.id} vehicle={candidate.TargetCrew.VehicleID.id} node={candidate.ContactNode?.id ?? NodeID.INVALID}{created} cashPreview={preview.Amount} availableCash={preview.AvailableCash} safehouseCash={preview.SafehouseCash} vehicleCash={preview.VehicleCash} peepCash={preview.PeepCash} canDebitSafehouse={preview.CanDebitSafehouse} canDebitCleanCash={preview.CanDebitCleanCash} canDebitVehicle={preview.CanDebitVehicle} canDebitPeep={preview.CanDebitPeep} favors={favors} reasonText=\"{robberyReason}\" evadeRefuseMode={evadeRefuseMode} cooldownDays={GetAiHumanRobberyCooldownSummary()} result=shown");
		ClearPendingAiHumanRobberyMeeting(candidate.Robber, humanPlayer, "response-popup-shown-choice-pending", source);

		Action clearActive = delegate
		{
			_activeAiHumanRobberyResponseKeys.Remove(key);
			_activeAiHumanRobberyResponseDayByKey.Remove(key);
			if (_activeAiHumanRobberyResponseKeys.Count == 0)
			{
				_activeAiHumanRobberyResponseHumanPids.Remove(humanPlayer.PID.id);
			}
		};
		Action pay = delegate
		{
			try
			{
				TryResolveAiHumanVehicleRobberyContact(candidate, humanPlayer, preview, now, source, mode + "-pay", createdDay);
			}
			finally
			{
				ClearAiHumanRobberyResponseAfterExplicitChoice(candidate, humanPlayer, "explicit-pay", source);
				clearActive();
			}
		};
		Action refuse = delegate
		{
			try
			{
				ResolveAiHumanRobberyRefusal(candidate, humanPlayer, preview, now, source, mode, createdDay);
			}
			finally
			{
				ClearAiHumanRobberyResponseAfterExplicitChoice(candidate, humanPlayer, "explicit-refuse", source);
				clearActive();
			}
		};
		Action favor = delegate
		{
			try
			{
				ResolveAiHumanRobberyFavor(candidate, humanPlayer, preview, now, source, mode, createdDay, favorPeepId);
			}
			finally
			{
				ClearAiHumanRobberyResponseAfterExplicitChoice(candidate, humanPlayer, "explicit-favor", source);
				clearActive();
			}
		};
		string message = FormatAiHumanRobberyPopupMessage(robberName, preview.Amount, favors, evadeRefuseMode, candidate);
		if (favors > 0)
		{
			global::Game.UI.Session.Popups.OkPopup.ShowOkCancel(
				message,
				"Use Favor",
				"Refuse",
				favor,
				refuse);
		}
		else
		{
			global::Game.UI.Session.Popups.OkPopup.ShowOkCancel(
				message,
				"Pay $" + preview.Amount.ToString(CultureInfo.InvariantCulture),
				"Refuse",
				pay,
				refuse);
		}
		return true;
	}

	private static void ClearAiHumanRobberyResponseAfterExplicitChoice(AiRobberyCandidate candidate, PlayerInfo humanPlayer, string reason, string source)
	{
		try
		{
			if (candidate?.Robber == null || humanPlayer == null)
			{
				return;
			}
			string deferredKey = GetAiHumanRobberyDeferredResponseKey(candidate.Robber, humanPlayer);
			bool deferredRemoved = !string.IsNullOrEmpty(deferredKey) && _deferredAiHumanRobberyResponsesByKey.Remove(deferredKey);
			ClearPendingAiHumanRobberyMeeting(candidate.Robber, humanPlayer, reason, source);
			VerificationLog(
				"AIPlayerRobbery",
				$"cleared phase=response-popup robber={candidate.Robber.PID.id} human={humanPlayer.PID.id} targetCrew={candidate.TargetCrew.peepId.id} vehicle={candidate.TargetCrew.VehicleID.id} node={candidate.ContactNode?.id ?? NodeID.INVALID} source={source} reason={reason} deferredRemoved={deferredRemoved} result=choice-cleared");
		}
		catch (Exception ex)
		{
			Debug.LogWarning("[GameplayTweaks] ClearAiHumanRobberyResponseAfterExplicitChoice failed: " + ex.Message);
		}
	}

	internal static bool HasActiveAiHumanRobberyResponseForHuman(PlayerInfo humanPlayer)
	{
		try
		{
			return humanPlayer?.PID.IsHumanPlayer == true && _activeAiHumanRobberyResponseHumanPids.Contains(humanPlayer.PID.id);
		}
		catch
		{
			return false;
		}
	}

	private static void ClearStaleAiHumanRobberyResponseLocks(PlayerInfo humanPlayer, SimTime now)
	{
		try
		{
			if (_activeAiHumanRobberyResponseKeys.Count == 0)
			{
				return;
			}
			int currentDay = now.days;
			List<string> staleKeys = _activeAiHumanRobberyResponseKeys
				.Where(key => !_activeAiHumanRobberyResponseDayByKey.TryGetValue(key, out int shownDay) || currentDay - shownDay >= AI_ROBBERY_RESPONSE_LOCK_STALE_DAYS)
				.ToList();
			if (staleKeys.Count == 0)
			{
				return;
			}
			foreach (string staleKey in staleKeys)
			{
				_activeAiHumanRobberyResponseKeys.Remove(staleKey);
				_activeAiHumanRobberyResponseDayByKey.Remove(staleKey);
			}
			if (_activeAiHumanRobberyResponseKeys.Count == 0)
			{
				_activeAiHumanRobberyResponseHumanPids.Clear();
			}
			VerificationLog("AIPlayerRobbery", $"cleared phase=response-popup reason=abandoned-active-locks count={staleKeys.Count} day={currentDay} staleAfterDays={AI_ROBBERY_RESPONSE_LOCK_STALE_DAYS} human={humanPlayer?.PID.id ?? -1} result=unlocked");
		}
		catch (Exception ex)
		{
			Debug.LogWarning("[GameplayTweaks] ClearStaleAiHumanRobberyResponseLocks failed: " + ex.Message);
		}
	}

	private static string FormatAiHumanRobberyPopupMessage(string robberName, int cash, int favors, bool evadeRefuseMode, AiRobberyCandidate candidate)
	{
		string reasonLine = "\n\n" + FormatAiHumanRobberyReasonForPlayer(candidate);
		string favorLine = favors > 0
			? "\n\nYou can call in a favor with the outfit or refuse and risk injury."
			: "\n\nYou can pay them or refuse and risk injury.";
		return robberName + " has caught up with one of your crews and is trying to rob them for $" + cash.ToString(CultureInfo.InvariantCulture) + "." + reasonLine + favorLine;
	}

	private static string FormatAiHumanRobberyReasonForPlayer(AiRobberyCandidate candidate)
	{
		string reason = candidate?.Reason ?? string.Empty;
		if (candidate?.StrictTrespassNode == true
			|| candidate?.EnemyTerritory == true
			|| candidate?.RecentTrespassMemory == true
			|| reason.IndexOf("trespass", StringComparison.OrdinalIgnoreCase) >= 0)
		{
			return "Reason: crew was in territory.";
		}
		if (candidate?.HumanTerritory == true
			|| candidate?.TerritoryPressure == true
			|| reason.IndexOf("territory-pressure", StringComparison.OrdinalIgnoreCase) >= 0
			|| reason.IndexOf("human-territory", StringComparison.OrdinalIgnoreCase) >= 0
			|| reason.IndexOf("weak", StringComparison.OrdinalIgnoreCase) >= 0)
		{
			return "Reason: outfit deems player a weak nearby neighbor.";
		}
		return "Reason: outfit sees an opening against your crew.";
	}

	private static bool TryShowAiHumanRobberyIntentTicker(AiRobberyCandidate candidate, SimTime now, string source, string mode)
	{
		if (candidate == null || candidate.Robber == null || !candidate.TargetCrew.IsValid)
		{
			return false;
		}
		try
		{
			string key = GetAiHumanRobberyContactKey(candidate.Robber.PID.id, candidate.TargetCrew.peepId.id) + ":intent";
			if (_aiHumanRobberyApproachWarningDayByKey.TryGetValue(key, out int lastDay)
				&& now.days - lastDay < AI_ROBBERY_APPROACH_WARNING_COOLDOWN_DAYS)
			{
				return false;
			}
			_aiHumanRobberyApproachWarningDayByKey[key] = now.days;
			string robberName = GetGangDisplayName(candidate.Robber.PID.id);
			string message = CrewRelationshipHandlerPatch.SanitizeUiGlyphText("ROBBERY: " + robberName + " is looking to rob one of your crews.\n" + FormatAiHumanRobberyReasonForPlayer(candidate), aggressive: true).Trim();
			TickerTarget target = candidate.ContactNode?.id.IsValid == true
				? (TickerTarget)candidate.ContactNode.id
				: default(TickerTarget);
			global::Game.Game.ctx?.hud?.tickers?.AddTextTicker(TickerIcon.GANG_ATTACK, TickerTitle.GANG_ATTACK, message, target, TickerPersistType.Persist);
			VerificationLog("AIPlayerRobbery", $"indicator phase=robbery-intent source={source} mode={mode} robber={candidate.Robber.PID.id} targetCrew={candidate.TargetCrew.peepId.id} node={candidate.ContactNode?.id ?? NodeID.INVALID} reasonText=\"{FormatAiHumanRobberyReasonForPlayer(candidate)}\" result=shown");
			return true;
		}
		catch (Exception ex)
		{
			Debug.LogWarning("[GameplayTweaks] TryShowAiHumanRobberyIntentTicker failed: " + ex.Message);
			return false;
		}
	}

	private static int GetAiHumanRobberyFavorCount(PlayerInfo humanPlayer, PlayerInfo robber, out EntityID favorPeepId)
	{
		favorPeepId = GetCrewPeepForPlayer(robber);
		try
		{
			if (humanPlayer?.social == null || favorPeepId.IsNotValid)
			{
				return 0;
			}
			return humanPlayer.social.GetRelationshipFromSourceToPlayer(favorPeepId)?.GetTicketsAvailable() ?? 0;
		}
		catch
		{
			return 0;
		}
	}

	private static bool TrySpendAiHumanRobberyFavor(PlayerInfo humanPlayer, PlayerInfo robber, EntityID favorPeepId, out int remaining)
	{
		remaining = 0;
		try
		{
			if (humanPlayer?.social == null)
			{
				return false;
			}
			if (favorPeepId.IsNotValid)
			{
				favorPeepId = GetCrewPeepForPlayer(robber);
			}
			Relationship relationship = favorPeepId.IsValid ? humanPlayer.social.GetRelationshipFromSourceToPlayer(favorPeepId) : null;
			if (relationship == null || relationship.GetTicketsAvailable() <= 0 || !relationship.DoSpendTickets(1))
			{
				return false;
			}
			remaining = relationship.GetTicketsAvailable();
			return true;
		}
		catch (Exception ex)
		{
			Debug.LogWarning("[GameplayTweaks] TrySpendAiHumanRobberyFavor failed: " + ex.Message);
			return false;
		}
	}

	private static void ResolveAiHumanRobberyFavor(AiRobberyCandidate candidate, PlayerInfo humanPlayer, AiRobberyResolutionPreview preview, SimTime now, string source, string mode, int createdDay, EntityID favorPeepId)
	{
		if (TryBlockDuplicateAiHumanRobberyResponse(candidate, humanPlayer, now, source, mode + "-favor", "favor"))
		{
			return;
		}
		string created = createdDay >= 0 ? $" createdDay={createdDay}" : string.Empty;
		string robberName = GetGangDisplayName(candidate.Robber.PID.id);
		if (!TrySpendAiHumanRobberyFavor(humanPlayer, candidate.Robber, favorPeepId, out int remaining))
		{
			VerificationLog(
				"AIPlayerRobbery",
				$"blocked phase=response-popup source={source} mode={mode}-favor reason=favor-missing robber={candidate.Robber.PID.id} targetCrew={candidate.TargetCrew.peepId.id} vehicle={candidate.TargetCrew.VehicleID.id} node={candidate.ContactNode?.id ?? NodeID.INVALID}{created} result=no-resolution");
			global::Game.UI.Session.Popups.OkPopup.ShowOkCancel(
				"You do not have a favor ready with " + robberName + ". Pay them or refuse and risk getting hurt.",
				"Pay $" + preview.Amount.ToString(CultureInfo.InvariantCulture),
				"Refuse",
				delegate { TryResolveAiHumanVehicleRobberyContact(candidate, humanPlayer, preview, now, source, mode + "-pay-after-favor-miss", createdDay); },
				delegate { ResolveAiHumanRobberyRefusal(candidate, humanPlayer, preview, now, source, mode + "-favor-miss", createdDay); });
			return;
		}

		LogGrapevine($"ROBBERY: {robberName} stopped one of your crews, but you called in a favor and talked your way out.");
		RecordAiHumanRobberyDiagnosticCooldown(candidate, now);
		RecordAiHumanRobberyPairCooldown(candidate, humanPlayer, now, "favor");
		VerificationLog(
			"AIPlayerRobbery",
			$"resolved phase=response-popup source={source} mode={mode}-favor robber={candidate.Robber.PID.id} targetCrew={candidate.TargetCrew.peepId.id} vehicle={candidate.TargetCrew.VehicleID.id} node={candidate.ContactNode?.id ?? NodeID.INVALID}{created} cash=0 favorPeep={favorPeepId.id} favorRemaining={remaining} cooldownDays={GetAiHumanRobberyCooldownSummary()} result=favor");
	}

	private static void ResolveAiHumanRobberyEvasion(AiRobberyCandidate candidate, PlayerInfo humanPlayer, AiRobberyResolutionPreview preview, SimTime now, string source, string mode, int createdDay)
	{
		if (candidate == null || candidate.Robber == null || humanPlayer == null || preview == null)
		{
			return;
		}
		if (TryBlockDuplicateAiHumanRobberyResponse(candidate, humanPlayer, now, source, mode + "-evade", "evade"))
		{
			return;
		}
		float chance = CalculateAiHumanRobberyEvasionChance(candidate);
		bool escaped = SharedRng.NextDouble() < (double)chance;
		string robberName = GetGangDisplayName(candidate.Robber.PID.id);
		string created = createdDay >= 0 ? $" createdDay={createdDay}" : string.Empty;
		if (!escaped)
		{
			VerificationLog(
				"AIPlayerRobbery",
				$"resolved phase=response-popup source={source} mode={mode}-evade robber={candidate.Robber.PID.id} targetCrew={candidate.TargetCrew.peepId.id} vehicle={candidate.TargetCrew.VehicleID.id} node={candidate.ContactNode?.id ?? NodeID.INVALID}{created} chance={chance:0.00} roll=fail cash=0 result=evade-failed");
			ResolveAiHumanRobberyRefusal(candidate, humanPlayer, preview, now, source, mode + "-evade-failed", createdDay);
			return;
		}

		CrewRelationshipHandlerPatch.TryAwardCrewStreetCreditProgress(humanPlayer, candidate.TargetCrew.peepId, 0.02f, 0.04f, "ai-robbery-evaded");
		GangOpsChannel channel = ResolveGangOpsChannelForGang(candidate.Robber.PID.id);
		int desiredCrewCount = Mathf.Clamp((candidate.Robber?.crew?.LivingCrewCount ?? 1) >= 3 || candidate.DirectAggro ? 3 : 2, 1, 3);
		float escalationChance = CalculateAiHumanRobberyEvasionRetaliationChance(candidate);
		double escalationRoll = SharedRng.NextDouble();
		bool retaliationQueued = false;
		bool retaliationInitializedSameTurn = false;
		int attackCrewCount = 0;
		int escalationDueDay = now.days + 1;
		string retaliationAction = "evaded-no-retaliation";
		string retaliationResult = "evaded";
		string escalationReason = FormatAiHumanRobberyEvasionRetaliationReason(candidate);
		if (escalationRoll < (double)escalationChance)
		{
			string successOutcome = ChooseAiHumanRobberyEvasionRetaliationOutcome(candidate, humanPlayer, out float attackChance, out double attackRoll, out string attackReason);
			escalationReason += $"; outcome={successOutcome}; attackChance={attackChance:0.00}; attackRoll={attackRoll:0.00}; {attackReason}";
			if (string.Equals(successOutcome, "attack", StringComparison.Ordinal))
			{
				retaliationQueued = TryInitializeAiHumanRobberyRefusalAttack(channel, candidate, humanPlayer, desiredCrewCount, escalationDueDay, "ai-human-robbery-evaded-attack-same-turn-init", out attackCrewCount, out retaliationAction);
				retaliationInitializedSameTurn = retaliationQueued;
				retaliationResult = retaliationQueued ? "evaded-attack-initialized" : "evaded-attack-init-failed";
				if (!retaliationQueued && TryForceCloseRobberyRetaliationImportantBusiness(channel, candidate.Robber, humanPlayer, "ai-human-robbery-evaded-attack-unavailable-business", out string fallbackBusinessAction))
				{
					retaliationQueued = true;
					retaliationAction = "attack-unavailable-business-closure:" + fallbackBusinessAction;
					retaliationResult = "evaded-attack-unavailable-business-closure";
				}
			}
			else if (TryForceCloseRobberyRetaliationImportantBusiness(channel, candidate.Robber, humanPlayer, "ai-human-robbery-evaded-business", out string businessAction))
			{
				retaliationQueued = true;
				retaliationAction = "business-closure:" + businessAction;
				retaliationResult = "evaded-business-closure";
			}
			else
			{
				retaliationQueued = TryInitializeAiHumanRobberyRefusalAttack(channel, candidate, humanPlayer, desiredCrewCount, escalationDueDay, "ai-human-robbery-evaded-business-unavailable-attack-same-turn-init", out attackCrewCount, out string fallbackAttackAction);
				retaliationInitializedSameTurn = retaliationQueued;
				retaliationAction = retaliationQueued ? "business-unavailable-" + fallbackAttackAction : "business-unavailable-attack-init-failed";
				retaliationResult = retaliationQueued ? "evaded-business-unavailable-attack-initialized" : "evaded-business-unavailable-attack-init-failed";
			}
		}
		if (retaliationQueued)
		{
			ActivateWarBetweenPlayers(candidate.Robber, humanPlayer);
			AddAiRobberyWarHeatWithDiagnostic(
				channel,
				candidate.Robber.PID.id,
				humanPlayer.PID.id,
				candidate.Robber,
				humanPlayer,
				AI_ROBBERY_CONTACT_EVADE_HEAT_GAIN,
				"ai-human-robbery-evaded",
				"human-robbery-evaded",
				"ai-human-contact",
				cash: 0,
				success: false,
				highValue: false,
				externalNetwork: true,
				direction: "robber-to-human");
		}
		VerificationLog(
			"AIPlayerRobbery",
			$"evasion-escalation phase=response-popup robber={candidate.Robber.PID.id} human={humanPlayer.PID.id} targetCrew={candidate.TargetCrew.peepId.id} chance={escalationChance:0.00} roll={escalationRoll:0.00} eligibleReason=\"{escalationReason}\" escalated={retaliationQueued} queued={retaliationQueued} initializedSameTurn={retaliationInitializedSameTurn} dueDay={(retaliationQueued && retaliationAction.IndexOf("attack", StringComparison.OrdinalIgnoreCase) >= 0 ? (retaliationInitializedSameTurn ? now.days : escalationDueDay) : -1)} desiredCrew={desiredCrewCount} action={retaliationAction} source={source} mode={mode}");
		if (retaliationAction.StartsWith("business-closure:", StringComparison.Ordinal) || retaliationAction.IndexOf("business-closure:", StringComparison.Ordinal) >= 0)
		{
			LogGrapevine($"ROBBERY: {robberName} tried to rob one of your crews, but they slipped away. {robberName} shifted pressure to one of your important shops.");
		}
		else if (retaliationQueued)
		{
			LogGrapevine($"ROBBERY: {robberName} tried to rob one of your crews, but they slipped away. {robberName} may hit back next turn.");
		}
		else
		{
			LogGrapevine($"ROBBERY: {robberName} tried to rob one of your crews, but they slipped away under your standing order.");
		}
		RecordAiHumanRobberyDiagnosticCooldown(candidate, now);
		RecordAiHumanRobberyPairCooldown(candidate, humanPlayer, now, retaliationResult);
		int cashBefore = preview.CanDebitVehicle ? preview.VehicleCash : Math.Max(preview.PeepCash, Math.Max(preview.SafehouseCash, preview.TotalCash));
		VerificationLog(
			"AIPlayerRobbery",
			$"resolved phase=response-popup source={source} mode={mode}-evade robber={candidate.Robber.PID.id} targetCrew={candidate.TargetCrew.peepId.id} vehicle={candidate.TargetCrew.VehicleID.id} node={candidate.ContactNode?.id ?? NodeID.INVALID}{created} chance={chance:0.00} cash=0 cashBefore={cashBefore} retaliationQueued={retaliationQueued} retaliationAction={retaliationAction} evasionEscalated={retaliationQueued} escalationChance={escalationChance:0.00} escalationRoll={escalationRoll:0.00} attackCrews={attackCrewCount} heat={(retaliationQueued ? AI_ROBBERY_CONTACT_EVADE_HEAT_GAIN : 0f):0.0} cooldownDays={GetAiHumanRobberyCooldownSummary()} result={retaliationResult}");
	}

	private static void ResolveAiHumanRobberyRefusal(AiRobberyCandidate candidate, PlayerInfo humanPlayer, AiRobberyResolutionPreview preview, SimTime now, string source, string mode, int createdDay)
	{
		if (TryBlockDuplicateAiHumanRobberyResponse(candidate, humanPlayer, now, source, mode + "-refuse", "refuse"))
		{
			return;
		}
		bool heldFirm = SharedRng.NextDouble() < CalculateAiHumanRobberyRefusalHoldChance(candidate);
		bool cashDebited = false;
		int cashBefore = preview.CanDebitVehicle ? preview.VehicleCash : preview.SafehouseCash;
		int cashAfter = cashBefore;
		string cashSource = preview.CanDebitVehicle ? "vehicle" : "safehouse";

		GangOpsChannel channel = ResolveGangOpsChannelForGang(candidate.Robber.PID.id);
		ActivateWarBetweenPlayers(candidate.Robber, humanPlayer);
		AddAiRobberyWarHeatWithDiagnostic(
			channel,
			candidate.Robber.PID.id,
			humanPlayer.PID.id,
			candidate.Robber,
			humanPlayer,
			AI_ROBBERY_CONTACT_REFUSE_HEAT_GAIN,
			"ai-human-robbery-refused",
			"human-robbery-refused",
			"ai-human-contact",
			cash: 0,
			success: false,
			highValue: false,
			externalNetwork: true,
			direction: "robber-to-human");
		int desiredCrewCount = Mathf.Clamp((candidate.Robber?.crew?.LivingCrewCount ?? 1) >= 3 || candidate.DirectAggro ? 3 : 2, 1, 3);
		bool retaliationQueued = false;
		bool retaliationInitializedSameTurn = false;
		int attackCrewCount = 0;
		string attackAction = "refusal-unresolved";
		int escalationDueDay = now.days + 1;
		float escalationChance = 1f;
		double escalationRoll = 0d;
		string escalationReason = "resolved-three-way";
		string result = "refused";
		if (!heldFirm)
		{
			cashDebited = TryDebitAiHumanRobberyCash(humanPlayer, candidate.TargetCrew, preview.Amount, out cashBefore, out cashAfter, out cashSource);
			if (cashDebited)
			{
				bool robberCredited = TryCreditGangSafehouseCash(candidate.Robber, preview.Amount);
				AddAiRobberyWarHeatWithDiagnostic(
					channel,
					humanPlayer.PID.id,
					candidate.Robber.PID.id,
					candidate.Robber,
					humanPlayer,
					AI_ROBBERY_CONTACT_VEHICLE_HEAT_GAIN,
					"ai-human-robbery-refusal-failed",
					"human-robbery-refusal-failed",
					"ai-human-contact",
					preview.Amount,
					success: true,
					highValue: false,
					externalNetwork: true,
					direction: "human-to-robber");
				attackAction = "refusal-failed-paid";
				result = "refusal-failed-paid";
				VerificationLog("AIPlayerRobbery", $"refusal-payment phase=response-popup robber={candidate.Robber.PID.id} human={humanPlayer.PID.id} targetCrew={candidate.TargetCrew.peepId.id} cash={preview.Amount} cashSource={cashSource} cashBefore={cashBefore} cashAfter={cashAfter} robberCredited={robberCredited} source={source} mode={mode}");
			}
			else
			{
				heldFirm = true;
				attackAction = "refusal-payment-failed";
				escalationReason = "payment-debit-failed";
			}
		}
		if (heldFirm)
		{
			string successOutcome = ChooseAiHumanRobberyRefusalSuccessOutcome(candidate, humanPlayer, out escalationChance, out escalationRoll, out escalationReason);
			if (string.Equals(successOutcome, "attack", StringComparison.Ordinal))
			{
				retaliationQueued = TryInitializeAiHumanRobberyRefusalAttack(channel, candidate, humanPlayer, desiredCrewCount, escalationDueDay, "ai-human-robbery-refused-attack-same-turn-init", out attackCrewCount, out attackAction);
				retaliationInitializedSameTurn = retaliationQueued;
				result = retaliationQueued ? "refused-attack-initialized" : "refused-attack-init-failed";
				if (!retaliationQueued && TryForceCloseRobberyRetaliationImportantBusiness(channel, candidate.Robber, humanPlayer, "ai-human-robbery-refused-attack-unavailable-business", out string fallbackBusinessAction))
				{
					retaliationQueued = true;
					attackAction = "attack-unavailable-business-closure:" + fallbackBusinessAction;
					result = "refused-attack-unavailable-business-closure";
				}
				else if (!retaliationQueued && QueueAiHumanRobberyRefusalAttack(channel, candidate, humanPlayer, escalationDueDay, "ai-human-robbery-refused-attack-delayed"))
				{
					retaliationQueued = true;
					retaliationInitializedSameTurn = false;
					attackCrewCount = 0;
					attackAction = "attack-delayed-revenge";
					result = "refused-attack-delayed-revenge";
				}
			}
			else if (TryForceCloseRobberyRetaliationImportantBusiness(channel, candidate.Robber, humanPlayer, "ai-human-robbery-refused-business", out string businessAction))
			{
				retaliationQueued = true;
				attackAction = "business-closure:" + businessAction;
				result = "refused-business-closure";
			}
			else
			{
				retaliationQueued = TryInitializeAiHumanRobberyRefusalAttack(channel, candidate, humanPlayer, desiredCrewCount, escalationDueDay, "ai-human-robbery-refused-business-unavailable-attack-same-turn-init", out attackCrewCount, out string fallbackAttackAction);
				retaliationInitializedSameTurn = retaliationQueued;
				if (retaliationQueued)
				{
					attackAction = "business-unavailable-" + fallbackAttackAction;
					result = "refused-business-unavailable-attack-initialized";
				}
				else if (QueueAiHumanRobberyRefusalAttack(channel, candidate, humanPlayer, escalationDueDay, "ai-human-robbery-refused-business-unavailable-attack-delayed"))
				{
					retaliationQueued = true;
					retaliationInitializedSameTurn = false;
					attackCrewCount = 0;
					attackAction = "business-unavailable-attack-delayed-revenge";
					result = "refused-business-unavailable-attack-delayed-revenge";
				}
				else
				{
					attackAction = "business-unavailable-attack-init-failed";
					result = "refused-business-unavailable-attack-init-failed";
				}
			}
		}
		ApplyAiHumanRobberyRefusalRelationshipBuffs(candidate, humanPlayer, heldFirm, result);
		VerificationLog(
			"AIPlayerRobbery",
			$"refusal-escalation phase=response-popup robber={candidate.Robber.PID.id} human={humanPlayer.PID.id} targetCrew={candidate.TargetCrew.peepId.id} chance={escalationChance:0.00} roll={escalationRoll:0.00} eligibleReason={escalationReason} escalated={retaliationQueued} queued={retaliationQueued} initializedSameTurn={retaliationInitializedSameTurn} dueDay={(retaliationQueued && attackAction.IndexOf("attack", StringComparison.OrdinalIgnoreCase) >= 0 ? (retaliationInitializedSameTurn ? now.days : escalationDueDay) : -1)} desiredCrew={desiredCrewCount} action={attackAction} source={source} mode={mode}");
		string robberName = GetGangDisplayName(candidate.Robber.PID.id);
		string created = createdDay >= 0 ? $" createdDay={createdDay}" : string.Empty;
		if (heldFirm)
		{
			GrantStreetCredit(humanPlayer, 1);
			CrewRelationshipHandlerPatch.TryAwardCrewStreetCreditProgress(humanPlayer, candidate.TargetCrew.peepId, 0.03f, 0.05f, "ai-robbery-refused");
			LogGrapevine(attackAction.StartsWith("business-closure:", StringComparison.Ordinal)
				? $"ROBBERY: {robberName} tried to rob one of your crews, but they stood firm. {robberName} shifted pressure to one of your important shops."
				: $"ROBBERY: {robberName} tried to rob one of your crews, but they stood firm and kept the cash. {robberName} may hit back next turn.");
		}
		else
		{
			LogGrapevine($"ROBBERY: One of your crews refused to pay {robberName}, but the refusal failed and they took ${preview.Amount}.");
		}
		RecordAiHumanRobberyDiagnosticCooldown(candidate, now);
		RecordAiHumanRobberyPairCooldown(candidate, humanPlayer, now, result);
		VerificationLog(
			"AIPlayerRobbery",
			$"resolved phase=response-popup source={source} mode={mode}-refuse robber={candidate.Robber.PID.id} targetCrew={candidate.TargetCrew.peepId.id} vehicle={candidate.TargetCrew.VehicleID.id} node={candidate.ContactNode?.id ?? NodeID.INVALID}{created} heldFirm={heldFirm} cash={(cashDebited ? preview.Amount : 0)} cashDebited={cashDebited} cashSource={cashSource} cashBefore={cashBefore} cashAfter={cashAfter} retaliationQueued={retaliationQueued} retaliationAction={attackAction} refusalEscalated={retaliationQueued} escalationChance={escalationChance:0.00} escalationRoll={escalationRoll:0.00} attackCrews={attackCrewCount} heat={AI_ROBBERY_CONTACT_REFUSE_HEAT_GAIN:0.0} cooldownDays={GetAiHumanRobberyCooldownSummary()} result={result}");
	}

	private static void ApplyAiHumanRobberyRefusalRelationshipBuffs(AiRobberyCandidate candidate, PlayerInfo humanPlayer, bool heldFirm, string result)
	{
		if (candidate?.Robber == null || humanPlayer == null || candidate.Robber.PID.id == humanPlayer.PID.id)
		{
			return;
		}
		try
		{
			EntityID robberCrewPeep = GetCrewPeepForPlayer(candidate.Robber);
			EntityID humanCrewPeep = candidate.TargetCrew.IsValid && candidate.TargetCrew.peepId.IsValid
				? candidate.TargetCrew.peepId
				: GetCrewPeepForPlayer(humanPlayer);
			bool robberTable = AddDirectedRelationshipBuff(candidate.Robber, humanPlayer, "relbuff-gangs-robbery1-table-on-finish", robberCrewPeep);
			bool robberBuff = AddDirectedRelationshipBuff(candidate.Robber, humanPlayer, "relbuff-gangs-robbery1-buff", robberCrewPeep);
			bool humanTable = AddDirectedRelationshipBuff(humanPlayer, candidate.Robber, "relbuff-gangs-robbery1-table-on-finish", humanCrewPeep);
			bool humanBuff = !heldFirm && AddDirectedRelationshipBuff(humanPlayer, candidate.Robber, "relbuff-gangs-robbery1-buff", humanCrewPeep);
			VerificationLog("AIPlayerRobbery", $"refusal-relationship-buffs robber={candidate.Robber.PID.id} human={humanPlayer.PID.id} heldFirm={heldFirm} result={result} robberTable={robberTable} robberBuff={robberBuff} humanTable={humanTable} humanBuff={humanBuff}");
		}
		catch (Exception ex)
		{
			Debug.LogWarning("[GameplayTweaks] ApplyAiHumanRobberyRefusalRelationshipBuffs failed: " + ex.Message);
		}
	}

	private static string ChooseAiHumanRobberyRefusalSuccessOutcome(AiRobberyCandidate candidate, PlayerInfo humanPlayer, out float attackChance, out double roll, out string reason)
	{
		attackChance = 0.12f;
		if (IsAiPersonalityAggressive(candidate?.Robber))
		{
			attackChance = 0.28f;
		}
		else if (IsAiPersonalityExpansionist(candidate?.Robber))
		{
			attackChance = 0.1f;
		}
		if (candidate?.DirectAggro == true)
		{
			attackChance += 0.04f;
		}
		if (candidate != null && candidate.RobberLocalPower > candidate.HumanLocalPower + 20)
		{
			attackChance += 0.04f;
		}
		if (candidate != null && candidate.RobberPower < candidate.HumanPower - 35)
		{
			attackChance -= 0.04f;
		}
		attackChance = Mathf.Clamp(attackChance, 0.05f, 0.32f);
		roll = SharedRng.NextDouble();
		reason = $"personality aggressive={IsAiPersonalityAggressive(candidate?.Robber)} expansionist={IsAiPersonalityExpansionist(candidate?.Robber)} directAggro={candidate?.DirectAggro == true} local={candidate?.RobberLocalPower ?? 0}/{candidate?.HumanLocalPower ?? 0}";
		return roll < attackChance ? "attack" : "business";
	}

	private static float CalculateAiHumanRobberyEvasionRetaliationChance(AiRobberyCandidate candidate)
	{
		if (candidate == null)
		{
			return 0.25f;
		}
		float chance = candidate.StrictTrespassNode || candidate.EnemyTerritory
			? 0.56f
			: (candidate.TerritoryPressure ? 0.5f : 0.3f);
		if (candidate.HumanTerritory && !candidate.StrictTrespassNode)
		{
			chance -= 0.06f;
		}
		if (IsAiPersonalityAggressive(candidate.Robber))
		{
			chance += 0.12f;
		}
		else if (IsAiPersonalityExpansionist(candidate.Robber))
		{
			chance += 0.08f;
		}
		if (candidate.DirectAggro)
		{
			chance += 0.08f;
		}
		chance += Mathf.Clamp((candidate.RobberLocalPower - candidate.HumanLocalPower) / 150f, -0.08f, 0.12f);
		chance += Mathf.Clamp((candidate.RobberPower - candidate.HumanPower) / 600f, -0.08f, 0.08f);
		return Mathf.Clamp(chance, 0.18f, 0.72f);
	}

	private static string ChooseAiHumanRobberyEvasionRetaliationOutcome(AiRobberyCandidate candidate, PlayerInfo humanPlayer, out float attackChance, out double roll, out string reason)
	{
		attackChance = 0.08f;
		if (IsAiPersonalityAggressive(candidate?.Robber))
		{
			attackChance = 0.18f;
		}
		else if (IsAiPersonalityExpansionist(candidate?.Robber))
		{
			attackChance = 0.07f;
		}
		if (candidate?.DirectAggro == true || candidate?.StrictTrespassNode == true)
		{
			attackChance += 0.04f;
		}
		if (candidate != null && candidate.RobberLocalPower > candidate.HumanLocalPower + 25)
		{
			attackChance += 0.03f;
		}
		if (candidate != null && candidate.RobberPower < candidate.HumanPower - 50)
		{
			attackChance -= 0.03f;
		}
		attackChance = Mathf.Clamp(attackChance, 0.04f, 0.24f);
		roll = SharedRng.NextDouble();
		reason = $"personality aggressive={IsAiPersonalityAggressive(candidate?.Robber)} expansionist={IsAiPersonalityExpansionist(candidate?.Robber)} directAggro={candidate?.DirectAggro == true} strictTrespass={candidate?.StrictTrespassNode == true} local={candidate?.RobberLocalPower ?? 0}/{candidate?.HumanLocalPower ?? 0}";
		return roll < attackChance ? "attack" : "business";
	}

	private static string FormatAiHumanRobberyEvasionRetaliationReason(AiRobberyCandidate candidate)
	{
		if (candidate == null)
		{
			return "missing-candidate";
		}
		return $"territory enemy={candidate.EnemyTerritory} human={candidate.HumanTerritory} strictTrespass={candidate.StrictTrespassNode} territoryPressure={candidate.TerritoryPressure} directAggro={candidate.DirectAggro} personality aggressive={IsAiPersonalityAggressive(candidate.Robber)} expansionist={IsAiPersonalityExpansionist(candidate.Robber)} power={candidate.RobberPower}/{candidate.HumanPower} local={candidate.RobberLocalPower}/{candidate.HumanLocalPower}";
	}

	private static bool TryInitializeAiHumanRobberyRefusalAttack(GangOpsChannel channel, AiRobberyCandidate candidate, PlayerInfo humanPlayer, int desiredCrewCount, int indicatorDueDay, string sourceTag, out int initializedCrewCount, out string actionSummary)
	{
		initializedCrewCount = 0;
		actionSummary = "attack-init-failed";
		if (candidate?.Robber == null || humanPlayer == null)
		{
			return false;
		}
		EntityID preferredTargetPeepId = candidate.TargetCrew.peepId.IsValid ? candidate.TargetCrew.peepId : GetCrewPeepForPlayer(humanPlayer);
		bool dispatched = TryDispatchRuntimeGangAttack(candidate.Robber, humanPlayer, desiredCrewCount, sourceTag, preferredTargetPeepId, out int dispatchedCount, out int approachCount);
		if (!dispatched && approachCount <= 0)
		{
			VerificationLog("AIPlayerRobbery", $"refusal-attack-initialized phase=response-popup source={sourceTag} robber={candidate.Robber.PID.id} human={humanPlayer.PID.id} targetCrew={preferredTargetPeepId.id} desiredCrew={desiredCrewCount} dispatched=0 approach=0 result=unavailable");
			return false;
		}
		initializedCrewCount = Mathf.Max(1, dispatchedCount + approachCount);
		bool followThroughQueued = false;
		if (dispatchedCount <= 0 && approachCount > 0)
		{
			followThroughQueued = QueueImmediateRevengeIfEligible(
				channel,
				candidate.Robber.PID.id,
				humanPlayer.PID.id,
				preferredTargetPeepId.IsValid ? (long)preferredTargetPeepId.id : 0L,
				indicatorDueDay,
				sourceTag + "-attack-next-turn-follow-through");
		}
		actionSummary = dispatchedCount > 0
			? $"attack-initialized:{dispatchedCount}{(approachCount > 0 ? "+approach:" + approachCount.ToString(CultureInfo.InvariantCulture) : string.Empty)}"
			: $"attack-approach-initialized:{approachCount}{(followThroughQueued ? "+follow-through" : "+follow-through-failed")}";
		TryShowAiHumanRobberyQuickAttackTicker(candidate, humanPlayer, indicatorDueDay, sourceTag);
		VerificationLog("AIPlayerRobbery", $"refusal-attack-initialized phase=response-popup source={sourceTag} robber={candidate.Robber.PID.id} human={humanPlayer.PID.id} targetCrew={preferredTargetPeepId.id} desiredCrew={desiredCrewCount} dispatched={dispatchedCount} approach={approachCount} followThroughQueued={followThroughQueued} followThroughDueDay={(followThroughQueued ? indicatorDueDay : -1)} action={actionSummary} result=initialized");
		return true;
	}

	private static bool QueueAiHumanRobberyRefusalAttack(GangOpsChannel channel, AiRobberyCandidate candidate, PlayerInfo humanPlayer, int dueDay, string sourceTag)
	{
		if (candidate?.Robber == null || humanPlayer == null)
		{
			return false;
		}
		bool queued = QueueImmediateRevengeIfEligible(channel, candidate.Robber.PID.id, humanPlayer.PID.id, candidate.TargetCrew.peepId.IsValid ? (long)candidate.TargetCrew.peepId.id : 0L, dueDay, sourceTag);
		if (queued)
		{
			TryShowAiHumanRobberyQuickAttackTicker(candidate, humanPlayer, dueDay, sourceTag);
		}
		return queued;
	}

	private static void TryShowAiHumanRobberyQuickAttackTicker(AiRobberyCandidate candidate, PlayerInfo humanPlayer, int dueDay, string sourceTag)
	{
		if (candidate?.Robber == null || humanPlayer?.PID.IsHumanPlayer != true)
		{
			return;
		}
		try
		{
			string robberName = GetGangDisplayName(candidate.Robber.PID.id);
			string message = CrewRelationshipHandlerPatch.SanitizeUiGlyphText("COORDINATED ATTACK: " + robberName + " is gathering hitters after the robbery refusal. Expect an attack next turn.", aggressive: true).Trim();
			NodeID targetNodeId = candidate.TargetCrew.IsValid ? (candidate.TargetCrew.GetPeep()?.data?.agent?.nid ?? NodeID.INVALID) : NodeID.INVALID;
			if (targetNodeId.IsNotValid && candidate.ContactNode?.id.IsValid == true)
			{
				targetNodeId = candidate.ContactNode.id;
			}
			TickerTarget target = targetNodeId.IsValid ? (TickerTarget)targetNodeId : default(TickerTarget);
			global::Game.Game.ctx?.hud?.tickers?.AddTextTicker(TickerIcon.GANG_ATTACK, TickerTitle.GANG_ATTACK, message, target, TickerPersistType.Persist);
			VerificationLog("AIPlayerRobbery", $"indicator phase=quick-attack source={sourceTag} robber={candidate.Robber.PID.id} human={humanPlayer.PID.id} targetCrew={candidate.TargetCrew.peepId.id} node={targetNodeId} dueDay={dueDay} result=shown");
		}
		catch (Exception ex)
		{
			Debug.LogWarning("[GameplayTweaks] TryShowAiHumanRobberyQuickAttackTicker failed: " + ex.Message);
		}
	}

	private static bool ShouldEscalateAiHumanRobberyRefusal(AiRobberyCandidate candidate, PlayerInfo humanPlayer, out float chance, out double roll, out string reason)
	{
		chance = 0f;
		roll = 1d;
		reason = "ineligible";
		if (candidate?.Robber == null || humanPlayer == null)
		{
			reason = "missing-context";
			return false;
		}
		int robberPower = candidate.RobberPower > 0 ? candidate.RobberPower : CalculateGangPower(candidate.Robber);
		int humanPower = candidate.HumanPower > 0 ? candidate.HumanPower : CalculateGangPower(humanPlayer);
		int robberLocalPower = candidate.RobberLocalPower;
		int humanLocalPower = candidate.HumanLocalPower;
		bool strongEnough = robberPower + 10 >= humanPower || robberLocalPower + 5 >= humanLocalPower || candidate.DirectAggro || candidate.EnemyTerritory || candidate.StrictTrespassNode;
		if (!strongEnough)
		{
			reason = $"too-weak robberPower={robberPower} humanPower={humanPower} robberLocal={robberLocalPower} humanLocal={humanLocalPower}";
			return false;
		}
		chance = AI_ROBBERY_REFUSAL_ESCALATION_CHANCE;
		roll = SharedRng.NextDouble();
		reason = $"eligible robberPower={robberPower} humanPower={humanPower} robberLocal={robberLocalPower} humanLocal={humanLocalPower}";
		return roll < (double)chance;
	}

	private static float CalculateAiHumanRobberyRefusalHoldChance(AiRobberyCandidate candidate)
	{
		if (candidate == null)
		{
			return 0.35f;
		}
		float chance = 0.38f;
		chance += Mathf.Clamp((candidate.HumanLocalPower - candidate.RobberLocalPower) / 120f, -0.18f, 0.18f);
		chance += Mathf.Clamp((candidate.HumanPower - candidate.RobberPower) / 350f, -0.12f, 0.12f);
		if (candidate.HumanTerritory)
		{
			chance += 0.08f;
		}
		if (candidate.EnemyTerritory)
		{
			chance -= 0.08f;
		}
		return Mathf.Clamp(chance, 0.18f, 0.72f);
	}

	private static float CalculateAiHumanRobberyEvasionChance(AiRobberyCandidate candidate)
	{
		if (candidate == null)
		{
			return 0.4f;
		}
		float chance = 0.42f;
		chance += Mathf.Clamp((candidate.HumanLocalPower - candidate.RobberLocalPower) / 140f, -0.2f, 0.2f);
		chance += Mathf.Clamp((candidate.HumanPower - candidate.RobberPower) / 500f, -0.12f, 0.12f);
		if (candidate.HumanTerritory)
		{
			chance += 0.08f;
		}
		if (candidate.EnemyTerritory || candidate.StrictTrespassNode)
		{
			chance -= 0.06f;
		}
		if (candidate.SameNode)
		{
			chance -= 0.05f;
		}
		if (candidate.TerritoryPressure && !candidate.StrictTrespassNode)
		{
			chance += 0.04f;
		}
		return Mathf.Clamp(chance, 0.2f, 0.78f);
	}

	private static bool TryApplyAiHumanRobberyRefusalInjury(CrewAssignment crew, out int damageApplied, out int healthAfter)
	{
		damageApplied = 0;
		healthAfter = 0;
		try
		{
			Entity peep = crew.GetPeep();
			if (peep?.components?.agent == null || !peep.components.agent.HasHealthPointsLeft)
			{
				return false;
			}
			int currentHealth = Math.Max(0, ReadFixnum(peep.components.agent.CurrentHealth));
			int maxDamage = Math.Max(0, currentHealth - AI_ROBBERY_REFUSE_MIN_HEALTH_LEFT);
			if (maxDamage <= 0)
			{
				healthAfter = currentHealth;
				return false;
			}
			int requestedDamage = SharedRng.Next(AI_ROBBERY_REFUSE_MIN_DAMAGE, AI_ROBBERY_REFUSE_MAX_DAMAGE + 1);
			damageApplied = Math.Min(maxDamage, requestedDamage);
			if (damageApplied <= 0)
			{
				healthAfter = currentHealth;
				return false;
			}
			Fixnum damage = damageApplied;
			peep.components.agent.IncrementHealth(-damage);
			peep.components.agent.RememberInjuryAtThisTime();
			healthAfter = Math.Max(0, ReadFixnum(peep.components.agent.CurrentHealth));
			return true;
		}
		catch (Exception ex)
		{
			Debug.LogWarning("[GameplayTweaks] TryApplyAiHumanRobberyRefusalInjury failed: " + ex.Message);
			return false;
		}
	}

	private static bool TryResolveAiHumanVehicleRobberyContact(AiRobberyCandidate candidate, PlayerInfo humanPlayer, AiRobberyResolutionPreview preview, SimTime now, string source, string mode, int createdDay)
	{
		if (candidate == null || candidate.Robber == null || humanPlayer == null || preview == null || preview.Amount <= 0 || (!preview.CanDebitVehicle && !preview.CanDebitPeep && !preview.CanDebitSafehouse && !preview.CanDebitCleanCash))
		{
			return false;
		}
		if (TryBlockDuplicateAiHumanRobberyResponse(candidate, humanPlayer, now, source, mode, "pay"))
		{
			return true;
		}
		if (!TryDebitAiHumanRobberyCash(humanPlayer, candidate.TargetCrew, preview.Amount, out int cashBefore, out int cashAfter, out string cashSource))
		{
			VerificationLog(
				"AIPlayerRobbery",
				$"blocked phase=vehicle-debit source={source} mode={mode} reason=cash-debit-failed robber={candidate.Robber.PID.id} targetCrew={candidate.TargetCrew.peepId.id} vehicle={candidate.TargetCrew.VehicleID.id} node={candidate.ContactNode?.id ?? NodeID.INVALID} cashPreview={preview.Amount} availableCash={preview.AvailableCash} safehouseCash={preview.SafehouseCash} vehicleCash={preview.VehicleCash} peepCash={preview.PeepCash} totalCash={preview.TotalCash} canDebitSafehouse={preview.CanDebitSafehouse} canDebitCleanCash={preview.CanDebitCleanCash} canDebitVehicle={preview.CanDebitVehicle} canDebitPeep={preview.CanDebitPeep} result=no-debit");
			return false;
		}

		bool robberCredited = TryCreditGangSafehouseCash(candidate.Robber, preview.Amount);
		GangOpsChannel channel = ResolveGangOpsChannelForGang(candidate.Robber.PID.id);
		AddAiRobberyWarHeatWithDiagnostic(
			channel,
			humanPlayer.PID.id,
			candidate.Robber.PID.id,
			candidate.Robber,
			humanPlayer,
			AI_ROBBERY_CONTACT_VEHICLE_HEAT_GAIN,
			"ai-vehicle-robbery-success",
			"vehicle-robbery-success",
			"ai-human-contact",
			preview.Amount,
			success: true,
			highValue: false,
			externalNetwork: true,
			direction: "human-to-robber");
		RecordAiHumanRobberyDiagnosticCooldown(candidate, now);
		RecordAiHumanRobberyPairCooldown(candidate, humanPlayer, now, "success");
		string created = createdDay >= 0 ? $" createdDay={createdDay}" : string.Empty;
		string robberName = GetGangDisplayName(candidate.Robber.PID.id);
		string sourceText = cashSource == "vehicle" ? "from their vehicle" : (cashSource == "peep" ? "from their carried cash" : (cashSource == "safehouse" ? "from your safehouse cash" : "from your cash reserves"));
		LogGrapevine($"ROBBERY: {robberName} stopped one of your crews and took ${preview.Amount} {sourceText}. No shots were fired.");
		VerificationLog(
			"AIPlayerRobbery",
			$"resolved phase=vehicle-debit source={source} mode={mode} robber={candidate.Robber.PID.id} targetCrew={candidate.TargetCrew.peepId.id} vehicle={candidate.TargetCrew.VehicleID.id} node={candidate.ContactNode?.id ?? NodeID.INVALID}{created} cash={preview.Amount} cashSource={cashSource} cashBefore={cashBefore} cashAfter={cashAfter} availableCash={preview.AvailableCash} safehouseCash={preview.SafehouseCash} vehicleCash={preview.VehicleCash} peepCash={preview.PeepCash} totalCash={preview.TotalCash} heat={AI_ROBBERY_CONTACT_VEHICLE_HEAT_GAIN:0.0} robberCredited={robberCredited} cooldownDays={GetAiHumanRobberyCooldownSummary()} result=success");
		return true;
	}

	private static bool TryBlockDuplicateAiHumanRobberyResponse(AiRobberyCandidate candidate, PlayerInfo humanPlayer, SimTime now, string source, string mode, string result)
	{
		if (candidate?.Robber == null || humanPlayer == null)
		{
			return false;
		}
		if (!TryGetAiHumanRobberyPairCooldown(candidate.Robber, humanPlayer, now, out int untilDay))
		{
			return false;
		}
		VerificationLog(
			"AIPlayerRobbery",
			$"blocked phase=response-popup source={source} mode={mode} reason=pair-cooldown-resolved robber={candidate.Robber.PID.id} human={humanPlayer.PID.id} targetCrew={candidate.TargetCrew.peepId.id} vehicle={candidate.TargetCrew.VehicleID.id} node={candidate.ContactNode?.id ?? NodeID.INVALID} untilDay={untilDay} cooldownDays={GetAiHumanRobberyCooldownSummary()} result=duplicate-{result}-ignored");
		return true;
	}

	private static void LogAiHumanRobberySameNodeMissPreview(AiRobberyCandidate candidate, PlayerInfo humanPlayer, SimTime now, string source)
	{
		if (candidate == null || candidate.Robber == null || humanPlayer == null || !candidate.TargetCrew.IsValid)
		{
			return;
		}
		AiRobberyResolutionPreview preview = BuildAiHumanRobberyResolutionPreview(humanPlayer, candidate);
		VerificationLog(
			"AIPlayerRobbery",
			$"preview phase=rarity-miss source={source} mode=same-node robber={candidate.Robber.PID.id} targetCrew={candidate.TargetCrew.peepId.id} vehicle={candidate.TargetCrew.VehicleID.id} node={candidate.ContactNode?.id ?? NodeID.INVALID} rarity={candidate.RarityRoll:0.00}/{candidate.RarityChance:0.00} cashPreview={preview.Amount} availableCash={preview.AvailableCash} safehouseCash={preview.SafehouseCash} vehicleCash={preview.VehicleCash} totalCash={preview.TotalCash} canDebitSafehouse={preview.CanDebitSafehouse} canDebitCleanCash={preview.CanDebitCleanCash} canDebitVehicle={preview.CanDebitVehicle} cashReason={preview.Reason} mutationEnabled={AI_ROBBERY_HUMAN_CASH_MUTATION_ENABLED} result=no-attempt");
	}

	private static void LogAiHumanRobberyLowCashContactPreview(AiRobberyCandidate candidate, PlayerInfo humanPlayer, SimTime now, string source, string mode)
	{
		if (candidate == null || candidate.Robber == null || humanPlayer == null || !candidate.TargetCrew.IsValid)
		{
			return;
		}
		AiRobberyResolutionPreview preview = BuildAiHumanRobberyResolutionPreview(humanPlayer, candidate);
		int minCash = GetAiHumanRobberyMinimumCash(candidate);
		VerificationLog(
			"AIPlayerRobbery",
			$"blocked phase=contact-preview source={source} mode={mode} reason=low-robbable-cash robber={candidate.Robber.PID.id} targetCrew={candidate.TargetCrew.peepId.id} vehicle={candidate.TargetCrew.VehicleID.id} node={candidate.ContactNode?.id ?? NodeID.INVALID} rarity={candidate.RarityRoll:0.00}/{candidate.RarityChance:0.00} cashPreview={preview.Amount} availableCash={preview.AvailableCash} safehouseCash={preview.SafehouseCash} vehicleCash={preview.VehicleCash} totalCash={preview.TotalCash} min={minCash} canDebitSafehouse={preview.CanDebitSafehouse} canDebitCleanCash={preview.CanDebitCleanCash} canDebitVehicle={preview.CanDebitVehicle} cashReason={preview.Reason} mutationEnabled={AI_ROBBERY_HUMAN_CASH_MUTATION_ENABLED} result=no-attempt");
	}

	private static AiRobberyResolutionPreview BuildAiHumanRobberyResolutionPreview(PlayerInfo humanPlayer, AiRobberyCandidate candidate)
	{
		int totalCash = GetGangCleanCash(humanPlayer);
		int safehouseCash = GetGangSafehouseCleanCash(humanPlayer);
		CrewAssignment targetCrew = candidate?.TargetCrew ?? CrewAssignment.EMPTY;
		int vehicleCash = GetCrewVehicleCleanCash(humanPlayer, targetCrew);
		int peepCash = GetCrewPeepCleanCash(humanPlayer, targetCrew);
		int carriedCash = Math.Max(0, vehicleCash) + Math.Max(0, peepCash);
		int robbableCash = carriedCash > 0 ? carriedCash : (safehouseCash > 0 ? safehouseCash : totalCash);
		int available = carriedCash > 0
			? carriedCash
			: Math.Min(Math.Max(0, totalCash), Math.Max(0, robbableCash));
		AiRobberyResolutionPreview preview = new AiRobberyResolutionPreview
		{
			SafehouseCash = safehouseCash,
			VehicleCash = vehicleCash,
			PeepCash = peepCash,
			TotalCash = totalCash,
			AvailableCash = available,
			Amount = 0,
			CanDebitSafehouse = false,
			CanDebitCleanCash = false,
			CanDebitVehicle = false,
			CanDebitPeep = false,
			Reason = "no-available-robbable-cash"
		};
		if (available <= 0)
		{
			return preview;
		}

		int baseAmount = RoundToNearest50(Mathf.Clamp(Mathf.RoundToInt(available * 0.03f), 100, 1000));
		int bonus = 0;
		if (candidate?.DirectAggro == true)
		{
			bonus += 100;
		}
		if (candidate?.EnemyTerritory == true)
		{
			bonus += 100;
		}
		int amount = Math.Min(available, Mathf.Clamp(baseAmount + bonus, 100, 1000));
		preview.Amount = Math.Max(0, amount);
		preview.CanDebitVehicle = preview.Amount > 0 && CanDebitCrewVehicleCash(humanPlayer, targetCrew, preview.Amount);
		preview.CanDebitPeep = !preview.CanDebitVehicle && preview.Amount > 0 && CanDebitCrewPeepCash(humanPlayer, targetCrew, preview.Amount);
		preview.CanDebitSafehouse = !preview.CanDebitVehicle && !preview.CanDebitPeep && preview.Amount > 0 && CanDebitGangSafehouseCash(humanPlayer, preview.Amount);
		preview.CanDebitCleanCash = !preview.CanDebitVehicle && !preview.CanDebitPeep && !preview.CanDebitSafehouse && preview.Amount > 0 && CanDebitGangCleanCash(humanPlayer, preview.Amount);
		if (preview.CanDebitVehicle)
		{
			preview.Reason = "target-vehicle-capped-preview";
		}
		else if (preview.CanDebitSafehouse)
		{
			preview.Reason = "safehouse-capped-preview";
		}
		else if (preview.CanDebitPeep)
		{
			preview.Reason = "target-peep-capped-preview";
		}
		else if (preview.CanDebitCleanCash)
		{
			preview.Reason = "clean-cash-capped-preview";
		}
		else
		{
			preview.Reason = "robbable-debit-blocked";
		}
		return preview;
	}

	private static int GetGangSafehouseCleanCash(PlayerInfo gang)
	{
		try
		{
			Entity safehouse = GetGangSafehouseEntity(gang);
			return safehouse == null || gang?.finances == null ? 0 : Math.Max(0, ReadFixnum(gang.finances.GetMoney(safehouse).cash));
		}
		catch (Exception ex)
		{
			Debug.LogWarning("[GameplayTweaks] GetGangSafehouseCleanCash failed: " + ex.Message);
			return 0;
		}
	}

	private static int GetCrewVehicleCleanCash(PlayerInfo player, CrewAssignment crew)
	{
		try
		{
			if (player?.finances == null || !crew.IsValid || !crew.VehicleID.IsValid)
			{
				return 0;
			}
			Entity vehicle = crew.VehicleID.FindEntity();
			return vehicle == null ? 0 : Math.Max(0, ReadFixnum(player.finances.GetMoney(vehicle).cash));
		}
		catch (Exception ex)
		{
			Debug.LogWarning("[GameplayTweaks] GetCrewVehicleCleanCash failed: " + ex.Message);
			return 0;
		}
	}

	private static int GetCrewPeepCleanCash(PlayerInfo player, CrewAssignment crew)
	{
		try
		{
			if (player?.finances == null || !crew.IsValid || !crew.peepId.IsValid)
			{
				return 0;
			}
			Entity peep = crew.GetPeep();
			if (peep == null)
			{
				return 0;
			}
			int financeCash = Math.Max(0, ReadFixnum(player.finances.GetMoney(peep).cash));
			if (financeCash > 0)
			{
				return financeCash;
			}
			return Math.Max(0, ReadInventoryAmount(peep, "cash"));
		}
		catch
		{
			return 0;
		}
	}

	private static bool CanDebitCrewVehicleCash(PlayerInfo player, CrewAssignment crew, int amount)
	{
		try
		{
			if (player?.finances == null || amount <= 0 || !crew.IsValid || !crew.VehicleID.IsValid)
			{
				return false;
			}
			Entity vehicle = crew.VehicleID.FindEntity();
			return vehicle != null && player.finances.CanChangeMoney(vehicle, new Price((Fixnum)(-amount)));
		}
		catch
		{
			return false;
		}
	}

	private static bool CanDebitCrewPeepCash(PlayerInfo player, CrewAssignment crew, int amount)
	{
		try
		{
			if (player?.finances == null || amount <= 0 || !crew.IsValid || !crew.peepId.IsValid)
			{
				return false;
			}
			Entity peep = crew.GetPeep();
			return peep != null && player.finances.CanChangeMoney(peep, new Price((Fixnum)(-amount)));
		}
		catch
		{
			return false;
		}
	}

	private static bool TryDebitCrewVehicleCash(PlayerInfo player, CrewAssignment crew, int amount, out int before, out int after)
	{
		before = 0;
		after = 0;
		try
		{
			if (player?.finances == null || amount <= 0 || !crew.IsValid || !crew.VehicleID.IsValid)
			{
				return false;
			}
			Entity vehicle = crew.VehicleID.FindEntity();
			if (vehicle == null)
			{
				return false;
			}
			before = Math.Max(0, ReadFixnum(player.finances.GetMoney(vehicle).cash));
			Price delta = new Price((Fixnum)(-amount));
			if (!player.finances.CanChangeMoney(vehicle, delta))
			{
				after = before;
				return false;
			}
			player.finances.DoChangeMoney(vehicle, delta, MoneyReason.Other);
			after = Math.Max(0, ReadFixnum(player.finances.GetMoney(vehicle).cash));
			return before - after >= amount;
		}
		catch (Exception ex)
		{
			Debug.LogWarning("[GameplayTweaks] TryDebitCrewVehicleCash failed: " + ex.Message);
			after = before;
			return false;
		}
	}

	private static bool TryDebitCrewPeepCash(PlayerInfo player, CrewAssignment crew, int amount, out int before, out int after)
	{
		before = 0;
		after = 0;
		try
		{
			if (player?.finances == null || amount <= 0 || !crew.IsValid || !crew.peepId.IsValid)
			{
				return false;
			}
			Entity peep = crew.GetPeep();
			if (peep == null)
			{
				return false;
			}
			before = Math.Max(0, ReadFixnum(player.finances.GetMoney(peep).cash));
			Price delta = new Price((Fixnum)(-amount));
			if (!player.finances.CanChangeMoney(peep, delta))
			{
				after = before;
				return false;
			}
			player.finances.DoChangeMoney(peep, delta, MoneyReason.Other);
			after = Math.Max(0, ReadFixnum(player.finances.GetMoney(peep).cash));
			return before - after >= amount;
		}
		catch (Exception ex)
		{
			Debug.LogWarning("[GameplayTweaks] TryDebitCrewPeepCash failed: " + ex.Message);
			after = before;
			return false;
		}
	}

	private static bool CanDebitGangSafehouseCash(PlayerInfo player, int amount)
	{
		try
		{
			if (player?.finances == null || amount <= 0)
			{
				return false;
			}
			Entity safehouse = GetGangSafehouseEntity(player);
			return safehouse != null && player.finances.CanChangeMoney(safehouse, new Price((Fixnum)(-amount)));
		}
		catch
		{
			return false;
		}
	}

	private static bool TryDebitGangSafehouseCash(PlayerInfo player, int amount, out int before, out int after)
	{
		before = 0;
		after = 0;
		try
		{
			if (player?.finances == null || amount <= 0)
			{
				return false;
			}
			Entity safehouse = GetGangSafehouseEntity(player);
			if (safehouse == null)
			{
				return false;
			}
			before = Math.Max(0, ReadFixnum(player.finances.GetMoney(safehouse).cash));
			Price delta = new Price((Fixnum)(-amount));
			if (!player.finances.CanChangeMoney(safehouse, delta))
			{
				after = before;
				return false;
			}
			player.finances.DoChangeMoney(safehouse, delta, MoneyReason.Other);
			after = Math.Max(0, ReadFixnum(player.finances.GetMoney(safehouse).cash));
			return before - after >= amount;
		}
		catch (Exception ex)
		{
			Debug.LogWarning("[GameplayTweaks] TryDebitGangSafehouseCash failed: " + ex.Message);
			after = before;
			return false;
		}
	}

	private static bool CanDebitGangCleanCash(PlayerInfo player, int amount)
	{
		try
		{
			return player?.finances != null && amount > 0 && GetGangCleanCash(player) >= amount;
		}
		catch
		{
			return false;
		}
	}

	private static bool TryDebitGangCleanCash(PlayerInfo player, int amount, out int before, out int after)
	{
		before = 0;
		after = 0;
		try
		{
			if (player?.finances == null || amount <= 0)
			{
				return false;
			}
			before = Math.Max(0, GetGangCleanCash(player));
			if (before < amount)
			{
				after = before;
				return false;
			}
			player.finances.DoChangeMoneyOnSafehouse(new Price((Fixnum)(-amount)), MoneyReason.Other);
			after = Math.Max(0, GetGangCleanCash(player));
			return before - after >= amount;
		}
		catch (Exception ex)
		{
			Debug.LogWarning("[GameplayTweaks] TryDebitGangCleanCash failed: " + ex.Message);
			after = before;
			return false;
		}
	}

	private static bool TryDebitAiHumanRobberyCash(PlayerInfo player, CrewAssignment crew, int amount, out int before, out int after, out string cashSource)
	{
		cashSource = "none";
		if (CanDebitCrewVehicleCash(player, crew, amount)
			&& TryDebitCrewVehicleCash(player, crew, amount, out before, out after))
		{
			cashSource = "vehicle";
			return true;
		}
		if (CanDebitCrewPeepCash(player, crew, amount)
			&& TryDebitCrewPeepCash(player, crew, amount, out before, out after))
		{
			cashSource = "peep";
			return true;
		}
		if (TryDebitGangSafehouseCash(player, amount, out before, out after))
		{
			cashSource = "safehouse";
			return true;
		}
		if (TryDebitGangCleanCash(player, amount, out before, out after))
		{
			cashSource = "clean";
			return true;
		}
		before = 0;
		after = 0;
		return false;
	}

	private static bool TryCreditGangSafehouseCash(PlayerInfo gang, int amount)
	{
		try
		{
			if (gang?.finances == null || amount <= 0)
			{
				return false;
			}
			Entity safehouse = GetGangSafehouseEntity(gang);
			if (safehouse == null)
			{
				return false;
			}
			gang.finances.DoChangeMoney(safehouse, new Price((Fixnum)amount), MoneyReason.Other);
			return true;
		}
		catch (Exception ex)
		{
			Debug.LogWarning("[GameplayTweaks] TryCreditGangSafehouseCash failed: " + ex.Message);
			return false;
		}
	}

	private static int RoundToNearest50(int amount)
	{
		return Mathf.Max(0, Mathf.RoundToInt(amount / 50f) * 50);
	}

	private static bool TryGetAiHumanRobberyDiagnosticCooldown(AiRobberyCandidate candidate, SimTime now, out string cooldownKey, out int untilDay)
	{
		cooldownKey = string.Empty;
		untilDay = int.MinValue;
		foreach (string key in GetAiHumanRobberyDiagnosticCooldownKeys(candidate))
		{
			if (!_aiHumanRobberyDiagnosticCooldownUntilDay.TryGetValue(key, out int candidateUntilDay))
			{
				continue;
			}
			if (candidateUntilDay >= now.days)
			{
				cooldownKey = key;
				untilDay = candidateUntilDay;
				return true;
			}
			_aiHumanRobberyDiagnosticCooldownUntilDay.Remove(key);
		}
		return false;
	}

	private static void RecordAiHumanRobberyDiagnosticCooldown(AiRobberyCandidate candidate, SimTime now)
	{
		if (candidate == null || candidate.Robber == null || !candidate.TargetCrew.IsValid)
		{
			return;
		}
		_aiHumanRobberyDiagnosticCooldownUntilDay[GetAiHumanRobberyCrewCooldownKey(candidate)] = now.days + AI_ROBBERY_HUMAN_DIAGNOSTIC_COOLDOWN_DAYS;
		_aiHumanRobberyDiagnosticCooldownUntilDay[GetAiHumanRobberyNodeCooldownKey(candidate)] = now.days + AI_ROBBERY_HUMAN_NODE_DIAGNOSTIC_COOLDOWN_DAYS;
		_aiHumanRobberyDiagnosticCooldownUntilDay[GetAiHumanRobberyTargetCooldownKey(candidate)] = now.days + AI_ROBBERY_HUMAN_TARGET_DIAGNOSTIC_COOLDOWN_DAYS;
		if (candidate.TargetCrew.VehicleID.IsValid)
		{
			_aiHumanRobberyDiagnosticCooldownUntilDay[GetAiHumanRobberyVehicleCooldownKey(candidate)] = now.days + AI_ROBBERY_HUMAN_VEHICLE_DIAGNOSTIC_COOLDOWN_DAYS;
		}
	}

	private static bool TryGetAiHumanRobberyPairCooldown(PlayerInfo robber, PlayerInfo humanPlayer, SimTime now, out int untilDay)
	{
		untilDay = int.MinValue;
		string key = GetAiHumanRobberyPairCooldownKey(robber, humanPlayer);
		if (string.IsNullOrEmpty(key))
		{
			return false;
		}
		if (!_aiHumanRobberyPairCooldownUntilDay.TryGetValue(key, out int candidateUntilDay))
		{
			return false;
		}
		if (candidateUntilDay >= now.days)
		{
			untilDay = candidateUntilDay;
			return true;
		}
		_aiHumanRobberyPairCooldownUntilDay.Remove(key);
		return false;
	}

	private static void RecordAiHumanRobberyPairCooldown(AiRobberyCandidate candidate, PlayerInfo humanPlayer, SimTime now, string reason)
	{
		if (candidate?.Robber == null || humanPlayer == null)
		{
			return;
		}
		string key = GetAiHumanRobberyPairCooldownKey(candidate.Robber, humanPlayer);
		if (string.IsNullOrEmpty(key))
		{
			return;
		}
		int untilDay = now.days + AI_ROBBERY_HUMAN_DIAGNOSTIC_COOLDOWN_DAYS;
		_aiHumanRobberyPairCooldownUntilDay[key] = untilDay;
		VerificationLog(
			"AIPlayerRobbery",
			$"cooldown phase=pair robber={candidate.Robber.PID.id} human={humanPlayer.PID.id} untilDay={untilDay} cooldownDays={AI_ROBBERY_HUMAN_DIAGNOSTIC_COOLDOWN_DAYS} reason={reason}");
	}

	private static string GetAiHumanRobberyPairCooldownKey(PlayerInfo robber, PlayerInfo humanPlayer)
	{
		if (robber == null || humanPlayer == null)
		{
			return string.Empty;
		}
		return "humanpair:" + robber.PID.id.ToString(CultureInfo.InvariantCulture) + ":" + humanPlayer.PID.id.ToString(CultureInfo.InvariantCulture);
	}

	private static string GetAiHumanRobberyDeferredResponseKey(PlayerInfo robber, PlayerInfo humanPlayer)
	{
		string pairKey = GetAiHumanRobberyPairCooldownKey(robber, humanPlayer);
		return string.IsNullOrEmpty(pairKey) ? string.Empty : pairKey + ":deferred-response";
	}

	private static string GetAiHumanRobberyDeferredResponseKey(int robberPid, int humanPid)
	{
		if (robberPid <= 0 || humanPid <= 0)
		{
			return string.Empty;
		}
		return "humanpair:" + robberPid.ToString(CultureInfo.InvariantCulture) + ":" + humanPid.ToString(CultureInfo.InvariantCulture) + ":deferred-response";
	}

	private static string GetAiHumanRobberyMeetingKey(PlayerInfo robber, PlayerInfo humanPlayer)
	{
		if (robber == null || humanPlayer == null)
		{
			return string.Empty;
		}
		return GetAiHumanRobberyMeetingKey(robber.PID.id, humanPlayer.PID.id);
	}

	private static string GetAiHumanRobberyMeetingKey(int robberPid, int humanPid)
	{
		if (robberPid <= 0 || humanPid <= 0)
		{
			return string.Empty;
		}
		return "humanpair:" + robberPid.ToString(CultureInfo.InvariantCulture) + ":" + humanPid.ToString(CultureInfo.InvariantCulture) + ":robbery-meeting";
	}

	private static IEnumerable<string> GetAiHumanRobberyDiagnosticCooldownKeys(AiRobberyCandidate candidate)
	{
		if (candidate == null || candidate.Robber == null || !candidate.TargetCrew.IsValid)
		{
			yield break;
		}
		yield return GetAiHumanRobberyCrewCooldownKey(candidate);
		yield return GetAiHumanRobberyNodeCooldownKey(candidate);
		yield return GetAiHumanRobberyTargetCooldownKey(candidate);
		if (candidate.TargetCrew.VehicleID.IsValid)
		{
			yield return GetAiHumanRobberyVehicleCooldownKey(candidate);
		}
	}

	private static string GetAiHumanRobberyContactKey(int robberPid, ulong targetCrewPeepId)
	{
		return robberPid.ToString(CultureInfo.InvariantCulture) + ":" + targetCrewPeepId.ToString(CultureInfo.InvariantCulture);
	}

	private static bool ShouldLogAiHumanRobberySameNodeMiss(AiRobberyCandidate candidate, SimTime now, bool record)
	{
		if (candidate == null || candidate.Robber == null || !candidate.TargetCrew.IsValid || !candidate.SameNode || candidate.WouldAttempt)
		{
			return false;
		}
		if (TryGetAiHumanRobberyDiagnosticCooldown(candidate, now, out _, out _))
		{
			return false;
		}
		string key = GetAiHumanRobberyContactKey(candidate.Robber.PID.id, candidate.TargetCrew.peepId.id) + ":miss";
		if (_aiHumanRobberySameNodeMissLogDayByKey.TryGetValue(key, out int lastDay)
			&& now.days - lastDay < AI_ROBBERY_SAME_NODE_MISS_LOG_COOLDOWN_DAYS)
		{
			return false;
		}
		if (record)
		{
			_aiHumanRobberySameNodeMissLogDayByKey[key] = now.days;
		}
		return true;
	}

	private static string GetAiHumanRobberyCrewCooldownKey(AiRobberyCandidate candidate)
	{
		return "crew:" + candidate.Robber.PID.id.ToString(CultureInfo.InvariantCulture) + ":" + candidate.TargetCrew.peepId.id.ToString(CultureInfo.InvariantCulture);
	}

	private static string GetAiHumanRobberyNodeCooldownKey(AiRobberyCandidate candidate)
	{
		int nodeIndex = candidate.ContactNode?.id.index ?? -1;
		return "node:" + candidate.Robber.PID.id.ToString(CultureInfo.InvariantCulture) + ":" + nodeIndex.ToString(CultureInfo.InvariantCulture);
	}

	private static string GetAiHumanRobberyTargetCooldownKey(AiRobberyCandidate candidate)
	{
		return "target:" + candidate.Robber.PID.id.ToString(CultureInfo.InvariantCulture) + ":" + candidate.TargetCrew.peepId.id.ToString(CultureInfo.InvariantCulture);
	}

	private static string GetAiHumanRobberyVehicleCooldownKey(AiRobberyCandidate candidate)
	{
		return "vehicle:" + candidate.Robber.PID.id.ToString(CultureInfo.InvariantCulture) + ":" + candidate.TargetCrew.VehicleID.id.ToString(CultureInfo.InvariantCulture);
	}

	private static string GetAiHumanRobberyCooldownSummary()
	{
		return "crew:" + AI_ROBBERY_HUMAN_DIAGNOSTIC_COOLDOWN_DAYS.ToString(CultureInfo.InvariantCulture)
			+ ",node:" + AI_ROBBERY_HUMAN_NODE_DIAGNOSTIC_COOLDOWN_DAYS.ToString(CultureInfo.InvariantCulture)
			+ ",target:" + AI_ROBBERY_HUMAN_TARGET_DIAGNOSTIC_COOLDOWN_DAYS.ToString(CultureInfo.InvariantCulture)
			+ ",vehicle:" + AI_ROBBERY_HUMAN_VEHICLE_DIAGNOSTIC_COOLDOWN_DAYS.ToString(CultureInfo.InvariantCulture);
	}

	private static bool TryBuildAiRobberyCandidateAgainstHuman(PlayerInfo robber, PlayerInfo humanPlayer, SimTime now, out AiRobberyCandidate candidate, out string blockReason, out bool hostile, out bool nearby)
	{
		candidate = null;
		blockReason = string.Empty;
		hostile = false;
		nearby = false;
		if (!IsEligibleAiTradeGang(robber) || humanPlayer == null || humanPlayer.crew == null)
		{
			blockReason = "invalid";
			return false;
		}
		if (ArePlayersProtectedByPactAlliance(robber, humanPlayer))
		{
			blockReason = "pact-protected";
			return false;
		}
		if (HasMutualTruce(robber, humanPlayer))
		{
			blockReason = "truce";
			return false;
		}
		if (TryGetAiHumanRobberyPairCooldown(robber, humanPlayer, now, out int pairCooldownUntilDay))
		{
			blockReason = "human-robbery-pair-cooldown";
			VerificationLog(
				"AIPlayerRobbery",
				$"blocked phase=candidate source=pair-cooldown robber={robber.PID.id} human={humanPlayer.PID.id} untilDay={pairCooldownUntilDay} cooldownDays={AI_ROBBERY_HUMAN_DIAGNOSTIC_COOLDOWN_DAYS}");
			return false;
		}
		if (IsAiPersonalityPeaceful(robber))
		{
			blockReason = "peaceful-personality";
			VerificationLog("AIPlayerRobbery", $"blocked phase=candidate source=personality robber={robber.PID.id} human={humanPlayer.PID.id} reason=peaceful-personality day={now.days}");
			return false;
		}

		bool directAggro = IsAggroWithoutTruceEitherWay(robber, humanPlayer);
		bool broadlyHostile = IsBroadlyHostileAiRobber(robber, humanPlayer, out int broadHostileCount);
		hostile = directAggro || broadlyHostile;
		if (IsAiHumanRobberyBlockedByGoodRelations(robber, humanPlayer, out float relationBias))
		{
			blockReason = "friendly-relations";
			return false;
		}
		bool humanTerritoryContact = false;
		if (!TryFindAiRobberyTrespassContact(robber, humanPlayer, out CrewAssignment targetCrew, out Node contactNode, out float distance, out bool sameNode, out int robberLocalPower, out int humanLocalPower, out string robberNodeSource, out string targetNodeSource, out bool strictTrespassNode, out bool territoryPressure, out bool recentTrespassMemory, out string contactReason))
		{
			bool territoryPresenceContact = false;
			if (!TryFindTerritoryPresenceRobberyContact(robber, humanPlayer, out targetCrew, out contactNode, out distance, out sameNode, out robberLocalPower, out humanLocalPower, out robberNodeSource, out targetNodeSource, out strictTrespassNode, out territoryPressure, out contactReason)
					&& !TryFindHumanTerritoryRobberyContact(robber, humanPlayer, out targetCrew, out contactNode, out distance, out sameNode, out robberLocalPower, out humanLocalPower, out robberNodeSource, out targetNodeSource, out contactReason)
					&& !TryFindHostileCashPressureRobberyContact(robber, humanPlayer, out targetCrew, out contactNode, out distance, out sameNode, out robberLocalPower, out humanLocalPower, out robberNodeSource, out targetNodeSource, out strictTrespassNode, out territoryPressure, out contactReason)
					&& !TryFindNearestAiRobberyContact(robber, humanPlayer, out targetCrew, out contactNode, out distance, out sameNode, out robberLocalPower, out humanLocalPower, out robberNodeSource, out targetNodeSource, out contactReason))
			{
				blockReason = contactReason;
				return false;
			}
			territoryPresenceContact = strictTrespassNode || territoryPressure;
			humanTerritoryContact = contactNode != null && PlayerTerritory.GetNodeOwner(contactNode) == humanPlayer.PID;
			if (!humanTerritoryContact && !territoryPresenceContact)
			{
				blockReason = "not-trespassing";
				return false;
			}
			if (!territoryPresenceContact)
			{
				strictTrespassNode = false;
				territoryPressure = false;
			}
			recentTrespassMemory = false;
		}
		nearby = true;

		int robberPower = CalculateGangPower(robber);
		int humanPower = CalculateGangPower(humanPlayer);
		PlayerID owner = PlayerTerritory.GetNodeOwner(contactNode);
		bool humanTerritory = owner == humanPlayer.PID;
		humanTerritoryContact = humanTerritoryContact || (humanTerritory && hostile);
		bool pressureTrespass = territoryPressure && !humanTerritory;
		if (!strictTrespassNode && !pressureTrespass && !humanTerritoryContact)
		{
			blockReason = territoryPressure
				? "territory-pressure-not-trespass"
				: (recentTrespassMemory ? "recent-trespass-not-current" : "not-trespassing");
			return false;
		}
		if (!IsAiHumanRobberyPhysicalTargetValid(humanPlayer, targetCrew, contactNode, out string physicalReason))
		{
			blockReason = physicalReason;
			return false;
		}
		bool enemyTerritory = owner == robber.PID || pressureTrespass;
		if (!enemyTerritory && !humanTerritoryContact)
		{
			blockReason = "not-trespassing";
			return false;
		}
		if (humanTerritoryContact && !enemyTerritory && !sameNode && distance > AI_ROBBERY_HUMAN_TERRITORY_MAX_CONTACT_DISTANCE)
		{
			blockReason = "human-territory-contact-too-far";
			return false;
		}
		if (pressureTrespass && !strictTrespassNode && !humanTerritoryContact)
		{
			if (distance > AI_ROBBERY_TERRITORY_PRESSURE_MAX_CONTACT_DISTANCE)
			{
				blockReason = "territory-pressure-too-far";
				return false;
			}
			if (robberPower + AI_ROBBERY_TERRITORY_PRESSURE_POWER_MARGIN < humanPower
				&& robberLocalPower < humanLocalPower)
			{
				blockReason = "weak-territory-pressure";
				return false;
			}
		}
		if (!PassesAiRobberyStrengthGate(enemyTerritory || humanTerritoryContact, robberPower, humanPower, robberLocalPower, humanLocalPower, out string strengthReason))
		{
			blockReason = strengthReason;
			return false;
		}

		int graceDays = CalculateAiRobberyTrespassGraceDays(directAggro, relationBias, robberPower - humanPower, robberLocalPower - humanLocalPower);
		candidate = new AiRobberyCandidate
		{
			Robber = robber,
			TargetCrew = targetCrew,
			ContactNode = contactNode,
			Distance = distance,
			SameNode = sameNode,
			RobberNodeSource = robberNodeSource,
			TargetNodeSource = targetNodeSource,
			DirectAggro = directAggro,
			BroadlyHostile = broadlyHostile,
			EnemyTerritory = enemyTerritory,
			HumanTerritory = humanTerritory,
			StrictTrespassNode = strictTrespassNode,
			TerritoryPressure = territoryPressure,
			RecentTrespassMemory = recentTrespassMemory,
			RobberPower = robberPower,
			HumanPower = humanPower,
			RobberLocalPower = robberLocalPower,
			HumanLocalPower = humanLocalPower,
			RarityChance = 1f,
			RarityRoll = 0f,
			WouldAttempt = true,
			TrespassGraceDays = graceDays,
			Reason = strictTrespassNode
				? (directAggro ? "trespass-direct-aggro" : "trespass")
				: (humanTerritoryContact ? "human-territory-contact" : (recentTrespassMemory ? "recent-trespass-shadow" : "territory-pressure"))
		};
		return true;
	}

	private static bool IsAiHumanRobberyBlockedByGoodRelations(PlayerInfo robber, PlayerInfo humanPlayer, out float relationBias)
	{
		relationBias = GetSignedGangRelationshipBias(robber, humanPlayer);
		return relationBias >= 0.2f;
	}

	private static bool IsGangTrespassingOnGangTerritory(PlayerInfo visitor, PlayerInfo owner, out int crewCount, out int nodeCount)
	{
		return IsGangTrespassingOrPressuringGangTerritory(visitor, owner, allowAdjacentPressure: false, out crewCount, out nodeCount, out _, out _);
	}

	private static bool HasRecentGangTrespassMemory(PlayerInfo visitor, PlayerInfo owner)
	{
		if (visitor == null || owner == null || visitor.PID.id == owner.PID.id || visitor.social == null || owner.social == null)
		{
			return false;
		}

		try
		{
			Relationship relationship = visitor.social.GetRelationshipFromPlayerTo(owner.PID);
			if (FindRelationshipBuffState(relationship, AI_ROBBERY_TRESPASS_RELBUFF_ID) != null)
			{
				return true;
			}
			return relationship?.GetHistoryOrNull()?.ContainsAction(SocialConstants.GANG_TRESPASS) == true;
		}
		catch
		{
			return false;
		}
	}

	private static bool IsGangTrespassingOrPressuringGangTerritory(PlayerInfo visitor, PlayerInfo owner, bool allowAdjacentPressure, out int trespassCrewCount, out int trespassNodeCount, out int adjacentCrewCount, out int adjacentNodeCount)
	{
		trespassCrewCount = 0;
		trespassNodeCount = 0;
		adjacentCrewCount = 0;
		adjacentNodeCount = 0;
		if (visitor == null || owner == null || visitor.PID.id == owner.PID.id || visitor.crew == null)
		{
			return false;
		}

		HashSet<int> trespassNodes = new HashSet<int>();
		HashSet<int> adjacentNodes = new HashSet<int>();
		foreach ((CrewAssignment crew, Node node, string source) entry in GetAiRobberyCrewNodes(visitor))
		{
			if (entry.node == null)
			{
				continue;
			}

			if (PlayerTerritory.GetNodeOwner(entry.node) == owner.PID)
			{
				trespassCrewCount++;
				trespassNodes.Add(entry.node.id.index);
				continue;
			}

			if (allowAdjacentPressure && IsNodeAdjacentToGangTerritory(entry.node, owner))
			{
				adjacentCrewCount++;
				adjacentNodes.Add(entry.node.id.index);
			}
		}

		trespassNodeCount = trespassNodes.Count;
		adjacentNodeCount = adjacentNodes.Count;
		return trespassCrewCount > 0 || adjacentCrewCount > 0;
	}

	private static bool IsNodeAdjacentToGangTerritory(Node node, PlayerInfo owner)
	{
		return TryFindGangTerritoryEdgeNode(node, owner, AI_ROBBERY_TERRITORY_EDGE_WORLD_DISTANCE, out _, out _, out _);
	}

	private static bool TryFindGangTerritoryEdgeNode(Node node, PlayerInfo owner, float maxDistance, out Node territoryNode, out float distance, out string source)
	{
		territoryNode = null;
		distance = float.MaxValue;
		source = "none";
		if (node == null || owner?.territory == null)
		{
			return false;
		}

		if (PlayerTerritory.GetNodeOwner(node) == owner.PID)
		{
			territoryNode = node;
			distance = 0f;
			source = "same-node";
			return true;
		}

		List<Node> neighbors = new List<Node>(16);
		AddAiRobberyTerritoryProbeNeighbors(node, neighbors, roadOnly: true);
		AddAiRobberyTerritoryProbeNeighbors(node, neighbors, roadOnly: false);
		for (int i = 0; i < neighbors.Count; i++)
		{
			Node neighbor = neighbors[i];
			if (neighbor != null && PlayerTerritory.GetNodeOwner(neighbor) == owner.PID)
			{
				territoryNode = neighbor;
				distance = (neighbor.pos - node.pos).Magnitude;
				source = "neighbor";
				return true;
			}
		}

		List<Node> secondHop = new List<Node>(32);
		for (int i = 0; i < neighbors.Count; i++)
		{
			AddAiRobberyTerritoryProbeNeighbors(neighbors[i], secondHop, roadOnly: false);
		}
		for (int i = 0; i < secondHop.Count; i++)
		{
			Node neighbor = secondHop[i];
			if (neighbor != null && PlayerTerritory.GetNodeOwner(neighbor) == owner.PID)
			{
				float currentDistance = (neighbor.pos - node.pos).Magnitude;
				if (currentDistance <= maxDistance)
				{
					territoryNode = neighbor;
					distance = currentDistance;
					source = "second-hop";
					return true;
				}
			}
		}

		try
		{
			foreach (NodeID ownedNodeId in owner.territory.OwnedNodeIds)
			{
				Node ownedNode = ownedNodeId.IsValid ? ownedNodeId.FindNode() : null;
				if (ownedNode == null)
				{
					continue;
				}
				float currentDistance = (ownedNode.pos - node.pos).Magnitude;
				if (currentDistance <= maxDistance && currentDistance < distance)
				{
					territoryNode = ownedNode;
					distance = currentDistance;
					source = "near-owned-node";
				}
			}
		}
		catch
		{
		}
		return territoryNode != null;
	}

	private static void AddAiRobberyTerritoryProbeNeighbors(Node node, List<Node> neighbors, bool roadOnly)
	{
		if (node == null || neighbors == null)
		{
			return;
		}
		try
		{
			if (roadOnly)
			{
				node.FindRoadNeighbors(neighbors);
			}
			else
			{
				node.FindAllNeighbors(neighbors);
			}
		}
		catch
		{
		}
	}

	private static void LogAiHumanRobberyNoTrespassProbe(PlayerInfo humanPlayer, List<PlayerInfo> gangs, SimTime now, string source)
	{
		try
		{
			List<(CrewAssignment crew, Node node, string source)> humanCrew = GetAiRobberyCrewNodes(humanPlayer)
				.Where(entry => entry.node != null)
				.Take(3)
				.ToList();
			if (humanCrew.Count == 0)
			{
				VerificationLog("AIPlayerRobbery", $"probe phase=trespass-scan source={source} day={now.days} reason=no-human-crew-node");
				return;
			}

			List<string> parts = new List<string>();
			foreach ((CrewAssignment crew, Node node, string nodeSource) entry in humanCrew)
			{
				PlayerID owner = PlayerTerritory.GetNodeOwner(entry.node);
				string nearest = FindNearestAiHumanRobberyTerritoryProbe(entry.node, gangs, humanPlayer);
				parts.Add($"crew={entry.crew.peepId.id} vehicle={entry.crew.VehicleID.id} node={entry.node.id} nodeSource={entry.nodeSource ?? "none"} owner={owner.id} nearest={nearest}");
			}
			VerificationLog("AIPlayerRobbery", $"probe phase=trespass-scan source={source} day={now.days} {string.Join(" | ", parts)}");
		}
		catch (Exception ex)
		{
			VerificationLog("AIPlayerRobbery", $"probe phase=trespass-scan source={source} day={now.days} reason=probe-failed-{ex.GetType().Name}");
		}
	}

	private static string FindNearestAiHumanRobberyTerritoryProbe(Node node, List<PlayerInfo> gangs, PlayerInfo humanPlayer)
	{
		if (node == null || gangs == null)
		{
			return "none";
		}
		PlayerInfo bestGang = null;
		Node bestNode = null;
		float bestDistance = float.MaxValue;
		string bestSource = "none";
		foreach (PlayerInfo gang in gangs)
		{
			if (gang == null || gang.PID.id == humanPlayer?.PID.id || IsAiHumanRobberyBlockedByGoodRelations(gang, humanPlayer, out _))
			{
				continue;
			}
			if (!TryFindGangTerritoryEdgeNode(node, gang, AI_ROBBERY_TERRITORY_EDGE_WORLD_DISTANCE * 2f, out Node territoryNode, out float distanceToTerritory, out string edgeSource))
			{
				continue;
			}
			if (distanceToTerritory < bestDistance)
			{
				bestGang = gang;
				bestNode = territoryNode;
				bestDistance = distanceToTerritory;
				bestSource = edgeSource;
			}
		}
		if (bestGang == null || bestNode == null)
		{
			return "none";
		}
		return $"{bestGang.PID.id}@{bestNode.id}/{bestDistance:0.0}/{bestSource}";
	}

	private static int CalculateAiRobberyTrespassGraceDays(bool directAggro, float relationBias, int powerEdge, int localPowerEdge)
	{
		if (directAggro || localPowerEdge >= 20 || powerEdge >= 80 || relationBias <= -0.35f)
		{
			return AI_ROBBERY_TRESPASS_GRACE_FAST_DAYS;
		}
		if (relationBias > 0.05f || (powerEdge < 0 && localPowerEdge < 10))
		{
			return AI_ROBBERY_TRESPASS_GRACE_SLOW_DAYS;
		}
		return AI_ROBBERY_TRESPASS_GRACE_DEFAULT_DAYS;
	}

	private static bool TryFindHumanTerritoryRobberyContact(PlayerInfo robber, PlayerInfo humanPlayer, out CrewAssignment targetCrew, out Node contactNode, out float distance, out bool sameNode, out int robberLocalPower, out int humanLocalPower, out string robberNodeSource, out string targetNodeSource, out string reason)
	{
		targetCrew = CrewAssignment.EMPTY;
		contactNode = null;
		distance = float.MaxValue;
		sameNode = false;
		robberLocalPower = 0;
		humanLocalPower = 0;
		robberNodeSource = "none";
		targetNodeSource = "none";
		reason = "no-human-territory-contact";
		List<(CrewAssignment crew, Node node, string source)> robberCrew = GetAiRobberyCrewNodes(robber, allowSafehouseFallback: true);
		List<(CrewAssignment crew, Node node, string source)> humanCrew = GetAiRobberyCrewNodes(humanPlayer);
		if (robberCrew.Count == 0)
		{
			reason = "no-robber-crew-node";
			return false;
		}
		if (humanCrew.Count == 0)
		{
			reason = "no-human-crew-node";
			return false;
		}

		Node robberContactNode = null;
		string bestRobberNodeSource = "none";
		string bestTargetNodeSource = "none";
		bool sawHumanTerritoryTarget = false;
		bool sawRobberInHumanTerritory = false;
		bool sawPhysicalTargetBlocked = false;
		string physicalTargetBlockReason = "no-vehicle-target";
		foreach ((CrewAssignment crew, Node node, string source) humanEntry in humanCrew)
		{
			if (humanEntry.node == null || PlayerTerritory.GetNodeOwner(humanEntry.node) != humanPlayer.PID)
			{
				continue;
			}
			sawHumanTerritoryTarget = true;
			if (!IsAiHumanRobberyPhysicalTargetValid(humanPlayer, humanEntry.crew, humanEntry.node, out string physicalReason))
			{
				sawPhysicalTargetBlocked = true;
				physicalTargetBlockReason = physicalReason;
				continue;
			}
			foreach ((CrewAssignment crew, Node node, string source) robberEntry in robberCrew)
			{
				if (robberEntry.node == null || PlayerTerritory.GetNodeOwner(robberEntry.node) != humanPlayer.PID)
				{
					continue;
				}
				sawRobberInHumanTerritory = true;
				float currentDistance = (robberEntry.node.pos - humanEntry.node.pos).Magnitude;
				if (currentDistance < distance)
				{
					distance = currentDistance;
					targetCrew = humanEntry.crew;
					contactNode = humanEntry.node;
					robberContactNode = robberEntry.node;
					bestRobberNodeSource = robberEntry.source ?? "none";
					bestTargetNodeSource = humanEntry.source ?? "none";
				}
			}
		}

		if (contactNode == null || robberContactNode == null)
		{
			reason = sawPhysicalTargetBlocked
				? physicalTargetBlockReason
				: (!sawHumanTerritoryTarget ? "no-human-territory-target" : (!sawRobberInHumanTerritory ? "no-robber-in-human-territory" : "no-human-territory-contact"));
			return false;
		}

		robberNodeSource = bestRobberNodeSource;
		targetNodeSource = bestTargetNodeSource;
		sameNode = robberContactNode.id == contactNode.id;
		if (!sameNode && distance > AI_ROBBERY_HUMAN_TERRITORY_MAX_CONTACT_DISTANCE)
		{
			reason = "human-territory-contact-too-far";
			return false;
		}
		robberLocalPower = Math.Max(10, CalculateAiRobberyLocalCrewPower(robberCrew, contactNode));
		humanLocalPower = Math.Max(10, CalculateAiRobberyLocalCrewPower(humanCrew, contactNode));
		reason = sameNode ? "human-territory-same-node-contact" : "human-territory-contact";
		return true;
	}

	private static bool TryFindTerritoryPresenceRobberyContact(PlayerInfo robber, PlayerInfo humanPlayer, out CrewAssignment targetCrew, out Node contactNode, out float distance, out bool sameNode, out int robberLocalPower, out int humanLocalPower, out string robberNodeSource, out string targetNodeSource, out bool strictTrespassNode, out bool territoryPressure, out string reason)
	{
		targetCrew = CrewAssignment.EMPTY;
		contactNode = null;
		distance = float.MaxValue;
		sameNode = false;
		robberLocalPower = 0;
		humanLocalPower = 0;
		robberNodeSource = "none";
		targetNodeSource = "none";
		strictTrespassNode = false;
		territoryPressure = false;
		reason = "no-territory-presence-contact";
		List<(CrewAssignment crew, Node node, string source)> robberCrew = GetAiRobberyCrewNodes(robber, allowSafehouseFallback: true);
		List<(CrewAssignment crew, Node node, string source)> humanCrew = GetAiRobberyCrewNodes(humanPlayer);
		if (robberCrew.Count == 0)
		{
			reason = "no-robber-crew-node";
			return false;
		}
		if (humanCrew.Count == 0)
		{
			reason = "no-human-crew-node";
			return false;
		}

		List<(CrewAssignment crew, Node node, string source)> robberTerritoryCrew = robberCrew
			.Where(entry => entry.node != null && PlayerTerritory.GetNodeOwner(entry.node) == robber.PID)
			.ToList();
		if (robberTerritoryCrew.Count == 0)
		{
			reason = "no-robber-territory-crew";
			return false;
		}

		bool sawPhysicalTargetBlocked = false;
		string physicalTargetBlockReason = "no-vehicle-target";
		foreach ((CrewAssignment crew, Node node, string source) humanEntry in humanCrew)
		{
			if (humanEntry.node == null)
			{
				continue;
			}
			PlayerID humanNodeOwner = PlayerTerritory.GetNodeOwner(humanEntry.node);
			bool strict = humanNodeOwner == robber.PID;
			bool pressure = !strict
				&& humanNodeOwner != humanPlayer.PID
				&& TryFindGangTerritoryEdgeNode(humanEntry.node, robber, AI_ROBBERY_TERRITORY_EDGE_WORLD_DISTANCE * 3f, out _, out _, out _);
			if (!strict && !pressure)
			{
				continue;
			}
			if (!IsAiHumanRobberyPhysicalTargetValid(humanPlayer, humanEntry.crew, humanEntry.node, out string physicalReason))
			{
				sawPhysicalTargetBlocked = true;
				physicalTargetBlockReason = physicalReason;
				continue;
			}

			foreach ((CrewAssignment crew, Node node, string source) robberEntry in robberTerritoryCrew)
			{
				float currentDistance = (robberEntry.node.pos - humanEntry.node.pos).Magnitude;
				if (currentDistance < distance)
				{
					distance = currentDistance;
					targetCrew = humanEntry.crew;
					contactNode = humanEntry.node;
					sameNode = robberEntry.node.id == humanEntry.node.id;
					robberNodeSource = robberEntry.source ?? "none";
					targetNodeSource = humanEntry.source ?? "none";
					strictTrespassNode = strict;
					territoryPressure = pressure;
				}
			}
		}

		if (contactNode == null)
		{
			reason = sawPhysicalTargetBlocked ? physicalTargetBlockReason : "no-human-target-in-robber-territory";
			return false;
		}

		robberLocalPower = Math.Max(10, CalculateAiRobberyLocalCrewPower(robberCrew, contactNode));
		humanLocalPower = Math.Max(10, CalculateAiRobberyLocalCrewPower(humanCrew, contactNode));
		reason = strictTrespassNode
			? (sameNode ? "territory-presence-trespass-same-node" : "territory-presence-trespass")
			: "territory-presence-pressure";
		return true;
	}

	private static bool TryFindHostileCashPressureRobberyContact(PlayerInfo robber, PlayerInfo humanPlayer, out CrewAssignment targetCrew, out Node contactNode, out float distance, out bool sameNode, out int robberLocalPower, out int humanLocalPower, out string robberNodeSource, out string targetNodeSource, out bool strictTrespassNode, out bool territoryPressure, out string reason)
	{
		targetCrew = CrewAssignment.EMPTY;
		contactNode = null;
		distance = float.MaxValue;
		sameNode = false;
		robberLocalPower = 0;
		humanLocalPower = 0;
		robberNodeSource = "none";
		targetNodeSource = "none";
		strictTrespassNode = false;
		territoryPressure = false;
		reason = "no-hostile-cash-pressure";
		List<(CrewAssignment crew, Node node, string source)> robberCrew = GetAiRobberyCrewNodes(robber, allowSafehouseFallback: true);
		List<(CrewAssignment crew, Node node, string source)> humanCrew = GetAiRobberyCrewNodes(humanPlayer);
		if (robberCrew.Count == 0)
		{
			reason = "no-robber-crew-node";
			return false;
		}
		if (humanCrew.Count == 0)
		{
			reason = "no-human-crew-node";
			return false;
		}

		int fallbackCash = Math.Max(GetGangSafehouseCleanCash(humanPlayer), GetGangCleanCash(humanPlayer));
		int bestCash = 0;
		Node robberContactNode = null;
		bool sawPhysicalTargetBlocked = false;
		bool sawCashTarget = false;
		bool sawTurfTarget = false;
		string physicalTargetBlockReason = "no-vehicle-target";
		foreach ((CrewAssignment crew, Node node, string source) humanEntry in humanCrew)
		{
			if (humanEntry.node == null)
			{
				continue;
			}
			if (!IsAiHumanRobberyPhysicalTargetValid(humanPlayer, humanEntry.crew, humanEntry.node, out string physicalReason))
			{
				sawPhysicalTargetBlocked = true;
				physicalTargetBlockReason = physicalReason;
				continue;
			}

			PlayerID owner = PlayerTerritory.GetNodeOwner(humanEntry.node);
			bool strict = owner == robber.PID;
			bool pressure = !strict
				&& owner != humanPlayer.PID
				&& TryFindGangTerritoryEdgeNode(humanEntry.node, robber, AI_ROBBERY_TERRITORY_EDGE_WORLD_DISTANCE * 3f, out _, out _, out _);
			if (!strict && !pressure)
			{
				continue;
			}
			sawTurfTarget = true;

			int carriedCash = Math.Max(0, GetCrewVehicleCleanCash(humanPlayer, humanEntry.crew))
				+ Math.Max(0, GetCrewPeepCleanCash(humanPlayer, humanEntry.crew));
			int availableCash = carriedCash > 0 ? carriedCash : fallbackCash;
			if (availableCash < AI_ROBBERY_CONTACT_LOW_CASH_MIN)
			{
				continue;
			}
			sawCashTarget = true;

			foreach ((CrewAssignment crew, Node node, string source) robberEntry in robberCrew)
			{
				if (robberEntry.node == null)
				{
					continue;
				}
				float currentDistance = (robberEntry.node.pos - humanEntry.node.pos).Magnitude;
				bool better = strictTrespassNode != strict
					? strict
					: (availableCash > bestCash || (availableCash == bestCash && currentDistance < distance));
				if (!better)
				{
					continue;
				}

				bestCash = availableCash;
				distance = currentDistance;
				targetCrew = humanEntry.crew;
				contactNode = humanEntry.node;
				robberContactNode = robberEntry.node;
				robberNodeSource = robberEntry.source ?? "none";
				targetNodeSource = humanEntry.source ?? "none";
				strictTrespassNode = strict;
				territoryPressure = pressure || strict;
			}
		}

		if (contactNode == null || robberContactNode == null)
		{
			reason = sawPhysicalTargetBlocked
				? physicalTargetBlockReason
				: (sawTurfTarget
					? (sawCashTarget ? "no-hostile-cash-robber-node" : "low-robbable-cash")
					: "no-hostile-turf-target");
			return false;
		}

		sameNode = robberContactNode.id == contactNode.id;
		robberLocalPower = Math.Max(10, CalculateAiRobberyLocalCrewPower(robberCrew, contactNode));
		humanLocalPower = Math.Max(10, CalculateAiRobberyLocalCrewPower(humanCrew, contactNode));
		reason = strictTrespassNode
			? (sameNode ? "hostile-cash-trespass-same-node" : "hostile-cash-trespass-pressure")
			: "hostile-cash-pressure";
		VerificationLog("AIPlayerRobbery", $"cash-pressure-contact robber={robber.PID.id} targetCrew={targetCrew.peepId.id} vehicle={targetCrew.VehicleID.id} node={contactNode.id} dist={distance:0.0} sameNode={sameNode} availableCash={bestCash} fallbackCash={fallbackCash} strictTrespass={strictTrespassNode} territoryPressure={territoryPressure} source={reason}");
		return true;
	}

	private static bool TryFindNearestAiRobberyContact(PlayerInfo robber, PlayerInfo humanPlayer, out CrewAssignment targetCrew, out Node contactNode, out float distance, out bool sameNode, out int robberLocalPower, out int humanLocalPower, out string robberNodeSource, out string targetNodeSource, out string reason)
	{
		targetCrew = CrewAssignment.EMPTY;
		contactNode = null;
		distance = float.MaxValue;
		sameNode = false;
		robberLocalPower = 0;
		humanLocalPower = 0;
		robberNodeSource = "none";
		targetNodeSource = "none";
		reason = "no-nearby-contact";
		List<(CrewAssignment crew, Node node, string source)> robberCrew = GetAiRobberyCrewNodes(robber, allowSafehouseFallback: true);
		List<(CrewAssignment crew, Node node, string source)> humanCrew = GetAiRobberyCrewNodes(humanPlayer);
		if (robberCrew.Count == 0)
		{
			reason = "no-robber-crew-node";
			return false;
		}
		if (humanCrew.Count == 0)
		{
			reason = "no-human-crew-node";
			return false;
		}

		Node robberContactNode = null;
		string bestRobberNodeSource = "none";
		string bestTargetNodeSource = "none";
		foreach ((CrewAssignment crew, Node node, string source) robberEntry in robberCrew)
		{
			foreach ((CrewAssignment crew, Node node, string source) humanEntry in humanCrew)
			{
				if (robberEntry.node == null || humanEntry.node == null)
				{
					continue;
				}
				float currentDistance = (robberEntry.node.pos - humanEntry.node.pos).Magnitude;
				if (currentDistance < distance)
				{
					distance = currentDistance;
					targetCrew = humanEntry.crew;
					contactNode = humanEntry.node;
					robberContactNode = robberEntry.node;
					bestRobberNodeSource = robberEntry.source ?? "none";
					bestTargetNodeSource = humanEntry.source ?? "none";
				}
			}
		}
		if (contactNode == null || robberContactNode == null)
		{
			return false;
		}
		robberNodeSource = bestRobberNodeSource;
		targetNodeSource = bestTargetNodeSource;

		sameNode = robberContactNode.id == contactNode.id;
		if (!sameNode && distance > AI_ROBBERY_NEARBY_PROMPT_WORLD_DISTANCE)
		{
			reason = "nearby-contact-too-far";
			return false;
		}

		robberLocalPower = CalculateAiRobberyLocalCrewPower(robberCrew, contactNode);
		humanLocalPower = CalculateAiRobberyLocalCrewPower(humanCrew, contactNode);
		if (robberLocalPower <= 0 || humanLocalPower <= 0)
		{
			reason = "missing-local-power";
			return false;
		}
		reason = sameNode ? "same-node-contact" : "nearby-contact";
		return true;
	}

	private static bool TryFindAiRobberyTrespassContact(PlayerInfo robber, PlayerInfo humanPlayer, out CrewAssignment targetCrew, out Node contactNode, out float distance, out bool sameNode, out int robberLocalPower, out int humanLocalPower, out string robberNodeSource, out string targetNodeSource, out bool strictTrespassNode, out bool territoryPressure, out bool recentTrespassMemory, out string reason)
	{
		targetCrew = CrewAssignment.EMPTY;
		contactNode = null;
		distance = float.MaxValue;
		sameNode = false;
		robberLocalPower = 0;
		humanLocalPower = 0;
		robberNodeSource = "none";
		targetNodeSource = "none";
		strictTrespassNode = false;
		territoryPressure = false;
		recentTrespassMemory = false;
		reason = "no-human-trespass";
		List<(CrewAssignment crew, Node node, string source)> robberCrew = GetAiRobberyCrewNodes(robber, allowSafehouseFallback: true);
		List<(CrewAssignment crew, Node node, string source)> humanCrew = GetAiRobberyCrewNodes(humanPlayer);
		if (humanCrew.Count == 0)
		{
			reason = "no-human-crew-node";
			return false;
		}
		if (robberCrew.Count == 0)
		{
			reason = "no-robber-crew-node";
			return false;
		}

		Node robberContactNode = null;
		string bestRobberNodeSource = "none";
		string bestTargetNodeSource = "none";
		bool sawPhysicalTargetBlocked = false;
		string physicalTargetBlockReason = "no-vehicle-target";
		bool sawNonStrictTrespass = false;
		bool bestStrictTrespass = false;
		bool bestTerritoryPressure = false;
		bool bestRecentTrespassMemory = false;
		foreach ((CrewAssignment crew, Node node, string source) humanEntry in humanCrew)
		{
			if (humanEntry.node == null)
			{
				continue;
			}
			if (!IsAiHumanRobberyPhysicalTargetValid(humanPlayer, humanEntry.crew, humanEntry.node, out string physicalReason))
			{
				sawPhysicalTargetBlocked = true;
				physicalTargetBlockReason = physicalReason;
				continue;
			}
			PlayerID humanNodeOwner = PlayerTerritory.GetNodeOwner(humanEntry.node);
			bool strictTrespass = humanNodeOwner == robber.PID;
			bool adjacentPressure = !strictTrespass
				&& humanNodeOwner != humanPlayer.PID
				&& TryFindGangTerritoryEdgeNode(humanEntry.node, robber, AI_ROBBERY_TERRITORY_EDGE_WORLD_DISTANCE * 3f, out _, out _, out _);
			if (!strictTrespass && !adjacentPressure)
			{
				sawNonStrictTrespass = true;
				continue;
			}

			foreach ((CrewAssignment crew, Node node, string source) robberEntry in robberCrew)
			{
				if (robberEntry.node == null)
				{
					continue;
				}

				float currentDistance = (robberEntry.node.pos - humanEntry.node.pos).Magnitude;
				if (currentDistance < distance)
				{
					distance = currentDistance;
					targetCrew = humanEntry.crew;
					contactNode = humanEntry.node;
					robberContactNode = robberEntry.node;
					bestRobberNodeSource = robberEntry.source ?? "none";
					bestTargetNodeSource = humanEntry.source ?? "none";
					bestStrictTrespass = strictTrespass;
					bestTerritoryPressure = adjacentPressure;
					bestRecentTrespassMemory = false;
				}
			}
		}

		if (contactNode == null || robberContactNode == null)
		{
			reason = sawPhysicalTargetBlocked
				? physicalTargetBlockReason
				: (sawNonStrictTrespass ? "territory-pressure-not-trespass" : "no-human-trespass");
			return false;
		}

		robberNodeSource = bestRobberNodeSource;
		targetNodeSource = bestTargetNodeSource;
		strictTrespassNode = bestStrictTrespass;
		territoryPressure = bestTerritoryPressure;
		recentTrespassMemory = bestRecentTrespassMemory;
		sameNode = robberContactNode.id == contactNode.id;
		robberLocalPower = Math.Max(10, CalculateAiRobberyLocalCrewPower(robberCrew, contactNode));
		humanLocalPower = CalculateAiRobberyLocalCrewPower(humanCrew, contactNode);
		if (humanLocalPower <= 0)
		{
			reason = "missing-human-local-power";
			return false;
		}

		reason = bestStrictTrespass
			? (sameNode ? "trespass-same-node-contact" : "trespass-shadow-contact")
			: "territory-pressure-contact";
		return true;
	}

	private static bool IsAiHumanRobberyPhysicalTargetValid(PlayerInfo humanPlayer, CrewAssignment targetCrew, Node contactNode, out string reason)
	{
		reason = "ok";
		try
		{
			if (humanPlayer?.crew == null || !targetCrew.IsValid || targetCrew.IsDead)
			{
				reason = "invalid-target-crew";
				return false;
			}
			if (!targetCrew.IsInVehicle || !targetCrew.VehicleID.IsValid)
			{
				Entity peep = targetCrew.GetPeep();
				Node peepNode = peep?.components?.agent?.GetNode();
				if (peepNode == null || contactNode == null || !contactNode.id.IsValid || peepNode.id != contactNode.id)
				{
					reason = "target-on-foot-not-at-node";
					return false;
				}
				return true;
			}
			if (!humanPlayer.crew.IsOnBoard(targetCrew.peepId))
			{
				reason = "target-not-onboard";
				return false;
			}
			if (MultiCrewVehicleHelper.GetLiveVehicleCrewCount(humanPlayer.crew, targetCrew.VehicleID) <= 0)
			{
				reason = "target-vehicle-empty";
				return false;
			}
			if (contactNode == null || !contactNode.id.IsValid)
			{
				reason = "missing-contact-node";
				return false;
			}
			if (!MultiCrewVehicleHelper.IsHumanVehiclePhysicallyAtNode(targetCrew.VehicleID, contactNode.id))
			{
				if (MultiCrewVehicleHelper.TryGetHumanVehicleCombatTargetNodeId(targetCrew.VehicleID, out NodeID combatNodeId, out string combatSource)
					&& combatNodeId == contactNode.id)
				{
					reason = "ok-" + (string.IsNullOrWhiteSpace(combatSource) ? "combat-authority" : combatSource);
					return true;
				}
				reason = "target-vehicle-not-physical";
				return false;
			}
			return true;
		}
		catch (Exception ex)
		{
			reason = "target-physical-check-failed-" + ex.GetType().Name;
			return false;
		}
	}

	private static List<(CrewAssignment crew, Node node, string source)> GetAiRobberyCrewNodes(PlayerInfo player, bool allowSafehouseFallback = false)
	{
		List<(CrewAssignment crew, Node node, string source)> result = new List<(CrewAssignment crew, Node node, string source)>();
		if (player?.crew == null)
		{
			return result;
		}
		Node fallbackNode = null;
		string fallbackSource = "none";
		if (allowSafehouseFallback)
		{
			TryResolveAiRobberySafehouseFallbackNode(player, out fallbackNode, out fallbackSource);
		}
		foreach (CrewAssignment crew in player.crew.GetLiving())
		{
			if (!TryGetAiRobberyCrewNode(crew, out Node node, out string source) || node == null)
			{
				if (fallbackNode == null)
				{
					continue;
				}
				node = fallbackNode;
				source = fallbackSource;
			}
			result.Add((crew, node, source));
		}
		return result;
	}

	private static bool TryResolveAiRobberySafehouseFallbackNode(PlayerInfo player, out Node node, out string source)
	{
		node = null;
		source = "none";
		try
		{
			EntityID safehouseId = player?.territory?.Safehouse ?? EntityID.INVALID;
			if (!safehouseId.IsValid)
			{
				source = "safehouse-missing";
				return false;
			}
			Entity safehouse = safehouseId.FindEntity();
			NodeID nodeId = safehouse?.data?.board?.bead.nodeId ?? NodeID.INVALID;
			if (!nodeId.IsValid && safehouse != null)
			{
				object board = safehouse.components?.board;
				nodeId = (NodeID)(board?.GetType().GetField("nodeId", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(board) ?? NodeID.INVALID);
			}
			node = nodeId.IsValid ? nodeId.FindNode() : null;
			source = node != null ? "safehouse-fallback" : "safehouse-node-missing";
			return node != null;
		}
		catch (Exception ex)
		{
			source = "safehouse-fallback-failed-" + ex.GetType().Name;
			return false;
		}
	}

	private static bool TryGetAiRobberyCrewNode(CrewAssignment crew, out Node node, out string source)
	{
		node = null;
		source = "none";
		if (!crew.IsValid || crew.IsDead)
		{
			return false;
		}
		if (crew.IsInVehicle && crew.VehicleID.IsValid)
		{
			if (MultiCrewVehicleHelper.TryGetStrictPhysicalVehicleNode(crew.VehicleID, out node, out source))
			{
				return node != null;
			}
			if (MultiCrewVehicleHelper.TryGetHumanVehicleCombatTargetNodeId(crew.VehicleID, out NodeID combatNodeId, out string combatSource)
				&& combatNodeId.IsValid)
			{
				node = combatNodeId.FindNode();
				source = string.IsNullOrWhiteSpace(combatSource) ? "combat-authority" : combatSource;
				return node != null;
			}
			if (MultiCrewVehicleHelper.TryGetAuthoritativeVehicleNodeId(crew.VehicleID, out NodeID authoritativeNodeId, out string authoritativeSource)
				&& authoritativeNodeId.IsValid)
			{
				node = authoritativeNodeId.FindNode();
				source = string.IsNullOrWhiteSpace(authoritativeSource) ? "authoritative" : authoritativeSource;
				return node != null;
			}
			source = "vehicle-not-physical";
			return false;
		}
		Entity peep = crew.GetPeep();
		node = peep?.components?.agent?.GetNode();
		source = node != null ? "agent" : "none";
		return node != null;
	}

	private static int CalculateAiRobberyLocalCrewPower(List<(CrewAssignment crew, Node node, string source)> crewNodes, Node contactNode)
	{
		if (crewNodes == null || contactNode == null)
		{
			return 0;
		}
		int total = 0;
		foreach ((CrewAssignment crew, Node node, string source) entry in crewNodes)
		{
			if (entry.node == null)
			{
				continue;
			}
			float distance = (entry.node.pos - contactNode.pos).Magnitude;
			if (entry.node.id == contactNode.id || distance <= AI_ROBBERY_NEARBY_WORLD_DISTANCE)
			{
				total += 10;
			}
		}
		return total;
	}

	private static bool PassesAiRobberyStrengthGate(bool enemyTerritory, int robberPower, int humanPower, int robberLocalPower, int humanLocalPower, out string reason)
	{
		if (enemyTerritory)
		{
			if (robberLocalPower >= Math.Max(10, humanLocalPower - 5))
			{
				reason = "enemy-territory-local-ok";
				return true;
			}
			if (robberLocalPower >= 10)
			{
				reason = "enemy-territory-shadow-ok";
				return true;
			}
			if (robberLocalPower >= 10 && robberPower + 20 >= humanPower)
			{
				reason = "enemy-territory-reinforcement-ok";
				return true;
			}
			if (robberPower >= humanPower + 30)
			{
				reason = "enemy-territory-gang-power-ok";
				return true;
			}
			reason = "weak-local-enemy-territory";
			return false;
		}
		if (robberPower >= humanPower || robberLocalPower >= humanLocalPower + 10)
		{
			reason = "neutral-or-human-territory-power-ok";
			return true;
		}
		reason = "weak-outside-territory";
		return false;
	}

	private static bool IsBroadlyHostileAiRobber(PlayerInfo robber, PlayerInfo humanPlayer, out int hostileCount)
	{
		hostileCount = 0;
		if (robber == null)
		{
			return false;
		}
		int activeTargets = 0;
		foreach (PlayerInfo other in G.GetAllPlayers())
		{
			if (other == null || other.PID.id == robber.PID.id || other.crew == null || other.crew.IsCrewDefeated)
			{
				continue;
			}
			if (!other.PID.IsHumanPlayer && !other.IsJustGang)
			{
				continue;
			}
			activeTargets++;
			if (IsAggroWithoutTruceEitherWay(robber, other))
			{
				hostileCount++;
			}
		}
		if (hostileCount >= 3)
		{
			return true;
		}
		if (activeTargets >= 4 && hostileCount * 2 >= activeTargets)
		{
			return true;
		}
		float humanHeat = humanPlayer == null ? 0f : GetHighestWarHeatForPair(robber.PID.id, humanPlayer.PID.id);
		return humanHeat >= 15f;
	}

	private static float CalculateAiRobberyHumanRarityChance(bool directAggro, bool broadlyHostile, bool enemyTerritory, bool sameNode, int powerEdge, int broadHostileCount)
	{
		float chance = 0.03f;
		if (directAggro)
		{
			chance += 0.04f;
		}
		if (broadlyHostile)
		{
			chance += Mathf.Min(0.04f, broadHostileCount * 0.01f);
		}
		if (enemyTerritory)
		{
			chance += 0.03f;
		}
		if (sameNode)
		{
			chance += 0.04f;
		}
		chance += Mathf.Clamp(powerEdge / 500f, -0.02f, 0.04f);
		return Mathf.Clamp(chance, 0.02f, 0.18f);
	}

	private static void ApplyPactTradeRelationshipBuff(PlayerInfo seller, PlayerInfo buyer, bool logSuccess = true)
	{
		if (seller == null || buyer == null)
		{
			return;
		}

		if (!EnsureCustomRelationshipBuffDefinitions())
		{
			return;
		}

		AddMutualRelationshipBuff(seller, buyer, "relbuff-pact-trade", GetCrewPeepForPlayer(seller), GetCrewPeepForPlayer(buyer), logSuccess);
	}

	private static bool ShouldLogAiTradeRelationshipBuffs(PlayerInfo seller, PlayerInfo buyer)
	{
		return seller?.PID.IsHumanPlayer == true || buyer?.PID.IsHumanPlayer == true;
	}

	private static void AwardHumanPactTradeStreetCredit(PlayerInfo seller, PlayerInfo buyer, string source, float minGain, float maxGain)
	{
		PlayerInfo humanPlayer = seller?.PID.IsHumanPlayer == true ? seller : buyer?.PID.IsHumanPlayer == true ? buyer : null;
		if (humanPlayer == null)
		{
			return;
		}

		CrewRelationshipHandlerPatch.TryAwardCrewStreetCreditProgress(humanPlayer, GetCrewPeepForPlayer(humanPlayer), minGain, maxGain, source);
	}

		private static void ApplyAiTradeMoodDelta(AiTradeProposal proposal, AlliancePact pact, float delta)
		{
			if (proposal == null || delta <= 0f)
			{
				return;
			}
			if (pact != null)
			{
				ApplyAiPactTradeMoodDelta(pact, proposal.Seller?.PID.id ?? -1, proposal.Buyer?.PID.id ?? -1, delta);
				return;
			}
			if (proposal.SellerActor?.Pact != null && proposal.Seller != null && IsGangInPact(proposal.SellerActor.Pact, proposal.Seller.PID.id))
			{
				proposal.SellerActor.Pact.SetBossHappiness(proposal.Seller.PID.id, proposal.SellerActor.Pact.GetBossHappiness(proposal.Seller.PID.id) + delta);
			}
			if (proposal.BuyerActor?.Pact != null && proposal.Buyer != null && IsGangInPact(proposal.BuyerActor.Pact, proposal.Buyer.PID.id))
			{
				proposal.BuyerActor.Pact.SetBossHappiness(proposal.Buyer.PID.id, proposal.BuyerActor.Pact.GetBossHappiness(proposal.Buyer.PID.id) + delta);
			}
		}

		private static void ApplyAiPactTradeMoodDelta(AlliancePact pact, int sellerGangId, int buyerGangId, float delta)
		{
			if (pact == null || delta <= 0f)
			{
				return;
			}
			if (sellerGangId >= 0 && IsGangInPact(pact, sellerGangId))
			{
				pact.SetBossHappiness(sellerGangId, pact.GetBossHappiness(sellerGangId) + delta);
			}
			if (buyerGangId >= 0 && IsGangInPact(pact, buyerGangId))
			{
				pact.SetBossHappiness(buyerGangId, pact.GetBossHappiness(buyerGangId) + delta);
			}
		}

		private static AiTradeActor CreateGangTradeActor(PlayerInfo gang)
		{
			return gang == null ? null : new AiTradeActor
			{
				Gang = gang,
				ActorKey = "gang:" + gang.PID.id
			};
		}

		private static AiTradeActor CreatePactTradeActor(AlliancePact pact)
		{
			return pact == null ? null : new AiTradeActor
			{
				Pact = pact,
				ActorKey = "pact:" + pact.PactId
			};
		}

		private static List<PlayerInfo> GetTradeActorGangCandidates(AiTradeActor actor)
		{
			if (actor == null)
			{
				return new List<PlayerInfo>();
			}
			if (actor.Gang != null)
			{
				return IsEligibleAiTradeGang(actor.Gang) ? new List<PlayerInfo> { actor.Gang } : new List<PlayerInfo>();
			}
			if (actor.Pact == null || !actor.Pact.IsActive)
			{
				return new List<PlayerInfo>();
			}
			return GetPactMemberGangIds(actor.Pact)
				.Select(G.FindPlayerById)
				.Where(IsEligibleAiTradeGang)
				.Distinct()
				.ToList();
		}

		private static bool ShouldSkipExternalTradePair(AiTradeActor sellerActor, AiTradeActor buyerActor)
		{
			if (sellerActor == null || buyerActor == null || string.Equals(sellerActor.ActorKey, buyerActor.ActorKey, StringComparison.Ordinal))
			{
				return true;
			}
			if (sellerActor.Pact != null && buyerActor.Pact != null)
			{
				return string.Equals(sellerActor.Pact.PactId, buyerActor.Pact.PactId, StringComparison.Ordinal);
			}
			if (sellerActor.Pact != null && buyerActor.Gang != null)
			{
				return IsGangInPact(sellerActor.Pact, buyerActor.Gang.PID.id);
			}
			if (buyerActor.Pact != null && sellerActor.Gang != null)
			{
				return IsGangInPact(buyerActor.Pact, sellerActor.Gang.PID.id);
			}
			if (sellerActor.Gang != null && buyerActor.Gang != null)
			{
				AlliancePact pactForPlayer = GetPactForPlayer(sellerActor.Gang.PID);
				AlliancePact pactForPlayer2 = GetPactForPlayer(buyerActor.Gang.PID);
				return pactForPlayer != null && pactForPlayer2 != null && string.Equals(pactForPlayer.PactId, pactForPlayer2.PactId, StringComparison.Ordinal);
			}
			return false;
		}

		private static bool IsEligibleAiTradeGang(PlayerInfo gang)
		{
			return gang != null && gang.IsJustGang && !gang.PID.IsHumanPlayer && gang.crew != null && !gang.crew.IsCrewDefeated;
		}

		private static bool IsGangInExcludedHumanPact(PlayerInfo gang, AlliancePact excludedPact)
		{
			if (gang == null || excludedPact == null)
			{
				return false;
			}
			AlliancePact pactForPlayer = GetPactForPlayer(gang.PID);
			return pactForPlayer != null && string.Equals(pactForPlayer.PactId, excludedPact.PactId, StringComparison.Ordinal);
		}

		private static string GetAiTradeActorDisplayName(AiTradeActor actor, PlayerInfo representative)
		{
			if (actor?.Pact != null)
			{
				string displayName = string.IsNullOrEmpty(actor.Pact.DisplayName) ? "Pact" : actor.Pact.DisplayName;
				return representative == null ? displayName : (displayName + " via " + GetGangDisplayName(representative.PID.id));
			}
			return representative == null ? "Unknown" : GetGangDisplayName(representative.PID.id);
		}

		private static string GetAiTradeGrapevinePrefix(bool externalNetwork)
		{
			return externalNetwork ? "TRADE" : "PACT";
		}

		private static float GetGangTradeDisposition(PlayerInfo gang)
		{
			if (gang == null)
			{
				return 0f;
			}
			float num = 0f;
			if (HasAnyGangBossTrait(gang, "trait-friendly", "trait-sociable", "trait-confident"))
			{
				num += 0.12f;
			}
			if (HasAnyGangBossTrait(gang, "trait-connected", "trait-talkative"))
			{
				num += 0.14f;
			}
			if (HasAnyGangBossTrait(gang, "trait-intelligent", "trait-organized"))
			{
				num += 0.08f;
			}
			if (HasAnyGangBossTrait(gang, "trait-cautious", "trait-upright", "trait-religious"))
			{
				num -= 0.12f;
			}
			if (HasAnyGangBossTrait(gang, "trait-loner", "trait-nervous"))
			{
				num -= 0.08f;
			}
			return Mathf.Clamp(num, -0.3f, 0.3f);
		}

		private static bool HasAnyGangBossTrait(PlayerInfo gang, params string[] traitIds)
		{
			if (gang == null || traitIds == null || traitIds.Length == 0)
			{
				return false;
			}
			EntityID crewPeepForPlayer = GetCrewPeepForPlayer(gang);
			Entity entity = crewPeepForPlayer.IsValid ? crewPeepForPlayer.FindEntity() : null;
			if (entity == null)
			{
				return false;
			}
			foreach (string traitId in traitIds)
			{
				if (HasTrait(entity, traitId))
				{
					return true;
				}
			}
			return false;
		}

		private static Entity GetGangSafehouseEntity(PlayerInfo gang)
		{
			EntityID safehouse = gang?.territory?.Safehouse ?? EntityID.INVALID;
			return safehouse.IsValid ? safehouse.FindEntity() : null;
		}

		private static int GetGangSafehouseResourceTotal(PlayerInfo gang, IReadOnlyList<string> labels)
		{
			Entity gangSafehouseEntity = GetGangSafehouseEntity(gang);
			if (gangSafehouseEntity == null || labels == null)
			{
				return 0;
			}
			int num = 0;
			foreach (string label in labels)
			{
				num += ReadInventoryAmount(gangSafehouseEntity, label);
			}
			return num;
		}

		private static bool TryTransferCleanCashBetweenGangs(PlayerInfo payer, PlayerInfo payee, int amount, int reserveAfterSpend)
		{
			if (payer == null || payee == null || amount <= 0)
			{
				return false;
			}
			if (GetGangCleanCash(payer) - amount < reserveAfterSpend)
			{
				return false;
			}
			try
			{
				payer.finances.DoChangeMoneyOnSafehouse(new Price((Fixnum)(-amount)), (MoneyReason)1);
				payee.finances.DoChangeMoneyOnSafehouse(new Price((Fixnum)(amount)), (MoneyReason)1);
				return true;
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] TryTransferCleanCashBetweenGangs failed: " + ex.Message);
				return false;
			}
		}

		private static int RemoveGangDirtyCash(PlayerInfo gang, int amount)
		{
			Entity gangSafehouseEntity = GetGangSafehouseEntity(gang);
			return gangSafehouseEntity != null ? RemoveDirtyCash(gangSafehouseEntity, amount) : 0;
		}

		private static bool AddDirtyCashToGang(PlayerInfo gang, int amount)
		{
			Entity gangSafehouseEntity = GetGangSafehouseEntity(gang);
			if (gangSafehouseEntity == null || amount <= 0)
			{
				return false;
			}
			int before = ReadInventoryAmount(gangSafehouseEntity, ModConstants.DIRTY_CASH_LABEL);
			AddDirtyCash(gangSafehouseEntity, amount, "ai-pact-trade");
			return ReadInventoryAmount(gangSafehouseEntity, ModConstants.DIRTY_CASH_LABEL) >= before + amount;
		}

		private static bool TryTransferGangSafehouseResources(PlayerInfo seller, PlayerInfo buyer, IReadOnlyList<string> labels, int requestedQty, out int movedQty, out string movedSummary)
		{
			movedQty = 0;
			movedSummary = string.Empty;
			Entity gangSafehouseEntity = GetGangSafehouseEntity(seller);
			Entity gangSafehouseEntity2 = GetGangSafehouseEntity(buyer);
			if (gangSafehouseEntity == null || gangSafehouseEntity2 == null || labels == null || requestedQty <= 0)
			{
				return false;
			}
			List<string> list = new List<string>();
			int num = requestedQty;
			foreach (string label in labels)
			{
				if (num <= 0)
				{
					break;
				}
				int num2 = Math.Min(ReadInventoryAmount(gangSafehouseEntity, label), num);
				if (num2 <= 0)
				{
					continue;
				}
				if (!TryRemoveInventoryResource(gangSafehouseEntity, label, num2))
				{
					continue;
				}
				if (!TryAddInventoryResource(gangSafehouseEntity2, label, num2))
				{
					TryAddInventoryResource(gangSafehouseEntity, label, num2);
					continue;
				}
				movedQty += num2;
				num -= num2;
				list.Add(label + " x" + num2);
			}
			movedSummary = string.Join(", ", list);
			return movedQty > 0;
		}

		private static bool TryAddInventoryResource(Entity entity, string labelName, int amount)
		{
			if (entity == null || amount <= 0 || string.IsNullOrEmpty(labelName))
			{
				return false;
			}
			try
			{
				InventoryModule inventory = ModulesUtil.GetInventory(entity);
				if (inventory == null)
				{
					return false;
				}
				Label val = new Label(labelName);
				return inventory.ForceAddResourcesRegardlessOfSpace(val, amount) > 0;
			}
			catch
			{
				return false;
			}
		}

		private static bool TryRemoveInventoryResource(Entity entity, string labelName, int amount)
		{
			if (entity == null || amount <= 0 || string.IsNullOrEmpty(labelName))
			{
				return false;
			}
			try
			{
				InventoryModule inventory = ModulesUtil.GetInventory(entity);
				if (((Module<InventoryModule, InventoryModuleConfig, InventoryModuleData>)(object)inventory)?.data == null)
				{
					return false;
				}
				Label val = new Label(labelName);
				int num2 = ReadInventoryAmount(entity, labelName);
				int num = Math.Min(num2, amount);
				if (num <= 0)
				{
					return false;
				}
				inventory.TryRemoveResourcesUpToAll(val, num);
				return ReadInventoryAmount(entity, labelName) <= Math.Max(0, num2 - num);
			}
			catch
			{
				return false;
			}
		}

		private static void ProcessPactVotes(SimTime now)
		{
			try
			{
				List<PlayerInfo> allPlayers = G.GetAllPlayers().ToList();
				PlayerInfo humanPlayer = G.GetHumanPlayer();
				int humanGangId = ((humanPlayer != null) ? humanPlayer.PID.id : (-1));
				bool pactMembershipChanged = false;
				foreach (AlliancePact pact in SaveData.Pacts)
				{
					if (!pact.IsActive) continue;
					if (pact.LastVoteDay < 0)
					{
						pact.LastVoteDay = now.days;
						CompletePactVoteCycle(pact);
						continue;
					}
					if (now.days - pact.LastVoteDay < 365) continue;
					List<int> allMembers = new List<int>();
					if (pact.LeaderGangId >= 0) allMembers.Add(pact.LeaderGangId);
					allMembers.AddRange(pact.MemberIds);
					allMembers = allMembers.Distinct().ToList();
					if (SaveData.PlayerJoinedPactIndex == pact.ColorIndex && humanGangId >= 0 && !allMembers.Contains(humanGangId))
					{
						allMembers.Add(humanGangId);
					}
					if (allMembers.Count < 1) continue;
					if (ShouldDelayPactVoteResolutionForPrompt(pact, allMembers, humanGangId, now.days))
					{
						continue;
					}
					pact.LastVoteDay = now.days;
					EnsurePactVotePreferences(pact, allMembers, humanGangId);
					Dictionary<int, int> tally = new Dictionary<int, int>
					{
						{ PACT_VOTE_EARNINGS_UP, 0 },
						{ PACT_VOTE_EARNINGS_DOWN, 0 },
						{ PACT_VOTE_CREW_UP, 0 },
						{ PACT_VOTE_CREW_STAY, 0 }
					};
					Dictionary<int, int> castVotes = new Dictionary<int, int>();
					foreach (int memberId in allMembers)
					{
						PlayerInfo member = allPlayers.FirstOrDefault(p => p.PID.id == memberId);
						if (member == null || member.crew.IsCrewDefeated)
						{
							continue;
						}
						bool isHumanMember = memberId == humanGangId;
						int voteChoice = GetOrAssignPactVotePreference(pact, memberId, isHumanMember, humanGangId);
						if (isHumanMember && pact.PlayerProposedVote >= 0)
						{
							voteChoice = ClampPactVoteChoice(pact.PlayerProposedVote, allowNone: false);
							pact.VotePreferences[memberId] = voteChoice;
						}
						castVotes[memberId] = voteChoice;
						tally[voteChoice] = tally[voteChoice] + 1;
					}
					if (castVotes.Count == 0)
					{
						continue;
					}
					int winningVote = DetermineWinningPactVote(tally, pact.PlayerProposedVote);
					pact.LastVoteType = winningVote;
					ApplyPactVoteOutcome(pact, winningVote);
					LogGrapevine($"PACT: {pact.DisplayName} vote passed: {GetPactVoteLabel(winningVote)}");
					foreach (KeyValuePair<int, int> castVote in castVotes)
					{
						float before = pact.GetBossHappiness(castVote.Key);
						float delta = (castVote.Value == winningVote) ? 0.03f : -0.07f;
						pact.SetBossHappiness(castVote.Key, before + delta);
					}
					if (ProcessAnnualPactDefections(pact, allPlayers))
					{
						pactMembershipChanged = true;
					}
					HashSet<int> survivingMembers = new HashSet<int>(pact.MemberIds);
					if (pact.LeaderGangId >= 0)
					{
						survivingMembers.Add(pact.LeaderGangId);
					}
					foreach (int memberId in survivingMembers.ToList())
					{
						if (!pact.VotePreferences.ContainsKey(memberId))
						{
							continue;
						}
						if (castVotes.TryGetValue(memberId, out int castChoice) && castChoice != winningVote && SharedRng.NextDouble() < 0.45)
						{
							pact.VotePreferences[memberId] = winningVote;
						}
						else if (SharedRng.NextDouble() < 0.2)
						{
							pact.VotePreferences[memberId] = RollPactVotePreference(pact.GetBossHappiness(memberId), memberId == humanGangId, pact.PlayerProposedVote);
						}
					}
					Debug.Log($"[GameplayTweaks] Pact {pact.DisplayName} vote result: {GetPactVoteShortLabel(winningVote)} | earnings={pact.EarningRate:P0}, crewCap=+{pact.CrewCapacityBonus}");
					CompletePactVoteCycle(pact);
				}
				if (pactMembershipChanged)
				{
					RefreshPactCache();
					TerritoryColorPatch.RefreshAllTerritoryColors();
				}
			}
			catch (Exception arg)
			{
				Debug.LogError($"[GameplayTweaks] ProcessPactVotes: {arg}");
			}
		}

		private static void ProcessAIInterPactAlliances(SimTime now)
		{
			try
			{
				if (now.days < 0 || now.days % 45 != 0)
				{
					return;
				}
				NormalizeInterPactAllianceData();
				PlayerInfo humanPlayer = G.GetHumanPlayer();
				int humanGangId = humanPlayer?.PID.id ?? -1;
				List<AlliancePact> list = SaveData.Pacts.Where((AlliancePact p) => p != null && p.IsActive && !GetPactMemberGangIds(p).Contains(humanGangId)).ToList();
				if (list.Count < 2)
				{
					return;
				}
				var source = (from a in list
					from b in list
					where !string.Equals(a.PactId, b.PactId, StringComparison.Ordinal)
					where string.CompareOrdinal(a.PactId, b.PactId) < 0
					select new
					{
						First = a,
						Second = b,
						Score = Mathf.Clamp01(GetInterGangRelationshipScore(G.FindPlayerById(a.LeaderGangId), G.FindPlayerById(b.LeaderGangId))) + (CanPactsFormAlliance(a, b) ? 0.15f : 0f)
					}).Where(x => CanPactsFormAlliance(x.First, x.Second) && FindPendingInterPactAllianceVote(x.First, x.Second, isRemoval: false) == null).OrderByDescending(x => x.Score).ToList();
				if (source.Count > 0 && source[0].Score >= 0.55f && SharedRng.NextDouble() < 0.65)
				{
					var anon = source[0];
					TryQueueInterPactAllianceVote(anon.First, anon.Second, isRemoval: false, anon.First.LeaderGangId, initiatedByHuman: false, out _);
				}
				if (SaveData.PactAlliances == null || SaveData.PactAlliances.Count == 0)
				{
					return;
				}
				foreach (InterPactAlliance item in SaveData.PactAlliances.ToList())
				{
					AlliancePact pactById = GetPactById(item.LeftPactId);
					AlliancePact pactById2 = GetPactById(item.RightPactId);
					if (pactById == null || pactById2 == null || !pactById.IsActive || !pactById2.IsActive)
					{
						continue;
					}
					if (FindPendingInterPactAllianceVote(pactById, pactById2, isRemoval: true) != null)
					{
						continue;
					}
					PlayerInfo playerById = G.FindPlayerById(pactById.LeaderGangId);
					PlayerInfo playerById2 = G.FindPlayerById(pactById2.LeaderGangId);
					float interGangRelationshipScore = GetInterGangRelationshipScore(playerById, playerById2);
					bool flag = (playerById != null && playerById2 != null && (IsAggroWithoutTruceOneWay(playerById, playerById2.PID) || IsAggroWithoutTruceOneWay(playerById2, playerById.PID))) || interGangRelationshipScore < 0.28f;
					if (flag && SharedRng.NextDouble() < 0.55)
					{
						TryQueueInterPactAllianceVote(pactById, pactById2, isRemoval: true, pactById.LeaderGangId, initiatedByHuman: false, out _);
					}
				}
			}
			catch (Exception ex)
			{
				Debug.LogWarning($"[GameplayTweaks] ProcessAIInterPactAlliances failed: {ex.Message}");
			}
		}

		private static void ProcessInterPactAllianceVotes(SimTime now)
		{
			try
			{
				NormalizeInterPactAllianceData();
				if (SaveData.PactAllianceVotes == null || SaveData.PactAllianceVotes.Count == 0)
				{
					return;
				}
				PlayerInfo humanPlayer = G.GetHumanPlayer();
				int humanGangId = humanPlayer?.PID.id ?? -1;
				foreach (InterPactAllianceVote item in SaveData.PactAllianceVotes.Where(v => v != null && !v.Resolved).ToList())
				{
					AlliancePact pactById = GetPactById(item.SourcePactId);
					AlliancePact pactById2 = GetPactById(item.TargetPactId);
					if (pactById == null || pactById2 == null || !pactById.IsActive || !pactById2.IsActive)
					{
						item.Resolved = true;
						item.ResolvedDay = now.days;
						continue;
					}
					List<int> interPactAllianceEligibleGangIds = GetInterPactAllianceEligibleGangIds(item);
					foreach (int item2 in interPactAllianceEligibleGangIds)
					{
						if (item.VotesByGang.ContainsKey(item2) || item2 == humanGangId)
						{
							continue;
						}
						PlayerInfo playerById = G.FindPlayerById(item2);
						if (playerById == null || playerById.crew == null || playerById.crew.IsCrewDefeated)
						{
							continue;
						}
						AlliancePact alliancePact = GetPactForPlayer(playerById.PID);
						AlliancePact otherPact = string.Equals(alliancePact?.PactId, pactById.PactId, StringComparison.Ordinal) ? pactById2 : pactById;
						bool value = EvaluateInterPactAllianceSupport(playerById, alliancePact, otherPact, item.IsRemoval) >= 0.5f;
						item.VotesByGang[item2] = value;
					}
					bool flag = now.days > item.ProposedDay || interPactAllianceEligibleGangIds.All((int id) => item.VotesByGang.ContainsKey(id) || id == humanGangId);
					if (!flag)
					{
						continue;
					}
					List<int> list = interPactAllianceEligibleGangIds.Where((int id) => item.VotesByGang.TryGetValue(id, out bool value3) && value3).ToList();
					List<int> list2 = interPactAllianceEligibleGangIds.Where((int id) => item.VotesByGang.TryGetValue(id, out bool value4) && !value4).ToList();
					bool flag2;
					if (!item.IsRemoval)
					{
						bool flag3 = list.Any((int id) => GetPactMemberGangIds(pactById).Contains(id));
						bool flag4 = list.Any((int id) => GetPactMemberGangIds(pactById2).Contains(id));
						flag2 = list.Count > list2.Count && flag3 && flag4 && FindActiveInterPactAlliance(pactById, pactById2) == null;
						if (flag2)
						{
							SaveData.PactAlliances.Add(new InterPactAlliance
							{
								AllianceId = "ipa_" + SaveData.NextPactAllianceId++,
								LeftPactId = pactById.PactId,
								RightPactId = pactById2.PactId,
								CreatedDay = now.days,
								Active = true
							});
							LogGrapevine($"PACT: {pactById.DisplayName} and {pactById2.DisplayName} formed an inter-pact alliance.");
						}
						else
						{
							LogGrapevine($"PACT: Alliance vote failed between {pactById.DisplayName} and {pactById2.DisplayName}.");
						}
					}
					else
					{
						flag2 = list.Count > 2;
						if (flag2)
						{
							InterPactAlliance activeInterPactAlliance = FindActiveInterPactAlliance(pactById, pactById2);
							if (activeInterPactAlliance != null)
							{
								SaveData.PactAlliances.Remove(activeInterPactAlliance);
								LogGrapevine($"PACT: The alliance between {pactById.DisplayName} and {pactById2.DisplayName} was dissolved.");
							}
						}
					}
					item.Resolved = true;
					item.ResolvedDay = now.days;
				}
			}
			catch (Exception ex)
			{
				Debug.LogWarning($"[GameplayTweaks] ProcessInterPactAllianceVotes failed: {ex.Message}");
			}
		}

		private static void ShareLeaderEnemiesAcrossInterPactAlliances()
		{
			try
			{
				foreach (InterPactAlliance activeInterPactAlliance in GetActiveInterPactAlliances().ToList())
				{
					AlliancePact pactById = GetPactById(activeInterPactAlliance.LeftPactId);
					AlliancePact pactById2 = GetPactById(activeInterPactAlliance.RightPactId);
					PlayerInfo playerById = G.FindPlayerById(pactById?.LeaderGangId ?? -1);
					PlayerInfo playerById2 = G.FindPlayerById(pactById2?.LeaderGangId ?? -1);
					if (pactById == null || pactById2 == null || playerById == null || playerById2 == null)
					{
						continue;
					}
					List<PlayerInfo> list = G.GetAllPlayers().Where((PlayerInfo p) => p != null && p.IsJustGang && p.crew != null && !p.crew.IsCrewDefeated).ToList();
					foreach (PlayerInfo item in list)
					{
						if (item.PID.id == playerById.PID.id || item.PID.id == playerById2.PID.id || ArePlayersProtectedByPactAlliance(item, playerById) || ArePlayersProtectedByPactAlliance(item, playerById2))
						{
							continue;
						}
						bool flag = IsAggroWithoutTruceOneWay(playerById, item.PID) || IsAggroWithoutTruceOneWay(item, playerById.PID);
						bool flag2 = IsAggroWithoutTruceOneWay(playerById2, item.PID) || IsAggroWithoutTruceOneWay(item, playerById2.PID);
						if (flag)
						{
							foreach (int item2 in GetPactMemberGangIds(pactById2))
							{
								PlayerInfo playerById3 = G.FindPlayerById(item2);
								if (playerById3 != null && !HasMutualTruce(playerById3, item))
								{
									ActivateWarBetweenPlayers(playerById3, item);
								}
							}
						}
						if (flag2)
						{
							foreach (int item3 in GetPactMemberGangIds(pactById))
							{
								PlayerInfo playerById4 = G.FindPlayerById(item3);
								if (playerById4 != null && !HasMutualTruce(playerById4, item))
								{
									ActivateWarBetweenPlayers(playerById4, item);
								}
							}
						}
					}
				}
			}
			catch (Exception ex)
			{
				Debug.LogWarning($"[GameplayTweaks] ShareLeaderEnemiesAcrossInterPactAlliances failed: {ex.Message}");
			}
		}

		private static int DetermineWinningPactVote(Dictionary<int, int> tally, int playerProposal)
		{
			int bestVote = PACT_VOTE_EARNINGS_UP;
			int bestVotes = -1;
			List<int> ties = new List<int>();
			for (int i = PACT_VOTE_EARNINGS_UP; i <= PACT_VOTE_CREW_STAY; i++)
			{
				int count = tally.TryGetValue(i, out int c) ? c : 0;
				if (count > bestVotes)
				{
					bestVotes = count;
					bestVote = i;
					ties.Clear();
					ties.Add(i);
				}
				else if (count == bestVotes)
				{
					ties.Add(i);
				}
			}
			if (ties.Count <= 1)
			{
				return bestVote;
			}
			int proposed = ClampPactVoteChoice(playerProposal, allowNone: true);
			if (proposed >= 0 && ties.Contains(proposed))
			{
				return proposed;
			}
			return ties[SharedRng.Next(ties.Count)];
		}

		private static bool ProcessAnnualPactDefections(AlliancePact pact, List<PlayerInfo> allPlayers)
		{
			bool changed = false;
			if (pact == null || pact.MemberIds == null || pact.MemberIds.Count == 0)
			{
				return false;
			}
			PlayerInfo leader = allPlayers.FirstOrDefault(p => p.PID.id == pact.LeaderGangId);
			string leaderName = leader?.social?.PlayerGroupName ?? ("Gang#" + pact.LeaderGangId);
			foreach (int memberId in pact.MemberIds.Distinct().ToList())
			{
				PlayerInfo member = allPlayers.FirstOrDefault(p => p.PID.id == memberId);
				if (member == null || member.crew.IsCrewDefeated)
				{
					GameplayTweaksPlugin.RemoveGangFromPactMembership(pact, memberId, "pact-member-prune");
					changed = true;
					continue;
				}
				if (member.IsHuman)
				{
					continue;
				}
				float happiness = pact.GetBossHappiness(memberId);
				if (happiness >= 0.45f)
				{
					continue;
				}
				float leaveChance = Mathf.Clamp01((0.45f - happiness) * 1.8f);
				if (SharedRng.NextDouble() < leaveChance)
				{
					GameplayTweaksPlugin.RemoveGangFromPactMembership(pact, memberId, "pact-member-defect");
					changed = true;
					string memberName = member.social?.PlayerGroupName ?? ("Gang#" + memberId);
					LogGrapevine($"PACT: {memberName} left {pact.DisplayName} and turned on {leaderName}.");
					if (leader != null && !leader.crew.IsCrewDefeated)
					{
						PactWarManager.ActivateWar(member, leader);
					}
				}
			}
			return changed;
		}

		private static List<Entity> GetHumanStorageEntities(PlayerInfo humanPlayer)
		{
			List<Entity> storage = new List<Entity>();
			if (humanPlayer == null || humanPlayer.territory == null)
			{
				return storage;
			}
			try
			{
				EntityID safehouseId = humanPlayer.territory.Safehouse;
				if (!safehouseId.IsNotValid)
				{
					Entity safehouse = EntityIDExtensions.FindEntity(safehouseId);
					if (safehouse != null)
					{
						storage.Add(safehouse);
					}
				}
				FieldInfo bldgField = humanPlayer.territory.GetType().GetField("_buildings", BindingFlags.Instance | BindingFlags.NonPublic)
					?? humanPlayer.territory.GetType().GetField("buildings", BindingFlags.Instance | BindingFlags.NonPublic);
				PropertyInfo bldgProp = humanPlayer.territory.GetType().GetProperty("OwnedBuildings", BindingFlags.Instance | BindingFlags.Public)
					?? humanPlayer.territory.GetType().GetProperty("Buildings", BindingFlags.Instance | BindingFlags.Public);
				object bldgList = bldgField?.GetValue(humanPlayer.territory) ?? bldgProp?.GetValue(humanPlayer.territory);
				if (bldgList is IEnumerable<EntityID> buildings)
				{
					foreach (EntityID bldgId in buildings)
					{
						if (bldgId.IsNotValid || bldgId == safehouseId)
						{
							continue;
						}
						Entity bldgEntity = EntityIDExtensions.FindEntity(bldgId);
						if (bldgEntity != null)
						{
							storage.Add(bldgEntity);
						}
					}
				}
			}
			catch
			{
			}
			return storage;
		}

		private static bool TryAddHumanDirtyCash(PlayerInfo humanPlayer, int amount)
		{
			if (humanPlayer == null || amount <= 0)
			{
				return false;
			}
			try
			{
				foreach (Entity storageEntity in GetHumanStorageEntities(humanPlayer))
				{
					int before = ReadInventoryAmount(storageEntity, ModConstants.DIRTY_CASH_LABEL);
					AddDirtyCash(storageEntity, amount, "human-pact-trade");
					int after = ReadInventoryAmount(storageEntity, ModConstants.DIRTY_CASH_LABEL);
					if (after >= before + amount)
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

		private static int TryTakeHumanPactContribution(PlayerInfo humanPlayer, int amount)
		{
			if (humanPlayer == null || amount <= 0)
			{
				return 0;
			}
			try
			{
				int removedDirty = 0;
				int dirtyRemaining = amount;
				foreach (Entity storageEntity in GetHumanStorageEntities(humanPlayer))
				{
					if (dirtyRemaining <= 0)
					{
						break;
					}
					int removedFromEntity = RemoveDirtyCash(storageEntity, dirtyRemaining);
					if (removedFromEntity > 0)
					{
						removedDirty += removedFromEntity;
						dirtyRemaining -= removedFromEntity;
					}
				}
				int remaining = Math.Max(0, amount - removedDirty);
				if (remaining <= 0)
				{
					return removedDirty;
				}
				try
				{
					humanPlayer.finances.DoChangeMoneyOnSafehouse(new Price((Fixnum)(-remaining)), (MoneyReason)1);
					return removedDirty + remaining;
				}
				catch
				{
					return removedDirty;
				}
			}
			catch
			{
			}
			return 0;
		}

		internal static void ProcessPactEarnings()
		{

			try
			{
				List<PlayerInfo> source = G.GetAllPlayers().ToList();
				PlayerInfo humanPlayer = G.GetHumanPlayer();
				int humanId = ((humanPlayer != null) ? humanPlayer.PID.id : (-1));
				foreach (AlliancePact pact in SaveData.Pacts)
				{
					if (!pact.IsActive) continue;
					if (pact.LeaderGangId < 0) continue;
					List<int> allMembers = new List<int>();
					if (pact.LeaderGangId >= 0) allMembers.Add(pact.LeaderGangId);
					allMembers.AddRange(pact.MemberIds);
					allMembers = allMembers.Distinct().ToList();
					if (SaveData.PlayerJoinedPactIndex == pact.ColorIndex && humanPlayer != null && !allMembers.Contains(humanId))
					{
						allMembers.Add(humanId);
					}
					if (allMembers.Count < 1) continue;
					int pooledFromMembers = 0;
					foreach (int gid in allMembers)
					{
						if (gid == pact.LeaderGangId)
						{
							continue;
						}
						PlayerInfo member = source.FirstOrDefault((PlayerInfo p) => p.PID.id == gid);
						if (member == null || member.crew.IsCrewDefeated) continue;
						int memberPower = CalculateGangPower(member);
						int payout = (int)((float)memberPower * GetEffectivePactEarningRate(pact) * 10f);
						if (payout <= 0) continue;
						int paidAmount = payout;
						if (member.PID.id == humanId)
						{
							paidAmount = TryTakeHumanPactContribution(humanPlayer, payout);
							if (paidAmount <= 0)
							try
							{
								member.finances.DoChangeMoneyOnSafehouse(new Price((Fixnum)(-payout)), (MoneyReason)1);
								paidAmount = payout;
							}
							catch (Exception ex)
							{
								Debug.LogWarning($"[GameplayTweaks] Pact member payment failed for human: {ex.Message}");
								paidAmount = 0;
							}
						}
						else
						{
							try
							{
								member.finances.DoChangeMoneyOnSafehouse(new Price((Fixnum)(-payout)), (MoneyReason)1);
								paidAmount = payout;
							}
							catch (Exception ex)
							{
								Debug.LogWarning($"[GameplayTweaks] Pact member payment failed for gang {gid}: {ex.Message}");
								paidAmount = 0;
							}
						}
						if (paidAmount > 0)
						{
							pooledFromMembers += paidAmount;
						}
					}
					if (pooledFromMembers > 0)
					{
						Debug.Log($"[GameplayTweaks] Pact upkeep collected ${pooledFromMembers} from non-leader members of {pact.DisplayName}");
					}
				}
			}
			catch (Exception arg)
			{
			Debug.LogError($"[GameplayTweaks] ProcessPactEarnings: {arg}");
			}
		}
	}

	internal static void CancelDeferredAlliancePactGangOpsForNextTurnInput(string source)
	{
		try
		{
			TurnUpdatePatch.CancelDeferredAlliancePactGangOpsForNextTurn(source ?? "next-turn-input");
		}
		catch
		{
		}
	}

	private static int TryGetObservedBoozeSoldCount(EntityID peepId)
	{
		if (peepId.IsNotValid)
		{
			return 0;
		}
		try
		{
			Entity entity = peepId.FindEntity();
			if (entity?.data?.agent?.crewHistoryStats == null)
			{
				return 0;
			}
			entity.data.agent.crewHistoryStats.TryGetValue((CrewStats)1, out int value);
			return Math.Max(0, value);
		}
		catch
		{
			return 0;
		}
	}

	internal static void ReconcileObservedBoozeStreetCredForCrew(PlayerInfo owner, Entity peep, CrewModState state, SimTime now)
	{
		if (owner?.crew == null || peep == null || !peep.Id.IsValid || state == null)
		{
			return;
		}

		int observedSoldCount = TryGetObservedBoozeSoldCount(peep.Id);
		int deliveredDelta = observedSoldCount - state.LastBoozeSoldCount;
		if (deliveredDelta < 0)
		{
			deliveredDelta = observedSoldCount;
		}
		if (deliveredDelta <= 0)
		{
			state.LastBoozeSoldCount = Math.Max(state.LastBoozeSoldCount, observedSoldCount);
			return;
		}

		CrewAssignment assignment = owner.crew.GetCrewForPeep(peep.Id);
		if (assignment.IsValid && assignment.IsInVehicle && assignment.VehicleID.IsValid)
		{
			HashSet<long> seenOccupants = new HashSet<long>();
			List<CrewAssignment> occupants = MultiCrewVehicleHelper.GetAllCrewInVehicle(owner.crew, assignment.VehicleID)
				.Where(item => item.IsValid
					&& item.peepId.IsValid
					&& seenOccupants.Add((long)item.peepId.id)
					&& MultiCrewVehicleHelper.IsActiveVehicleOccupant(owner.crew, item))
				.ToList();
			if (occupants.Count > 0)
			{
				foreach (CrewAssignment occupant in occupants)
				{
					CrewModState occupantState = GetOrCreateCrewState(occupant.peepId);
					if (occupantState == null)
					{
						continue;
					}
					ApplyBoozeSoldForStreetCred(occupantState, deliveredDelta, owner, occupant.peepId, now, applyHeat: occupant.peepId == peep.Id);
					occupantState.LastBoozeSoldCount = Math.Max(occupantState.LastBoozeSoldCount, observedSoldCount);
				}
				VerificationLog("StreetCredit", $"source=booze-vehicle peep={peep.Id.id} vehicle={assignment.VehicleID.id} sold={deliveredDelta} occupants={occupants.Count}");
				return;
			}
		}

		ApplyBoozeSoldForStreetCred(state, deliveredDelta, owner, peep.Id, now);
		state.LastBoozeSoldCount = observedSoldCount;
	}
}
}

