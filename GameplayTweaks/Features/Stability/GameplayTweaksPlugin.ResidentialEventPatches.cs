using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Game.Core;
using Game.Services;
using Game.Session.Board;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Player;
using Game.Session.Sim;
using Game.UI.Session;
using Game.UI.Session.Convo;
using HarmonyLib;
using UnityEngine;

namespace GameplayTweaks
{
public partial class GameplayTweaksPlugin
{
	internal static class ResidentialEventStabilityPatch
	{
		private static readonly Dictionary<ulong, int> LastPickerLogTurnByOwner = new Dictionary<ulong, int>();
		private static readonly Dictionary<ulong, int> LastCandidateLogTurnByOwner = new Dictionary<ulong, int>();
		private static readonly Dictionary<string, int> LastCandidateTickerTurnByKey = new Dictionary<string, int>();
		private static readonly Dictionary<string, int> LastTickerTurnByBuildingState = new Dictionary<string, int>();
		private static int LastTickerScanLogTurn = int.MinValue;
		private static readonly bool EnableResidentialEventRepairTickers = false;

		public static void ApplyPatch(Harmony harmony)
		{
			try
			{
				int patched = 0;
				MethodInfo picker = AccessTools.Method(typeof(SpecialResEventCanBePicked), nameof(SpecialResEventCanBePicked.DoesPass));
				if (picker != null)
				{
					harmony.Patch(picker, postfix: new HarmonyMethod(typeof(ResidentialEventStabilityPatch), nameof(CanBePickedPostfix)));
					patched++;
				}

				MethodInfo candidatePicker = AccessTools.Method(typeof(ResEventManager), nameof(ResEventManager.FindPossibleResEventToCreate));
				if (candidatePicker != null)
				{
					harmony.Patch(candidatePicker, postfix: new HarmonyMethod(typeof(ResidentialEventStabilityPatch), nameof(FindPossibleResEventToCreatePostfix)));
					patched++;
				}

				patched += PatchLifecycleDiagnostics(harmony);

				patched += PatchResultGenerator(harmony, typeof(ResEventResultIntro));
				patched += PatchResultGenerator(harmony, typeof(ResEventResultQuest));
				patched += PatchResultGenerator(harmony, typeof(ResEventResultRelbuff));

				MethodInfo blurb = AccessTools.Method(typeof(SpecialResEventResultBlurb), nameof(SpecialResEventResultBlurb.GetBlurb));
				if (blurb != null)
				{
					harmony.Patch(blurb, finalizer: new HarmonyMethod(typeof(ResidentialEventStabilityPatch), nameof(ResultBlurbFinalizer)));
					patched++;
				}

				VerificationLog("ResEvents", $"stability diagnostics applied patched={patched}");
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] ResidentialEventStabilityPatch failed: " + ex.Message);
			}
		}

		private static int PatchResultGenerator(Harmony harmony, Type type)
		{
			MethodInfo method = AccessTools.Method(type, nameof(ResidentialEventResultConfig.GenerateResultData), new[] { typeof(VisitState) });
			if (method == null)
			{
				Debug.LogWarning("[GameplayTweaks] ResEvents result generator missing: " + type.FullName);
				return 0;
			}

			harmony.Patch(method, finalizer: new HarmonyMethod(typeof(ResidentialEventStabilityPatch), nameof(GenerateResultDataFinalizer)));
			return 1;
		}

