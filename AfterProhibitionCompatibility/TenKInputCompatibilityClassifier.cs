using System;
using System.Collections.Generic;
using BepInEx.Logging;

namespace AfterProhibitionCompatibility
{
	internal sealed class TenKInputCompatibilityDecision
	{
		internal bool TenKInputActive { get; set; }
		internal bool UiEnhancerActive { get; set; }
		internal bool MenuSidebarOwnerActive { get; set; }
		internal bool ProtectRouteInput { get; set; }
		internal bool ProtectButtonInput { get; set; }
		internal bool ProtectMenuSidebar { get; set; }
		internal string DetectionSummary { get; set; } = "detection=unscanned";
		internal string RouteInputReason { get; set; } = "inactive";
		internal string ButtonInputReason { get; set; } = "inactive";
		internal string MenuSidebarReason { get; set; } = "inactive";
		internal string OwnerSummary { get; set; } = "owners=none";

		internal string FormatBridgeString()
		{
			return "tenk-input-classifier tenKInput=" + TenKInputActive
				+ " uiEnhancer=" + UiEnhancerActive
				+ " menuSidebar=" + MenuSidebarOwnerActive
				+ " protectRouteInput=" + ProtectRouteInput
				+ " routeReason=" + RouteInputReason
				+ " protectButtonInput=" + ProtectButtonInput
				+ " buttonReason=" + ButtonInputReason
				+ " protectMenuSidebar=" + ProtectMenuSidebar
				+ " menuSidebarReason=" + MenuSidebarReason
				+ " " + DetectionSummary
				+ " " + OwnerSummary;
		}
	}

	internal static class TenKInputCompatibilityClassifier
	{
		private static readonly HashSet<string> LoggedClassifications = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		internal static TenKInputCompatibilityDecision Classify(CompatibilitySnapshot snapshot)
		{
			snapshot = snapshot ?? CompatibilitySnapshot.Empty;
			bool tenKInput = snapshot.TenKButtonInput;
			bool uiEnhancer = snapshot.PiaUiEnhancer;
			bool menuSidebar = snapshot.ExternalMenuSidebar;
			bool inputOwner = tenKInput || uiEnhancer;
			bool directInputDll = snapshot.TenKButtonInputDll || snapshot.TenKButtonInputToken;
			bool directUiEnhancer = snapshot.UiEnhancerGuidOrDll;

			return new TenKInputCompatibilityDecision
			{
				TenKInputActive = tenKInput,
				UiEnhancerActive = uiEnhancer,
				MenuSidebarOwnerActive = menuSidebar,
				ProtectRouteInput = inputOwner,
				ProtectButtonInput = inputOwner,
				ProtectMenuSidebar = menuSidebar,
				DetectionSummary = BuildDetectionSummary(snapshot),
				RouteInputReason = inputOwner ? BuildInputReason(directInputDll, directUiEnhancer) : "inactive",
				ButtonInputReason = inputOwner ? BuildInputReason(directInputDll, directUiEnhancer) : "inactive",
				MenuSidebarReason = menuSidebar ? BuildMenuSidebarReason(snapshot) : "inactive",
				OwnerSummary = BuildOwnerSummary(snapshot)
			};
		}

		internal static void LogBaseline(CompatibilitySnapshot snapshot, ManualLogSource log, string source)
		{
			if (log == null)
			{
				return;
			}

			TenKInputCompatibilityDecision decision = Classify(snapshot);
			string key = source + "|" + decision.FormatBridgeString();
			if (!LoggedClassifications.Add(key))
			{
				return;
			}

			log.LogInfo(decision.FormatBridgeString() + " source=" + source);
		}

		private static string BuildDetectionSummary(CompatibilitySnapshot snapshot)
		{
			return "detection"
				+ " tenKDll=" + snapshot.TenKButtonInputDll
				+ " tenKToken=" + snapshot.TenKButtonInputToken
				+ " uiEnhancerGuidOrDll=" + snapshot.UiEnhancerGuidOrDll
				+ " menuSidebarToken=" + snapshot.MenuSidebarToken
				+ " piaLauncher=" + snapshot.PiaModLauncher
				+ " launcher=" + snapshot.Launcher
				+ " knownExternalDlls=" + snapshot.KnownExternalDllNames;
		}

		private static string BuildInputReason(bool directInputDll, bool directUiEnhancer)
		{
			if (directInputDll && directUiEnhancer)
			{
				return "external-10k-and-ui-enhancer-input-owner";
			}

			if (directInputDll)
			{
				return "external-10k-input-owner";
			}

			if (directUiEnhancer)
			{
				return "external-ui-enhancer-input-owner";
			}

			return "external-route-input-owner";
		}

		private static string BuildMenuSidebarReason(CompatibilitySnapshot snapshot)
		{
			if (snapshot.UiEnhancerGuidOrDll && snapshot.PiaModLauncher)
			{
				return "external-ui-enhancer-and-launcher-menu-sidebar-owner";
			}

			if (snapshot.UiEnhancerGuidOrDll)
			{
				return "external-ui-enhancer-menu-sidebar-owner";
			}

			if (snapshot.PiaModLauncher || snapshot.Launcher)
			{
				return "external-launcher-menu-sidebar-owner";
			}

			if (snapshot.MenuSidebarToken)
			{
				return "external-menu-sidebar-token";
			}

			return "external-menu-sidebar-owner";
		}

		private static string BuildOwnerSummary(CompatibilitySnapshot snapshot)
		{
			var owners = new List<string>();
			if (snapshot.TenKButtonInputDll || snapshot.TenKButtonInputToken)
			{
				owners.Add("10KButtonInput");
			}

			if (snapshot.UiEnhancerGuidOrDll)
			{
				owners.Add("PIAUiEnhancer");
			}

			if (snapshot.MenuSidebarToken)
			{
				owners.Add("MenuSidebarToken");
			}

			if (snapshot.PiaModLauncher)
			{
				owners.Add("PIAModLauncher");
			}

			if (snapshot.Launcher)
			{
				owners.Add("Launcher");
			}

			if (snapshot.ExternalMenuSidebar && !owners.Contains("MenuSidebarToken") && !owners.Contains("PIAUiEnhancer") && !owners.Contains("PIAModLauncher") && !owners.Contains("Launcher"))
			{
				owners.Add("ExternalMenuSidebar");
			}

			return owners.Count > 0 ? "owners=" + string.Join(",", owners.ToArray()) : "owners=none";
		}
	}
}
