using System;
using System.Collections.Generic;
using BepInEx.Logging;
using Game.Core;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Sim;

namespace AfterProhibitionFamily
{
	public static class RelationshipSafetyClassifier
	{
		public static bool IsValidSpouseCandidate(Entity current, Entity other, out string reason)
		{
			reason = GetSpouseCandidateBlockReason(current, other);
			return string.Equals(reason, "ok", StringComparison.Ordinal);
		}

		public static bool IsValidParentChildLink(Entity parent, Entity child, out string reason)
		{
			reason = GetParentChildBlockReason(parent, child);
			return string.Equals(reason, "ok", StringComparison.Ordinal);
		}

		public static bool IsValidExistingSpouseLink(Entity current, Entity other, out string reason)
		{
			reason = GetExistingSpouseBlockReason(current, other);
			return string.Equals(reason, "ok", StringComparison.Ordinal);
		}

		public static bool IsBusinessOwnerFamilySafe(Entity person, out string reason)
		{
			reason = GetBusinessOwnerFamilyBlockReason(person);
			return string.Equals(reason, "ok", StringComparison.Ordinal);
		}

		public static bool IsRelationshipTargetAlive(EntityID peepId, out string reason)
		{
			Entity peep = FindTrackedPerson(peepId);
			if (peep?.data?.person == null)
			{
				reason = "missing-person-data";
				return false;
			}
			if (!peep.data.person.IsAlive)
			{
				reason = "dead-person";
				return false;
			}

			reason = "ok";
			return true;
		}

		public static string GetRelationshipSafetySummary(EntityID peepId)
		{
			try
			{
				Entity peep = FindTrackedPerson(peepId);
				if (peep?.data?.person == null)
				{
					return "status=invalid reason=missing-person-data peep=" + IdText(peepId);
				}

				RelationshipTracker rels = global::Game.Game.ctx?.simman?.rels;
				RelationshipList list = rels?.GetListOrNull(peepId);
				int outgoing = 0;
				int family = 0;
				int spouse = 0;
				int deadTargets = 0;
				int missingTargets = 0;
				if (list?.data != null)
				{
					foreach (Relationship rel in list.data)
					{
						if (rel == null)
						{
							continue;
						}

						outgoing++;
						if (rel.IsAnyFamily)
						{
							family++;
						}
						if (rel.type == RelationshipType.Spouse)
						{
							spouse++;
						}

						Entity target = FindTrackedPerson(rel.to);
						if (target?.data?.person == null)
						{
							missingTargets++;
						}
						else if (!target.data.person.IsAlive)
						{
							deadTargets++;
						}
					}
				}

				string status = deadTargets == 0 && missingTargets == 0 ? "ok" : "warning";
				return "status=" + status +
					" peep=" + IdText(peepId) +
					" alive=" + peep.data.person.IsAlive +
					" outgoing=" + outgoing +
					" family=" + family +
					" spouse=" + spouse +
					" deadTargets=" + deadTargets +
					" missingTargets=" + missingTargets;
			}
			catch (Exception ex)
			{
				return "status=error peep=" + IdText(peepId) + " error=" + ex.GetType().Name + ":" + ex.Message;
			}
		}

		internal static void LogStartupSamples(string source, ManualLogSource logger, int sampleLimit)
		{
			if (logger == null || sampleLimit <= 0)
			{
				return;
			}

			try
			{
				RelationshipTracker rels = global::Game.Game.ctx?.simman?.rels;
				if (rels?.data?.entries == null)
				{
					logger.LogInfo("relationship-safety source=" + source + " unavailable reason=missing-relationship-tracker");
					return;
				}

				int logged = 0;
				foreach (KeyValuePair<EntityID, RelationshipList> entry in rels.data.entries)
				{
					if (logged >= sampleLimit)
					{
						break;
					}

					Entity current = FindTrackedPerson(entry.Key);
					if (current?.data?.person == null)
					{
						continue;
					}

					if (logged == 0)
					{
						logger.LogInfo("relationship-safety source=" + source + " " + GetRelationshipSafetySummary(entry.Key));
						logged++;
						if (logged >= sampleLimit)
						{
							break;
						}
					}

					if (entry.Value?.data == null)
					{
						continue;
					}

					foreach (Relationship rel in entry.Value.data)
					{
						if (logged >= sampleLimit)
						{
							break;
						}
						if (rel == null || rel.type != RelationshipType.Spouse)
						{
							continue;
						}

						Entity other = FindTrackedPerson(rel.to);
						bool allowed = IsValidSpouseCandidate(current, other, out string reason);
						logger.LogInfo("spouse-safety source=" + source + " current=" + IdText(entry.Key) + " other=" + IdText(rel.to) + " allowed=" + allowed + " reason=" + reason);
						logged++;
						break;
					}
				}
			}
			catch (Exception ex)
			{
				logger.LogWarning("relationship-safety failed source=" + source + " error=" + ex.GetType().Name + ":" + ex.Message);
			}
		}

