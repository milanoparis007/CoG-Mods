using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Game.Core;
using Game.Session;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Player;
using Game.Session.Sim;
using HarmonyLib;
using UnityEngine;

namespace AfterProhibitionFamily
{
	internal static class DeadRelationshipCleanupPatch
	{
		private const int DeadRelationshipStartupBatchFrameFloor = 300;

		private static readonly HashSet<ulong> PendingDeadRelationshipScrubPeepIds = new HashSet<ulong>();
		private static readonly HashSet<ulong> CompletedDeadRelationshipScrubPeepIds = new HashSet<ulong>();
		private static readonly HashSet<ulong> ActiveStartupScrubPeepIds = new HashSet<ulong>();
		private static List<EntityID> ActiveStartupScrubSourceIds;
		private static int StartupBatchChunks;
		private static int ActiveStartupScrubSourceIndex;
		private static int ActiveStartupScrubPendingBefore;
		private static int ActiveStartupScrubRemovedSourceLists;
		private static int ActiveStartupScrubOutgoingRemoved;
		private static int ActiveStartupScrubIncomingRemoved;
		private static int ActiveStartupScrubEmptiedIncomingLists;

		internal static void ApplyPatch(Harmony harmony)
		{
			if (!AfterProhibitionFamilyPlugin.OwnsDeadRelationshipCleanupMigration())
			{
				AfterProhibitionFamilyPlugin.Log?.LogInfo("dead relationship cleanup patches skipped by config");
				return;
			}

			try
			{
				ResetRuntime();
				MethodInfo processDeath = typeof(PlayerCrew).GetMethod("ProcessCrewMemberDeath", BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(Entity) }, null);
				MethodInfo markAsDead = typeof(PeopleTracker).GetMethod("MarkAsDead", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { typeof(Entity), typeof(SimTime) }, null);
				if (processDeath == null && markAsDead == null)
				{
					AfterProhibitionFamilyPlugin.Log?.LogWarning("dead relationship cleanup patch skipped: methods not found");
					return;
				}

				if (processDeath != null)
				{
					harmony.Patch(processDeath, postfix: new HarmonyMethod(typeof(DeadRelationshipCleanupPatch), nameof(ProcessCrewMemberDeathPostfix)));
				}
				if (markAsDead != null)
				{
					harmony.Patch(markAsDead, postfix: new HarmonyMethod(typeof(DeadRelationshipCleanupPatch), nameof(MarkAsDeadPostfix)));
				}

				AfterProhibitionFamilyPlugin.Log?.LogInfo("dead relationship cleanup patches applied crewDeath=" + (processDeath != null) + " peopleDeath=" + (markAsDead != null));
			}
			catch (Exception ex)
			{
				AfterProhibitionFamilyPlugin.Log?.LogWarning("dead relationship cleanup patch failed error=" + ex.GetType().Name + ":" + ex.Message);
			}
		}

		internal static bool HasPendingDeadRelationshipStartupScrubs()
		{
			return PendingDeadRelationshipScrubPeepIds.Count > 0;
		}

		internal static int PendingDeadRelationshipStartupScrubCount()
		{
			return PendingDeadRelationshipScrubPeepIds.Count;
		}

