using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using BepInEx;
using Game.Services;
using Game.Session.Board;
using Game.Session.Sim;
using HarmonyLib;
using UnityEngine;

namespace GameplayTweaks
{
public partial class GameplayTweaksPlugin
{
	internal static class AfterProhibitionAssemblyPortPatches
	{
		private const int DefaultDirtyCashGoal = 50000;
		private const int DefaultCounterfeitGoal = 50;
		private const int DefaultStolenGoodsGoal = 20;

		private static readonly FieldInfo SeasonOverrideHourField = AccessTools.Field(typeof(SeasonManager), "_overrideHour");
		private static readonly FieldInfo LocalizationLocDataField = AccessTools.Field(typeof(LocalizationService), "_locdata");
		private static readonly MethodInfo SeasonGetSecondsMethod = AccessTools.Method(typeof(SeasonManager), "GetSeconds");
		private static readonly Regex VictoryAmountRegex = new Regex("^\\s*(amtDirtyCash|amtCounterfeit|amtStolenGoods)\\s+([0-9]+(?:\\.[0-9]+)?)\\s*$", RegexOptions.Compiled);

		private static bool portraitsPatched;
		private static bool settingsLoaded;
		private static bool victoryLocSanitized;
		private static int dirtyCashGoal = DefaultDirtyCashGoal;
		private static int counterfeitGoal = DefaultCounterfeitGoal;
		private static int stolenGoodsGoal = DefaultStolenGoodsGoal;

		public static void ApplyPatches(Harmony harmony)
		{
			try
			{
				MethodInfo initializeGoals = AccessTools.Method(typeof(VictoryTracker), "InitializeGoals");
				if (initializeGoals != null)
				{
					harmony.Patch(initializeGoals, postfix: new HarmonyMethod(typeof(AfterProhibitionAssemblyPortPatches), nameof(InitializeGoalsPostfix)));
				}

				MethodInfo findDisplayedTimeOfDay = AccessTools.Method(typeof(SeasonManager), nameof(SeasonManager.FindDisplayedTimeOfDay));
				if (findDisplayedTimeOfDay != null)
				{
					harmony.Patch(findDisplayedTimeOfDay, postfix: new HarmonyMethod(typeof(AfterProhibitionAssemblyPortPatches), nameof(FindDisplayedTimeOfDayPostfix)));
				}

				ApplyPortraitTableEdits();
				Debug.Log("[GameplayTweaks] After Prohibition Assembly-CSharp portability patches applied");
			}
			catch (Exception ex)
			{
				Debug.LogError("[GameplayTweaks] Failed to apply After Prohibition portability patches: " + ex);
			}
		}

		private static void InitializeGoalsPostfix(ref List<VictoryCategory> __result)
		{
			try
			{
				if (__result == null || __result.Count == 0)
				{
					return;
				}

				SanitizeVictoryLocalization();

				List<VictoryGoal> allGoals = __result.SelectMany(cat => cat.goals ?? Enumerable.Empty<VictoryGoal>()).ToList();
				if (allGoals.Any(goal => goal != null && goal.locname == "victory.dirtycash.goal.name"))
				{
					return;
				}

				VictoryCategory targetCategory = FindEconomyVictoryCategory(__result);
				if (targetCategory == null)
				{
					Debug.LogWarning("[GameplayTweaks] After Prohibition victory goal patch skipped: could not find economy category");
					return;
				}

				LoadVictoryGoalsFromSettings();
				if (targetCategory.goals == null)
				{
					targetCategory.goals = new List<VictoryGoal>();
				}

				targetCategory.goals.Add(new VictoryGoal(
					"victory.dirtycash.goal.name",
					"victory.dirtycash.goal.desc",
					Loc.FormatNumber(dirtyCashGoal),
					VictoryTracker.visDefault,
					new VictorySpecificSaleSubgoal("victory.dirtycash.1sub.desc", "counterfeitcash", dirtyCashGoal),
					new VictorySpecificSaleSubgoal("victory.dirtycash.2sub.desc", "dirty-cash", dirtyCashGoal)));

				targetCategory.goals.Add(new VictoryGoal(
					"victory.drugs.goal.name",
					"victory.drugs.goal.desc",
					Loc.FormatNumber(counterfeitGoal),
					VictoryTracker.visDefault,
					new VictorySpecificSaleSubgoal("victory.drugs.1sub.desc", "cannabis-pound", counterfeitGoal),
					new VictorySpecificSaleSubgoal("victory.drugs.2sub.desc", "heroin-packs", counterfeitGoal)));

				targetCategory.goals.Add(new VictoryGoal(
					"victory.homes.goal.name",
					"victory.homes.goal.desc",
					Loc.FormatNumber(stolenGoodsGoal),
					VictoryTracker.visDefault,
					new VictorySpecificSaleSubgoal("victory.homes.1sub.desc", "bandlow", stolenGoodsGoal),
					new VictorySpecificSaleSubgoal("victory.homes.2sub.desc", "homemid", stolenGoodsGoal)));
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] After Prohibition victory goal patch failed: " + ex.Message);
			}
		}

