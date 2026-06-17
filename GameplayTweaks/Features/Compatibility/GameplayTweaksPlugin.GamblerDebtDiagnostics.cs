using System;
using System.Collections.Generic;
using System.Reflection;
using Game.Core;
using Game.Services;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Player;
using Game.Session.Sim;
using Game.UI.Session.Convo;
using HarmonyLib;
using SomaSim.Util;

namespace GameplayTweaks
{
	internal static class GamblerDebtDiagnosticsPatch
	{
		private sealed class DebtTransitionState
		{
			public GamblerState State;
			public EntityID GamblerId;
			public EntityID GamblingHouseId;
			public EntityID ResidenceId;
			public Label DebtId;
			public bool InitialDebt;
			public bool GamblerExists;
			public bool GamblingHouseExists;
			public bool ResidenceExists;
			public bool GamblerRelationshipListExists;
			public int FamilyRelationshipCount;
			public List<EntityID> MissingFamilyRelationshipLists = new List<EntityID>();
			public bool TickerExpected;
			public int Day;
		}

		public static void ApplyPatch(Harmony harmony)
		{
			MethodInfo target = AccessTools.Method(
				typeof(PlayerGambling),
				"MarkGamblerInDebt",
				new[] { typeof(GamblerState), typeof(DebtLevelDef) });
			if (target == null)
			{
				GameplayTweaksPlugin.VerificationLog(
					"GamblerDebt",
					"diagnostic-patch-skipped reason=mark-gambler-in-debt-not-found");
				return;
			}

			harmony.Patch(
				target,
				prefix: new HarmonyMethod(typeof(GamblerDebtDiagnosticsPatch), nameof(Prefix)),
				postfix: new HarmonyMethod(typeof(GamblerDebtDiagnosticsPatch), nameof(Postfix)),
				finalizer: new HarmonyMethod(typeof(GamblerDebtDiagnosticsPatch), nameof(Finalizer)));
			ApplyStaleDebtorConversationPatches(harmony);
			GameplayTweaksPlugin.VerificationLog(
				"GamblerDebt",
				"patches-applied debtTransition=recover-known-family-null staleConversation=guarded");
		}

		private static void ApplyStaleDebtorConversationPatches(Harmony harmony)
		{
			MethodInfo startDebtorVisit = AccessTools.Method(
				typeof(ConversationController),
				nameof(ConversationController.StartDebtorVisit),
				new[] { typeof(Entity), typeof(CrewAssignment) });
			MethodInfo makeReplacements = AccessTools.Method(
				typeof(ConvoDataGamblingDebtor),
				nameof(ConvoDataGamblingDebtor.MakeReplacements),
				new[] { typeof(VisitState), typeof(int) });
			MethodInfo isVisible = AccessTools.Method(
				typeof(ConvoButton),
				nameof(ConvoButton.IsVisible),
				new[] { typeof(VisitState) });
			MethodInfo isEnabled = AccessTools.Method(
				typeof(ConvoButton),
				nameof(ConvoButton.IsEnabled),
				new[] { typeof(VisitState) });

			if (startDebtorVisit != null)
			{
				harmony.Patch(
					startDebtorVisit,
					prefix: new HarmonyMethod(typeof(GamblerDebtDiagnosticsPatch), nameof(StartDebtorVisitPrefix)));
			}
			if (makeReplacements != null)
			{
				harmony.Patch(
					makeReplacements,
					prefix: new HarmonyMethod(typeof(GamblerDebtDiagnosticsPatch), nameof(MakeDebtorReplacementsPrefix)));
			}
			if (isVisible != null)
			{
				harmony.Patch(
					isVisible,
					prefix: new HarmonyMethod(typeof(GamblerDebtDiagnosticsPatch), nameof(DebtorButtonVisiblePrefix)));
			}
			if (isEnabled != null)
			{
				harmony.Patch(
					isEnabled,
					prefix: new HarmonyMethod(typeof(GamblerDebtDiagnosticsPatch), nameof(DebtorButtonEnabledPrefix)));
			}

			string[] debtorClickMethods =
			{
				"DoAttachRepayment",
				"DoExecuteGrantsOnRepayment",
				"DoCleanupGamblerOnFail",
				"ExecuteExtendCredit",
				"DoAttachPayCash",
				"ExecuteRepayCashLess",
				"ExecuteRepayCashFull"
			};
			for (int i = 0; i < debtorClickMethods.Length; i++)
			{
				MethodInfo callback = AccessTools.Method(
					typeof(ConvoCallbacks),
					debtorClickMethods[i],
					new[] { typeof(ConvoButton) });
				if (callback != null)
				{
					harmony.Patch(
						callback,
						prefix: new HarmonyMethod(typeof(GamblerDebtDiagnosticsPatch), nameof(StaleDebtorClickPrefix)));
				}
			}
		}