		internal static bool FlushPendingDeadRelationshipStartupScrubs(string sourceTag)
		{
			if (!AfterProhibitionFamilyPlugin.OwnsDeadRelationshipCleanupMigration())
			{
				return false;
			}
			if ((PendingDeadRelationshipScrubPeepIds.Count == 0 && ActiveStartupScrubPeepIds.Count == 0)
				|| ShouldBatchDeadRelationshipScrubForStartup()
				|| ShouldPauseDeadRelationshipScrubForTurnSimulation())
			{
				return false;
			}
			if (!TryGetDeadRelationshipEntries(out RelationshipEntries entries))
			{
				return false;
			}

			if (ActiveStartupScrubPeepIds.Count == 0 && !BeginIncrementalStartupScrub(entries))
			{
				return false;
			}

			try
			{
				int sourceBudget = AfterProhibitionFamilyPlugin.GetDeadRelationshipStartupEntryScanBudget();
				int scannedSources = 0;
				int batchRemovedSourceLists = 0;
				int batchOutgoingRemoved = 0;
				int batchIncomingRemoved = 0;
				int batchEmptiedIncomingLists = 0;
				while (ActiveStartupScrubSourceIds != null
					&& ActiveStartupScrubSourceIndex < ActiveStartupScrubSourceIds.Count
					&& scannedSources < sourceBudget)
				{
					EntityID sourceId = ActiveStartupScrubSourceIds[ActiveStartupScrubSourceIndex++];
					scannedSources++;

					if (!entries.TryGetValue(sourceId, out RelationshipList value))
					{
						continue;
					}

					if (ActiveStartupScrubPeepIds.Contains(sourceId.id))
					{
						batchOutgoingRemoved += value?.data?.Count ?? 0;
						if (entries.Remove(sourceId))
						{
							batchRemovedSourceLists++;
						}
						continue;
					}

					if (value?.data == null || value.data.Count == 0)
					{
						continue;
					}

					int count = value.data.Count;
					value.data.RemoveAll((Relationship rel) => rel != null && ActiveStartupScrubPeepIds.Contains(rel.to.id));
					int removedCount = count - value.data.Count;
					if (removedCount <= 0)
					{
						continue;
					}

					batchIncomingRemoved += removedCount;
					if (value.data.Count == 0)
					{
						if (entries.Remove(sourceId))
						{
							batchEmptiedIncomingLists++;
						}
					}
				}

				ActiveStartupScrubRemovedSourceLists += batchRemovedSourceLists;
				ActiveStartupScrubOutgoingRemoved += batchOutgoingRemoved;
				ActiveStartupScrubIncomingRemoved += batchIncomingRemoved;
				ActiveStartupScrubEmptiedIncomingLists += batchEmptiedIncomingLists;
				StartupBatchChunks++;

				bool completed = ActiveStartupScrubSourceIds == null || ActiveStartupScrubSourceIndex >= ActiveStartupScrubSourceIds.Count;
				if (!completed)
				{
					if (ShouldLogIncrementalStartupScrubProgress())
					{
						AfterProhibitionFamilyPlugin.Log?.LogInfo(
							"dead-relationship-cleanup source=startup-incremental trigger=" + sourceTag +
							" chunk=" + StartupBatchChunks +
							" peeps=" + ActiveStartupScrubPeepIds.Count +
							" pendingBefore=" + ActiveStartupScrubPendingBefore +
							" sourceIndex=" + ActiveStartupScrubSourceIndex +
							" sources=" + ActiveStartupScrubSourceIds.Count +
							" sourceListsRemoved=" + ActiveStartupScrubRemovedSourceLists +
							" outgoing=" + ActiveStartupScrubOutgoingRemoved +
							" incoming=" + ActiveStartupScrubIncomingRemoved +
							" emptiedIncomingLists=" + ActiveStartupScrubEmptiedIncomingLists);
					}

					return batchRemovedSourceLists > 0 || batchOutgoingRemoved > 0 || batchIncomingRemoved > 0 || batchEmptiedIncomingLists > 0;
				}

				bool changed = ActiveStartupScrubRemovedSourceLists > 0
					|| ActiveStartupScrubOutgoingRemoved > 0
					|| ActiveStartupScrubIncomingRemoved > 0
					|| ActiveStartupScrubEmptiedIncomingLists > 0;
				CompleteIncrementalStartupScrub(sourceTag, changed);
				return changed;
			}
			catch (Exception ex)
			{
				AfterProhibitionFamilyPlugin.Log?.LogWarning("dead relationship startup batch cleanup failed error=" + ex.GetType().Name + ":" + ex.Message);
				ClearIncrementalStartupScrubState();
				return false;
			}
		}

		private static void ResetRuntime()
		{
			PendingDeadRelationshipScrubPeepIds.Clear();
			CompletedDeadRelationshipScrubPeepIds.Clear();
			ClearIncrementalStartupScrubState();
		}

