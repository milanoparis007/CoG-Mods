using System;
using System.Reflection;
using Game.Core;
using Game.Session.Entities;
using Game.Session.Player;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AfterProhibitionUI
{
	internal static class CrewManagementJailVisualPatch
	{
		private static readonly BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

		public static int ApplyPatch(Harmony harmony)
		{
			int patched = 0;
			Type crewManagementPopup = typeof(Entity).Assembly.GetType("Game.UI.Session.Crew.CrewManagementPopup");
			Type crewInfoGen = typeof(Entity).Assembly.GetType("Game.UI.Session.Crew.CrewInfoGen");

			MethodInfo setCardDesc = crewManagementPopup?.GetMethod("SetCardDesc", InstanceFlags);
			if (setCardDesc != null)
			{
				harmony.Patch(setCardDesc, postfix: new HarmonyMethod(typeof(CrewManagementJailVisualPatch), nameof(SetCardDescPostfix)));
				patched++;
			}

			MethodInfo getArrestedDesc = crewManagementPopup?.GetMethod("GetArrestedDesc", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
			if (getArrestedDesc != null)
			{
				harmony.Patch(getArrestedDesc, prefix: new HarmonyMethod(typeof(CrewManagementJailVisualPatch), nameof(GetArrestedDescPrefix)));
				patched++;
			}

			MethodInfo describeCrewPeep = crewInfoGen?.GetMethod("DescribeCrewPeep", InstanceFlags);
			if (describeCrewPeep != null)
			{
				harmony.Patch(describeCrewPeep, postfix: new HarmonyMethod(typeof(CrewManagementJailVisualPatch), nameof(DescribeCrewPeepPostfix)));
				patched++;
			}

			AfterProhibitionUIPlugin.Log?.LogInfo("CrewManagementJailVisuals patched=" + patched + " owner=AfterProhibitionUI");
			return patched;
		}

		private static void SetCardDescPostfix(GameObject __0, object __1)
		{
			if (!TryGetCrewAssignment(__1, out CrewAssignment crew))
			{
				return;
			}

			if (CrewInfoActionBridge.TryGetCrewAssignmentBlockReason(crew.peepId, out string jailStatus))
			{
				SetCardText(__0, "Description", jailStatus);
			}
		}

		private static void DescribeCrewPeepPostfix(object __instance, ref string __result)
		{
			if (!TryGetCrewInfoGenAssignment(__instance, out CrewAssignment crew))
			{
				return;
			}

			if (CrewInfoActionBridge.TryGetCrewAssignmentBlockReason(crew.peepId, out string jailStatus))
			{
				__result = jailStatus;
			}
		}

		private static bool GetArrestedDescPrefix(EntityID peepId, ref string __result)
		{
			if (!CrewInfoActionBridge.TryGetCrewAssignmentBlockReason(peepId, out string jailStatus))
			{
				return true;
			}

			__result = jailStatus;
			return false;
		}

		private static bool TryGetCrewInfoGenAssignment(object crewInfoGen, out CrewAssignment crew)
		{
			crew = CrewAssignment.EMPTY;
			object data = AccessTools.Field(crewInfoGen?.GetType(), "data")?.GetValue(crewInfoGen);
			if (data == null)
			{
				return false;
			}

			object rawCrew = AccessTools.Field(data.GetType(), "crew")?.GetValue(data);
			if (rawCrew is CrewAssignment assignment && assignment.IsValid && assignment.peepId.IsValid)
			{
				crew = assignment;
				return true;
			}

			return false;
		}

		private static bool TryGetCrewAssignment(object entry, out CrewAssignment crew)
		{
			crew = CrewAssignment.EMPTY;
			FieldInfo crewField = entry?.GetType().GetField("crew", InstanceFlags);
			object rawCrew = crewField?.GetValue(entry);
			if (rawCrew is CrewAssignment assignment && assignment.IsValid && assignment.peepId.IsValid)
			{
				crew = assignment;
				return true;
			}

			return false;
		}

		private static void SetCardText(GameObject card, string childPath, string value)
		{
			if (card == null || string.IsNullOrWhiteSpace(value))
			{
				return;
			}

			Transform target = card.transform.Find(childPath);
			if (target == null)
			{
				return;
			}

			TMP_Text tmp = target.GetComponent<TMP_Text>();
			if (tmp != null)
			{
				tmp.text = value;
				return;
			}

			Text text = target.GetComponent<Text>();
			if (text != null)
			{
				text.text = value;
			}
		}
	}
}
