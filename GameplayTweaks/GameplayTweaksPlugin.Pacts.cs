using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Game.Core;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Player;
using Game.Session.Sim;
using Game.Session.Sim.Modules;
using HarmonyLib;
using SomaSim.Util;
using UnityEngine;
namespace GameplayTweaks
{
public partial class GameplayTweaksPlugin
{

	private static class TurnUpdatePatch
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

		internal static void ResetRuntime()
		{
			_lastKnownBossPeepByGang.Clear();
			_lastBossTrackDay = -1;
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
				ProcessHumanHideoutPrepass(humanPlayer, now);
				ProcessNationalHeatTurn(humanPlayer, now);
				ReconcileAllCrewJailStates("turn");
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
						ProcessCrewMemberTurn(peep, now, humanPlayer);
					}
				}
				ProcessSnitchCaseTurn(humanPlayer, now);
				foreach (PlayerInfo gang in G.GetAllPlayers())
				{
					if (gang == null || !gang.IsJustGang || gang.crew == null || gang.crew.IsCrewDefeated)
					{
						continue;
					}
					if (gang.PID.IsHumanPlayer)
					{
						continue;
					}
					foreach (CrewAssignment assignment in gang.crew.GetLiving().ToList())
					{
						Entity aiPeep = assignment.GetPeep();
						if (aiPeep != null)
						{
							ProcessCrewMemberTurn(aiPeep, now, gang);
						}
					}
				}
				ReconcileRaidedGangSafehouseTerritory("pact-turn");
				EnsureHumanSafehouseTerritoryColorOwner("pact-turn", refreshColors: false);
				ReconcileLowRespectTerritoryOwnership("pact-turn");
				int days = now.days;
				if (days != _lastGangTrackDay)
				{
					_lastGangTrackDay = days;
					RefreshGangTracker();
					ReconcilePersistentGangRelationshipBuffs("pact-turn-day");
				}
				if (EnableAIAlliances.Value)
				{
					ProcessAIAlliances(now);
					ProcessPactVotes(now);
					ProcessAIInterPactAlliances(now);
					ProcessInterPactAllianceVotes(now);
					ShareLeaderEnemiesAcrossInterPactAlliances();
					ProcessPactEarnings();
					EnforcePactPeace();
					CheckPlayerPactWarKick(now);
				}
				if (_lastPactOpsTurnDay != now.days)
				{
					_lastPactOpsTurnDay = now.days;
					RunGangOpsTurn(GangOpsChannel.Pact, humanPlayer, now);
					RunGangOpsTurn(GangOpsChannel.Independent, humanPlayer, now);
				}
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
				FlushCrewPickAggroRefreshes("turn-reconcile");
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
					try
					{
						SaveData.AiGangLastSnitchMeetingDayByGang[gang.PID.id] = days2;
						RunAIGangSnitchMeeting(gang, now);
						RunAiCrewRelationsDecisions(gang, now);
						RunRelationshipDrivenInternalCrewEvents(gang, now);
					}
					catch (Exception ex4)
					{
						Debug.LogError($"[GameplayTweaks] AI gang snitch meeting failed for gang={gang.PID.id}: {ex4}");
					}
				}
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
				PactColorUiPatch.RequestFullCrewPickRefresh("human-turn", 5);
				SaveModData();
			}
			catch (Exception arg4)
			{
				Debug.LogError($"[GameplayTweaks] OnTurnPostfix: {arg4}");
			}
		}

		// Tunable: AI crew-relations decisions (bribes / meetings)
		private const int AI_LEGAL_ACTION_COOLDOWN_DAYS = 14;  // Min days between expensive legal actions (mayor/judge) per gang
		private const int AI_MIN_CASH_RESERVE = 2000;         // Do not spend below this (clean+dirty) for bribes/meetings
		private const float AI_MORALE_MEETING_HAPPINESS_THRESHOLD = 0.45f;  // Run morale meeting when avg crew happiness below this

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
		private const int AI_CREW_RELATIONS_MAX_SUPPORT_TARGETS = 3;
		private const int AI_CREW_RELATIONS_MINOR_RETAINER = 500;
		private const int AI_CREW_RELATIONS_UNDERBOSS_MIN_CREW = 3;
		private const int AI_CREW_RELATIONS_SNITCH_DISAPPEAR_MIN_LEAKS = 2;
		private const float AI_CREW_RELATIONS_CRITICAL_LOYALTY_RATIO = 0.35f;
		private const float AI_CREW_RELATIONS_MORALE_MEETING_LOYALTY_THRESHOLD = 0.55f;
		private const int AI_PACT_TRADE_INTERVAL_DAYS = 21;
		private const int AI_NETWORK_TRADE_INTERVAL_DAYS = 42;
		private const int AI_NETWORK_TRADE_MAX_SUCCESS_COUNT = 3;
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
		private const int AI_ROBBERY_LOW_CASH_MAX = 900;
		private const int AI_ROBBERY_HIGH_CASH_MIN = 900;
		private const int AI_ROBBERY_HIGH_CASH_MAX = 1800;
		private const float AI_ROBBERY_PLAYER_TARGET_CHANCE = 0.12f;
		private static readonly string[] AI_PACT_TRADE_LIQUOR_LABELS = new string[8] { "home-brew", "moonshine", "cider", "brick-wine", "fake-beer", "fake-wine", "bathtub-gin", "sparkling-cider" };
		private static readonly string[] AI_PACT_TRADE_DRUG_LABELS = new string[7] { "cannabis", "cannabis-joints", "heroin", "opium", "opiumlow", "cocaine", "cocainelow" };

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
			VerificationLog("AiCrewRelations", $"summary gang={gang.PID.id} avgHappy={avgHappiness:0.000} avgLoyaltyRatio={avgLoyaltyRatio:0.000} actions={relationActions} cash={totalCash}");
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

		private static void ProcessCrewMemberTurn(Entity peep, SimTime now, PlayerInfo player)
		{

			if (!EnableCrewStats.Value)
			{
				return;
			}
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
			bool nationalHeatActiveForCrew = SaveData.NationalHeat != null && SaveData.NationalHeat.Active && CountActiveImportantWitnessEntries() > 0;
			if (nationalHeatActiveForCrew && !JailSystem.IsInJail(peep.Id))
			{
				RaiseLocalHeatFloor(peep.Id, LOCAL_HEAT_LOW_FLOOR, refreshDecayAnchor: true);
			}
			bool hasActiveImportantWitness = HasActiveImportantWitnessForCrew(peep.Id);
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
			if (orCreateCrewState.StreetCreditLevel < 2)
			{
				float loyaltyMul = GetLoyaltyHappinessPenaltyMultiplier(orCreateCrewState);
				orCreateCrewState.HappinessValue = Mathf.Clamp01(orCreateCrewState.HappinessValue - 0.03f * loyaltyMul);
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
							}
							Debug.Log($"[GameplayTweaks] {fullName} defected to {targetName} - loyalty hit zero!");
							VerificationLog("Loyalty", $"Zero-loyalty defection peep={peep.Id.id} fromGang={player.PID.id} toGang={target.PID.id}");
						}
						else
						{
							if (player.PID.IsHumanPlayer)
							{
								CrewRelationshipHandlerPatch.ShowCrewDepartureAlert(peep, null);
							}
							Debug.Log($"[GameplayTweaks] {fullName} left the crew (couldn't join another gang)");
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
						}
						Debug.Log($"[GameplayTweaks] {fullName} left the crew (no valid defection target)");
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
				List<PlayerInfo> source = G.GetAllPlayers().ToList();
				List<PlayerInfo> list = source.Where(delegate(PlayerInfo p)
				{

					if (p.IsJustGang && !p.crew.IsCrewDefeated)
					{
						PlayerID pID = p.PID;
						return !pID.IsHumanPlayer;
					}
					return false;
				}).ToList();
				List<AlliancePact> list2 = new List<AlliancePact>();
				Dictionary<AlliancePact, string> removalReasons = new Dictionary<AlliancePact, string>();
				int unlockedAIPactSlots = GetUnlockedAIPactSlots(now);
				Dictionary<int, ulong> currentBossByGang = new Dictionary<int, ulong>();
				foreach (PlayerInfo p in source)
				{
					if (p != null && p.IsJustGang && !p.crew.IsCrewDefeated)
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
						PlayerInfo memberP = source.FirstOrDefault(p => p.PID.id == mid);
						if (memberP == null || memberP.crew.IsCrewDefeated)
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
						PlayerInfo killedMember = source.FirstOrDefault(p => p.PID.id == killedMemberId);
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
						PlayerInfo bossKilledGang = source.FirstOrDefault(p => p.PID.id == bossKilledGangId);
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
						Power = CalculateGangPower(g)
					} into x
					orderby x.Power descending
					select x).ToList();
				int num = SaveData.Pacts.Count((AlliancePact p) => p.ColorIndex < AI_PACT_SLOT_COUNT);
				int maxAIPacts = GetUnlockedAIPactSlots(now);
				if (now.days % 30 == 0 && num < maxAIPacts)
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
								Score = CalculateAIPactFoundingScore(x.Gang)
							})
							.Where(x => x.Score > 0f)
							.OrderByDescending(x => x.Score)
							.Select(x => x.Entry)
							.FirstOrDefault();
						if (leader != null)
						{
							int leaderId = leader.Gang.PID.id;
							int leaderTerritory = GetGangTerritoryCount(leader.Gang);
							int leaderSc = GetGangStreetCredLevel(leader.Gang);
							float leaderScore = CalculateAIPactFoundingScore(leader.Gang);
							var partnerCandidates = list4
								.Where(x => x.Gang.PID.id != leader.Gang.PID.id && x.Power > leader.Power / 3)
								.ToList();
							var anon = partnerCandidates
								.Select(x => new
								{
									Entry = x,
									Score = CalculateAIPactPartnerScore(leader.Gang, x.Gang)
								})
								.OrderByDescending(x => x.Score)
								.Select(x => x.Entry)
								.FirstOrDefault();
							if (anon != null)
							{
								int partnerId = anon.Gang.PID.id;
								int partnerTerritory = GetGangTerritoryCount(anon.Gang);
								int partnerSc = GetGangStreetCredLevel(anon.Gang);
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
					return;
				}
				AiTradeProposal aiTradeProposal = null;
				foreach (AiTradeActor item in list)
				{
					foreach (AiTradeActor item2 in list)
					{
						if (item == null || item2 == null || string.Equals(item.ActorKey, item2.ActorKey, StringComparison.Ordinal))
						{
							continue;
						}
						AiTradeProposal aiTradeProposal2 = EvaluateAiTradeProposal(item, item2, isInternalPactTrade: true);
						if (aiTradeProposal2 != null && (aiTradeProposal == null || aiTradeProposal2.Score > aiTradeProposal.Score))
						{
							aiTradeProposal = aiTradeProposal2;
						}
					}
				}
				if (aiTradeProposal == null || aiTradeProposal.Score < 0.48f || SharedRng.NextDouble() > aiTradeProposal.Score)
				{
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
					return;
				}
				List<AiTradeProposal> list2 = new List<AiTradeProposal>();
				foreach (AiTradeActor item in list)
				{
					foreach (AiTradeActor item2 in list)
					{
						if (ShouldSkipExternalTradePair(item, item2))
						{
							continue;
						}
						AiTradeProposal aiTradeProposal = EvaluateAiTradeProposal(item, item2, isInternalPactTrade: false);
						if (aiTradeProposal != null && aiTradeProposal.Score >= 0.5f)
						{
							list2.Add(aiTradeProposal);
						}
					}
				}
				if (list2.Count == 0)
				{
					TryRunRareAiRobberyAgainstHuman(humanPlayer, now);
					return;
				}
				HashSet<string> hashSet = new HashSet<string>(StringComparer.Ordinal);
				int num = 0;
				foreach (AiTradeProposal item3 in list2.OrderByDescending(p => p.Score).ToList())
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
				TryRunRareAiRobberyAgainstHuman(humanPlayer, now);
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] TryRunAiExternalTradeCycle failed: " + ex.Message);
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
					bool alreadyAggro = IsAggroWithoutTruceEitherWay(item, item2);
					if (!isInternalPactTrade && !ArePlayersProtectedByPactAlliance(item, item2) && !HasMutualTruce(item, item2))
					{
						int victimCleanCash = GetGangCleanCash(item2);
						bool robberTraits = flag7 || HasAnyGangBossTrait(item, "trait-vindictive", "trait-bold", "trait-cruel");
						int powerEdge = CalculateGangPower(item) - CalculateGangPower(item2);
						if (victimCleanCash >= AI_ROBBERY_LOW_CASH_MIN + AI_ROBBERY_MIN_CASH_RESERVE
							&& interGangRelationshipDisplayScore <= -18
							&& (robberTraits || alreadyAggro || powerEdge > -25))
						{
							float robberyScore = 0.42f
								+ Mathf.Clamp01((-interGangRelationshipDisplayScore - 15) / 80f) * 0.22f
								+ Mathf.Clamp(powerEdge / 300f, -0.08f, 0.16f)
								+ (robberTraits ? 0.08f : 0f)
								+ (alreadyAggro ? 0.06f : 0f)
								- (flag5 ? 0.04f : 0f)
								- (flag6 ? 0.03f : 0f);
							consider(victimCleanCash >= AI_ROBBERY_HIGH_CASH_MIN + AI_ROBBERY_MIN_CASH_RESERVE && (robberTraits || powerEdge > 35) ? "gang-robbery-high" : "gang-robbery-low", item, item2, robberyScore);
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
		AddMutualRelationshipBuff(seller, buyer, "relbuff-gangs-leisure-table-on-finish", GetCrewPeepForPlayer(seller), GetCrewPeepForPlayer(buyer));
		AddMutualRelationshipBuff(seller, buyer, "relbuff-gangs-leisure-buff", GetCrewPeepForPlayer(seller), GetCrewPeepForPlayer(buyer));
		ApplyPactTradeRelationshipBuff(seller, buyer);
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
		AddMutualRelationshipBuff(seller, buyer, "relbuff-gangs-loot5-table-on-finish", GetCrewPeepForPlayer(seller), GetCrewPeepForPlayer(buyer));
		ApplyPactTradeRelationshipBuff(seller, buyer);
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
		AddMutualRelationshipBuff(seller, buyer, finishBuffId, GetCrewPeepForPlayer(seller), GetCrewPeepForPlayer(buyer));
		AddMutualRelationshipBuff(seller, buyer, flavorBuffId, GetCrewPeepForPlayer(seller), GetCrewPeepForPlayer(buyer));
		ApplyPactTradeRelationshipBuff(seller, buyer);
		ApplyAiTradeMoodDelta(proposal, pact, moodDelta);
		string text = string.Equals(tradeKey, "gang-trade-drugs-low", StringComparison.Ordinal) ? "drug" : "liquor";
		AwardHumanPactTradeStreetCredit(seller, buyer, string.Equals(tradeKey, "gang-trade-drugs-low", StringComparison.Ordinal) ? "pact-trade-drugs" : "pact-trade-liquor", 0.03f, 0.06f);
		LogGrapevine($"{GetAiTradeGrapevinePrefix(externalNetwork)}: {GetAiTradeActorDisplayName(proposal.BuyerActor, buyer)} stocked up on {text} from {GetAiTradeActorDisplayName(proposal.SellerActor, seller)}.");
		VerificationLog(verificationChannel, $"type={tradeKey} day={now.days} sellerActor={proposal.SellerActor?.ActorKey} buyerActor={proposal.BuyerActor?.ActorKey} sellerGang={seller.PID.id} buyerGang={buyer.PID.id} clean={cleanCost} moved={moved} summary={movedSummary}");
		return true;
	}

	private static bool TryExecuteAiGangRobbery(AiTradeProposal proposal, SimTime now, string verificationChannel, bool externalNetwork)
	{
		PlayerInfo robber = proposal?.Seller;
		PlayerInfo victim = proposal?.Buyer;
		if (robber == null || victim == null || robber.PID.id == victim.PID.id || robber.PID.IsHumanPlayer)
		{
			return false;
		}
		if (ArePlayersProtectedByPactAlliance(robber, victim) || HasMutualTruce(robber, victim))
		{
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
		float heatGain = success ? (highValue ? 8f : 5f) : (highValue ? 18f : 12f);
		string robberName = GetGangDisplayName(robber.PID.id);
		string victimName = victim.PID.IsHumanPlayer ? "your outfit" : GetGangDisplayName(victim.PID.id);

		if (success)
		{
			if (!TryTransferCleanCashBetweenGangs(victim, robber, cash, victim.PID.IsHumanPlayer ? 0 : AI_ROBBERY_MIN_CASH_RESERVE))
			{
				return false;
			}
			AddDirectedRelationshipBuff(victim, robber, "relbuff-gangs-robbery2-table-on-finish", GetCrewPeepForPlayer(victim));
			AddWarHeat(channel, victim.PID.id, robber.PID.id, heatGain, victim.PID.IsHumanPlayer ? "ai-robbery-player-success" : "ai-robbery-success");
			LogGrapevine($"{(externalNetwork ? "ROBBERY" : "PACT")}: {robberName} robbed {victimName} for ${cash}. {victimName} is angry, but no shots were fired.");
			VerificationLog(verificationChannel, $"type={proposal.TradeKey} result=success day={now.days} robber={robber.PID.id} victim={victim.PID.id} cash={cash} chance={successChance:0.00} heat={heatGain:0.0}");
			return true;
		}

		AddDirectedRelationshipBuff(victim, robber, "relbuff-gangs-robbery1-buff", GetCrewPeepForPlayer(victim));
		AddWarHeat(channel, victim.PID.id, robber.PID.id, heatGain, victim.PID.IsHumanPlayer ? "ai-robbery-player-failed" : "ai-robbery-failed");
		ActivateWarBetweenPlayers(victim, robber);
		bool dispatched = false;
		int dispatchedCount = 0;
		if (victim.PID.IsHumanPlayer)
		{
			dispatched = TryDispatchRuntimeGangAttack(robber, victim, highValue ? 2 : 1, "ai-robbery-player-failed", GetCrewPeepForPlayer(victim), out dispatchedCount);
		}
		else
		{
			dispatched = TryDispatchRuntimeGangAttack(victim, robber, highValue ? 2 : 1, "ai-robbery-failed", GetCrewPeepForPlayer(robber), out dispatchedCount);
		}
		string attackText = victim.PID.IsHumanPlayer ? $"{robberName} started shooting when the job went bad." : $"{victimName} struck back.";
		LogGrapevine($"{(externalNetwork ? "ROBBERY" : "PACT")}: {robberName} tried to rob {victimName}, but the job failed. {attackText}");
		VerificationLog(verificationChannel, $"type={proposal.TradeKey} result=failed day={now.days} robber={robber.PID.id} victim={victim.PID.id} chance={successChance:0.00} heat={heatGain:0.0} attack={dispatched} crews={dispatchedCount}");
		return true;
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

	private static void TryRunRareAiRobberyAgainstHuman(PlayerInfo humanPlayer, SimTime now)
	{
		try
		{
			if (humanPlayer == null || humanPlayer.crew == null || humanPlayer.crew.IsCrewDefeated || GetGangCleanCash(humanPlayer) < AI_ROBBERY_LOW_CASH_MIN)
			{
				return;
			}
			if (SharedRng.NextDouble() >= AI_ROBBERY_PLAYER_TARGET_CHANCE)
			{
				return;
			}
			List<PlayerInfo> candidates = G.GetAllPlayers()
				.Where(gang => IsEligibleAiTradeGang(gang)
					&& !ArePlayersProtectedByPactAlliance(gang, humanPlayer)
					&& !HasMutualTruce(gang, humanPlayer))
				.ToList();
			if (candidates.Count == 0)
			{
				return;
			}
			PlayerInfo robber = candidates
				.OrderByDescending(gang =>
				{
					int relationship = GetInterGangRelationshipDisplayScore(gang, humanPlayer);
					int powerEdge = CalculateGangPower(gang) - CalculateGangPower(humanPlayer);
					int trait = HasAnyGangBossTrait(gang, "trait-aggressive", "trait-vindictive", "trait-bold", "trait-cruel") ? 20 : 0;
					return -relationship + powerEdge / 3 + trait + SharedRng.Next(0, 12);
				})
				.FirstOrDefault();
			if (robber == null)
			{
				return;
			}
			int rel = GetInterGangRelationshipDisplayScore(robber, humanPlayer);
			if (rel > -10 && !IsAggroWithoutTruceEitherWay(robber, humanPlayer) && SharedRng.NextDouble() < 0.65)
			{
				return;
			}
			bool highValue = GetGangCleanCash(humanPlayer) >= AI_ROBBERY_HIGH_CASH_MIN + AI_ROBBERY_MIN_CASH_RESERVE
				&& (HasAnyGangBossTrait(robber, "trait-aggressive", "trait-vindictive", "trait-bold") || CalculateGangPower(robber) > CalculateGangPower(humanPlayer));
			AiTradeProposal proposal = new AiTradeProposal
			{
				TradeKey = highValue ? "gang-robbery-high" : "gang-robbery-low",
				SellerActor = CreateGangTradeActor(robber),
				BuyerActor = new AiTradeActor
				{
					Gang = humanPlayer,
					ActorKey = "human:" + humanPlayer.PID.id
				},
				Seller = robber,
				Buyer = humanPlayer,
				Score = 0.5f
			};
			TryExecuteAiGangRobbery(proposal, now, "AIPlayerRobbery", externalNetwork: true);
		}
		catch (Exception ex)
		{
			Debug.LogWarning("[GameplayTweaks] TryRunRareAiRobberyAgainstHuman failed: " + ex.Message);
		}
	}

	private static void ApplyPactTradeRelationshipBuff(PlayerInfo seller, PlayerInfo buyer)
	{
		if (seller == null || buyer == null)
		{
			return;
		}

		EnsureCustomRelationshipBuffDefinitions();
		AddMutualRelationshipBuff(seller, buyer, "relbuff-pact-trade", GetCrewPeepForPlayer(seller), GetCrewPeepForPlayer(buyer));
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
			AddDirtyCash(gangSafehouseEntity, amount);
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
					AddDirtyCash(storageEntity, amount);
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