		private static bool StartDebtorVisitPrefix(Entity building)
		{
			Entity debtor = building?.components?.residence?.GetNpcResident();
			EntityID debtorId = debtor?.Id ?? EntityID.INVALID;
			if (TryGetValidDebtorState(debtorId, out _, out string reason))
			{
				return true;
			}

			LogStaleDebtorGuard("start-visit-blocked", debtorId, building?.Id ?? EntityID.INVALID, reason);
			global::Game.Game.ctx?.events?.SendImmediate(global::Game.Session.SessionEventType.UIDebtorChange);
			return false;
		}

		private static bool MakeDebtorReplacementsPrefix(
			ConvoDataGamblingDebtor __instance,
			VisitState visit,
			ref string[] __result)
		{
			if (TryGetValidDebtorState(__instance?.gamblerId ?? EntityID.INVALID, out _, out string reason))
			{
				return true;
			}

			EntityID gamblerId = __instance?.gamblerId ?? EntityID.INVALID;
			LogStaleDebtorGuard(
				"replacement-blocked",
				gamblerId,
				visit?.building?.Id ?? EntityID.INVALID,
				reason);
			__result = MakeUnavailableDebtorReplacements(visit, gamblerId);
			return false;
		}

		private static bool DebtorButtonVisiblePrefix(ConvoButton __instance, ref bool __result)
		{
			ConvoDataGamblingDebtor data = __instance?.state.data as ConvoDataGamblingDebtor;
			if (data == null || TryGetValidDebtorState(data.gamblerId, out _, out _))
			{
				return true;
			}

			__result = false;
			return false;
		}

		private static bool DebtorButtonEnabledPrefix(ConvoButton __instance, ref bool __result)
		{
			ConvoDataGamblingDebtor data = __instance?.state.data as ConvoDataGamblingDebtor;
			if (data == null || TryGetValidDebtorState(data.gamblerId, out _, out _))
			{
				return true;
			}

			__result = false;
			return false;
		}

		private static bool StaleDebtorClickPrefix(ConvoButton button, ref OnClickResult __result)
		{
			ConvoDataGamblingDebtor data = button?.GetData<ConvoDataGamblingDebtor>();
			string reason = "missing-convo-data";
			if (data != null && TryGetValidDebtorState(data.gamblerId, out _, out reason))
			{
				return true;
			}

			EntityID gamblerId = data?.gamblerId ?? EntityID.INVALID;
			LogStaleDebtorGuard("click-blocked", gamblerId, EntityID.INVALID, reason);
			global::Game.Game.ctx?.events?.SendImmediate(global::Game.Session.SessionEventType.UIDebtorChange);
			__result = OnClickResult.END_CONVERSATION;
			return false;
		}

		private static bool TryGetValidDebtorState(
			EntityID gamblerId,
			out GamblerState state,
			out string reason)
		{
			state = null;
			reason = "unknown";
			if (!gamblerId.IsValid)
			{
				reason = "invalid-gambler-id";
				return false;
			}

			Entity gambler = gamblerId.FindEntity();
			if (gambler?.data?.person == null)
			{
				reason = "missing-gambler-entity";
				return false;
			}

			PlayerGambling gambling = global::Game.Game.ctx?.players?.Human?.gambling;
			state = gambling?.FindGamblerState(gamblerId);
			if (state == null)
			{
				reason = "missing-gambler-state";
				return false;
			}
			if (!state.DebtIsDue)
			{
				reason = "debt-not-due";
				return false;
			}

			Entity gamblingHouse = state.gamblingHouseId.IsValid ? state.gamblingHouseId.FindEntity() : null;
			if (gamblingHouse?.data?.building == null ||
				gamblingHouse.components?.board == null ||
				gamblingHouse.components?.modules?.gambling == null)
			{
				reason = "invalid-gambling-house";
				return false;
			}

			Entity residence = state.gamblerResidence.IsValid ? state.gamblerResidence.FindEntity() : null;
			if (residence?.components?.residence == null)
			{
				reason = "invalid-debtor-residence";
				return false;
			}

			reason = "valid";
			return true;
		}

		private static string[] MakeUnavailableDebtorReplacements(VisitState visit, EntityID gamblerId)
		{
			Entity gambler = gamblerId.IsValid ? gamblerId.FindEntity() : null;
			Entity crewPeep = null;
			try
			{
				crewPeep = visit?.crew.GetPeep();
			}
			catch
			{
			}

			return new[]
			{
				"newcredit", string.Empty,
				"days", string.Empty,
				"date", string.Empty,
				"fullname", crewPeep?.data?.person?.FullName ?? string.Empty,
				"name", gambler?.data?.person?.FullName ?? string.Empty,
				"amount", string.Empty,
				"rollAmount", string.Empty
			};
		}