		private static void SanitizeVictoryLocalization()
		{
			if (victoryLocSanitized)
			{
				return;
			}

			victoryLocSanitized = true;
			try
			{
				LocalizationService localizationService = Loc.instance;
				object locSetCollection = LocalizationLocDataField?.GetValue(localizationService);
				if (locSetCollection == null)
				{
					return;
				}

				object builtins = AccessTools.Field(locSetCollection.GetType(), "builtins")?.GetValue(locSetCollection);
				IDictionary languageData = AccessTools.Field(builtins?.GetType(), "langdata")?.GetValue(builtins) as IDictionary;
				if (languageData == null)
				{
					return;
				}

				int sanitizedCount = 0;
				foreach (DictionaryEntry languageEntry in languageData)
				{
					IDictionary localizedEntries = languageEntry.Value as IDictionary;
					if (localizedEntries == null)
					{
						continue;
					}

					foreach (DictionaryEntry localizedEntry in localizedEntries)
					{
						string key = localizedEntry.Key as string;
						IList values = localizedEntry.Value as IList;
						if (string.IsNullOrEmpty(key) || values == null || !key.StartsWith("victory.", StringComparison.Ordinal))
						{
							continue;
						}

						for (int i = 0; i < values.Count; i++)
						{
							string original = values[i] as string;
							string sanitized = SanitizeVictoryLocValue(original);
							if (!string.Equals(original, sanitized, StringComparison.Ordinal))
							{
								values[i] = sanitized;
								sanitizedCount++;
							}
						}
					}
				}

				if (sanitizedCount > 0)
				{
					Debug.Log("[GameplayTweaks] Sanitized " + sanitizedCount.ToString(CultureInfo.InvariantCulture) + " victory localization entries with invalid glyphs");
				}
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] Victory localization sanitization failed: " + ex.Message);
			}
		}

		private static string SanitizeVictoryLocValue(string value)
		{
			if (string.IsNullOrEmpty(value) || value.IndexOf('\uFFFD') < 0)
			{
				return value;
			}

			return value.Replace('\uFFFD', ' ');
		}

		private static VictoryCategory FindEconomyVictoryCategory(List<VictoryCategory> categories)
		{
			if (categories == null || categories.Count == 0)
			{
				return null;
			}

			VictoryCategory directMatch = categories.FirstOrDefault(cat => string.Equals(cat?.locname, "victory.category.3.name", StringComparison.Ordinal));
			if (directMatch != null)
			{
				return directMatch;
			}

			return categories.FirstOrDefault(cat =>
				cat?.goals != null &&
				cat.goals.Any(goal =>
					goal != null &&
					(
						string.Equals(goal.locname, "victory.producer.goal.name", StringComparison.Ordinal) ||
						string.Equals(goal.locname, "victory.consumer.goal.name", StringComparison.Ordinal) ||
						string.Equals(goal.locname, "victory.trade-hotel.goal.name", StringComparison.Ordinal) ||
						string.Equals(goal.locname, "victory.trade-cincinnati.goal.name", StringComparison.Ordinal))));
		}

		private static void FindDisplayedTimeOfDayPostfix(SeasonManager __instance, ref float __result)
		{
			try
			{
				if (SeasonOverrideHourField == null || SeasonGetSecondsMethod == null)
				{
					return;
				}

				float overrideHour = Convert.ToSingle(SeasonOverrideHourField.GetValue(__instance), CultureInfo.InvariantCulture);
				if (overrideHour >= 0f)
				{
					return;
				}

				float seconds = Convert.ToSingle(SeasonGetSecondsMethod.Invoke(__instance, null), CultureInfo.InvariantCulture);
				__result = seconds * 0.2f % 24f;
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] After Prohibition season speed patch failed: " + ex.Message);
			}
		}

		private static void ApplyPortraitTableEdits()
		{
			if (portraitsPatched)
			{
				return;
			}

			try
			{
				if (PortraitMakerService.BUSTS_FEDS.TryGetValue(Skin.Light, out PortraitMakerService.GenderKeyedDictionary lightFedBusts) &&
					lightFedBusts != null &&
					lightFedBusts.TryGetValue(Gender.F, out List<PortraitMakerService.Piece> femaleFedBusts) &&
					femaleFedBusts != null &&
					femaleFedBusts.Count > 0)
				{
					femaleFedBusts[0] = new PortraitMakerService.Piece("Char_F_White_Bust2", null, null, null);
				}

				if (PortraitMakerService.HATS_FEDS.TryGetValue(Gender.F, out List<PortraitMakerService.Piece> femaleFedHats) &&
					femaleFedHats != null &&
					femaleFedHats.Count >= 2)
				{
					femaleFedHats[0] = new PortraitMakerService.Piece("Char_M_Hat1", null, null, null);
					femaleFedHats[1] = new PortraitMakerService.Piece("Char_M_Hat2", null, null, null);
				}

				portraitsPatched = true;
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] After Prohibition portrait patch failed: " + ex.Message);
			}
		}

		private static void LoadVictoryGoalsFromSettings()
		{
			if (settingsLoaded)
			{
				return;
			}

			settingsLoaded = true;
			try
			{
				string settingsPath = Path.Combine(Paths.GameRootPath, "CoG_Data", "StreamingAssets", "Settings", "Settings.sim");
				if (!File.Exists(settingsPath))
				{
					Debug.LogWarning("[GameplayTweaks] After Prohibition settings file not found, using default victory thresholds");
					return;
				}

				foreach (string line in File.ReadLines(settingsPath))
				{
					Match match = VictoryAmountRegex.Match(line);
					if (!match.Success)
					{
						continue;
					}

					if (!double.TryParse(match.Groups[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed))
					{
						continue;
					}

					int value = Math.Max(0, (int)Math.Round(parsed, MidpointRounding.AwayFromZero));
					switch (match.Groups[1].Value)
					{
					case "amtDirtyCash":
						dirtyCashGoal = value;
						break;
					case "amtCounterfeit":
						counterfeitGoal = value;
						break;
					case "amtStolenGoods":
						stolenGoodsGoal = value;
						break;
					}
				}
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] After Prohibition settings parse failed, using defaults: " + ex.Message);
			}
		}
	}
}
}
