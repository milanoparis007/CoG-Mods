using System;
using System.Collections.Generic;
using BepInEx.Logging;

namespace AfterProhibitionCompatibility
{
	internal sealed class BroadUnpatchDecision
	{
		internal string Owner { get; set; } = "";
		internal string NormalizedOwner { get; set; } = "";
		internal string Action { get; set; } = "ignore";
		internal string Reason { get; set; } = "unknown-owner";
		internal string Category { get; set; } = "unknown";
		internal bool Protect { get; set; }

		internal BroadUnpatchDecision WithDisabledClassification()
		{
			return new BroadUnpatchDecision
			{
				Owner = Owner,
				NormalizedOwner = NormalizedOwner,
				Action = "ignore",
				Reason = "classification-disabled",
				Category = Category,
				Protect = false
			};
		}

		internal string FormatBridgeString()
		{
			return "broad-unpatch owner=" + Owner
				+ " action=" + Action
				+ " reason=" + Reason
				+ " category=" + Category
				+ " protect=" + Protect;
		}
	}

	internal static class BroadUnpatchGuard
	{
		private static readonly HashSet<string> LoggedDecisions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		internal static BroadUnpatchDecision Classify(string owner)
		{
			string displayOwner = string.IsNullOrWhiteSpace(owner) ? "(empty)" : owner.Trim();
			string normalizedOwner = NormalizeOwner(owner);

			if (string.IsNullOrEmpty(normalizedOwner))
			{
				return Ignore(displayOwner, normalizedOwner, "empty-owner", "unknown");
			}

			if (ContainsAny(normalizedOwner, "afterprohibition", "gameplaytweaks", "comcogmodgameplaytweaks"))
			{
				return Ignore(displayOwner, normalizedOwner, "own-plugin", "after-prohibition");
			}

			if (ContainsAny(normalizedOwner, "dirtycasheconomy", "comcogmoddirtycasheconomy"))
			{
				return Protect(displayOwner, normalizedOwner, "route-input", "dirty-cash-economy");
			}

			if (ContainsAny(normalizedOwner, "dirtycashvolumefix"))
			{
				return Protect(displayOwner, normalizedOwner, "dirty-cash-volume", "dirty-cash-volume");
			}

			if (ContainsAny(normalizedOwner, "10kbutton", "10kinput", "10kbuttoninput", "uienhancer", "compiacityofgangstersuienhancer"))
			{
				return Protect(displayOwner, normalizedOwner, "button-input", "route-input");
			}

			if (ContainsAny(normalizedOwner, "safebox", "modlauncher", "prohibitionlauncher", "traplifemodlauncher", "compiamodlauncher", "commodsmodlauncher"))
			{
				return Protect(displayOwner, normalizedOwner, "safebox-ui", "launcher-ui");
			}

			if (ContainsAny(normalizedOwner, "cogcustomassets", "customassets"))
			{
				return Protect(displayOwner, normalizedOwner, "custom-assets", "asset-loader");
			}

			if (ContainsAny(normalizedOwner, "customportraits"))
			{
				return Protect(displayOwner, normalizedOwner, "custom-portraits", "asset-loader");
			}

			if (ContainsAny(normalizedOwner, "gangwars", "territoryexpansion", "territoryautoexpand"))
			{
				return Protect(displayOwner, normalizedOwner, "territory-state", "map-ai");
			}

			if (ContainsAny(normalizedOwner, "tickerenhancer", "outpostticker"))
			{
				return Protect(displayOwner, normalizedOwner, "ticker-ui", "ui");
			}

			if (ContainsAny(normalizedOwner, "externalcheatmenu", "cheatmenu", "cogcheat", "electionmanager", "bossmanager"))
			{
				return Protect(displayOwner, normalizedOwner, "external-menu", "cheat-menu");
			}

			return Ignore(displayOwner, normalizedOwner, "unknown-owner", "unknown");
		}

		internal static bool ShouldProtect(string owner)
		{
			return Classify(owner).Protect;
		}

		internal static string GetAction(string owner)
		{
			return Classify(owner).Action;
		}

		internal static string GetReason(string owner)
		{
			return Classify(owner).Reason;
		}

		internal static void LogBaseline(CompatibilitySnapshot snapshot, ManualLogSource log, string source)
		{
			if (snapshot == null || log == null)
			{
				return;
			}

			var owners = new List<string>();
			if (snapshot.DirtyCashEconomy)
			{
				owners.Add("DirtyCashEconomy");
			}

			if (snapshot.DirtyCashVolumeFix)
			{
				owners.Add("DirtyCashVolumeFix");
			}

			if (snapshot.TenKButtonInput || snapshot.PiaUiEnhancer)
			{
				owners.Add("10KButtonInput");
			}

			if (snapshot.ExternalSafeboxUi || snapshot.Launcher)
			{
				owners.Add("SafeboxLauncher");
			}

			if (snapshot.CoGCustomAssets)
			{
				owners.Add("CoGCustomAssets");
			}

			if (snapshot.CustomPortraits)
			{
				owners.Add("CustomPortraits");
			}

			if (snapshot.GangWars)
			{
				owners.Add("GangWars");
			}

			if (snapshot.TerritoryExpansion)
			{
				owners.Add("TerritoryExpansion");
			}

			if (snapshot.TickerEnhancer)
			{
				owners.Add("TickerEnhancer");
			}

			if (snapshot.CoreCheatMenu || snapshot.ElectionCheat || snapshot.BossManagerCheat || snapshot.BlockedCheatPlugins)
			{
				owners.Add("ExternalCheatMenu");
			}

			owners.Add("GameplayTweaks");

			foreach (string owner in owners)
			{
				LogDecision(Classify(owner), log, source);
			}
		}

		private static void LogDecision(BroadUnpatchDecision decision, ManualLogSource log, string source)
		{
			string key = source + "|" + decision.Owner + "|" + decision.Action + "|" + decision.Reason;
			if (!LoggedDecisions.Add(key))
			{
				return;
			}

			log.LogInfo("broad-unpatch guard owner=" + decision.Owner
				+ " action=" + decision.Action
				+ " reason=" + decision.Reason
				+ " category=" + decision.Category
				+ " source=" + source);
		}

		private static BroadUnpatchDecision Protect(string owner, string normalizedOwner, string reason, string category)
		{
			return new BroadUnpatchDecision
			{
				Owner = owner,
				NormalizedOwner = normalizedOwner,
				Action = "protect",
				Reason = reason,
				Category = category,
				Protect = true
			};
		}

		private static BroadUnpatchDecision Ignore(string owner, string normalizedOwner, string reason, string category)
		{
			return new BroadUnpatchDecision
			{
				Owner = owner,
				NormalizedOwner = normalizedOwner,
				Action = "ignore",
				Reason = reason,
				Category = category,
				Protect = false
			};
		}

		private static bool ContainsAny(string value, params string[] tokens)
		{
			foreach (string token in tokens)
			{
				if (!string.IsNullOrEmpty(token) && value.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
				{
					return true;
				}
			}

			return false;
		}

		private static string NormalizeOwner(string owner)
		{
			if (string.IsNullOrWhiteSpace(owner))
			{
				return "";
			}

			var chars = new char[owner.Length];
			int index = 0;
			foreach (char ch in owner)
			{
				if (char.IsLetterOrDigit(ch))
				{
					chars[index++] = char.ToLowerInvariant(ch);
				}
			}

			return new string(chars, 0, index);
		}
	}
}
