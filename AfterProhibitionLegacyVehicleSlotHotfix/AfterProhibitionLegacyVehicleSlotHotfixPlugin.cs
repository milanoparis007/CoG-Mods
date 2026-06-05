using System;
using System.Collections;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;

namespace AfterProhibitionLegacyVehicleSlotHotfix
{
	[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
	[BepInDependency("com.mods.gameplaytweaks", BepInDependency.DependencyFlags.HardDependency)]
	public sealed class AfterProhibitionLegacyVehicleSlotHotfixPlugin : BaseUnityPlugin
	{
		public const string PluginGuid = "afterprohibition.legacyvehicleslothotfix";
		public const string PluginName = "After Prohibition Legacy Vehicle Slot Hotfix";
		public const string PluginVersion = "1.0.0";

		private static readonly Tuple<string, int>[] SlotAliases =
		{
			Tuple.Create("vehicle-town-car", 4),
			Tuple.Create("vehicle-car-preorder", 4),
			Tuple.Create("vehicle-bulletproof-car", 4),
			Tuple.Create("vehicle-bulletproof-jaguar", 2),
		};

		private static ManualLogSource Log;

		private ConfigEntry<bool> _enableHotfix;
		private bool _applied;

		private void Awake()
		{
			Log = Logger;
			_enableHotfix = Config.Bind(
				"Features",
				"EnableVehicleSlotAliasHotfix",
				true,
				"Adds missing vanilla vehicle template aliases to legacy GameplayTweaks multi-crew slot lookup.");

			TryApply("awake");
		}

		private IEnumerator Start()
		{
			yield return null;
			TryApply("start");
		}

		private void TryApply(string source)
		{
			if (_applied || !_enableHotfix.Value)
			{
				return;
			}

			try
			{
				Type helperType = FindType("GameplayTweaks.MultiCrewVehicleHelper");
				if (helperType == null)
				{
					Logger.LogWarning("vehicle-slot-hotfix skipped source=" + source + " reason=helper-type-missing");
					return;
				}

				FieldInfo field = helperType.GetField("VehicleCrewSlots", BindingFlags.Static | BindingFlags.NonPublic);
				if (field == null)
				{
					Logger.LogWarning("vehicle-slot-hotfix skipped source=" + source + " reason=slot-field-missing");
					return;
				}

				IDictionary slots = field.GetValue(null) as IDictionary;
				if (slots == null)
				{
					Logger.LogWarning("vehicle-slot-hotfix skipped source=" + source + " reason=slot-field-not-dictionary");
					return;
				}

				int added = 0;
				int updated = 0;
				foreach (Tuple<string, int> alias in SlotAliases)
				{
					if (slots.Contains(alias.Item1))
					{
						object existing = slots[alias.Item1];
						if (!(existing is int existingSlots) || existingSlots != alias.Item2)
						{
							slots[alias.Item1] = alias.Item2;
							updated++;
						}
					}
					else
					{
						slots.Add(alias.Item1, alias.Item2);
						added++;
					}
				}

				_applied = true;
				Logger.LogInfo("vehicle-slot-hotfix applied source=" + source + " added=" + added + " updated=" + updated);
			}
			catch (Exception ex)
			{
				Logger.LogWarning("vehicle-slot-hotfix failed source=" + source + " error=" + ex.GetType().Name + ": " + ex.Message);
			}
		}

		private static Type FindType(string fullName)
		{
			foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
			{
				Type type = assembly.GetType(fullName, throwOnError: false);
				if (type != null)
				{
					return type;
				}
			}

			return null;
		}
	}
}