		private static int PatchLifecycleDiagnostics(Harmony harmony)
		{
			int patched = 0;
			patched += PatchResEventManagerMethod(harmony, nameof(ResEventManager.OnSystemTurn), Type.EmptyTypes, nameof(OnSystemTurnPostfix));
			patched += PatchResEventManagerMethod(harmony, nameof(ResEventManager.AssignResEventHost), new[] { typeof(Label), typeof(Entity), typeof(Entity), typeof(PlayerID) }, nameof(AssignResEventHostPostfix));
			patched += PatchResEventManagerMethod(harmony, nameof(ResEventManager.SetAttendenceRegistered), new[] { typeof(VisitState) }, nameof(SetAttendanceRegisteredPostfix));
			patched += PatchResEventManagerMethod(harmony, nameof(ResEventManager.SetAttendanceDone), new[] { typeof(VisitState) }, nameof(SetAttendanceDonePostfix));
			patched += PatchResEventManagerMethod(harmony, nameof(ResEventManager.ProcessChosenResult), new[] { typeof(VisitState) }, nameof(ProcessChosenResultPostfix));

			MethodInfo playerTurnStarted = AccessTools.Method(typeof(PlayerInfo), nameof(PlayerInfo.OnPlayerTurnStarted));
			if (playerTurnStarted != null)
			{
				harmony.Patch(playerTurnStarted, postfix: new HarmonyMethod(typeof(ResidentialEventStabilityPatch), nameof(PlayerInfoOnPlayerTurnStartedPostfix)));
				patched++;
			}
			else
			{
				Debug.LogWarning("[GameplayTweaks] ResEvents lifecycle method missing: PlayerInfo.OnPlayerTurnStarted");
			}

			return patched;
		}

		private static int PatchResEventManagerMethod(Harmony harmony, string methodName, Type[] args, string postfixName)
		{
			MethodInfo method = AccessTools.Method(typeof(ResEventManager), methodName, args);
			if (method == null)
			{
				Debug.LogWarning("[GameplayTweaks] ResEvents lifecycle method missing: " + methodName);
				return 0;
			}

			harmony.Patch(method, postfix: new HarmonyMethod(typeof(ResidentialEventStabilityPatch), postfixName));
			return 1;
		}

		private static void FindPossibleResEventToCreatePostfix(PlayerID pid, Entity introducer, ref ResEventCandidate? __result)
		{
			try
			{
				if (introducer == null)
				{
					return;
				}

				ulong ownerId = introducer.Id.id;
				int turn = global::Game.Game.ctx?.clock?.CurrentTurn ?? -1;
				if (LastCandidateLogTurnByOwner.TryGetValue(ownerId, out int lastTurn) && lastTurn == turn)
				{
					return;
				}

				LastCandidateLogTurnByOwner[ownerId] = turn;
				if (__result.HasValue)
				{
					ResEventCandidate candidate = __result.Value;
					Entity host = candidate.hostNpc.FindEntity();
					Entity building = candidate.building.FindEntity();
					bool tickerPosted = TryPostResidentialCandidateTicker(introducer, host, building, candidate, "candidate-picker");
					VerificationLog("ResEvents", $"candidate-available pid={pid} introducer={DescribeEntity(introducer)} host={DescribeEntity(host)} building={DescribeBuilding(building)} event={candidate.eventId} turn={turn} tickerPosted={tickerPosted}");
					return;
				}

				string reason = DiagnoseNoCandidate(pid, introducer);
				VerificationLog("ResEvents", $"candidate-missing pid={pid} introducer={DescribeEntity(introducer)} turn={turn} reason={reason}");
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] ResEvents candidate diagnostics failed: " + ex.Message);
			}
		}

