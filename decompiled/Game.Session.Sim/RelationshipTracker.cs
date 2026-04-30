using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Services;
using Game.Session.Data;
using SomaSim.SION;
using SomaSim.Util;

namespace Game.Session.Sim;

public sealed class RelationshipTracker : ISystemTurnSubManager<SimulationManager>, ISubManager<SimulationManager>, ISaveLoadProvider
{
	public class SavedEntryList : List<RelationshipList>
	{
		public static SavedEntryList FromSource(IEnumerable<RelationshipList> entries)
		{
			SavedEntryList savedEntryList = new SavedEntryList();
			savedEntryList.AddRange(entries);
			return savedEntryList;
		}

		public RelationshipEntries ToEntries()
		{
			RelationshipEntries relationshipEntries = new RelationshipEntries();
			using Enumerator enumerator = GetEnumerator();
			while (enumerator.MoveNext())
			{
				RelationshipList current = enumerator.Current;
				relationshipEntries.Add(current.sourceId, current);
			}
			return relationshipEntries;
		}
	}

	private class RelationshipBatcher : ParallelBatchingSave.JobCollection<ulong, RelationshipList>
	{
		public override string DebugName => "relationships";

		public override ulong GetIndex(RelationshipList entry)
		{
			return entry.sourceId.id;
		}

		public override Hashtable Serialize(RelationshipList entry, Serializer s)
		{
			return s.Serialize(entry) as Hashtable;
		}
	}

	public RelationshipPersistedData data;

	public void Initialize(SimulationManager manager)
	{
		data = new RelationshipPersistedData();
	}

	public void OnSystemTurn()
	{
	}

	public void Release()
	{
		data = null;
	}

	public RelationshipList GetListOrNull(EntityID sourceId)
	{
		return data.entries.FindOrNull(sourceId);
	}

	public RelationshipList GetListOrCreate(EntityID sourceId)
	{
		if (!data.entries.TryGetValue(sourceId, out var value))
		{
			RelationshipEntries entries = data.entries;
			RelationshipList obj = new RelationshipList
			{
				sourceId = sourceId
			};
			RelationshipList result = obj;
			entries[sourceId] = obj;
			return result;
		}
		return value;
	}

	public bool HasAny(EntityID sourceId, EntityID targetId)
	{
		return GetOrNull(sourceId, targetId) != null;
	}

	public bool HasNone(EntityID sourceId, EntityID targetId)
	{
		return GetOrNull(sourceId, targetId) == null;
	}

	public Relationship GetOrNull(EntityID sourceId, EntityID targetId)
	{
		return data.entries.FindOrNull(sourceId)?.FindOrNull(targetId);
	}

	public RelationshipType GetTypeOrNone(EntityID sourceId, EntityID targetId)
	{
		return GetOrNull(sourceId, targetId)?.type ?? RelationshipType.None;
	}

	public Relationship GetOrCreate(EntityID sourceId, EntityID targetId, RelationshipType type, bool warnOnExisting)
	{
		RelationshipList listOrCreate = GetListOrCreate(sourceId);
		Relationship relationship = listOrCreate.FindOrNull(targetId);
		if (relationship != null)
		{
			if (warnOnExisting)
			{
				_ = relationship.type;
			}
			return relationship;
		}
		Relationship relationship2 = new Relationship(type, sourceId, targetId);
		listOrCreate.Add(listOrCreate, relationship2);
		return relationship2;
	}

	public (Relationship toTarget, Relationship fromTarget) GetOrNullSymmetrical(EntityID sourceId, EntityID targetId)
	{
		Relationship orNull = GetOrNull(sourceId, targetId);
		Relationship orNull2 = GetOrNull(targetId, sourceId);
		return (toTarget: orNull, fromTarget: orNull2);
	}

	public (Relationship toTarget, Relationship fromTarget) GetOrMakeSymmetrical(EntityID sourceId, EntityID targetId, RelationshipType type, bool warnOnExisting)
	{
		Relationship orCreate = GetOrCreate(sourceId, targetId, type, warnOnExisting);
		Relationship orCreate2 = GetOrCreate(targetId, sourceId, type, warnOnExisting);
		return (toTarget: orCreate, fromTarget: orCreate2);
	}

	public void Save(Serializer s, ConcurrentSaveTable results)
	{
		List<RelationshipList> entries = data.entries.Values.ToList();
		RelationshipBatcher relationshipBatcher = Game.serv.saveload.batcher.MakeParallelJobs<RelationshipBatcher, ulong, RelationshipList>(entries, 1000);
		relationshipBatcher.RunAllJobs();
		results.Set("data", relationshipBatcher.collector.ValuesToArrayList());
	}

	public IEnumerator Load(Hashtable data)
	{
		this.data = new RelationshipPersistedData();
		SaveLoadUtils.DeserializeSingleKey(data, "data", delegate(SavedEntryList result)
		{
			this.data.entries = result.ToEntries();
		});
		yield break;
	}
}