		private static void LogStaleDebtorGuard(
			string stage,
			EntityID gamblerId,
			EntityID buildingId,
			string reason)
		{
			GameplayTweaksPlugin.VerificationLog(
				"GamblerDebt",
				"stale-debtor-guard" +
				" stage=" + stage +
				" gambler=" + gamblerId +
				" building=" + buildingId +
				" reason=" + reason +
				" day=" + (global::Game.Game.ctx?.clock?.Now.days ?? int.MinValue));
		}

		private static void Prefix(
			PlayerGambling __instance,
			GamblerState state,
			DebtLevelDef debt,
			ref DebtTransitionState __state)
		{
			__state = CaptureState(__instance, state, debt);
		}

		private static void Postfix(DebtTransitionState __state)
		{
			LogTransition(__state, "completed", null, tickerReached: __state?.TickerExpected == true);
		}

		private static Exception Finalizer(Exception __exception, DebtTransitionState __state)
		{
			if (__exception == null)
			{
				return null;
			}

			if (TryRecoverKnownFamilyRelationshipNull(__exception, __state, out int inheritedConnections))
			{
				LogTransition(
					__state,
					"recovered",
					null,
					tickerReached: __state?.TickerExpected == true,
					recoveryDetail: "inheritedConnections=" + inheritedConnections);
				return null;
			}

			LogTransition(__state, "failed", __exception, tickerReached: false);
			return __exception;
		}

		private static bool TryRecoverKnownFamilyRelationshipNull(
			Exception exception,
			DebtTransitionState state,
			out int inheritedConnections)
		{
			inheritedConnections = 0;
			if (!(exception is NullReferenceException) ||
				state?.State == null ||
				!state.InitialDebt ||
				state.State.currentDebtDue != state.DebtId ||
				!state.State.gamblerResidence.IsValid ||
				(state.GamblerRelationshipListExists && state.MissingFamilyRelationshipLists.Count == 0))
			{
				return false;
			}

			try
			{
				RelationshipTracker relationships = global::Game.Game.ctx?.simman?.rels;
				RelationshipList gamblerRelationships = relationships?.GetListOrNull(state.GamblerId);
				HashSet<EntityID> inheritedTargets = new HashSet<EntityID>();
				if (gamblerRelationships?.data != null)
				{
					for (int i = 0; i < gamblerRelationships.data.Count; i++)
					{
						Relationship familyRelationship = gamblerRelationships.data[i];
						if (familyRelationship == null || !familyRelationship.IsAnyFamily)
						{
							continue;
						}

						RelationshipList familyRelationships = relationships.GetListOrNull(familyRelationship.to);
						if (familyRelationships?.data == null)
						{
							continue;
						}

						for (int j = 0; j < familyRelationships.data.Count; j++)
						{
							Relationship inheritedRelationship = familyRelationships.data[j];
							if (inheritedRelationship != null)
							{
								inheritedTargets.Add(inheritedRelationship.to);
							}
						}
					}
				}

				foreach (EntityID targetId in inheritedTargets)
				{
					if (targetId == state.GamblerId || relationships.HasAny(state.GamblerId, targetId))
					{
						continue;
					}

					relationships.GetOrMakeSymmetrical(
						state.GamblerId,
						targetId,
						RelationshipType.Acquaintance,
						warnOnExisting: true);
					inheritedConnections++;
				}

				Entity gambler = state.GamblerId.FindEntity();
				Entity gamblingHouse = state.GamblingHouseId.FindEntity();
				if (state.TickerExpected && gambler != null && gamblingHouse != null)
				{
					global::Game.Game.ctx.hud.tickers.AddTextTicker(
						TickerIcon.DEBTOR_NEW,
						TickerTitle.CASINO_UPDATE,
						Loc.Get(
							"ui.tickers.debtor-new",
							"name",
							gambler.data.person.FullName,
							"den",
							BuildingUtil.GetGamblingHouseName(gamblingHouse)),
						gambler.Id,
						TickerPersistType.DebtorPersist);
				}

				global::Game.Game.ctx.events.SendImmediate(global::Game.Session.SessionEventType.UIDebtorChange);
				return true;
			}
			catch (Exception recoveryException)
			{
				GameplayTweaksPlugin.VerificationLog(
					"GamblerDebt",
					"initial-debt-recovery-failed" +
					" gambler=" + state.GamblerId +
					" debt=" + state.DebtId +
					" originalException=" + FormatException(exception) +
					" recoveryException=" + FormatException(recoveryException));
				return false;
			}
		}