		private static bool BeginIncrementalStartupScrub(RelationshipEntries entries)
		{
			ActiveStartupScrubPeepIds.Clear();
			foreach (ulong pendingPeepId in PendingDeadRelationshipScrubPeepIds)
			{
				if (!CompletedDeadRelationshipScrubPeepIds.Contains(pendingPeepId))
				{
					ActiveStartupScrubPeepIds.Add(pendingPeepId);
				}
			}

			if (ActiveStartupScrubPeepIds.Count == 0)
			{
				PendingDeadRelationshipScrubPeepIds.Clear();
				return false;
			}

			PruneDeadSourceRelationshipLists(entries);
			ActiveStartupScrubSourceIds = BuildStartupPriorityIncomingScrubSources(entries);
			ActiveStartupScrubSourceIndex = 0;
			ActiveStartupScrubPendingBefore = PendingDeadRelationshipScrubPeepIds.Count;
			ActiveStartupScrubIncomingRemoved = 0;
			ActiveStartupScrubEmptiedIncomingLists = 0;
			StartupBatchChunks = 0;
			return ActiveStartupScrubSourceIds.Count > 0 || ActiveStartupScrubRemovedSourceLists > 0 || ActiveStartupScrubOutgoingRemoved > 0;
		}

		private static void PruneDeadSourceRelationshipLists(RelationshipEntries entries)
		{
			ActiveStartupScrubRemovedSourceLists = 0;
			ActiveStartupScrubOutgoingRemoved = 0;
			foreach (ulong pendingPeepId in ActiveStartupScrubPeepIds)
			{
				EntityID sourceId = EntityID.FromID(pendingPeepId);
				if (!entries.TryGetValue(sourceId, out RelationshipList list))
				{
					continue;
				}

				ActiveStartupScrubOutgoingRemoved += list?.data?.Count ?? 0;
				if (entries.Remove(sourceId))
				{
					ActiveStartupScrubRemovedSourceLists++;
				}
			}
		}

		private static List<EntityID> BuildStartupPriorityIncomingScrubSources(RelationshipEntries entries)
		{
			List<EntityID> sources = new List<EntityID>();
			HashSet<EntityID> seen = new HashSet<EntityID>();
			try
			{
				foreach (PlayerInfo player in global::Game.Game.ctx?.players?.all ?? Enumerable.Empty<PlayerInfo>())
				{
					EntityID peepId = player?.social?.PlayerPeepId ?? EntityID.INVALID;
					if (peepId.IsNotValid || ActiveStartupScrubPeepIds.Contains(peepId.id) || !seen.Add(peepId) || !entries.ContainsKey(peepId))
					{
						continue;
					}

					sources.Add(peepId);
				}
			}
			catch
			{
			}

			return sources;
		}

		private static bool ShouldLogIncrementalStartupScrubProgress()
		{
			return StartupBatchChunks == 1 || StartupBatchChunks % 8 == 0;
		}

		private static void CompleteIncrementalStartupScrub(string sourceTag, bool changed)
		{
			foreach (ulong pendingPeepId in ActiveStartupScrubPeepIds)
			{
				CompletedDeadRelationshipScrubPeepIds.Add(pendingPeepId);
				PendingDeadRelationshipScrubPeepIds.Remove(pendingPeepId);
			}

			if (changed)
			{
				global::Game.Game.ctx?.events?.EnqueueOnce(SessionEventType.SomeEntityRelationshipChanged);
			}

			AfterProhibitionFamilyPlugin.Log?.LogInfo(
				"dead-relationship-cleanup source=startup-incremental-complete trigger=" + sourceTag +
				" chunks=" + StartupBatchChunks +
				" peeps=" + ActiveStartupScrubPeepIds.Count +
				" pendingBefore=" + ActiveStartupScrubPendingBefore +
				" remaining=" + PendingDeadRelationshipScrubPeepIds.Count +
				" sources=" + (ActiveStartupScrubSourceIds?.Count ?? 0) +
				" sourceListsRemoved=" + ActiveStartupScrubRemovedSourceLists +
				" outgoing=" + ActiveStartupScrubOutgoingRemoved +
				" incoming=" + ActiveStartupScrubIncomingRemoved +
				" emptiedIncomingLists=" + ActiveStartupScrubEmptiedIncomingLists);

			ClearIncrementalStartupScrubState();
		}

		private static void ClearIncrementalStartupScrubState()
		{
			ActiveStartupScrubPeepIds.Clear();
			ActiveStartupScrubSourceIds = null;
			ActiveStartupScrubSourceIndex = 0;
			ActiveStartupScrubPendingBefore = 0;
			ActiveStartupScrubRemovedSourceLists = 0;
			ActiveStartupScrubOutgoingRemoved = 0;
			ActiveStartupScrubIncomingRemoved = 0;
			ActiveStartupScrubEmptiedIncomingLists = 0;
			StartupBatchChunks = 0;
		}

