using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TMPro;

namespace AfterProhibitionUI
{
	internal static class TmpReplacementCharacterSanitizerPatch
	{
		private static readonly HashSet<MethodBase> PatchedSetters = new HashSet<MethodBase>();
		private static readonly HashSet<MethodBase> PatchedSetTextMethods = new HashSet<MethodBase>();

		public static int ApplyPatch(Harmony harmony)
		{
			try
			{
				int patched = 0;
				patched += PatchTextSetter(harmony, typeof(TMP_Text));
				patched += PatchTextSetter(harmony, typeof(TextMeshProUGUI));
				patched += PatchTextSetter(harmony, typeof(TextMeshPro));
				patched += PatchSetTextMethods(harmony, typeof(TMP_Text));
				patched += PatchSetTextMethods(harmony, typeof(TextMeshProUGUI));
				patched += PatchSetTextMethods(harmony, typeof(TextMeshPro));
				AfterProhibitionUIPlugin.Log?.LogInfo("UISanitize TMP replacement-character sanitizer patched=" + patched);
				return patched;
			}
			catch (Exception ex)
			{
				AfterProhibitionUIPlugin.Log?.LogWarning("TmpReplacementCharacterSanitizerPatch failed: " + ex.Message);
				return 0;
			}
		}

		private static int PatchTextSetter(Harmony harmony, Type type)
		{
			MethodInfo setter = AccessTools.PropertySetter(type, "text");
			if (setter == null || !PatchedSetters.Add(setter))
			{
				return 0;
			}

			harmony.Patch(setter, prefix: new HarmonyMethod(typeof(TmpReplacementCharacterSanitizerPatch), nameof(TextSetterPrefix)));
			return 1;
		}

		private static int PatchSetTextMethods(Harmony harmony, Type type)
		{
			int patched = 0;
			foreach (MethodInfo method in type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
			{
				if (!string.Equals(method.Name, "SetText", StringComparison.Ordinal))
				{
					continue;
				}

				ParameterInfo[] parameters = method.GetParameters();
				if (parameters.Length == 0 || parameters[0].ParameterType != typeof(string) || !PatchedSetTextMethods.Add(method))
				{
					continue;
				}

				harmony.Patch(method, prefix: new HarmonyMethod(typeof(TmpReplacementCharacterSanitizerPatch), nameof(SetTextPrefix)));
				patched++;
			}

			return patched;
		}

		private static void TextSetterPrefix(ref string value)
		{
			if (string.IsNullOrEmpty(value) || value.IndexOf('\uFFFD') < 0)
			{
				return;
			}

			value = value.Replace("\uFFFD", string.Empty);
		}

		private static void SetTextPrefix(ref string __0)
		{
			TextSetterPrefix(ref __0);
		}
	}
}