		private static DebtTransitionState CaptureState(
			PlayerGambling gambling,
			GamblerState state,
			DebtLevelDef debt)
		{
			DebtTransitionState snapshot = new DebtTransitionState
			{
				State = state,
				GamblerId = state?.gamblerId ?? EntityID.INVALID,
				GamblingHouseId = state?.gamblingHouseId ?? EntityID.INVALID,
				ResidenceId = state?.gamblerResidence ?? EntityID.INVALID,
				DebtId = debt?.id ?? Label.NULL,
				InitialDebt = debt?.initial ?? false,
				TickerExpected = gambling?.PID.IsHumanPlayer == true,
				Day = global::Game.Game.ctx?.clock?.Now.days ?? int.MinValue
			};

			Entity gambler = snapshot.GamblerId.IsValid ? snapshot.GamblerId.FindEntity() : null;
			Entity gamblingHouse = snapshot.GamblingHouseId.IsValid ? snapshot.GamblingHouseId.FindEntity() : null;
			Entity residence = snapshot.ResidenceId.IsValid ? snapshot.ResidenceId.FindEntity() : null;
			snapshot.GamblerExists = gambler != null;
			snapshot.GamblingHouseExists = gamblingHouse != null;
			snapshot.ResidenceExists = residence != null;

			RelationshipTracker relationships = global::Game.Game.ctx?.simman?.rels;
			RelationshipList gamblerRelationships = relationships?.GetListOrNull(snapshot.GamblerId);
			snapshot.GamblerRelationshipListExists = gamblerRelationships?.data != null;
			if (gamblerRelationships?.data == null)
			{
				return snapshot;
			}

			for (int i = 0; i < gamblerRelationships.data.Count; i++)
			{
				Relationship relationship = gamblerRelationships.data[i];
				if (relationship == null || !relationship.IsAnyFamily)
				{
					continue;
				}

				snapshot.FamilyRelationshipCount++;
				RelationshipList familyRelationships = relationships.GetListOrNull(relationship.to);
				if (familyRelationships?.data == null)
				{
					snapshot.MissingFamilyRelationshipLists.Add(relationship.to);
				}
			}

			return snapshot;
		}

		private static void LogTransition(
			DebtTransitionState state,
			string stage,
			Exception exception,
			bool tickerReached,
			string recoveryDetail = null)
		{
			if (state == null)
			{
				GameplayTweaksPlugin.VerificationLog(
					"GamblerDebt",
					"initial-debt-transition stage=" + stage +
					" statePresent=false" +
					" exception=" + FormatException(exception));
				return;
			}

			GameplayTweaksPlugin.VerificationLog(
				"GamblerDebt",
				"initial-debt-transition" +
				" stage=" + stage +
				" gambler=" + state.GamblerId +
				" gamblerExists=" + state.GamblerExists +
				" gamblingHouse=" + state.GamblingHouseId +
				" gamblingHouseExists=" + state.GamblingHouseExists +
				" residenceBefore=" + state.ResidenceId +
				" residenceExistsBefore=" + state.ResidenceExists +
				" residenceAfter=" + (state.State?.gamblerResidence ?? EntityID.INVALID) +
				" residenceExistsAfter=" + ResidenceExists(state.State?.gamblerResidence ?? EntityID.INVALID) +
				" debt=" + state.DebtId +
				" currentDebtAfter=" + (state.State?.currentDebtDue ?? Label.NULL) +
				" initial=" + state.InitialDebt +
				" gamblerRelationshipList=" + state.GamblerRelationshipListExists +
				" familyRelationships=" + state.FamilyRelationshipCount +
				" missingFamilyRelationshipLists=" + FormatEntityIds(state.MissingFamilyRelationshipLists) +
				" tickerExpected=" + state.TickerExpected +
				" tickerReached=" + tickerReached +
				" recovery=" + (string.IsNullOrEmpty(recoveryDetail) ? "none" : recoveryDetail) +
				" exception=" + FormatException(exception) +
				" day=" + state.Day);
		}

		private static bool ResidenceExists(EntityID residenceId)
		{
			return residenceId.IsValid && residenceId.FindEntity() != null;
		}

		private static string FormatEntityIds(List<EntityID> ids)
		{
			if (ids == null || ids.Count == 0)
			{
				return "none";
			}

			string[] values = new string[ids.Count];
			for (int i = 0; i < ids.Count; i++)
			{
				values[i] = ids[i].ToString();
			}

			return string.Join(",", values);
		}

		private static string FormatException(Exception exception)
		{
			return exception == null
				? "none"
				: exception.GetType().Name + ":" + exception.Message;
		}
	}
}
