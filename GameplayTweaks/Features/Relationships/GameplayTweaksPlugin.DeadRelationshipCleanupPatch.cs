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

namespace GameplayTweaks
{
public partial class GameplayTweaksPlugin
{
	private const int DeadRelationshipStartupBatchFrameFloor = 300;

	private static readonly HashSet<ulong> _pendingDeadRelationshipScrubPeepIds = new HashSet<ulong>();

	private static readonly HashSet<ulong> _completedDeadRelationshipScrubPeepIds = new HashSet<ulong>();

	internal static void ResetDeadRelationshipCleanupRuntime()
	{
		_pendingDeadRelationshipScrubPeepIds.Clear();
		_completedDeadRelationshipScrubPeepIds.Clear();
	}

	private static bool ShouldBatchDeadRelationshipScrubForStartup()
	{
		return Time.frameCount < DeadRelationshipStartupBatchFrameFloor || !AreRuntimePromptsReady();
	}

	private static bool TryGetDeadRelationshipEntries(out RelationshipEntries entries)
	{
		entries = G.GetRels()?.data?.entries;
		return entries != null;
	}

	internal static bool TryScrubDeadRelationshipState(EntityID peepId, string sourceTag)
	{
		if (peepId.IsNotValid)
		{
			return false;
		}

		ulong peepKey = peepId.id;
		if (_completedDeadRelationshipScrubPeepIds.Contains(peepKey))
		{
			return false;
		}

		if (ShouldBatchDeadRelationshipScrubForStartup())
		{
			_pendingDeadRelationshipScrubPeepIds.Add(peepKey);
			return false;
		}

		if (!TryGetDeadRelationshipEntries(out RelationshipEntries entries))
		{
			return false;
		}

		bool scrubbed = TryScrubDeadRelationshipStateImmediate(entries, peepId, sourceTag);
		_completedDeadRelationshipScrubPeepIds.Add(peepKey);
		return scrubbed;
	}

	internal static void FlushPendingDeadRelationshipStartupScrubs(string sourceTag)
	{
		if (_pendingDeadRelationshipScrubPeepIds.Count == 0 || ShouldBatchDeadRelationshipScrubForStartup())
		{
			return;
		}

		if (!TryGetDeadRelationshipEntries(out RelationshipEntries entries))
		{
			return;
		}

		List<ulong> pendingPeepIds = _pendingDeadRelationshipScrubPeepIds
			.Where(id => !_completedDeadRelationshipScrubPeepIds.Contains(id))
			.ToList();
		if (pendingPeepIds.Count == 0)
		{
			_pendingDeadRelationshipScrubPeepIds.Clear();
			return;
		}

		try
		{
			HashSet<ulong> pendingPeepIdSet = new HashSet<ulong>(pendingPeepIds);
			int outgoingRemoved = 0;
			int incomingRemoved = 0;
			int emptiedIncomingLists = 0;
			int removedSourceLists = 0;

			foreach (EntityID sourceId in entries.Keys.Where(id => pendingPeepIdSet.Contains(id.id)).ToList())
			{
				if (!entries.TryGetValue(sourceId, out RelationshipList ownList))
				{
					continue;
				}

				outgoingRemoved += ownList?.data?.Count ?? 0;
				if (entries.Remove(sourceId))
				{
					removedSourceLists++;
				}
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
				value.data.RemoveAll((Relationship rel) => rel != null && pendingPeepIdSet.Contains(rel.to.id));
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

			foreach (ulong pendingPeepId in pendingPeepIds)
			{
				_completedDeadRelationshipScrubPeepIds.Add(pendingPeepId);
				_pendingDeadRelationshipScrubPeepIds.Remove(pendingPeepId);
			}

			if (removedSourceLists <= 0 && outgoingRemoved <= 0 && incomingRemoved <= 0 && emptiedIncomingLists <= 0)
			{
				return;
			}

			global::Game.Game.ctx?.events?.EnqueueOnce(SessionEventType.SomeEntityRelationshipChanged);
			VerificationLog("RelationshipCleanup", $"startup-batch peeps={pendingPeepIds.Count} source={sourceTag} sourceListsRemoved={removedSourceLists} outgoing={outgoingRemoved} incoming={incomingRemoved} emptiedIncomingLists={emptiedIncomingLists}");
		}
		catch (Exception ex)
		{
			Debug.LogWarning("[GameplayTweaks] Dead relationship startup batch cleanup failed: " + ex.Message);
		}
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
				int num = count - value.data.Count;
				if (num <= 0)
				{
					continue;
				}

				incomingRemoved += num;
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
			VerificationLog("RelationshipCleanup", $"dead-peep scrub peep={peepId.id} source={sourceTag} sourceListRemoved={removedSourceList} outgoing={outgoingRemoved} incoming={incomingRemoved} emptiedIncomingLists={emptiedIncomingLists}");
			return true;
		}
		catch (Exception ex)
		{
			Debug.LogWarning("[GameplayTweaks] Dead relationship cleanup failed: " + ex.Message);
			return false;
		}
	}

	internal static class DeadRelationshipCleanupPatch
	{
		public static void ApplyPatch(Harmony harmony)
		{
			try
			{
				ResetDeadRelationshipCleanupRuntime();
				MethodInfo processDeath = typeof(PlayerCrew).GetMethod("ProcessCrewMemberDeath", BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(Entity) }, null);
				MethodInfo markAsDead = typeof(PeopleTracker).GetMethod("MarkAsDead", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { typeof(Entity), typeof(SimTime) }, null);
				if (processDeath == null && markAsDead == null)
				{
					Debug.LogWarning("[GameplayTweaks] DeadRelationshipCleanupPatch methods not found");
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

				VerificationLog("RelationshipCleanup", $"patch applied crewDeath={(processDeath != null)} peopleDeath={(markAsDead != null)}");
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] DeadRelationshipCleanupPatch failed: " + ex.Message);
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
}