		private static string GetSpouseCandidateBlockReason(Entity current, Entity other)
		{
			PersonData currentPerson = current?.data?.person;
			PersonData otherPerson = other?.data?.person;
			if (currentPerson == null || otherPerson == null)
			{
				return "missing-person-data";
			}
			if (current.Id == other.Id)
			{
				return "same-person";
			}
			if (!currentPerson.IsAlive || !otherPerson.IsAlive)
			{
				return "dead-person";
			}
			if (otherPerson.business.IsValid)
			{
				return "business-owner";
			}
			if (!otherPerson.resassigned.IsNotValid)
			{
				return "resassigned";
			}
			if (currentPerson.famId >= 0 && currentPerson.famId == otherPerson.famId)
			{
				return "same-family-tree";
			}

			RelationshipTracker rels = global::Game.Game.ctx?.simman?.rels;
			Relationship currentToOther = rels?.GetOrNull(current.Id, other.Id);
			if (currentToOther != null && currentToOther.IsAnyFamily)
			{
				return "family-current-to-other:" + currentToOther.type;
			}

			Relationship otherToCurrent = rels?.GetOrNull(other.Id, current.Id);
			if (otherToCurrent != null && otherToCurrent.IsAnyFamily)
			{
				return "family-other-to-current:" + otherToCurrent.type;
			}

			return "ok";
		}

		private static string GetBusinessOwnerFamilyBlockReason(Entity person)
		{
			PersonData personData = person?.data?.person;
			if (personData == null)
			{
				return "missing-person-data";
			}
			if (!personData.IsAlive)
			{
				return "dead-person";
			}
			if (personData.futurekids != null && personData.futurekids.Count > 0)
			{
				return "future-kids";
			}

			RelationshipTracker rels = global::Game.Game.ctx?.simman?.rels;
			RelationshipList list = rels?.GetListOrNull(person.Id);
			if (list?.data != null)
			{
				foreach (Relationship rel in list.data)
				{
					if (rel == null)
					{
						continue;
					}

					if (rel.type == RelationshipType.Spouse)
					{
						Entity spouse = FindTrackedPerson(rel.to);
						if (spouse?.data?.person == null)
						{
							return "spouse-missing";
						}
						if (!spouse.data.person.IsAlive)
						{
							return "spouse-dead";
						}
						return "has-spouse";
					}

					if (rel.IsAnyFamily)
					{
						Entity relative = FindTrackedPerson(rel.to);
						if (relative?.data?.person == null)
						{
							return "family-missing:" + rel.type;
						}
						if (!relative.data.person.IsAlive)
						{
							return "family-dead:" + rel.type;
						}
						return "family-link:" + rel.type;
					}
				}
			}

			return "ok";
		}

		private static string GetExistingSpouseBlockReason(Entity current, Entity other)
		{
			PersonData currentPerson = current?.data?.person;
			PersonData otherPerson = other?.data?.person;
			if (currentPerson == null || otherPerson == null)
			{
				return "missing-person-data";
			}
			if (current.Id == other.Id)
			{
				return "same-person";
			}
			if (!currentPerson.IsAlive || !otherPerson.IsAlive)
			{
				return "dead-person";
			}

			return "ok";
		}

		private static string GetParentChildBlockReason(Entity parent, Entity child)
		{
			PersonData parentPerson = parent?.data?.person;
			PersonData childPerson = child?.data?.person;
			if (parentPerson == null || childPerson == null)
			{
				return "missing-person-data";
			}
			if (parent.Id == child.Id)
			{
				return "same-person";
			}
			if (!parentPerson.IsAlive || !childPerson.IsAlive)
			{
				return "dead-person";
			}

			SimTime now = global::Game.Game.ctx?.clock != null
				? global::Game.Game.ctx.clock.Now
				: SimTime.MIN_DATE;
			if (parentPerson.GetAge(now).YearsFloat < childPerson.GetAge(now).YearsFloat + 12f)
			{
				return "parent-too-young";
			}

			return "ok";
		}

		private static Entity FindTrackedPerson(EntityID id)
		{
			if (!id.IsValid)
			{
				return null;
			}

			PeopleTracker people = global::Game.Game.ctx?.simman?.peoplegen;
			if (people == null)
			{
				return null;
			}

			foreach (Entity person in people.GetAllTrackedPeople())
			{
				if (person?.Id == id)
				{
					return person;
				}
			}

			return null;
		}

		private static string IdText(EntityID id)
		{
			return id.IsValid ? id.id.ToString() : "invalid";
		}
	}
}
