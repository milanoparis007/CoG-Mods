using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;
using Game.Core;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Sim;

namespace AfterProhibitionFamily
{
	internal static class FamilyStartupAudit
	{
		internal static void LogStartupAudit(string source, ManualLogSource logger, int sampleLimit)
		{
			if (logger == null)
			{
				return;
			}

			try
			{
				PeopleTracker people = global::Game.Game.ctx?.simman?.peoplegen;
				RelationshipTracker rels = global::Game.Game.ctx?.simman?.rels;
				if (people == null)
				{
					logger.LogInfo("relationship-audit source=" + source + " unavailable reason=missing-people-tracker");
					return;
				}
				if (rels?.data?.entries == null)
				{
					logger.LogInfo("relationship-audit source=" + source + " unavailable reason=missing-relationship-tracker");
					return;
				}

				Dictionary<ulong, Entity> peopleById = new Dictionary<ulong, Entity>();
				AuditCounts counts = new AuditCounts();
				List<string> samples = new List<string>();

				foreach (Entity person in people.GetAllTrackedPeople() ?? Enumerable.Empty<Entity>())
				{
					try
					{
						AuditPerson(person, peopleById, counts);
					}
					catch
					{
						counts.ScanErrors++;
					}
				}

				foreach (KeyValuePair<EntityID, RelationshipList> entry in rels.data.entries)
				{
					try
					{
						AuditRelationshipList(entry.Key, entry.Value, peopleById, counts, samples, sampleLimit);
					}
					catch
					{
						counts.ScanErrors++;
					}
				}

				foreach (Entity person in peopleById.Values)
				{
					try
					{
						AuditFutureKids(person, rels, peopleById, counts, samples, sampleLimit);
					}
					catch
					{
						counts.ScanErrors++;
					}
				}

				logger.LogInfo(
					"relationship-audit source=" + source +
					" people=" + counts.People +
					" living=" + counts.Living +
					" dead=" + counts.Dead +
					" missingPersonData=" + counts.MissingPersonData +
					" relationshipSources=" + counts.RelationshipSources +
					" relationships=" + counts.Relationships +
					" spouseLinks=" + counts.SpouseLinks +
					" parentLinks=" + counts.ParentLinks +
					" childLinks=" + counts.ChildLinks +
					" familyLinks=" + counts.FamilyLinks +
					" deadRefs=" + counts.DeadRefs +
					" missingTargetRefs=" + counts.MissingTargetRefs +
					" invalidSpouses=" + counts.InvalidSpouses +
					" futureKids=" + counts.FutureKids +
					" invalidFutureKids=" + counts.InvalidFutureKids +
					" scanErrors=" + counts.ScanErrors);

				foreach (string sample in samples)
				{
					logger.LogInfo("relationship-audit-sample source=" + source + " " + sample);
				}
			}
			catch (Exception ex)
			{
				logger.LogWarning("relationship-audit failed source=" + source + " error=" + ex.GetType().Name + ":" + ex.Message);
			}
		}

		private static void AuditPerson(Entity person, Dictionary<ulong, Entity> peopleById, AuditCounts counts)
		{
			if (person == null || !person.Id.IsValid)
			{
				counts.MissingPersonData++;
				return;
			}

			counts.People++;
			peopleById[person.Id.id] = person;

			PersonData pdata = person.data?.person;
			if (pdata == null)
			{
				counts.MissingPersonData++;
				return;
			}

			if (pdata.IsAlive)
			{
				counts.Living++;
			}
			else
			{
				counts.Dead++;
			}
		}

		private static void AuditRelationshipList(
			EntityID sourceId,
			RelationshipList list,
			Dictionary<ulong, Entity> peopleById,
			AuditCounts counts,
			List<string> samples,
			int sampleLimit)
		{
			counts.RelationshipSources++;

			Entity source = FindKnownPerson(sourceId, peopleById);
			PersonData sourcePerson = source?.data?.person;
			if (source == null)
			{
				counts.MissingSourceRefs++;
				AddSample(samples, sampleLimit, "type=missing-source source=" + IdText(sourceId));
			}
			else if (sourcePerson != null && !sourcePerson.IsAlive)
			{
				counts.DeadSourceLists++;
				AddSample(samples, sampleLimit, "type=dead-source source=" + IdText(sourceId));
			}

			if (list?.data == null)
			{
				return;
			}

			foreach (Relationship rel in list.data)
			{
				if (rel == null)
				{
					counts.NullRelationships++;
					continue;
				}

				counts.Relationships++;

				if (rel.IsAnyFamily)
				{
					counts.FamilyLinks++;
				}
				if (rel.type == RelationshipType.Spouse)
				{
					counts.SpouseLinks++;
				}
				else if (rel.type == RelationshipType.Mother || rel.type == RelationshipType.Father)
				{
					counts.ParentLinks++;
				}
				else if (rel.type == RelationshipType.Child)
				{
					counts.ChildLinks++;
				}

				Entity target = FindKnownPerson(rel.to, peopleById);
				PersonData targetPerson = target?.data?.person;
				if (target == null)
				{
					counts.MissingTargetRefs++;
					AddSample(samples, sampleLimit, "type=missing-target source=" + IdText(sourceId) + " target=" + IdText(rel.to) + " rel=" + rel.type);
					continue;
				}

				if (targetPerson != null && !targetPerson.IsAlive)
				{
					counts.DeadRefs++;
					AddSample(samples, sampleLimit, "type=dead-target source=" + IdText(sourceId) + " target=" + IdText(rel.to) + " rel=" + rel.type);
				}

				if (rel.type == RelationshipType.Spouse && IsInvalidExistingSpouseLink(source, target, out string reason))
				{
					counts.InvalidSpouses++;
					AddSample(samples, sampleLimit, "type=invalid-spouse current=" + IdText(sourceId) + " other=" + IdText(rel.to) + " reason=" + reason);
				}
			}
		}