		private static bool TryScrubDeadRelationshipState(EntityID peepId, string sourceTag)
		{
			if (peepId.IsNotValid)
			{
				return false;
			}

			ulong peepKey = peepId.id;
			if (CompletedDeadRelationshipScrubPeepIds.Contains(peepKey))
			{
				return false;
			}

			if (ShouldBatchDeadRelationshipScrubForStartup())
			{
				PendingDeadRelationshipScrubPeepIds.Add(peepKey);
				return false;
			}

			if (!TryGetDeadRelationshipEntries(out RelationshipEntries entries))
			{
				return false;
			}

			bool scrubbed = TryScrubDeadRelationshipStateImmediate(entries, peepId, sourceTag);
			CompletedDeadRelationshipScrubPeepIds.Add(peepKey);
			return scrubbed;
		}

		private static bool TryScrubDeadRelationshipStateImmediate(RelationshipEntries entries, EntityID peepId, string sourceTag)
		{
			if (entries == null)
			{
				return false;
			}

			try
			{
				if (entries.Count == 0)
				{
					return false;
				}

				int outgoingRemoved = 0;
				int incomingRemoved = 0;
				int emptiedIncomingLists = 0;
				bool removedSourceList = false;
				if (entries.TryGetValue(peepId, out RelationshipList ownList))
				{
					outgoingRemoved = ownList?.data?.Count ?? 0;
					removedSourceList = entries.Remove(peepId);
				}

				List<EntityID> emptiedSources = null;
				foreach (KeyValuePair<EntityID, RelationshipList> item in entries.ToList())
				{
					RelationshipList value = item.Value;
					if (value?.data == null || value.data.Count == 0)
					{
						continue;
					}

					int count = value.data.Count;
					value.data.RemoveAll((Relationship rel) => rel != null && rel.to == peepId);
					int removedCount = count - value.data.Count;
					if (removedCount <= 0)
					{
						continue;
					}

					incomingRemoved += removedCount;
					if (value.data.Count == 0)
					{
						(emptiedSources ?? (emptiedSources = new List<EntityID>())).Add(item.Key);
					}
				}

				if (emptiedSources != null)
				{
					foreach (EntityID emptiedSource in emptiedSources)
					{
						if (entries.Remove(emptiedSource))
						{
							emptiedIncomingLists++;
						}
					}
				}

				if (!removedSourceList && outgoingRemoved <= 0 && incomingRemoved <= 0 && emptiedIncomingLists <= 0)
				{
					return false;
				}

				global::Game.Game.ctx?.events?.EnqueueOnce(SessionEventType.SomeEntityRelationshipChanged);
				AfterProhibitionFamilyPlugin.Log?.LogInfo("dead-relationship-cleanup source=" + sourceTag + " peep=" + peepId.id + " sourceListRemoved=" + removedSourceList + " outgoing=" + outgoingRemoved + " incoming=" + incomingRemoved + " emptiedIncomingLists=" + emptiedIncomingLists);
				return true;
			}
			catch (Exception ex)
			{
				AfterProhibitionFamilyPlugin.Log?.LogWarning("dead relationship cleanup failed error=" + ex.GetType().Name + ":" + ex.Message);
				return false;
			}
		}

		private static bool TryGetDeadRelationshipEntries(out RelationshipEntries entries)
		{
			entries = global::Game.Game.ctx?.simman?.rels?.data?.entries;
			return entries != null;
		}

		private static bool ShouldBatchDeadRelationshipScrubForStartup()
		{
			return Time.frameCount < DeadRelationshipStartupBatchFrameFloor
				|| !(global::Game.Game.ctx?.IsInteractive ?? false);
		}

		private static bool ShouldPauseDeadRelationshipScrubForTurnSimulation()
		{
			try
			{
				PlayerID pid = global::Game.Game.ctx?.clock?.State.pid ?? PlayerID.System;
				return !pid.IsHumanPlayer;
			}
			catch
			{
				return true;
			}
		}

		private static void ProcessCrewMemberDeathPostfix(Entity peep)
		{
			TryScrubDeadRelationshipState(peep?.Id ?? EntityID.INVALID, "crew-death");
		}

		private static void MarkAsDeadPostfix(Entity peep, SimTime timeOfDeath)
		{
			_ = timeOfDeath;
			TryScrubDeadRelationshipState(peep?.Id ?? EntityID.INVALID, "people-tracker");
		}
	}
}