		private static void OnSystemTurnPostfix()
		{
			try
			{
				PostDueResidentialEventTickers("system-turn");
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] ResEvents turn ticker diagnostics failed: " + ex.Message);
			}
		}

		private static void PlayerInfoOnPlayerTurnStartedPostfix(PlayerInfo __instance)
		{
			if (__instance == null || !__instance.IsHuman)
			{
				return;
			}

			long totalTicks = GameplayTweaksPlugin.StartPerfTimer();
			try
			{
				PostDueResidentialEventTickers("human-turn-start");
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] ResEvents human turn ticker diagnostics failed: " + ex.Message);
			}
			finally
			{
				GameplayTweaksPlugin.LogHumanTurnStartPhase("resevents:post-due-tickers", totalTicks);
			}
		}

		private static void AssignResEventHostPostfix(Label eventId, Entity peep, Entity building, PlayerID pid)
		{
			try
			{
				ResEventData data = building?.components?.residence?.GetResEventOrNull();
				bool scheduleStarted = EnsureResidentialEventScheduled(building, data, pid, "host-assigned");
				bool tickerPosted = TryPostResidentialEventTicker(building, data, start: false, "host-assigned");
				VerificationLog("ResEvents", $"host-assigned pid={pid} event={eventId} host={DescribeEntity(peep)} building={DescribeBuilding(building)} data={DescribeResEventData(data)} scheduleStarted={scheduleStarted} tickerPosted={tickerPosted}");
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] ResEvents host assignment diagnostics failed: " + ex.Message);
			}
		}

		private static void SetAttendanceRegisteredPostfix(VisitState visit, ResEventData __result)
		{
			try
			{
				bool started = EnsureResidentialEventScheduled(visit?.building, __result, PlayerID.HumanPlayer, "attendance-registered");
				bool ticker = TryPostResidentialEventTicker(visit?.building, __result, __result?.IsThisTurn == true, "attendance-registered");
				VerificationLog("ResEvents", $"attendance-registered crew={DescribeCrew(visit?.crew)} visitor={DescribeEntity(visit?.npc)} building={DescribeBuilding(visit?.building)} data={DescribeResEventData(__result)} scheduleStarted={started} tickerPosted={ticker}");
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] ResEvents attendance registration diagnostics failed: " + ex.Message);
			}
		}

		private static void SetAttendanceDonePostfix(VisitState visit, ResEventData __result)
		{
			try
			{
				VerificationLog("ResEvents", $"attendance-done crew={DescribeCrew(visit?.crew)} visitor={DescribeEntity(visit?.npc)} building={DescribeBuilding(visit?.building)} data={DescribeResEventData(__result)}");
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] ResEvents attendance completion diagnostics failed: " + ex.Message);
			}
		}

		private static void ProcessChosenResultPostfix(VisitState visit, ResEventData __result)
		{
			try
			{
				VerificationLog("ResEvents", $"result-processed crew={DescribeCrew(visit?.crew)} visitor={DescribeEntity(visit?.npc)} building={DescribeBuilding(visit?.building)} data={DescribeResEventData(__result)}");
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] ResEvents result processing diagnostics failed: " + ex.Message);
			}
		}

		private static void CanBePickedPostfix(SpecialResEventCanBePicked __instance, VisitState visit, ref bool __result)
		{
			if (__instance == null || !__instance.expected || __result)
			{
				return;
			}

			try
			{
				Entity owner = visit?.npc;
				ulong ownerId = owner?.Id.id ?? 0UL;
				int turn = global::Game.Game.ctx?.clock?.CurrentTurn ?? -1;
				if (LastPickerLogTurnByOwner.TryGetValue(ownerId, out int lastTurn) && lastTurn == turn)
				{
					return;
				}

				LastPickerLogTurnByOwner[ownerId] = turn;
				string reason = DiagnoseNoCandidate(PlayerID.HumanPlayer, owner);
				Debug.LogWarning($"[GameplayTweaks] ResEvents picker-hidden owner={DescribeEntity(owner)} turn={turn} reason={reason}");
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] ResEvents picker diagnostics failed: " + ex.Message);
			}
		}

		private static Exception GenerateResultDataFinalizer(Exception __exception, object __instance, VisitState visit, ref ResEventResultData __result)
		{
			if (__exception == null && IsValidResult(__result))
			{
				return null;
			}

			ResidentialEventResultConfig config = __instance as ResidentialEventResultConfig;
			if (config == null)
			{
				return __exception;
			}

			try
			{
				ResEventResultData recovered = MakeFallbackResult(config, visit, __exception);
				if (recovered == null)
				{
					return __exception;
				}

				__result = recovered;
				Debug.LogWarning($"[GameplayTweaks] ResEvents result recovered result={config.id} target={DescribeEntity(recovered.targetNpc.FindEntity())} exception={__exception?.GetType().Name ?? "invalid-target"}");
				return null;
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] ResEvents result recovery failed: " + ex.Message);
				return __exception;
			}
		}

		private static Exception ResultBlurbFinalizer(Exception __exception, SpecialResEventResultBlurb __instance, ConversationModel model, ref string __result)
		{
			if (__exception == null)
			{
				return null;
			}

			try
			{
				ResEventData data = model?.visit?.building?.components?.residence?.GetResEventOrNull();
				if (!string.IsNullOrEmpty(data?.chosenResult?.eventResultMessage))
				{
					__result = data.chosenResult.eventResultMessage;
					Debug.LogWarning("[GameplayTweaks] ResEvents result blurb recovered from cached message: " + __exception.GetType().Name);
					return null;
				}

				ResidentialEventResultConfig config = data?.chosenResult?.GetConfig();
				Entity target = data?.chosenResult?.targetNpc.FindEntity() ?? model?.visit?.npc;
				if (config != null && target?.data?.person != null)
				{
					string fullName = target.data.person.FullName;
					string key = config.locroot + "." + __instance.suffix;
					__result = Loc.Get(key, "name", model.visit.npc.data.person.FullName, "othername", fullName, "rel", Loc.FormatNumber(0));
					Debug.LogWarning("[GameplayTweaks] ResEvents result blurb recovered with fallback target: " + __exception.GetType().Name);
					return null;
				}
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] ResEvents result blurb recovery failed: " + ex.Message);
			}

			return __exception;
		}

		private static string DiagnoseNoCandidate(PlayerID pid, Entity owner)
		{
			if (owner == null || owner.data?.person == null)
			{
				return "owner-null-or-not-person";
			}

			PlayerInfo player = pid.FindPlayer();
			Entity playerPeep = player?.social?.GetPlayerPeep();
			if (player == null || playerPeep == null)
			{
				return "human-player-missing";
			}

			int potentialHosts = CountPotentialHosts(playerPeep, owner);
			if (potentialHosts <= 0)
			{
				return "no-unknown-host";
			}

			Entity ownerBuilding = BuildingUtil.FindBuildingForBizOwner(owner);
			if (ownerBuilding == null)
			{
				return "owner-has-no-business-building";
			}

			int searchNodes = global::Game.Game.serv?.globals?.settings?.people?.residentialEvents?.resSearchNearbyCorners ?? 0;
			int visitedNodes;
			int reservedGlobal;
			int activeGlobal;
			int offNodeGlobal;
			int globalEventSpaces = CountGlobalEventHostSpaces(out reservedGlobal, out activeGlobal, out offNodeGlobal);
			int openEventSpaces = CountAvailableEventHostSpaces(ownerBuilding, searchNodes, out visitedNodes);
			if (openEventSpaces <= 0)
			{
				return $"no-open-residence-event-space searchNodes={searchNodes} visited={visitedNodes} ownerBuilding={DescribeBuilding(ownerBuilding)} globalSpaces={globalEventSpaces} globalReserved={reservedGlobal} globalActive={activeGlobal} globalOffNode={offNodeGlobal}";
			}

			int eventCount;
			int passingEvents = CountPassingEvents(player, owner, out eventCount);
			if (passingEvents <= 0)
			{
				bool controlledByHuman = ownerBuilding.data?.building?.controlled.pid.IsHumanPlayer == true;
				return $"no-event-visreqs-passed events={eventCount} controlledByHuman={controlledByHuman} ownerBuilding={DescribeBuilding(ownerBuilding)}";
			}

			return $"candidate-null-after-gates hosts={potentialHosts} spaces={openEventSpaces} passingEvents={passingEvents} ownerBuilding={DescribeBuilding(ownerBuilding)}";
		}

		private static int CountPotentialHosts(Entity playerPeep, Entity introducer)
		{
			try
			{
				return SocQ.FindPeople(playerPeep, introducer, IsUnknownResEventHostCandidate).Count();
			}
			catch
			{
				return -1;
			}
		}

		private static bool IsUnknownResEventHostCandidate(Entity player, Entity other, Entity candidate)
		{
			if (candidate == null || candidate.data?.person == null)
			{
				return false;
			}

			if (!SocQ.IsNotKnownToPlayer(player, other, candidate))
			{
				return false;
			}

			PersonData person = candidate.data.person;
			if (!person.IsAlive || person.GetAge(global::Game.Game.ctx.clock.Now).YearsInt < 20 || person.IsEmployed)
			{
				return false;
			}

			PlayerInfo candidatePlayer = candidate.data.agent?.pid.FindPlayer();
			return candidatePlayer == null || (!candidatePlayer.IsHuman && !candidatePlayer.IsGangOrGoon);
		}

		private static int CountAvailableEventHostSpaces(Entity ownerBuilding, int maxNodes, out int visitedNodes)
		{
			visitedNodes = 0;
			Node source = ownerBuilding?.data?.board?.bead.nodeId.FindNode();
			if (source == null || maxNodes <= 0)
			{
				return 0;
			}

			int spaces = 0;
			HashSet<Node> visited = new HashSet<Node>();
			Queue<Node> queue = new Queue<Node>();
			queue.Enqueue(source);
			while (queue.Count > 0)
			{
				Node node = queue.Dequeue();
				if (node == null || visited.Contains(node))
				{
					continue;
				}

				visited.Add(node);
				visitedNodes = visited.Count;
				if (NodeHasOpenEventSpace(node))
				{
					spaces++;
				}

				if (visited.Count >= maxNodes)
				{
					break;
				}

				NodeEdgeID[] edges = node.edges;
				for (int i = 0; i < edges.Length; i++)
				{
					NodeEdgeID edgeId = edges[i];
					if (edgeId.IsNotValid)
					{
						continue;
					}

					NodeEdge edge = global::Game.Game.ctx.board.nodes.GetEdge(edgeId);
					Node other = edge?.FindOtherNode(node);
					if (other != null && !visited.Contains(other))
					{
						queue.Enqueue(other);
					}
				}
			}

			return spaces;
		}

		private static int CountGlobalEventHostSpaces(out int reservedSpaces, out int activeSpaces, out int offNodeSpaces)
		{
			reservedSpaces = 0;
			activeSpaces = 0;
			offNodeSpaces = 0;
			try
			{
				int total = 0;
				foreach (Entity entity in global::Game.Game.ctx.entityman.GetCachedEntitiesBuildingsUnsafe())
				{
					ResidenceComponent residence = entity?.components?.residence;
					if (residence == null || !residence.IsEventHostingSpace)
					{
						continue;
					}

					total++;
					if (entity.data?.board?.bead.nodeId.FindNode() == null)
					{
						offNodeSpaces++;
					}

					if (residence.IsEventHostingActive)
					{
						activeSpaces++;
					}
					else
					{
						reservedSpaces++;
					}
				}

				return total;
			}
			catch
			{
				return -1;
			}
		}

		private static bool EnsureResidentialEventScheduled(Entity building, ResEventData data, PlayerID pid, string source)
		{
			if (building == null || data == null)
			{
				return false;
			}

			int currentTurn = global::Game.Game.ctx?.clock?.CurrentTurn ?? -1;
			if (data.turnStarts >= 0 && data.turnEnds >= currentTurn)
			{
				return false;
			}

			ResidentialEventConfig config = data.GetConfig();
			if (config == null)
			{
				return false;
			}

			int waitTurns = config.FindWaitTurns(pid);
			bool skipped = config.ShouldSkip(pid, building.data.ident.rng);
			data.Start(waitTurns, skipped);
			VerificationLog("ResEvents", $"schedule-started source={source} building={DescribeBuilding(building)} data={DescribeResEventData(data)} waitTurns={waitTurns} skipped={skipped}");
			return true;
		}

		private static void PostDueResidentialEventTickers(string source)
		{
			if (global::Game.Game.ctx?.IsInteractive != true)
			{
				return;
			}

			int turn = global::Game.Game.ctx?.clock?.CurrentTurn ?? -1;
			int activeEvents = 0;
			int nullData = 0;
			int skippedEvents = 0;
			int expiredEvents = 0;
			int restartedExpiredEvents = 0;
			int comingSoonEvents = 0;
			int thisTurnEvents = 0;
			int registeredThisTurnEvents = 0;
			int postedTickers = 0;

			foreach (Entity building in global::Game.Game.ctx.entityman.GetCachedEntitiesBuildingsUnsafe())
			{
				ResidenceComponent residence = building?.components?.residence;
				if (residence?.IsEventHostingActive != true)
				{
					continue;
				}

				activeEvents++;
				ResEventData data = residence.GetResEventOrNull();
				if (data == null)
				{
					nullData++;
					continue;
				}

				if (data.IsExpired)
				{
					expiredEvents++;
					if (EnsureResidentialEventScheduled(building, data, PlayerID.HumanPlayer, source + "-expired-restart"))
					{
						restartedExpiredEvents++;
					}
				}

				if (data.IsSkipped)
				{
					skippedEvents++;
					continue;
				}

				if (data.IsThisTurn && data.IsAttendanceRegistered)
				{
					thisTurnEvents++;
					registeredThisTurnEvents++;
					if (TryPostResidentialEventTicker(building, data, start: true, source: source + "-this-turn"))
					{
						postedTickers++;
					}
				}
				else if (data.IsThisTurn)
				{
					thisTurnEvents++;
					if (TryPostResidentialEventTicker(building, data, start: false, source: source + "-this-turn-unregistered-info"))
					{
						postedTickers++;
					}
				}
				else if (data.IsComingSoon)
				{
					comingSoonEvents++;
					if (TryPostResidentialEventTicker(building, data, start: false, source: source + "-coming-soon"))
					{
						postedTickers++;
					}
				}
			}

			LogTickerScanSummary(source, turn, activeEvents, nullData, skippedEvents, expiredEvents, restartedExpiredEvents, comingSoonEvents, thisTurnEvents, registeredThisTurnEvents, postedTickers);
		}

		private static void LogTickerScanSummary(string source, int turn, int activeEvents, int nullData, int skippedEvents, int expiredEvents, int restartedExpiredEvents, int comingSoonEvents, int thisTurnEvents, int registeredThisTurnEvents, int postedTickers)
		{
			if (LastTickerScanLogTurn == turn)
			{
				return;
			}

			if (activeEvents <= 0 && source != "human-turn-start" && (turn < 0 || turn % 7 != 0))
			{
				return;
			}

			LastTickerScanLogTurn = turn;
			int reservedGlobal;
			int activeGlobal;
			int offNodeGlobal;
			int globalEventSpaces = CountGlobalEventHostSpaces(out reservedGlobal, out activeGlobal, out offNodeGlobal);
			VerificationLog("ResEvents", $"ticker-scan source={source} turn={turn} active={activeEvents} nullData={nullData} skipped={skippedEvents} expired={expiredEvents} restartedExpired={restartedExpiredEvents} comingSoon={comingSoonEvents} thisTurn={thisTurnEvents} registeredThisTurn={registeredThisTurnEvents} posted={postedTickers} globalSpaces={globalEventSpaces} globalReserved={reservedGlobal} globalActive={activeGlobal} globalOffNode={offNodeGlobal}");
		}

		private static bool TryPostResidentialCandidateTicker(Entity introducer, Entity host, Entity building, ResEventCandidate candidate, string source)
		{
			try
			{
				if (global::Game.Game.ctx?.IsInteractive != true || building == null)
				{
					return false;
				}

				ResidentialEventConfig config = global::Game.Game.serv?.globals?.settings?.people?.residentialEvents?.FindEvent(candidate.eventId);
				if (config == null)
				{
					return false;
				}

				int turn = global::Game.Game.ctx?.clock?.CurrentTurn ?? -1;
				string tickerKey = $"{introducer?.Id.id ?? 0UL}:{host?.Id.id ?? 0UL}:{building.Id.id}:{candidate.eventId}";
				if (LastCandidateTickerTurnByKey.TryGetValue(tickerKey, out int lastTurn) && lastTurn == turn)
				{
					return false;
				}

				LastCandidateTickerTurnByKey[tickerKey] = turn;
				if (!EnableResidentialEventRepairTickers)
				{
					VerificationLog("ResEvents", $"candidate-ticker-suppressed source={source} event={candidate.eventId} introducer={DescribeEntity(introducer)} host={DescribeEntity(host)} building={DescribeBuilding(building)} turn={turn} reason=vanilla-ui-owner");
					return false;
				}

				string hostName = host?.data?.person?.FullName ?? "Someone";
				string eventName = config.GetName();
				string text = string.IsNullOrEmpty(eventName)
					? $"{hostName} has a residential event available."
					: $"{hostName} has a {eventName} available.";
				global::Game.Game.ctx?.hud?.tickers?.AddTextTicker(TickerIcon.EVENT_UPDATE, TickerTitle.EVENT_UPDATE, text, building.Id);
				VerificationLog("ResEvents", $"candidate-ticker-posted source={source} event={candidate.eventId} introducer={DescribeEntity(introducer)} host={DescribeEntity(host)} building={DescribeBuilding(building)} turn={turn}");
				return true;
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] ResEvents candidate ticker post failed: " + ex.Message);
				return false;
			}
		}

		private static bool TryPostResidentialEventTicker(Entity building, ResEventData data, bool start, string source)
		{
			try
			{
				if (building == null || data == null || data.IsSkipped)
				{
					return false;
				}

				ResidentialEventConfig config = data.GetConfig();
				string locKey = start ? config?.tickerstart : config?.tickerinfo;
				if (string.IsNullOrEmpty(locKey))
				{
					return false;
				}

				int turn = global::Game.Game.ctx?.clock?.CurrentTurn ?? -1;
				string state = start ? "start" : "info";
				string tickerKey = $"{building.Id.id}:{data.eventId}:{state}:{data.turnStarts}:{data.turnEnds}:{locKey}";
				if (LastTickerTurnByBuildingState.ContainsKey(tickerKey))
				{
					return false;
				}

				LastTickerTurnByBuildingState[tickerKey] = turn;
				if (!EnableResidentialEventRepairTickers)
				{
					VerificationLog("ResEvents", $"ticker-suppressed source={source} state={state} building={DescribeBuilding(building)} locKey={locKey} turn={turn} data={DescribeResEventData(data)} reason=vanilla-ui-owner");
					return false;
				}

				string hostName = building.components?.residence?.GetNpcResident()?.data?.person?.FullName ?? "the host";
				string text = Loc.Get(locKey, "name", hostName);
				if (string.IsNullOrEmpty(text))
				{
					return false;
				}

				if (start)
				{
					global::Game.Game.ctx?.sfx?.PlayPartyImminent();
				}

				global::Game.Game.ctx?.hud?.tickers?.AddTextTicker(TickerIcon.EVENT_UPDATE, TickerTitle.EVENT_UPDATE, text, building.Id);
				VerificationLog("ResEvents", $"ticker-posted source={source} state={state} building={DescribeBuilding(building)} host={hostName} locKey={locKey} turn={turn} data={DescribeResEventData(data)}");
				return true;
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] ResEvents ticker post failed: " + ex.Message);
				return false;
			}
		}

		private static bool NodeHasOpenEventSpace(Node node)
		{
			for (int i = 0; i < node.contained.Count; i++)
			{
				Entity entity = node.contained[i].FindEntity();
				ResidenceComponent residence = entity?.components?.residence;
				if (residence != null && residence.IsEventHostingSpace && !residence.IsEventHostingActive)
				{
					return true;
				}
			}

			return false;
		}

		private static int CountPassingEvents(PlayerInfo player, Entity owner, out int eventCount)
		{
			eventCount = 0;
			try
			{
				ResidentialEventSettings settings = global::Game.Game.serv?.globals?.settings?.people?.residentialEvents;
				List<ResidentialEventConfig> events = settings?.events;
				eventCount = events?.Count ?? 0;
				if (events == null || eventCount == 0)
				{
					return 0;
				}

				BuildingAndBusinessData bbdata = BuildingUtil.FindDataForOwner(owner.Id);
				VisitState eventVisit = new VisitState(player.crew.GetCrewForPlayerPeep(), bbdata, global::Game.Game.ctx.clock.Now, player.PID);
				int passing = 0;
				for (int i = 0; i < events.Count; i++)
				{
					ResidentialEventConfig e = events[i];
					try
					{
						if (e?.visreqs == null || e.visreqs.AllPass(eventVisit))
						{
							passing++;
						}
					}
					catch (Exception ex)
					{
						Debug.LogWarning($"[GameplayTweaks] ResEvents visreq check failed event={e?.id} error={ex.GetType().Name}: {ex.Message}");
					}
				}

				return passing;
			}
			catch
			{
				return -1;
			}
		}

		private static bool IsValidResult(ResEventResultData data)
		{
			if (data == null || !data.resultId.IsSet || !data.targetNpc.IsValid)
			{
				return false;
			}

			return data.targetNpc.FindEntity()?.data?.person != null;
		}

		private static ResEventResultData MakeFallbackResult(ResidentialEventResultConfig config, VisitState visit, Exception ex)
		{
			Entity target = FindFallbackTarget(visit);
			if (target?.data?.person == null)
			{
				return null;
			}

			string fullName = target.data.person.FullName;
			string message = string.IsNullOrEmpty(config.loceventresult)
				? fullName
				: Loc.GetGendered(config.loceventresult, target.data.person.g, "othername", fullName);

			return new ResEventResultData
			{
				resultId = config.id,
				targetNpc = target.Id,
				eventResultMessage = message
			};
		}

		private static Entity FindFallbackTarget(VisitState visit)
		{
			Entity npc = visit?.npc;
			if (npc?.data?.person?.IsAlive == true)
			{
				return npc;
			}

			try
			{
				PlayerInfo player = visit?.GetPlayer();
				return player?.social?.GetAllPlayerRelationshipsUnsafe()
					.Select(rel => rel.to.FindEntity())
					.FirstOrDefault(entity => entity?.data?.person?.IsAlive == true);
			}
			catch
			{
				return null;
			}
		}

		private static string DescribeEntity(Entity entity)
		{
			if (entity == null)
			{
				return "null";
			}

			string name = entity.data?.person?.FullName ?? entity.data?.biz?.bizname ?? entity.Id.ToString();
			return $"{name}({entity.Id})";
		}

		private static string DescribeBuilding(Entity entity)
		{
			if (entity == null)
			{
				return "null";
			}

			string name = entity.data?.biz?.bizname ?? entity.config?.GetType().Name ?? entity.Id.ToString();
			Node node = entity.data?.board?.bead.nodeId.FindNode();
			string nodeText = node?.id.ToString() ?? "off-node";
			PlayerID owner = entity.data?.building?.controlled.pid ?? PlayerID.INVALID;
			return $"{name}({entity.Id},node={nodeText},owner={owner})";
		}

		private static string DescribeCrew(CrewAssignment? crew)
		{
			if (!crew.HasValue || !crew.Value.IsValid)
			{
				return "null";
			}

			CrewAssignment value = crew.Value;
			return $"{DescribeEntity(value.GetPeep())}/{value.type}:{value.targetId}";
		}

		private static string DescribeResEventData(ResEventData data)
		{
			if (data == null)
			{
				return "null";
			}

			return $"event={data.eventId} starts={data.turnStarts} ends={data.turnEnds} skipped={data.IsSkipped} attendance={data.attendance} result={DescribeResult(data.chosenResult)}";
		}

		private static string DescribeResult(ResEventResultData result)
		{
			if (result == null)
			{
				return "none";
			}

			return $"{result.resultId}:{DescribeEntity(result.targetNpc.FindEntity())}";
		}
	}
}
}