		private static void AuditFutureKids(
			Entity person,
			RelationshipTracker rels,
			Dictionary<ulong, Entity> peopleById,
			AuditCounts counts,
			List<string> samples,
			int sampleLimit)
		{
			PersonData pdata = person?.data?.person;
			List<SimTime> futureKids = pdata?.futurekids;
			if (futureKids == null || futureKids.Count == 0)
			{
				return;
			}

			counts.FutureKids += futureKids.Count;

			string invalidReason = GetFutureKidInvalidReason(person, rels, peopleById);
			if (!string.Equals(invalidReason, "ok", StringComparison.Ordinal))
			{
				counts.InvalidFutureKids += futureKids.Count;
				AddSample(samples, sampleLimit, "type=invalid-futurekids parent=" + IdText(person.Id) + " count=" + futureKids.Count + " reason=" + invalidReason);
			}
		}

		private static string GetFutureKidInvalidReason(Entity parent, RelationshipTracker rels, Dictionary<ulong, Entity> peopleById)
		{
			PersonData pdata = parent?.data?.person;
			if (pdata == null)
			{
				return "missing-person-data";
			}
			if (!pdata.IsAlive)
			{
				return "dead-parent";
			}

			Relationship spouseRel = rels?.GetListOrNull(parent.Id)?.FindFirstOrNull(RelationshipType.Spouse);
			if (spouseRel == null)
			{
				return "missing-spouse";
			}

			Entity spouse = FindKnownPerson(spouseRel.to, peopleById);
			PersonData spousePerson = spouse?.data?.person;
			if (spousePerson == null)
			{
				return "missing-spouse-person-data";
			}
			if (!spousePerson.IsAlive)
			{
				return "dead-spouse";
			}
			if (IsInvalidExistingSpouseLink(parent, spouse, out string spouseReason))
			{
				return "invalid-spouse:" + spouseReason;
			}

			return "ok";
		}

		private static bool IsInvalidExistingSpouseLink(Entity current, Entity other, out string reason)
		{
			reason = "ok";
			PersonData currentPerson = current?.data?.person;
			PersonData otherPerson = other?.data?.person;
			if (currentPerson == null || otherPerson == null)
			{
				reason = "missing-person-data";
				return true;
			}
			if (current.Id == other.Id)
			{
				reason = "same-person";
				return true;
			}
			if (!currentPerson.IsAlive || !otherPerson.IsAlive)
			{
				reason = "dead-person";
				return true;
			}

			return false;
		}

		private static Entity FindKnownPerson(EntityID id, Dictionary<ulong, Entity> peopleById)
		{
			if (!id.IsValid || peopleById == null)
			{
				return null;
			}

			peopleById.TryGetValue(id.id, out Entity person);
			return person;
		}

		private static void AddSample(List<string> samples, int sampleLimit, string sample)
		{
			if (samples == null || sampleLimit <= 0 || samples.Count >= sampleLimit)
			{
				return;
			}

			samples.Add(sample);
		}

		private static string IdText(EntityID id)
		{
			return id.IsValid ? id.id.ToString() : "invalid";
		}

		private sealed class AuditCounts
		{
			public int People;
			public int Living;
			public int Dead;
			public int MissingPersonData;
			public int RelationshipSources;
			public int Relationships;
			public int SpouseLinks;
			public int ParentLinks;
			public int ChildLinks;
			public int FamilyLinks;
			public int DeadRefs;
			public int MissingSourceRefs;
			public int MissingTargetRefs;
			public int DeadSourceLists;
			public int NullRelationships;
			public int InvalidSpouses;
			public int FutureKids;
			public int InvalidFutureKids;
			public int ScanErrors;
		}
	}
}
