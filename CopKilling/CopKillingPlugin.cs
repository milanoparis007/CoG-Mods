using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using BepInEx;
using BepInEx.Configuration;
using Game.Core;
using Game.Session.Board;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Player;
using Game.Session.Sim;
using HarmonyLib;
using UnityEngine;

namespace CopKilling
{
    [BepInPlugin("com.mods.copkilling", "Cop Killing", "1.0.0")]
    [BepInDependency("com.mods.gameplaytweaks")]
    public class CopKillingPlugin : BaseUnityPlugin
    {
        internal static ConfigEntry<bool> EnableRuntimeFeatures;
        internal static ConfigEntry<bool> EnableCombatNameDisplayFeatures;
        internal static ConfigEntry<bool> EnableAttackSuppressionFeatures;
        internal static ConfigEntry<bool> EnableAttackSuppressionIsAttackAllowedPatch;
        internal static ConfigEntry<bool> EnableAttackSuppressionTryPickBuildingGuard;
        internal static ConfigEntry<bool> EnableAttackSuppressionAttackAdvisorOnTurnUpdateGuard;
        internal static ConfigEntry<bool> EnableAttackSuppressionMaybeAskForTruceGuard;
        internal static ConfigEntry<bool> EnableAttackSuppressionCombatAdvisorOnTurnUpdateGuard;
        internal static ConfigEntry<bool> EnableAttackSuppressionUpdateRequestsAfterAggroGuard;
        internal static ConfigEntry<bool> EnableCombatIncidentRuntimeFeatures;
        internal static ConfigEntry<bool> EnableHumanCopTargetingFeatures;
        internal static ConfigEntry<bool> EnableCrewPickTintFeatures;
        internal static ConfigEntry<bool> EnableTurnStartReconciliationFeatures;
        internal static ConfigEntry<bool> EnableCombatPostfixDiagnosticFeatures;
        internal static ConfigEntry<KeyboardShortcut> DebugResetCopWarHotkey;
        internal static ConfigEntry<KeyboardShortcut> DebugSimulateCopKillHotkey;
        internal static ConfigEntry<KeyboardShortcut> DebugSimulateCopAssaultHotkey;

        private static bool IsRuntimeEnabled()
        {
            return EnableRuntimeFeatures?.Value ?? false;
        }

        private static bool IsAttackSuppressionRequested()
        {
            return EnableAttackSuppressionFeatures?.Value ?? false;
        }

        internal static bool IsAttackSuppressionIsAttackAllowedEnabled()
        {
            return IsAttackSuppressionRequested() && (EnableAttackSuppressionIsAttackAllowedPatch?.Value ?? true);
        }

        internal static bool IsAttackSuppressionTryPickBuildingEnabled()
        {
            return IsAttackSuppressionRequested() && (EnableAttackSuppressionTryPickBuildingGuard?.Value ?? true);
        }

        internal static bool IsAttackSuppressionAttackAdvisorOnTurnUpdateEnabled()
        {
            return IsAttackSuppressionRequested() && (EnableAttackSuppressionAttackAdvisorOnTurnUpdateGuard?.Value ?? true);
        }

        internal static bool IsAttackSuppressionMaybeAskForTruceEnabled()
        {
            return IsAttackSuppressionRequested() && (EnableAttackSuppressionMaybeAskForTruceGuard?.Value ?? true);
        }

        internal static bool IsAttackSuppressionCombatAdvisorOnTurnUpdateEnabled()
        {
            return IsAttackSuppressionRequested() && (EnableAttackSuppressionCombatAdvisorOnTurnUpdateGuard?.Value ?? true);
        }

        internal static bool IsAttackSuppressionUpdateRequestsAfterAggroEnabled()
        {
            return IsAttackSuppressionRequested() && (EnableAttackSuppressionUpdateRequestsAfterAggroGuard?.Value ?? true);
        }

        internal static bool IsAnyAttackSuppressionHookEnabled()
        {
            return IsAttackSuppressionIsAttackAllowedEnabled()
                || IsAttackSuppressionTryPickBuildingEnabled()
                || IsAttackSuppressionAttackAdvisorOnTurnUpdateEnabled()
                || IsAttackSuppressionMaybeAskForTruceEnabled()
                || IsAttackSuppressionCombatAdvisorOnTurnUpdateEnabled()
                || IsAttackSuppressionUpdateRequestsAfterAggroEnabled();
        }

        internal static string GetAttackSuppressionHookSummary()
        {
            var hooks = new List<string>();
            if (IsAttackSuppressionIsAttackAllowedEnabled())
            {
                hooks.Add("IsAttackAllowed");
            }
            if (IsAttackSuppressionTryPickBuildingEnabled())
            {
                hooks.Add("TryPickBuilding");
            }
            if (IsAttackSuppressionAttackAdvisorOnTurnUpdateEnabled())
            {
                hooks.Add("AttackAdvisor.OnTurnUpdate");
            }
            if (IsAttackSuppressionMaybeAskForTruceEnabled())
            {
                hooks.Add("MaybeAskForTruce");
            }
            if (IsAttackSuppressionCombatAdvisorOnTurnUpdateEnabled())
            {
                hooks.Add("CombatAdvisor.OnTurnUpdate");
            }
            if (IsAttackSuppressionUpdateRequestsAfterAggroEnabled())
            {
                hooks.Add("UpdateRequestsAfterAggro");
            }
            return hooks.Count == 0 ? "none" : string.Join(",", hooks);
        }

        private static readonly KeyboardShortcut SafeResetCopWarHotkey = new KeyboardShortcut(KeyCode.F8, KeyCode.LeftControl, KeyCode.LeftShift);
        private static readonly KeyboardShortcut SafeSimulateCopKillHotkey = new KeyboardShortcut(KeyCode.F9, KeyCode.LeftControl, KeyCode.LeftShift);
        private static readonly KeyboardShortcut SafeSimulateCopAssaultHotkey = new KeyboardShortcut(KeyCode.F12, KeyCode.LeftControl, KeyCode.LeftShift);

        private void Awake()
        {
            EnableRuntimeFeatures = Config.Bind("FeatureGates", "EnableRuntimeFeatures", false, "Enable CopKilling runtime hooks. Default false while stabilizing startup/load crashes.");
            EnableCombatNameDisplayFeatures = Config.Bind("FeatureGates", "EnableCombatNameDisplayFeatures", true, "Enable the combat name display patch while deeper CopKilling runtime hooks stay disabled.");
            EnableAttackSuppressionFeatures = Config.Bind("FeatureGates", "EnableAttackSuppressionFeatures", true, "Enable the supported cop attack-suppression subset while deeper CopKilling runtime hooks stay disabled. The stable default keeps TryPickBuilding, AttackAdvisor.OnTurnUpdate, MaybeAskForTruce, CombatAdvisor.OnTurnUpdate, and UpdateRequestsAfterAggro on, while leaving IsAttackAllowed off.");
            EnableAttackSuppressionIsAttackAllowedPatch = Config.Bind("FeatureGates", "EnableAttackSuppressionIsAttackAllowedPatch", false, "When EnableAttackSuppressionFeatures is on, patch CombatAdvisor.IsAttackAllowed. Default false because isolated AS1 testing reproduced a native crash with this hook alone; re-enable only for focused debugging.");
            EnableAttackSuppressionTryPickBuildingGuard = Config.Bind("FeatureGates", "EnableAttackSuppressionTryPickBuildingGuard", true, "When EnableAttackSuppressionFeatures is on, patch AttackAdvisor.TryPickBuilding. Default true as part of the stable suppression subset.");
            EnableAttackSuppressionAttackAdvisorOnTurnUpdateGuard = Config.Bind("FeatureGates", "EnableAttackSuppressionAttackAdvisorOnTurnUpdateGuard", true, "When EnableAttackSuppressionFeatures is on, patch AttackAdvisor.OnTurnUpdate. Default true as part of the stable suppression subset.");
            EnableAttackSuppressionMaybeAskForTruceGuard = Config.Bind("FeatureGates", "EnableAttackSuppressionMaybeAskForTruceGuard", true, "When EnableAttackSuppressionFeatures is on, patch CombatAdvisor.MaybeAskForTruce. Default true as part of the stable suppression subset.");
            EnableAttackSuppressionCombatAdvisorOnTurnUpdateGuard = Config.Bind("FeatureGates", "EnableAttackSuppressionCombatAdvisorOnTurnUpdateGuard", true, "When EnableAttackSuppressionFeatures is on, patch CombatAdvisor.OnTurnUpdate. Default true as part of the stable suppression subset.");
            EnableAttackSuppressionUpdateRequestsAfterAggroGuard = Config.Bind("FeatureGates", "EnableAttackSuppressionUpdateRequestsAfterAggroGuard", true, "When EnableAttackSuppressionFeatures is on, patch CombatAdvisor.UpdateRequestsAfterAggro. Default true as part of the stable suppression subset.");
            EnableCombatIncidentRuntimeFeatures = Config.Bind("FeatureGates", "EnableCombatIncidentRuntimeFeatures", false, "Enable only the combat incident runtime hooks that translate confirmed cop combat into real cop-war retaliation while broader runtime slices stay disabled.");
            EnableHumanCopTargetingFeatures = Config.Bind("FeatureGates", "EnableHumanCopTargetingFeatures", false, "Enable only the rule that allows the human player to target cops in combat while deeper CopKilling runtime hooks stay disabled.");
            EnableCrewPickTintFeatures = Config.Bind("FeatureGates", "EnableCrewPickTintFeatures", false, "Enable only the cop aggro crew-pick tint patch while deeper CopKilling runtime hooks stay disabled.");
            EnableTurnStartReconciliationFeatures = Config.Bind("FeatureGates", "EnableTurnStartReconciliationFeatures", false, "Enable only the player turn-start cop-war reconciliation hook while deeper CopKilling runtime hooks stay disabled.");
            EnableCombatPostfixDiagnosticFeatures = Config.Bind("FeatureGates", "EnableCombatPostfixDiagnosticFeatures", false, "Enable only diagnostic combat postfix hooks that log cop-combat entrypoints without applying witness or retaliation systems.");
            DebugResetCopWarHotkey = Config.Bind("Debug", "ResetCopWarHotkey", SafeResetCopWarHotkey, "Debug-only hotkey to clear the current simulated cop-war state. Bare F8/F9/F10 are reserved by the base game, so use a modifier chord.");
            DebugSimulateCopKillHotkey = Config.Bind("Debug", "SimulateCopKillHotkey", SafeSimulateCopKillHotkey, "Debug-only hotkey to simulate a cop kill against a living precinct without using the unstable combat entrypoint. Bare F8/F9/F10 are reserved by the base game.");
            DebugSimulateCopAssaultHotkey = Config.Bind("Debug", "SimulateCopAssaultHotkey", SafeSimulateCopAssaultHotkey, "Debug-only hotkey to simulate a cop assault against a living precinct without using the unstable combat entrypoint. Uses Ctrl+Shift+F12 so it does not collide with GameplayTweaks murder-witness testing.");
            NormalizeReservedDebugHotkey(DebugResetCopWarHotkey, KeyCode.F8, SafeResetCopWarHotkey, "ResetCopWarHotkey");
            NormalizeReservedDebugHotkey(DebugSimulateCopKillHotkey, KeyCode.F9, SafeSimulateCopKillHotkey, "SimulateCopKillHotkey");
            NormalizeReservedDebugHotkey(DebugSimulateCopAssaultHotkey, KeyCode.F10, SafeSimulateCopAssaultHotkey, "SimulateCopAssaultHotkey");
            NormalizeDebugHotkeyConflict(DebugSimulateCopAssaultHotkey, new KeyboardShortcut(KeyCode.F10, KeyCode.LeftControl, KeyCode.LeftShift), SafeSimulateCopAssaultHotkey, "SimulateCopAssaultHotkey", "GameplayTweaks AddMurderWitnessHotkey");
            LogLoadedBuildBanner();
            var harmony = new Harmony("com.mods.copkilling");
            bool runtimeEnabled = IsRuntimeEnabled();
            bool allowExperimentalSlices = runtimeEnabled;
            bool combatIncidentRuntimeEnabled = runtimeEnabled && (EnableCombatIncidentRuntimeFeatures?.Value ?? false);
            bool humanCopTargetingEnabled = runtimeEnabled && (EnableHumanCopTargetingFeatures?.Value ?? false);
            bool crewPickTintEnabled = runtimeEnabled && (EnableCrewPickTintFeatures?.Value ?? false);
            bool turnStartEnabled = allowExperimentalSlices && (EnableTurnStartReconciliationFeatures?.Value ?? false);
            bool runtimeSliceEnabled = combatIncidentRuntimeEnabled || humanCopTargetingEnabled || crewPickTintEnabled || turnStartEnabled;

            if ((EnableCombatNameDisplayFeatures?.Value ?? false) && runtimeEnabled)
            {
                CombatNameDisplayPatch.ApplyPatch(harmony);
                Logger.LogInfo("CopKilling combat name display slice enabled.");
            }
            else
            {
                Logger.LogInfo(runtimeEnabled
                    ? "CopKilling combat name display slice skipped."
                    : "CopKilling combat name display slice forced off while runtime hooks are disabled.");
            }

            if (IsAttackSuppressionRequested() && IsAnyAttackSuppressionHookEnabled())
            {
                CopWarSystem.ApplyAttackSuppressionPatch(harmony);
                Logger.LogInfo("CopKilling attack suppression slice enabled. hooks=" + GetAttackSuppressionHookSummary());
                if (IsAttackSuppressionIsAttackAllowedEnabled())
                {
                    Logger.LogWarning("CopKilling attack suppression is running with CombatAdvisor.IsAttackAllowed enabled. This hook is known-bad and previously reproduced a native crash when isolated.");
                }
                else
                {
                    Logger.LogInfo("CopKilling attack suppression is using the stable default subset with CombatAdvisor.IsAttackAllowed disabled.");
                }
            }
            else if (IsAttackSuppressionRequested())
            {
                Logger.LogInfo("CopKilling attack suppression slice skipped because all suppression sub-hooks are disabled.");
            }
            else
            {
                Logger.LogInfo("CopKilling attack suppression slice skipped.");
            }

            if (runtimeSliceEnabled)
            {
                CopWarSystem.Initialize();
            }

            if (combatIncidentRuntimeEnabled)
            {
                CopWarSystem.ApplyCombatIncidentRuntimePatches(harmony);
                Logger.LogInfo("CopKilling combat incident runtime slice enabled.");
            }
            else
            {
                Logger.LogInfo(runtimeEnabled
                    ? "CopKilling combat incident runtime slice skipped."
                    : "CopKilling combat incident runtime slice forced off while runtime hooks are disabled.");
            }

            if (humanCopTargetingEnabled)
            {
                CopWarSystem.ApplyHumanCopTargetingPatch(harmony);
                Logger.LogInfo("CopKilling human cop targeting slice enabled.");
            }
            else
            {
                Logger.LogInfo(runtimeEnabled
                    ? "CopKilling human cop targeting slice skipped."
                    : "CopKilling human cop targeting slice forced off while runtime hooks are disabled.");
            }

            if (crewPickTintEnabled)
            {
                CopWarSystem.ApplyCrewPickTintPatch(harmony);
                Logger.LogInfo("CopKilling crew pick tint slice enabled.");
            }
            else
            {
                Logger.LogInfo(runtimeEnabled
                    ? "CopKilling crew pick tint slice skipped."
                    : "CopKilling crew pick tint slice forced off while runtime hooks are disabled.");
            }

            if (turnStartEnabled)
            {
                CopWarSystem.ApplyTurnStartReconciliationPatch(harmony);
                Logger.LogInfo("CopKilling turn-start reconciliation slice enabled.");
            }
            else
            {
                Logger.LogInfo(allowExperimentalSlices
                    ? "CopKilling turn-start reconciliation slice skipped."
                    : "CopKilling turn-start reconciliation slice forced off while runtime hooks are disabled.");
            }

            if (EnableCombatPostfixDiagnosticFeatures?.Value ?? false)
            {
                CopWarSystem.ApplyCombatPostfixDiagnosticPatches(harmony, humanCopTargetingEnabled);
                Logger.LogInfo("CopKilling combat postfix diagnostic slice enabled.");
            }
            else
            {
                Logger.LogInfo("CopKilling combat postfix diagnostic slice skipped.");
            }

            if (!runtimeEnabled)
            {
                Logger.LogInfo("CopKilling no-op diagnostic mode active; runtime hooks skipped.");
                return;
            }

            if (!runtimeSliceEnabled)
            {
                Logger.LogInfo("CopKilling runtime hooks enabled, but no runtime slices requested.");
            }

            Logger.LogInfo("CopKilling runtime-configured mod loaded.");
        }

        private void Update()
        {
            try
            {
                if (IsRuntimeEnabled())
                {
                    CopWarSystem.ProcessPendingCommittedCopCombatFallbacks();
                    return;
                }

                CopWarSystem.ProcessPendingDebugReset();
                CopWarSystem.ProcessPendingDebugTransitionCleanup();

                if (DebugResetCopWarHotkey?.Value.IsDown() ?? false)
                {
                    CopWarSystem.ResetDebugCopWarState();
                }
                else if (DebugSimulateCopKillHotkey?.Value.IsDown() ?? false)
                {
                    CopWarSystem.TriggerDebugCopIncident(lethal: true);
                }
                else if (DebugSimulateCopAssaultHotkey?.Value.IsDown() ?? false)
                {
                    CopWarSystem.TriggerDebugCopIncident(lethal: false);
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning("CopKilling debug hotkey polling failed: " + ex.Message);
            }
        }

        private static void NormalizeReservedDebugHotkey(ConfigEntry<KeyboardShortcut> entry, KeyCode reservedMainKey, KeyboardShortcut replacement, string settingName)
        {
            if (entry == null)
            {
                return;
            }

            KeyboardShortcut current = entry.Value;
            bool bareReservedFunctionKey = current.MainKey == reservedMainKey && !current.Modifiers.Any();
            if (!bareReservedFunctionKey)
            {
                return;
            }

            entry.Value = replacement;
            Debug.Log($"[CopKilling] {settingName} used reserved base-game key {reservedMainKey}; migrated to {replacement}.");
        }

        private static void NormalizeDebugHotkeyConflict(ConfigEntry<KeyboardShortcut> entry, KeyboardShortcut conflictingShortcut, KeyboardShortcut replacement, string settingName, string conflictOwner)
        {
            if (entry == null)
            {
                return;
            }

            if (!KeyboardShortcutsMatch(entry.Value, conflictingShortcut))
            {
                return;
            }

            entry.Value = replacement;
            Debug.Log($"[CopKilling] {settingName} conflicted with {conflictOwner}; migrated to {replacement}.");
        }

        private static bool KeyboardShortcutsMatch(KeyboardShortcut left, KeyboardShortcut right)
        {
            if (left.MainKey != right.MainKey)
            {
                return false;
            }

            var leftModifiers = left.Modifiers == null
                ? new List<KeyCode>()
                : left.Modifiers.OrderBy(k => k).ToList();
            var rightModifiers = right.Modifiers == null
                ? new List<KeyCode>()
                : right.Modifiers.OrderBy(k => k).ToList();
            if (leftModifiers.Count != rightModifiers.Count)
            {
                return false;
            }

            for (int i = 0; i < leftModifiers.Count; i++)
            {
                if (leftModifiers[i] != rightModifiers[i])
                {
                    return false;
                }
            }

            return true;
        }

        private void LogLoadedBuildBanner()
        {
            try
            {
                var assembly = typeof(CopKillingPlugin).Assembly;
                string version = assembly.GetName().Version?.ToString() ?? "0.0.0.0";
                string assemblyPath = assembly.Location;
                string fileName = string.IsNullOrEmpty(assemblyPath) ? "CopKilling.dll" : Path.GetFileName(assemblyPath);
                string builtAt = !string.IsNullOrEmpty(assemblyPath) && File.Exists(assemblyPath)
                    ? File.GetLastWriteTime(assemblyPath).ToString("yyyy-MM-dd HH:mm:ss")
                    : "unknown";
                string normalizedPath = string.IsNullOrEmpty(assemblyPath) ? "unknown" : assemblyPath;
                bool runtimeEnabled = IsRuntimeEnabled();
                bool combatNameRequested = EnableCombatNameDisplayFeatures?.Value ?? false;
                bool attackSuppressionRequested = IsAttackSuppressionRequested();
                bool attackSuppressionEnabled = IsAnyAttackSuppressionHookEnabled();
                bool attackSuppressionIsAttackAllowedEnabled = IsAttackSuppressionIsAttackAllowedEnabled();
                bool attackSuppressionTryPickBuildingEnabled = IsAttackSuppressionTryPickBuildingEnabled();
                bool attackSuppressionAttackAdvisorOnTurnUpdateEnabled = IsAttackSuppressionAttackAdvisorOnTurnUpdateEnabled();
                bool attackSuppressionMaybeAskForTruceEnabled = IsAttackSuppressionMaybeAskForTruceEnabled();
                bool attackSuppressionCombatAdvisorOnTurnUpdateEnabled = IsAttackSuppressionCombatAdvisorOnTurnUpdateEnabled();
                bool attackSuppressionUpdateRequestsAfterAggroEnabled = IsAttackSuppressionUpdateRequestsAfterAggroEnabled();
                bool combatIncidentRuntimeRequested = EnableCombatIncidentRuntimeFeatures?.Value ?? false;
                bool humanCopTargetingRequested = EnableHumanCopTargetingFeatures?.Value ?? false;
                bool crewPickTintRequested = EnableCrewPickTintFeatures?.Value ?? false;
                bool turnStartRequested = EnableTurnStartReconciliationFeatures?.Value ?? false;
                bool combatPostfixDiagnosticRequested = EnableCombatPostfixDiagnosticFeatures?.Value ?? false;
                bool combatNameEnabled = runtimeEnabled && combatNameRequested;
                bool combatIncidentRuntimeEnabled = runtimeEnabled && combatIncidentRuntimeRequested;
                bool humanCopTargetingEnabled = runtimeEnabled && humanCopTargetingRequested;
                bool crewPickTintEnabled = runtimeEnabled && crewPickTintRequested;
                bool turnStartEnabled = runtimeEnabled && turnStartRequested;
                bool combatPostfixDiagnosticEnabled = combatPostfixDiagnosticRequested;
                string message = $"Cop Killing loaded version={version} file={fileName} built={builtAt} path={normalizedPath} runtimeEnabled={runtimeEnabled} combatNameRequested={combatNameRequested} combatNameEnabled={combatNameEnabled} attackSuppressionRequested={attackSuppressionRequested} attackSuppressionEnabled={attackSuppressionEnabled} attackSuppressionIsAttackAllowedEnabled={attackSuppressionIsAttackAllowedEnabled} attackSuppressionTryPickBuildingEnabled={attackSuppressionTryPickBuildingEnabled} attackSuppressionAttackAdvisorOnTurnUpdateEnabled={attackSuppressionAttackAdvisorOnTurnUpdateEnabled} attackSuppressionMaybeAskForTruceEnabled={attackSuppressionMaybeAskForTruceEnabled} attackSuppressionCombatAdvisorOnTurnUpdateEnabled={attackSuppressionCombatAdvisorOnTurnUpdateEnabled} attackSuppressionUpdateRequestsAfterAggroEnabled={attackSuppressionUpdateRequestsAfterAggroEnabled} combatIncidentRuntimeRequested={combatIncidentRuntimeRequested} combatIncidentRuntimeEnabled={combatIncidentRuntimeEnabled} humanCopTargetingRequested={humanCopTargetingRequested} humanCopTargetingEnabled={humanCopTargetingEnabled} crewPickTintRequested={crewPickTintRequested} crewPickTintEnabled={crewPickTintEnabled} turnStartRequested={turnStartRequested} turnStartEnabled={turnStartEnabled} combatPostfixDiagnosticRequested={combatPostfixDiagnosticRequested} combatPostfixDiagnosticEnabled={combatPostfixDiagnosticEnabled}";
                Logger.LogInfo(message);
                Debug.Log("[CopKilling] " + message);
            }
            catch (Exception ex)
            {
                Logger.LogWarning("LogLoadedBuildBanner failed: " + ex.Message);
            }
        }
    }

    internal static class CopWarSystem
    {
        private const string VerificationLogPrefix = "[VERIFY-HOTFIX]";
        private static readonly string[] CopKillWarBuffIds = new string[2] { "relbuff-cop-killed-precinct", "relbuff-cop-killed-citywide" };
        private const int InvalidPrecinctKey = int.MinValue;
        private const int FedArrestCountdownDays = 3;
        private const int FreshRetaliationCleanupGraceDays = 1;

        private sealed class PendingCopWitnessCase
        {
            public ulong KillerPeepId;
            public int VictimPrecinctKey = InvalidPrecinctKey;
            public int VictimCopPlayerId = -1;
            public int DayCreated = -1;
            public bool WitnessEvidenceConfirmed;
            public bool HardMaxApplied;
            public bool PoliticalFavorGrandfathered;
        }

        private sealed class PendingCommittedCopCombat
        {
            public ulong AttackerPeepId;
            public ulong TargetPeepId;
            public int DayCreated = -1;
            public int CommitFrame = -1;
            public string SourceTag = string.Empty;
        }

        private struct AggroCleanupResult
        {
            public int ConsideredCount;
            public int HadAggroCount;
            public int ClearedCount;
            public bool Silent;
        }

        private enum CopIncidentScope
        {
            None = 0,
            Assault = 1,
            Kill = 2
        }

        private static Type _copUtilType;
        private static MethodInfo _isCopMethod;
        private static bool _initialized;
        private static MethodInfo _addAggroOnMethod;
        private static MethodInfo _removeAggroOnMethod;
        private static MethodInfo _isAttackAllowedMethod;
        private static MethodInfo _isAggroWithoutTruceMethod;
        private static MethodInfo _getAggroAndTruceMethod;
        private static FieldInfo _combatDataField;
        private static FieldInfo _combatPopupEnemiesField;
        private static FieldInfo _combatPopupMyCrewField;
        private static MethodInfo _removeAggroHelperMethod;
        private static bool _loggedAggroProbeSelection;
        private static Type _jailSystemType;
        private static MethodInfo _jailArrestCrewMethod;
        private static MethodInfo _jailIsInJailMethod;
        private static FieldInfo _agentEntityField;
        private static readonly List<PendingCopWitnessCase> _pendingWitnessCases = new List<PendingCopWitnessCase>();
        private static readonly List<PendingCommittedCopCombat> _pendingCommittedCopCombats = new List<PendingCommittedCopCombat>();
        private static readonly Dictionary<ulong, ulong> _observedCopTargetByAttacker = new Dictionary<ulong, ulong>();
        private static readonly Dictionary<ulong, int> _observedCopTargetDayByAttacker = new Dictionary<ulong, int>();
        private static readonly HashSet<string> _processedCopKillKeys = new HashSet<string>();
        private static readonly HashSet<string> _processedGroupedCopAttackerCaseKeys = new HashSet<string>();
        private static int _activeWarPrecinctKey = InvalidPrecinctKey;
        private static int _activeWarVictimCopPlayerId = -1;
        private static ulong _activeWarKillerPeepId;
        private static CopIncidentScope _activeIncidentScope = CopIncidentScope.None;
        private static readonly HashSet<int> _resolvedWarPrecinctKeys = new HashSet<int>();
        private static int _freshRetaliationHumanPlayerId = -1;
        private static int _freshRetaliationGraceDay = -1;
        private static int _freshRetaliationExpiryDay = -1;
        private static int _freshRetaliationPrecinctKey = InvalidPrecinctKey;
        private static ulong _freshRetaliationKillerPeepId;
        private static bool _freshRetaliationPendingCleanupProtection;
        private static int _lastProcessedCopKillDay = -1;
        private static int _combatHookMonitorDay = -1;
        private static bool _sawCommittedCopCombatThisDay;
        private static bool _sawCombatIncidentHookThisDay;
        private static bool _loggedMissingCombatHookWarningThisDay;
        private static int _lastSafeHookNormalizationFrame = -1;
        private static int _lastSafeHookStateReconcileFrame = -1;
        private static int _lastSafeHookBribedPrecinctRelaxFrame = -1;
        private static int _lastDebugIncidentFrame = -1;
        private static int _lastAttackAdvisorFinalizerLogFrame = -1;
        private static int _lastAttackAdvisorTurnFinalizerLogFrame = -1;
        private static int _lastCombatAdvisorTruceFinalizerLogFrame = -1;
        private static int _lastCombatAdvisorUpdateFinalizerLogFrame = -1;
        private static int _safeInteractionMaintenanceDepth;
        private static bool _isRefreshingCopAggroUi;
        private static readonly Color CopAggroPickTintColor = new Color(0.55f, 0.78f, 1f, 1f);
        private static FieldInfo _crewPickPidField;
        private static FieldInfo _crewPickPlayerColorField;
        private static FieldInfo _crewPickGoField;
        private static MethodInfo _crewPickGoGetImageMethod;
        private static PropertyInfo _graphicColorProperty;
        private static FieldInfo _groupedCombatTransactionSourceField;
        private static MethodInfo _combatPopupFindTargetForMethod;
        private static int _lastCopPickTintLogDay = -1;
        private static readonly HashSet<int> _copPickTintLogPids = new HashSet<int>();
        private static int _lastNoBuffAttackLogDay = -1;
        private static readonly HashSet<int> _noBuffAttackLoggedPids = new HashSet<int>();
        private static bool _externalCombatObserverSubscribed;
        private static int _lastIncomingAttackDiagnosticDay = -1;
        private static readonly HashSet<string> _incomingAttackDiagnosticKeys = new HashSet<string>();
        private static int _lastDeferredTurnStartLogFrame = -1;
        private static bool _debugResetPending;
        private static int _debugResetStage = -1;
        private static int _debugResetRequestedFrame = -1;
        private static ulong _debugResetKillerPeepId;
        private static int _debugResetSuppressionUntilFrame = -1;
        private static int _debugResetSuppressionUntilDay = -1;
        private static int _politicalFavorSuppressionUntilFrame = -1;
        private static int _politicalFavorSuppressionUntilDay = -1;
        private static bool _debugTransitionFromKillToAssaultPending = false;
        private static bool _pendingPostTransitionAssaultCleanup = false;
        private static int _pendingPostTransitionAssaultCleanupFrame = -1;
        private static int _lastAttackAdvisorTryPickSuppressionLogFrame = -1;
        private static int _lastAttackAdvisorTurnSuppressionLogFrame = -1;
        private static int _lastAttackAdvisorCoordStateFixLogFrame = -1;
        private static int _lastCombatAdvisorTruceSuppressionLogFrame = -1;
        private static int _lastCombatAdvisorTurnSuppressionLogFrame = -1;
        private static int _lastCombatAdvisorUpdateSuppressionLogFrame = -1;
        private static int _explicitHostilityApplyDepth;
        private static int _lastExplicitHostilityReentryLogFrame = -1;
        private static PropertyInfo _convoCallbacksVisitProperty;
        private static FieldInfo _attackAdvisorDataField;
        private static FieldInfo _attackAdvisorPlayerField;
        private static FieldInfo _attackAdvisorCoordStateField;
        private static FieldInfo _attackAdvisorCoordStateAttackingCrewField;
        private static FieldInfo _attackAdvisorCoordStateTargetPlayerField;
        private static FieldInfo _attackAdvisorCoordStateRallyPointField;
        private static FieldInfo _attackAdvisorCoordStateRallyPointNodeIdField;
        private static FieldInfo _attackAdvisorCoordStateRallyExpiresField;

        public static bool IsCopWarActive => GameplayTweaks.GameplayTweaksPlugin.SaveData.CopWarActive;

        private static void VerificationLog(string message)
        {
            Debug.Log($"{VerificationLogPrefix} [CopKilling] {message}");
        }

        private static bool IsApplyingExplicitHostilityState => _explicitHostilityApplyDepth > 0;

        public static void Initialize()
        {
            try
            {
                _copUtilType = typeof(GameClock).Assembly.GetType("Game.Session.Sim.CopUtil");
                if (_copUtilType != null)
                {
                    _isCopMethod = _copUtilType.GetMethod("IsCop", BindingFlags.Static | BindingFlags.Public, null, new Type[] { typeof(Entity) }, null);
                    _initialized = true;
                    Debug.Log("[CopKilling] CopWarSystem initialized");
                }
                SubscribeExternalCombatObserver();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CopKilling] CopWarSystem init failed: {ex}");
            }
        }

        private static void SubscribeExternalCombatObserver()
        {
            if (_externalCombatObserverSubscribed)
            {
                return;
            }
            try
            {
                GameplayTweaks.CombatObserverBridge.CombatResolved += OnExternalCombatResolved;
                _externalCombatObserverSubscribed = true;
                Debug.Log("[CopKilling] Grouped combat observer bridge subscribed");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[CopKilling] Failed to subscribe grouped combat observer bridge: {ex.Message}");
            }
        }

        public static bool IsCop(Entity peep)
        {
            if (!_initialized || _isCopMethod == null || peep == null)
                return false;
            try
            {
                return (bool)_isCopMethod.Invoke(null, new object[] { peep });
            }
            catch
            {
                return false;
            }
        }

        private static bool IsCopPlayer(PlayerInfo player)
        {
            if (player == null)
            {
                return false;
            }
            try
            {
                if (GetPrecinctObject(player) != null)
                {
                    return true;
                }
                if (player.IsJustCop || player.IsCopOrFed)
                {
                    return true;
                }
                PropertyInfo isJustCop = player.GetType().GetProperty("IsJustCop");
                if (isJustCop != null)
                {
                    object val = isJustCop.GetValue(player);
                    if (val is bool b && b)
                    {
                        return true;
                    }
                }
                PropertyInfo isCopOrFed = player.GetType().GetProperty("IsCopOrFed");
                if (isCopOrFed != null)
                {
                    object val2 = isCopOrFed.GetValue(player);
                    if (val2 is bool b2 && b2)
                    {
                        return true;
                    }
                }
                FieldInfo isJustCopField = player.GetType().GetField("IsJustCop", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (isJustCopField != null)
                {
                    object val3 = isJustCopField.GetValue(player);
                    if (val3 is bool b3 && b3)
                    {
                        return true;
                    }
                }
                FieldInfo isCopOrFedField = player.GetType().GetField("IsCopOrFed", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (isCopOrFedField != null)
                {
                    object val4 = isCopOrFedField.GetValue(player);
                    if (val4 is bool b4 && b4)
                    {
                        return true;
                    }
                }
            }
            catch
            {
            }
            return false;
        }

        private static bool IsMunicipalCopPlayer(PlayerInfo player)
        {
            if (!IsCopPlayer(player))
            {
                return false;
            }
            try
            {
                if (player.IsJustCop)
                {
                    return true;
                }
            }
            catch
            {
            }
            try
            {
                PropertyInfo isJustCop = player.GetType().GetProperty("IsJustCop");
                if (isJustCop != null && isJustCop.GetValue(player) is bool justCop && justCop)
                {
                    return true;
                }
            }
            catch
            {
            }
            try
            {
                FieldInfo isJustCopField = player.GetType().GetField("IsJustCop", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (isJustCopField != null && isJustCopField.GetValue(player) is bool justCopFieldValue && justCopFieldValue)
                {
                    return true;
                }
            }
            catch
            {
            }

            string displayName = GetCopDisplayName(player);
            if (string.IsNullOrWhiteSpace(displayName))
            {
                return false;
            }
            string normalized = displayName.ToLowerInvariant();
            if (normalized.Contains("federal") || normalized.Contains("bureau") || normalized.Contains("prohibition") || normalized.Contains("fed"))
            {
                return false;
            }
            return normalized.Contains("police") || normalized.Contains("precinct");
        }

        private static bool IsValidPrecinctKey(int precinctKey)
        {
            return precinctKey != InvalidPrecinctKey;
        }

        private static PlayerInfo FindPreferredDebugVictimCopGang(PlayerInfo humanPlayer)
        {
            List<PlayerInfo> candidates = GetCopPlayers()
                .Where(c => c != null
                    && c.PID.id != humanPlayer?.PID.id
                    && (c.crew == null || !c.crew.IsCrewDefeated))
                .OrderBy(c => GetPrecinctKey(c))
                .ThenBy(c => c.PID.id)
                .ToList();
            if (candidates.Count <= 0)
            {
                return null;
            }

            PlayerInfo municipal = candidates.FirstOrDefault(IsMunicipalCopPlayer);
            return municipal ?? candidates[0];
        }

        private static object GetPrecinctObject(PlayerInfo copPlayer)
        {
            if (copPlayer == null)
            {
                return null;
            }
            try
            {
                object aiObj = copPlayer.ai;
                if (aiObj == null)
                {
                    return null;
                }
                PropertyInfo precinctProp = aiObj.GetType().GetProperty("precinct");
                if (precinctProp != null)
                {
                    return precinctProp.GetValue(aiObj);
                }
                FieldInfo precinctField = aiObj.GetType().GetField("precinct");
                if (precinctField != null)
                {
                    return precinctField.GetValue(aiObj);
                }
            }
            catch
            {
            }
            return null;
        }

        private static int GetPrecinctKey(PlayerInfo copPlayer)
        {
            object precinctObj = GetPrecinctObject(copPlayer);
            if (precinctObj == null)
            {
                return copPlayer?.PID.id ?? -1;
            }
            return RuntimeHelpers.GetHashCode(precinctObj);
        }

        private static List<PlayerInfo> GetCopPlayers()
        {
            return GameplayTweaks.G.GetAllPlayers().Where(IsCopPlayer).ToList();
        }

        private static List<PlayerInfo> GetMunicipalCopPlayers()
        {
            return GameplayTweaks.G.GetAllPlayers().Where(IsMunicipalCopPlayer).ToList();
        }

        private static object GetCombatObject(PlayerInfo player)
        {
            return player?.ai?.combat;
        }

        private static void CacheCombatMethods(object combat)
        {
            if (combat == null)
            {
                return;
            }
            Type type = combat.GetType();
            if (_addAggroOnMethod == null)
            {
                _addAggroOnMethod = type.GetMethod("AddAggroOn", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            }
            if (_removeAggroOnMethod == null)
            {
                _removeAggroOnMethod = type.GetMethod("RemoveAggroOn", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            }
            if (_isAttackAllowedMethod == null)
            {
                _isAttackAllowedMethod = type.GetMethod("IsAttackAllowed", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[] { typeof(PlayerID) }, null);
            }
            if (_isAggroWithoutTruceMethod == null)
            {
                _isAggroWithoutTruceMethod = type.GetMethod("IsAggroWithoutTruce", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[] { typeof(PlayerID) }, null);
            }
            if (_getAggroAndTruceMethod == null)
            {
                _getAggroAndTruceMethod = type.GetMethod("GetAggroAndTruce", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[] { typeof(PlayerID) }, null);
            }
            if (_combatDataField == null)
            {
                _combatDataField = type.GetField("_data", BindingFlags.Instance | BindingFlags.NonPublic);
            }
            if (_removeAggroHelperMethod == null)
            {
                Type combatDataType = _combatDataField?.FieldType;
                if (combatDataType != null)
                {
                    _removeAggroHelperMethod = combatDataType.GetMethod("RemoveAggroHelper", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[] { typeof(PlayerID) }, null);
                }
            }
            if (!_loggedAggroProbeSelection)
            {
                _loggedAggroProbeSelection = true;
                Debug.Log($"{VerificationLogPrefix} [CopKilling] aggro probe methods attackAllowed={_isAttackAllowedMethod != null} aggroWithoutTruce={_isAggroWithoutTruceMethod != null} aggroAndTruce={_getAggroAndTruceMethod != null}");
            }
        }

        private static bool TryGetBool(object obj, string name, out bool value)
        {
            value = false;
            if (obj == null)
            {
                return false;
            }
            Type type = obj.GetType();
            PropertyInfo prop = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (prop != null)
            {
                object propVal = prop.GetValue(obj);
                if (propVal is bool b)
                {
                    value = b;
                    return true;
                }
            }
            FieldInfo field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null)
            {
                object fieldVal = field.GetValue(obj);
                if (fieldVal is bool b2)
                {
                    value = b2;
                    return true;
                }
            }
            return false;
        }

        private static bool TryGetAggroAndTruceFlags(object tupleObj, out bool isAggro, out bool hasTruce)
        {
            isAggro = false;
            hasTruce = false;
            if (tupleObj == null)
            {
                return false;
            }
            bool gotAggro = TryGetBool(tupleObj, "Item1", out isAggro) || TryGetBool(tupleObj, "isAggro", out isAggro);
            bool gotTruce = TryGetBool(tupleObj, "Item2", out hasTruce) || TryGetBool(tupleObj, "hasTruce", out hasTruce);
            return gotAggro || gotTruce;
        }

        private static bool IsAttackAllowedOneWay(PlayerInfo owner, PlayerID targetPid)
        {
            try
            {
                object combat = GetCombatObject(owner);
                if (combat == null)
                {
                    return false;
                }
                CacheCombatMethods(combat);
                if (_isAttackAllowedMethod != null)
                {
                    object result = _isAttackAllowedMethod.Invoke(combat, new object[] { targetPid });
                    if (result is bool allowed)
                    {
                        return allowed;
                    }
                }
            }
            catch
            {
            }
            return false;
        }

        private static bool ShouldSuppressCopAttack(PlayerInfo owner, PlayerInfo target)
        {
            if (owner == null || target == null)
            {
                return false;
            }
            if (!IsCopPlayer(owner) || target.PID.id != (GameplayTweaks.G.GetHumanPlayer()?.PID.id ?? -1))
            {
                return false;
            }
            if (IsAiStabilizationSuppressionActive())
            {
                return true;
            }
            if (!EnableQueuedCopWarAggro)
            {
                return true;
            }
            if (IsPoliticallyResolvedPrecinct(owner, target))
            {
                return true;
            }
            if (HasCopKillBuffBetween(owner, target))
            {
                return false;
            }
            if (GameplayTweaks.GameplayTweaksPlugin.SaveData.CopWarActive)
            {
                return false;
            }
            return true;
        }

        private static bool ShouldForceCopAttack(PlayerInfo owner, PlayerInfo target)
        {
            if (owner == null || target == null || !IsCopPlayer(owner))
            {
                return false;
            }
            if (target.PID.id != (GameplayTweaks.G.GetHumanPlayer()?.PID.id ?? -1))
            {
                return false;
            }
            if (!EnableQueuedCopWarAggro)
            {
                return false;
            }
            if (_activeIncidentScope == CopIncidentScope.None)
            {
                return false;
            }
            HashSet<int> hostilePrecincts = GetDesiredHostilePrecinctKeys(target);
            if (hostilePrecincts.Count == 0)
            {
                return false;
            }
            return hostilePrecincts.Contains(GetPrecinctKey(owner));
        }

        private static bool IsPoliticallyResolvedPrecinct(PlayerInfo owner, PlayerInfo target)
        {
            if (owner == null || target == null || !IsCopPlayer(owner))
            {
                return false;
            }
            if (!HasActivePoliticalProtection(target))
            {
                return false;
            }
            int ownerPrecinctKey = GetPrecinctKey(owner);
            if (!_resolvedWarPrecinctKeys.Contains(ownerPrecinctKey))
            {
                return false;
            }
            HashSet<int> hostilePrecincts = GetDesiredHostilePrecinctKeys(target);
            return !hostilePrecincts.Contains(ownerPrecinctKey);
        }

        private static void TryAddAggroOn(PlayerInfo owner, PlayerID targetPid)
        {
            try
            {
                object combat = GetCombatObject(owner);
                if (combat == null)
                {
                    return;
                }
                CacheCombatMethods(combat);
                if (_addAggroOnMethod != null)
                {
                    object aggroDuration = -120;
                    try
                    {
                        ParameterInfo[] parameters = _addAggroOnMethod.GetParameters();
                        if (parameters.Length > 1)
                        {
                            Type durationType = parameters[1].ParameterType;
                            if (durationType != typeof(int))
                            {
                                aggroDuration = Activator.CreateInstance(durationType, new object[] { -120 });
                            }
                        }
                    }
                    catch
                    {
                        aggroDuration = -120;
                    }
                    _addAggroOnMethod.Invoke(combat, new object[] { targetPid, aggroDuration });
                }
            }
            catch
            {
            }
        }

        private static void TryRemoveAggroOn(PlayerInfo owner, PlayerID targetPid)
        {
            try
            {
                object combat = GetCombatObject(owner);
                if (combat == null)
                {
                    return;
                }
                CacheCombatMethods(combat);
                if (_removeAggroOnMethod != null)
                {
                    _removeAggroOnMethod.Invoke(combat, new object[] { targetPid });
                }
            }
            catch
            {
            }
        }

        private static bool TryRemoveAggroOnSilently(PlayerInfo owner, PlayerID targetPid)
        {
            try
            {
                object combat = GetCombatObject(owner);
                if (combat == null)
                {
                    return false;
                }
                CacheCombatMethods(combat);
                object combatData = _combatDataField?.GetValue(combat);
                if (combatData == null)
                {
                    return false;
                }
                MethodInfo removeHelperMethod = _removeAggroHelperMethod;
                if (removeHelperMethod == null || removeHelperMethod.DeclaringType != combatData.GetType())
                {
                    removeHelperMethod = combatData.GetType().GetMethod("RemoveAggroHelper", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[] { typeof(PlayerID) }, null);
                    if (removeHelperMethod != null)
                    {
                        _removeAggroHelperMethod = removeHelperMethod;
                    }
                }
                if (removeHelperMethod == null)
                {
                    return false;
                }
                object value = removeHelperMethod.Invoke(combatData, new object[] { targetPid });
                return value is bool removed && removed;
            }
            catch
            {
            }
            return false;
        }

        private static void ForceClearAggroPair(PlayerInfo owner, PlayerID targetPid)
        {
            if (owner == null || !targetPid.IsAnyPlayer)
            {
                return;
            }
            TryRemoveAggroOn(owner, targetPid);
            if (IsAggroOn(owner, targetPid))
            {
                TryRemoveAggroOnSilently(owner, targetPid);
            }
            if (IsAggroOn(owner, targetPid))
            {
                TryRemoveAggroOn(owner, targetPid);
            }
        }

        private static bool IsAggroOn(PlayerInfo owner, PlayerID targetPid)
        {
            try
            {
                object combat = GetCombatObject(owner);
                if (combat == null)
                {
                    return false;
                }
                CacheCombatMethods(combat);
                if (_isAttackAllowedMethod != null)
                {
                    object allowed = _isAttackAllowedMethod.Invoke(combat, new object[] { targetPid });
                    if (allowed is bool attackAllowed)
                    {
                        return attackAllowed;
                    }
                }
                if (_isAggroWithoutTruceMethod != null)
                {
                    object noTruceAggro = _isAggroWithoutTruceMethod.Invoke(combat, new object[] { targetPid });
                    if (noTruceAggro is bool aggroWithoutTruce)
                    {
                        return aggroWithoutTruce;
                    }
                }
                if (_getAggroAndTruceMethod != null)
                {
                    object aggroTuple = _getAggroAndTruceMethod.Invoke(combat, new object[] { targetPid });
                    if (TryGetAggroAndTruceFlags(aggroTuple, out bool isAggro, out bool hasTruce))
                    {
                        return isAggro && !hasTruce;
                    }
                }
            }
            catch
            {
            }
            return false;
        }

        private static HashSet<int> GetBribedPrecinctKeys(PlayerInfo humanPlayer)
        {
            HashSet<int> keys = new HashSet<int>();
            if (humanPlayer == null)
            {
                return keys;
            }
            foreach (PlayerInfo cop in GetMunicipalCopPlayers())
            {
                try
                {
                    object precinctObj = GetPrecinctObject(cop);
                    if (precinctObj == null)
                    {
                        continue;
                    }
                    MethodInfo hasDonation = precinctObj.GetType().GetMethod("HasDonationFrom");
                    if (hasDonation == null)
                    {
                        continue;
                    }
                    object result = hasDonation.Invoke(precinctObj, new object[] { humanPlayer.PID });
                    int donationLevel = 0;
                    try
                    {
                        if (result != null)
                        {
                            donationLevel = Convert.ToInt32(result);
                        }
                    }
                    catch
                    {
                        donationLevel = 0;
                    }
                    if (donationLevel > 0)
                    {
                        keys.Add(GetPrecinctKey(cop));
                    }
                }
                catch
                {
                }
            }
            return keys;
        }

        private static void ApplyCopAggressionToAllPrecincts(PlayerInfo humanPlayer)
        {
            if (humanPlayer == null)
            {
                return;
            }
            foreach (PlayerInfo cop in GetMunicipalCopPlayers())
            {
                TryAddAggroOn(cop, humanPlayer.PID);
                TryAddAggroOn(humanPlayer, cop.PID);
            }
        }

        private static void ClearCopTruceForPrecincts(PlayerInfo humanPlayer, HashSet<int> precinctKeys)
        {
            if (humanPlayer == null || precinctKeys == null || precinctKeys.Count == 0)
            {
                return;
            }
            foreach (PlayerInfo cop in GetMunicipalCopPlayers())
            {
                if (!precinctKeys.Contains(GetPrecinctKey(cop)))
                {
                    continue;
                }
                ForceClearAggroPair(cop, humanPlayer.PID);
                ForceClearAggroPair(humanPlayer, cop.PID);
            }
        }

        private static bool AnyCopAggroAgainstHuman(PlayerInfo humanPlayer)
        {
            if (humanPlayer == null)
            {
                return false;
            }
            return GetMunicipalCopPlayers().Any(cop => IsAggroOn(cop, humanPlayer.PID) || IsAggroOn(humanPlayer, cop.PID));
        }

        private static bool HasCopKillBuffBetween(PlayerInfo cop, PlayerInfo humanPlayer)
        {
            if (cop == null || humanPlayer == null)
            {
                return false;
            }
            return GameplayTweaks.GameplayTweaksPlugin.HasAnyRelationshipBuff(cop, humanPlayer, CopKillWarBuffIds)
                || GameplayTweaks.GameplayTweaksPlugin.HasAnyRelationshipBuff(humanPlayer, cop, CopKillWarBuffIds);
        }

        private static bool HasAnyCopKillWarBuffActive(PlayerInfo humanPlayer)
        {
            if (humanPlayer == null)
            {
                return false;
            }
            foreach (PlayerInfo cop in GetMunicipalCopPlayers())
            {
                if (cop == null || cop.PID.id == humanPlayer.PID.id)
                {
                    continue;
                }
                if (HasCopKillBuffBetween(cop, humanPlayer))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool HasTrackedCopWarState(PlayerInfo humanPlayer)
        {
            if (_activeIncidentScope != CopIncidentScope.None
                || GameplayTweaks.GameplayTweaksPlugin.SaveData.CopWarActive
                || GameplayTweaks.GameplayTweaksPlugin.SaveData.CopWarWitnessCount > 0
                || IsValidPrecinctKey(_activeWarPrecinctKey)
                || _activeWarVictimCopPlayerId >= 0
                || _activeWarKillerPeepId > 0UL
                || _resolvedWarPrecinctKeys.Count > 0)
            {
                return true;
            }

            if (humanPlayer == null)
            {
                return false;
            }

            return GetDesiredHostilePrecinctKeys(humanPlayer).Count > 0;
        }

        private static bool EnableQueuedCopWarAggro => true;
        private static bool EnableCopWitnessArrestEscalation => true;

        private static bool IsCopOrFedAggroOnHuman(PlayerInfo player)
        {
            if (player == null || !IsMunicipalCopPlayer(player))
            {
                return false;
            }
            PlayerInfo humanPlayer = GameplayTweaks.G.GetHumanPlayer();
            if (humanPlayer == null)
            {
                return false;
            }
            return IsAttackAllowedOneWay(player, humanPlayer.PID);
        }

        private static bool IsCrewPeepHidden(EntityID peepId)
        {
            if (peepId.IsNotValid)
            {
                return false;
            }
            try
            {
                var state = GameplayTweaks.GameplayTweaksPlugin.GetCrewStateOrNull(peepId);
                return state != null && (state.OnHideout || state.HideoutPending);
            }
            catch
            {
                return false;
            }
        }

        private static bool IsActiveWarCulpritHidden()
        {
            if (_activeWarKillerPeepId <= 0UL)
            {
                return false;
            }
            try
            {
                return IsCrewPeepHidden(EntityID.FromID(_activeWarKillerPeepId));
            }
            catch
            {
                return false;
            }
        }

        private static bool IsActiveWarCulpritInJail()
        {
            if (_activeWarKillerPeepId <= 0UL)
            {
                return false;
            }
            try
            {
                EntityID killerId = EntityID.FromID(_activeWarKillerPeepId);
                if (IsPeepInJailViaGameplayTweaks(killerId))
                {
                    return true;
                }
                var state = GameplayTweaks.GameplayTweaksPlugin.GetCrewStateOrNull(killerId);
                return state != null && state.InJail;
            }
            catch
            {
                return false;
            }
        }

        private static void MarkFreshRetaliationWindow(Entity killer, int precinctKey)
        {
            int nowDay = GameplayTweaks.G.GetNow().days;
            _freshRetaliationHumanPlayerId = GameplayTweaks.G.GetHumanPlayer()?.PID.id ?? PlayerID.HumanPlayer.id;
            _freshRetaliationGraceDay = nowDay;
            _freshRetaliationExpiryDay = nowDay + FreshRetaliationCleanupGraceDays;
            _freshRetaliationPrecinctKey = precinctKey;
            _freshRetaliationKillerPeepId = killer?.Id.id ?? 0UL;
            _freshRetaliationPendingCleanupProtection = true;
        }

        private static bool IsFreshRetaliationWindowActive(PlayerInfo humanPlayer, out string reason)
        {
            reason = "inactive";
            if (humanPlayer == null)
            {
                reason = "missing-human";
                return false;
            }
            int nowDay = GameplayTweaks.G.GetNow().days;
            if (_freshRetaliationHumanPlayerId >= 0 && humanPlayer.PID.id != _freshRetaliationHumanPlayerId)
            {
                reason = "different-human";
                return false;
            }
            if (_freshRetaliationGraceDay < 0 || _freshRetaliationExpiryDay < _freshRetaliationGraceDay || nowDay < _freshRetaliationGraceDay || nowDay > _freshRetaliationExpiryDay)
            {
                reason = "grace-expired";
                return false;
            }
            if (!_freshRetaliationPendingCleanupProtection)
            {
                reason = "cleanup-finished";
                return false;
            }
            if (_freshRetaliationKillerPeepId <= 0UL)
            {
                reason = "fresh-retaliation-missing-killer";
            }
            else
            {
                reason = IsCrewPeepHidden(EntityID.FromID(_freshRetaliationKillerPeepId)) ? "fresh-retaliation-hidden" : "fresh-retaliation";
            }
            if (IsValidPrecinctKey(_activeWarPrecinctKey) && IsValidPrecinctKey(_freshRetaliationPrecinctKey) && _activeWarPrecinctKey != _freshRetaliationPrecinctKey)
            {
                reason = "precinct-mismatch";
                return false;
            }
            if (!GameplayTweaks.GameplayTweaksPlugin.SaveData.CopWarActive && !AnyCopAggroAgainstHuman(humanPlayer))
            {
                reason = "no-war-or-aggro";
                return false;
            }
            return true;
        }

        private static void ClearFreshRetaliationWindow()
        {
            _freshRetaliationHumanPlayerId = -1;
            _freshRetaliationGraceDay = -1;
            _freshRetaliationExpiryDay = -1;
            _freshRetaliationPrecinctKey = InvalidPrecinctKey;
            _freshRetaliationKillerPeepId = 0UL;
            _freshRetaliationPendingCleanupProtection = false;
        }

        private static void ResetExplicitCopWarState()
        {
            GameplayTweaks.GameplayTweaksPlugin.SaveData.CopWarActive = false;
            GameplayTweaks.GameplayTweaksPlugin.SaveData.CopWarWitnessCount = 0;
            GameplayTweaks.GameplayTweaksPlugin.SaveData.CopWarKillIncidentActive = false;
            GameplayTweaks.GameplayTweaksPlugin.SaveData.CopWarPoliticalFavorGrandfathered = false;
            _activeWarPrecinctKey = InvalidPrecinctKey;
            _activeWarVictimCopPlayerId = -1;
            _activeWarKillerPeepId = 0UL;
            _activeIncidentScope = CopIncidentScope.None;
            _resolvedWarPrecinctKeys.Clear();
            ClearFreshRetaliationWindow();
        }

        private static void ClearDebugCopWarRuntimeTracking()
        {
            _pendingWitnessCases.Clear();
            _observedCopTargetByAttacker.Clear();
            _observedCopTargetDayByAttacker.Clear();
            _processedCopKillKeys.Clear();
            _processedGroupedCopAttackerCaseKeys.Clear();
            _incomingAttackDiagnosticKeys.Clear();
            _lastIncomingAttackDiagnosticDay = -1;
            _combatHookMonitorDay = -1;
            _sawCommittedCopCombatThisDay = false;
            _sawCombatIncidentHookThisDay = false;
            _loggedMissingCombatHookWarningThisDay = false;
            _lastAttackAdvisorFinalizerLogFrame = -1;
            _lastAttackAdvisorTurnFinalizerLogFrame = -1;
            _lastCombatAdvisorTruceFinalizerLogFrame = -1;
            _lastCombatAdvisorUpdateFinalizerLogFrame = -1;
        }

        private static void ActivateDebugResetSuppressionWindow(int frameDuration = 240, int dayDuration = 1)
        {
            _debugResetSuppressionUntilFrame = Mathf.Max(_debugResetSuppressionUntilFrame, Time.frameCount + Mathf.Max(1, frameDuration));
            try
            {
                _debugResetSuppressionUntilDay = Mathf.Max(_debugResetSuppressionUntilDay, GameplayTweaks.G.GetNow().days + Mathf.Max(1, dayDuration));
            }
            catch
            {
            }
        }

        private static void PrepareForDebugIncidentTransition(PlayerInfo humanPlayer, string source)
        {
            if (humanPlayer == null)
            {
                return;
            }

            bool wasKillScope = _activeIncidentScope == CopIncidentScope.Kill;
            bool targetingAssault = !string.IsNullOrEmpty(source) && source.IndexOf("assault", StringComparison.OrdinalIgnoreCase) >= 0;
            ResetExplicitCopWarState();
            ClearDebugCopWarRuntimeTracking();
            RemoveCopKillRelationshipBuffs(humanPlayer);
            _debugTransitionFromKillToAssaultPending = wasKillScope && targetingAssault;
            _pendingPostTransitionAssaultCleanup = false;
            _pendingPostTransitionAssaultCleanupFrame = -1;
            VerificationLog($"debug-transition source={source} suppressionUntilFrame={_debugResetSuppressionUntilFrame} suppressionUntilDay={_debugResetSuppressionUntilDay}");
        }

        private static bool ShouldLogSuppressionSkip(ref int lastLoggedFrame, int minFrameDelta = 90)
        {
            int currentFrame = Time.frameCount;
            if (lastLoggedFrame >= 0 && currentFrame - lastLoggedFrame < minFrameDelta)
            {
                return false;
            }

            lastLoggedFrame = currentFrame;
            return true;
        }

        private static bool TryGetAdvisorSuppressionReason(out string reason)
        {
            reason = null;
            if (IsAiStabilizationSuppressionActive())
            {
                reason = "stabilization";
                return true;
            }
            if (EnableQueuedCopWarAggro)
            {
                return false;
            }

            PlayerInfo humanPlayer = GameplayTweaks.G.GetHumanPlayer();
            if (humanPlayer == null)
            {
                return false;
            }

            bool hasExplicitWarState = _activeIncidentScope != CopIncidentScope.None
                || GameplayTweaks.GameplayTweaksPlugin.SaveData.CopWarActive
                || HasTrackedCopWarState(humanPlayer)
                || HasAnyCopKillWarBuffActive(humanPlayer);
            if (!hasExplicitWarState)
            {
                return false;
            }

            reason = "nonqueued-explicit-war";
            return true;
        }

        internal static void ProcessPendingDebugTransitionCleanup()
        {
            if (!_pendingPostTransitionAssaultCleanup)
            {
                return;
            }

            if (IsDebugResetSuppressionActive())
            {
                return;
            }

            if (_pendingPostTransitionAssaultCleanupFrame >= 0 && Time.frameCount < _pendingPostTransitionAssaultCleanupFrame)
            {
                return;
            }

            PlayerInfo humanPlayer = GameplayTweaks.G.GetHumanPlayer();
            if (humanPlayer == null || _activeIncidentScope != CopIncidentScope.Assault)
            {
                _pendingPostTransitionAssaultCleanup = false;
                _pendingPostTransitionAssaultCleanupFrame = -1;
                return;
            }

            _pendingPostTransitionAssaultCleanup = false;
            _pendingPostTransitionAssaultCleanupFrame = -1;
            ApplyExplicitPrecinctHostilityState(humanPlayer, "assault-post-transition-cleanup", refreshUi: true);
            LogCopWarStateSummary(humanPlayer, "assault-post-transition-cleanup");
        }

        private static bool IsDebugResetSuppressionActive()
        {
            if (_debugResetSuppressionUntilFrame >= Time.frameCount)
            {
                return true;
            }

            try
            {
                return _debugResetSuppressionUntilDay >= GameplayTweaks.G.GetNow().days;
            }
            catch
            {
                return false;
            }
        }

        private static void ActivatePoliticalFavorSuppressionWindow()
        {
            _politicalFavorSuppressionUntilFrame = Mathf.Max(_politicalFavorSuppressionUntilFrame, Time.frameCount + 240);
            try
            {
                _politicalFavorSuppressionUntilDay = Mathf.Max(_politicalFavorSuppressionUntilDay, GameplayTweaks.G.GetNow().days + 1);
            }
            catch
            {
            }
        }

        private static bool IsAiStabilizationSuppressionActive()
        {
            if (IsDebugResetSuppressionActive())
            {
                return true;
            }
            if (_politicalFavorSuppressionUntilFrame >= Time.frameCount)
            {
                return true;
            }
            try
            {
                return _politicalFavorSuppressionUntilDay >= GameplayTweaks.G.GetNow().days;
            }
            catch
            {
                return false;
            }
        }

        private static void QueueDebugCopWarReset()
        {
            if (_debugResetPending)
            {
                Debug.Log($"[CopKilling] Debug cop war reset already pending stage={_debugResetStage} frame={_debugResetRequestedFrame}");
                return;
            }

            _debugResetPending = true;
            _debugResetStage = 0;
            _debugResetRequestedFrame = Time.frameCount;
            _debugResetKillerPeepId = _activeWarKillerPeepId;
            Debug.Log($"[CopKilling] Debug cop war reset queued frame={_debugResetRequestedFrame} killer={_debugResetKillerPeepId} scope={_activeIncidentScope} precinct={_activeWarPrecinctKey}");
        }

        internal static void ProcessPendingDebugReset()
        {
            if (!_debugResetPending)
            {
                return;
            }

            try
            {
                PlayerInfo humanPlayer = GameplayTweaks.G.GetHumanPlayer();
                switch (_debugResetStage)
                {
                    case 0:
                        ActivateDebugResetSuppressionWindow();
                        ResetExplicitCopWarState();
                        ClearDebugCopWarRuntimeTracking();
                        _lastDebugIncidentFrame = -1;
                        _debugResetPending = false;
                        _debugResetStage = -1;
                        _debugResetRequestedFrame = -1;
                        _debugResetKillerPeepId = 0UL;
                        VerificationLog($"debug-reset stage=0 frame={Time.frameCount}");
                        Debug.Log($"[CopKilling] Debug cop war state reset. minimal=True suppressionUntilFrame={_debugResetSuppressionUntilFrame}");
                        break;
                }
            }
            catch (Exception ex)
            {
                _debugResetPending = false;
                _debugResetStage = -1;
                _debugResetRequestedFrame = -1;
                _debugResetKillerPeepId = 0UL;
                Debug.LogWarning("[CopKilling] ProcessPendingDebugReset failed: " + ex.Message);
            }
        }

        private static AggroCleanupResult ForceClearAllCopAggro(PlayerInfo humanPlayer, bool silent)
        {
            AggroCleanupResult result = new AggroCleanupResult
            {
                Silent = silent
            };
            if (humanPlayer == null)
            {
                return result;
            }

            List<PlayerInfo> cops = GetMunicipalCopPlayers()
                .Where(cop => cop != null && cop.PID.id != humanPlayer.PID.id)
                .ToList();

            result.ConsideredCount = cops.Count;
            foreach (PlayerInfo cop in cops)
            {
                if (IsAggroOn(cop, humanPlayer.PID) || IsAggroOn(humanPlayer, cop.PID))
                {
                    result.HadAggroCount++;
                }
            }

            for (int pass = 0; pass < 3; pass++)
            {
                foreach (PlayerInfo cop in cops)
                {
                    ForceClearAggroPair(cop, humanPlayer.PID);
                    ForceClearAggroPair(humanPlayer, cop.PID);
                }
            }

            foreach (PlayerInfo cop in cops)
            {
                if (!IsAggroOn(cop, humanPlayer.PID) && !IsAggroOn(humanPlayer, cop.PID))
                {
                    result.ClearedCount++;
                }
            }

            Debug.Log($"{VerificationLogPrefix} [CopKilling] force aggro clear pass cops={result.ConsideredCount} hadAggro={result.HadAggroCount} cleared={result.ClearedCount} silent={result.Silent}");
            return result;
        }

        internal static void ResetDebugCopWarState()
        {
            QueueDebugCopWarReset();
        }

        private static void TryClearDebugNationalHeatState(ulong killerPeepId)
        {
            try
            {
                var saveData = GameplayTweaks.GameplayTweaksPlugin.SaveData;
                if (saveData?.NationalHeat?.WitnessEntries == null)
                {
                    return;
                }

                long killerLongId = killerPeepId > 0UL ? unchecked((long)killerPeepId) : 0L;
                saveData.NationalHeat.WitnessEntries.RemoveAll(entry =>
                    entry != null
                    && (entry.SourceType == GameplayTweaks.GameplayTweaksPlugin.NATIONAL_HEAT_SOURCE_COP_KILL
                        || entry.SourceType == GameplayTweaks.GameplayTweaksPlugin.NATIONAL_HEAT_SOURCE_COP_ASSAULT)
                    && (killerLongId == 0L || entry.CrewPeepId == killerLongId));

                if (saveData.NationalHeat.WitnessEntries.Count == 0)
                {
                    saveData.NationalHeat.Active = false;
                    saveData.NationalHeat.Level = 0;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[CopKilling] TryClearDebugNationalHeatState failed: " + ex.Message);
            }
        }

        private static bool HasActivePoliticalProtection(PlayerInfo humanPlayer)
        {
            if (humanPlayer == null || !humanPlayer.PID.IsHumanPlayer)
            {
                return false;
            }
            try
            {
                Type crewRelationsType = Type.GetType("GameplayTweaks.GameplayTweaksPlugin+CrewRelationshipHandlerPatch, GameplayTweaks");
                MethodInfo method = crewRelationsType?.GetMethod("IsHumanPoliticalBribeActive", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
                return method != null && method.Invoke(null, null) is bool flag && flag;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[CopKilling] Political protection check failed: " + ex.Message);
                return false;
            }
        }

        private static bool IsCopWitnessSourceType(int sourceType)
        {
            return sourceType == GameplayTweaks.GameplayTweaksPlugin.NATIONAL_HEAT_SOURCE_COP_KILL
                || sourceType == GameplayTweaks.GameplayTweaksPlugin.NATIONAL_HEAT_SOURCE_COP_ASSAULT;
        }

        private static HashSet<ulong> GetOutstandingCopTargetPeepIds()
        {
            HashSet<ulong> outstandingTargets = new HashSet<ulong>();
            try
            {
                var nationalHeat = GameplayTweaks.GameplayTweaksPlugin.SaveData?.NationalHeat;
                if (nationalHeat?.WitnessEntries == null)
                {
                    return outstandingTargets;
                }

                foreach (var entry in nationalHeat.WitnessEntries)
                {
                    if (entry == null || entry.ArrestProcessed || !IsCopWitnessSourceType(entry.SourceType) || entry.CrewPeepId <= 0L)
                    {
                        continue;
                    }

                    EntityID peepId = EntityID.FromID((ulong)entry.CrewPeepId);
                    if (GameplayTweaks.GameplayTweaksPlugin.IsCrewCurrentlyJailed(peepId))
                    {
                        continue;
                    }

                    outstandingTargets.Add(peepId.id);
                }
            }
            catch
            {
            }
            return outstandingTargets;
        }

        private static void BeginKillIncidentPoliticalGate(PlayerInfo humanPlayer, bool politicalFavorGrandfathered, string sourceTag)
        {
            var saveData = GameplayTweaks.GameplayTweaksPlugin.SaveData;
            if (saveData == null)
            {
                return;
            }

            if (saveData.CopWarKillIncidentActive)
            {
                VerificationLog($"political-favor kill-gate preserved source={sourceTag} grandfathered={saveData.CopWarPoliticalFavorGrandfathered}");
                return;
            }

            saveData.CopWarKillIncidentActive = true;
            saveData.CopWarPoliticalFavorGrandfathered = politicalFavorGrandfathered;
            VerificationLog($"political-favor kill-gate initialized source={sourceTag} grandfathered={politicalFavorGrandfathered} bribeActive={HasActivePoliticalProtection(humanPlayer)}");
        }

        private static bool IsPoliticalFavorLockedBehindPreKillBribe(PlayerInfo humanPlayer, out int outstandingTargetCount)
        {
            outstandingTargetCount = 0;
            var saveData = GameplayTweaks.GameplayTweaksPlugin.SaveData;
            if (humanPlayer == null
                || saveData == null
                || !saveData.CopWarKillIncidentActive
                || saveData.CopWarPoliticalFavorGrandfathered
                || HasActivePoliticalProtection(humanPlayer))
            {
                return false;
            }

            outstandingTargetCount = GetOutstandingCopTargetPeepIds().Count;
            return outstandingTargetCount > 0;
        }

        public static bool IsPoliticalBribeActivationLockedForHumanPlayer()
        {
            try
            {
                PlayerInfo humanPlayer = GameplayTweaks.G.GetHumanPlayer();
                bool locked = IsPoliticalFavorLockedBehindPreKillBribe(humanPlayer, out int outstandingTargetCount);
                if (locked)
                {
                    VerificationLog($"political-bribe activation-locked outstandingTargets={outstandingTargetCount}");
                }
                return locked;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CopKilling] IsPoliticalBribeActivationLockedForHumanPlayer failed: {ex}");
                return false;
            }
        }

        public static bool IsPoliticalFavorLockedBehindPreKillBribeForHumanPlayer()
        {
            try
            {
                PlayerInfo humanPlayer = GameplayTweaks.G.GetHumanPlayer();
                bool locked = IsPoliticalFavorLockedBehindPreKillBribe(humanPlayer, out int outstandingTargetCount);
                if (locked)
                {
                    VerificationLog($"political-favor prekill-lock outstandingTargets={outstandingTargetCount}");
                }
                return locked;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CopKilling] IsPoliticalFavorLockedBehindPreKillBribeForHumanPlayer failed: {ex}");
                return false;
            }
        }

        private static HashSet<int> GetDesiredHostilePrecinctKeys(PlayerInfo humanPlayer)
        {
            HashSet<int> desired = new HashSet<int>();
            if (humanPlayer == null || _activeIncidentScope == CopIncidentScope.None)
            {
                return desired;
            }

            HashSet<int> bribedPrecincts = new HashSet<int>(_resolvedWarPrecinctKeys);
            bool culpritInJail = IsActiveWarCulpritInJail();

            if (IsValidPrecinctKey(_activeWarPrecinctKey) && !bribedPrecincts.Contains(_activeWarPrecinctKey))
            {
                desired.Add(_activeWarPrecinctKey);
            }

            // Hideout should not shrink a live cop-kill war to a single precinct. Jail still can.
            if (_activeIncidentScope != CopIncidentScope.Kill || culpritInJail)
            {
                return desired;
            }

            foreach (PlayerInfo cop in GetMunicipalCopPlayers())
            {
                if (cop == null || cop.PID.id == humanPlayer.PID.id)
                {
                    continue;
                }
                int precinctKey = GetPrecinctKey(cop);
                if (precinctKey == InvalidPrecinctKey || precinctKey == _activeWarPrecinctKey)
                {
                    continue;
                }
                if (bribedPrecincts.Contains(precinctKey))
                {
                    continue;
                }
                desired.Add(precinctKey);
            }
            return desired;
        }

        private static void LogCopWarStateSummary(PlayerInfo humanPlayer, string sourceTag)
        {
            if (humanPlayer == null)
            {
                VerificationLog($"cop-war-state source={sourceTag} humanMissing=True");
                return;
            }

            HashSet<int> desiredPrecincts = GetDesiredHostilePrecinctKeys(humanPlayer);
            HashSet<int> resolvedPrecincts = new HashSet<int>(_resolvedWarPrecinctKeys);
            bool culpritHidden = IsActiveWarCulpritHidden();
            bool culpritInJail = IsActiveWarCulpritInJail();
            bool hasCopKillBuffs = HasTrackedCopWarState(humanPlayer) || HasAnyCopKillWarBuffActive(humanPlayer);
            bool politicalProtectionActive = HasActivePoliticalProtection(humanPlayer);
            int witnessCases = _pendingWitnessCases?.Count ?? 0;
            bool killGateActive = GameplayTweaks.GameplayTweaksPlugin.SaveData?.CopWarKillIncidentActive ?? false;
            bool favorGrandfathered = GameplayTweaks.GameplayTweaksPlugin.SaveData?.CopWarPoliticalFavorGrandfathered ?? false;
            int outstandingTargets = GetOutstandingCopTargetPeepIds().Count;
            VerificationLog(
                $"cop-war-state source={sourceTag} scope={_activeIncidentScope} active={GameplayTweaks.GameplayTweaksPlugin.SaveData.CopWarActive} " +
                $"victimPrecinct={_activeWarPrecinctKey} victimCopPid={_activeWarVictimCopPlayerId} killerPeep={_activeWarKillerPeepId} " +
                $"desiredPrecincts=[{string.Join(",", desiredPrecincts.OrderBy(v => v))}] resolvedPrecincts=[{string.Join(",", resolvedPrecincts.OrderBy(v => v))}] " +
                $"hidden={culpritHidden} jailed={culpritInJail} politicalProtection={politicalProtectionActive} hasBuffs={hasCopKillBuffs} witnessCases={witnessCases} killGateActive={killGateActive} favorGrandfathered={favorGrandfathered} outstandingTargets={outstandingTargets}");
        }

        private static void RemovePoliticalProtectionRelationshipBuffs(PlayerInfo humanPlayer)
        {
        }

        private static void ApplyPoliticalProtectionRelationshipBuffs(PlayerInfo humanPlayer, HashSet<int> resolvedPrecincts)
        {
        }

        private static void ApplyExplicitPrecinctHostilityState(PlayerInfo humanPlayer, string sourceTag, bool refreshUi)
        {
            if (humanPlayer == null)
            {
                return;
            }

            if (IsApplyingExplicitHostilityState)
            {
                int frame = Time.frameCount;
                if (_lastExplicitHostilityReentryLogFrame != frame)
                {
                    _lastExplicitHostilityReentryLogFrame = frame;
                    VerificationLog($"explicit-hostility reentry skipped source={sourceTag} frame={frame} depth={_explicitHostilityApplyDepth}");
                }
                return;
            }

            _explicitHostilityApplyDepth++;
            try
            {
                HashSet<int> desiredPrecincts = GetDesiredHostilePrecinctKeys(humanPlayer);
                bool deferCrossPrecinctAggroClear = _debugTransitionFromKillToAssaultPending
                    && _activeIncidentScope == CopIncidentScope.Assault
                    && string.Equals(sourceTag, "assault-immediate", StringComparison.OrdinalIgnoreCase);
                RemoveCopKillRelationshipBuffs(humanPlayer);
                RemovePoliticalProtectionRelationshipBuffs(humanPlayer);
                if (desiredPrecincts.Count > 0)
                {
                    Entity killerPeep = _activeWarKillerPeepId > 0UL ? EntityID.FromID(_activeWarKillerPeepId).FindEntity() : null;
                    ApplyCopIncidentRelationshipBuffs(humanPlayer, _activeWarPrecinctKey, killerPeep, _activeIncidentScope);
                }
                int hostileCopCount = 0;
                int hostileBuffConfirmedCount = 0;
                int hostileNegativeRelationCount = 0;
                int hostileAttackAllowedCount = 0;
                int deferredClearCount = 0;

                foreach (PlayerInfo cop in GetMunicipalCopPlayers())
                {
                    if (cop == null || cop.PID.id == humanPlayer.PID.id)
                    {
                        continue;
                    }
                    int precinctKey = GetPrecinctKey(cop);
                    if (desiredPrecincts.Contains(precinctKey))
                    {
                        if (EnableQueuedCopWarAggro)
                        {
                            ActivateWarBetweenPlayers(cop, humanPlayer);
                            if (!IsAggroOn(cop, humanPlayer.PID))
                            {
                                TryAddAggroOn(cop, humanPlayer.PID);
                            }
                            if (!IsAggroOn(humanPlayer, cop.PID))
                            {
                                TryAddAggroOn(humanPlayer, cop.PID);
                            }
                        }
                        hostileCopCount++;
                        if (GameplayTweaks.GameplayTweaksPlugin.GetInterGangRelationshipDisplayScore(cop, humanPlayer) < 0)
                        {
                            hostileNegativeRelationCount++;
                        }
                        if (HasCopKillBuffBetween(cop, humanPlayer))
                        {
                            hostileBuffConfirmedCount++;
                        }
                        if (IsAttackAllowedOneWay(cop, humanPlayer.PID))
                        {
                            hostileAttackAllowedCount++;
                        }
                        continue;
                    }
                    if (EnableQueuedCopWarAggro && deferCrossPrecinctAggroClear)
                    {
                        deferredClearCount++;
                        continue;
                    }
                    ForceClearAggroPair(cop, humanPlayer.PID);
                    ForceClearAggroPair(humanPlayer, cop.PID);
                }

                bool hostilityCleared = desiredPrecincts.Count <= 0;
                if (hostilityCleared)
                {
                    AggroCleanupResult cleanupResult = ClearResidualCopAggroWithoutBuffs(humanPlayer, silent: false);
                    VerificationLog($"explicit-hostility cleared source={sourceTag} hadAggro={cleanupResult.HadAggroCount} cleared={cleanupResult.ClearedCount}");
                    ResetExplicitCopWarState();
                }
                else
                {
                    GameplayTweaks.GameplayTweaksPlugin.SaveData.CopWarActive = true;
                    VerificationLog($"explicit-hostility source={sourceTag} scope={_activeIncidentScope} precincts={string.Join(",", desiredPrecincts.OrderBy(v => v))} hostileCops={hostileCopCount} buffConfirmed={hostileBuffConfirmedCount} negativeRelation={hostileNegativeRelationCount} attackAllowed={hostileAttackAllowedCount} assaultStacked={(_activeIncidentScope == CopIncidentScope.Assault)} killerHidden={IsActiveWarCulpritHidden()} killerJailed={IsActiveWarCulpritInJail()} queuedOnly={!EnableQueuedCopWarAggro}");
                    if (EnableQueuedCopWarAggro && deferCrossPrecinctAggroClear)
                    {
                        VerificationLog($"explicit-hostility deferred-cross-precinct-clear source={sourceTag} deferredPairs={deferredClearCount}");
                        _pendingPostTransitionAssaultCleanup = true;
                        _pendingPostTransitionAssaultCleanupFrame = Time.frameCount + 90;
                    }
                }

                ApplyPoliticalProtectionRelationshipBuffs(humanPlayer, _resolvedWarPrecinctKeys);

                if (EnableQueuedCopWarAggro && deferCrossPrecinctAggroClear)
                {
                    _debugTransitionFromKillToAssaultPending = false;
                }

                if (refreshUi || hostilityCleared)
                {
                    RefreshCopAggroUi(hostilityCleared && !refreshUi ? sourceTag + "-cleared" : sourceTag);
                }
            }
            finally
            {
                _explicitHostilityApplyDepth = Mathf.Max(0, _explicitHostilityApplyDepth - 1);
            }
        }

        private static void RelaxBribedPrecinctAggroDuringFreshRetaliation(PlayerInfo humanPlayer)
        {
            if (humanPlayer == null)
            {
                return;
            }
            HashSet<int> bribedPrecincts = new HashSet<int>(_resolvedWarPrecinctKeys);
            if (bribedPrecincts.Count == 0)
            {
                return;
            }
            HashSet<int> precinctsToCalm = new HashSet<int>(bribedPrecincts);
            if (IsValidPrecinctKey(_activeWarPrecinctKey))
            {
                precinctsToCalm.Remove(_activeWarPrecinctKey);
            }
            if (precinctsToCalm.Count == 0)
            {
                return;
            }
            ClearCopTruceForPrecincts(humanPlayer, precinctsToCalm);
            VerificationLog($"fresh-retaliation bribed-precinct-truce preservedActivePrecinct={_activeWarPrecinctKey} cooledPrecincts={string.Join(",", precinctsToCalm.OrderBy(v => v))}");
        }

        private static AggroCleanupResult ClearResidualCopAggroWithoutBuffs(PlayerInfo humanPlayer, bool silent)
        {
            AggroCleanupResult result = new AggroCleanupResult
            {
                Silent = silent
            };
            if (humanPlayer == null)
            {
                return result;
            }
            foreach (PlayerInfo cop in GetMunicipalCopPlayers())
            {
                if (cop == null || cop.PID.id == humanPlayer.PID.id)
                {
                    continue;
                }
                if (HasCopKillBuffBetween(cop, humanPlayer))
                {
                    continue;
                }
                result.ConsideredCount++;
                bool copAggroOnHuman = IsAggroOn(cop, humanPlayer.PID);
                bool humanAggroOnCop = IsAggroOn(humanPlayer, cop.PID);
                if (!copAggroOnHuman && !humanAggroOnCop)
                {
                    continue;
                }
                result.HadAggroCount++;
                if (copAggroOnHuman)
                {
                    ForceClearAggroPair(cop, humanPlayer.PID);
                }
                if (humanAggroOnCop)
                {
                    ForceClearAggroPair(humanPlayer, cop.PID);
                }
                bool stillAggro = IsAggroOn(cop, humanPlayer.PID) || IsAggroOn(humanPlayer, cop.PID);
                if (!stillAggro)
                {
                    result.ClearedCount++;
                }
            }
            if (!result.Silent || result.HadAggroCount > 0 || result.ClearedCount > 0)
            {
                Debug.Log($"{VerificationLogPrefix} [CopKilling] residual aggro clear pass cops={result.ConsideredCount} hadAggro={result.HadAggroCount} cleared={result.ClearedCount} silent={result.Silent}");
            }
            return result;
        }

        private static void TryNormalizeSafeHookCopAggro(PlayerInfo humanPlayer, string sourceTag)
        {
            if (humanPlayer == null)
            {
                return;
            }
            int frame = Time.frameCount;
            if (_lastSafeHookNormalizationFrame == frame)
            {
                return;
            }
            _lastSafeHookNormalizationFrame = frame;
            if (GameplayTweaks.GameplayTweaksPlugin.SaveData.CopWarActive)
            {
                return;
            }
            if (HasTrackedCopWarState(humanPlayer))
            {
                return;
            }
            if (IsFreshRetaliationWindowActive(humanPlayer, out _))
            {
                return;
            }
            AggroCleanupResult cleanupResult = ClearResidualCopAggroWithoutBuffs(humanPlayer, silent: true);
            if (cleanupResult.HadAggroCount <= 0)
            {
                return;
            }
            VerificationLog($"safe-hook aggro normalization source={sourceTag} hadAggro={cleanupResult.HadAggroCount} cleared={cleanupResult.ClearedCount} frame={frame}");
            RefreshCopAggroUi("safe-" + sourceTag);
        }

        private static void TryRelaxBribedPrecinctAggroFromSafeHooks(PlayerInfo humanPlayer, string sourceTag)
        {
            if (humanPlayer == null)
            {
                return;
            }
            int frame = Time.frameCount;
            if (_lastSafeHookBribedPrecinctRelaxFrame == frame)
            {
                return;
            }
            _lastSafeHookBribedPrecinctRelaxFrame = frame;

            HashSet<int> bribedPrecincts = new HashSet<int>(_resolvedWarPrecinctKeys);
            if (bribedPrecincts.Count == 0)
            {
                return;
            }

            if (IsFreshRetaliationWindowActive(humanPlayer, out _))
            {
                RelaxBribedPrecinctAggroDuringFreshRetaliation(humanPlayer);
                return;
            }

            int hadAggro = 0;
            int cleared = 0;
            foreach (PlayerInfo cop in GetMunicipalCopPlayers())
            {
                if (cop == null || cop.PID.id == humanPlayer.PID.id)
                {
                    continue;
                }
                if (!bribedPrecincts.Contains(GetPrecinctKey(cop)))
                {
                    continue;
                }

                bool copAggroOnHuman = IsAggroOn(cop, humanPlayer.PID);
                bool humanAggroOnCop = IsAggroOn(humanPlayer, cop.PID);
                if (!copAggroOnHuman && !humanAggroOnCop)
                {
                    continue;
                }

                hadAggro++;
                if (copAggroOnHuman)
                {
                    ForceClearAggroPair(cop, humanPlayer.PID);
                }
                if (humanAggroOnCop)
                {
                    ForceClearAggroPair(humanPlayer, cop.PID);
                }
                if (!IsAggroOn(cop, humanPlayer.PID) && !IsAggroOn(humanPlayer, cop.PID))
                {
                    cleared++;
                }
            }

            if (hadAggro <= 0)
            {
                return;
            }

            VerificationLog($"safe-hook bribed-precinct relax source={sourceTag} precincts={bribedPrecincts.Count} hadAggro={hadAggro} cleared={cleared} frame={frame}");
            RefreshCopAggroUi("safe-bribed-" + sourceTag);
        }

        private static void TryReconcileSafeHookCopWarState(PlayerInfo humanPlayer, string sourceTag)
        {
            if (humanPlayer == null)
            {
                return;
            }
            int frame = Time.frameCount;
            if (_lastSafeHookStateReconcileFrame == frame)
            {
                return;
            }
            _lastSafeHookStateReconcileFrame = frame;
            if (IsFreshRetaliationWindowActive(humanPlayer, out _))
            {
                return;
            }
            if (TryResetResolvedJailedCopWarState(humanPlayer, "safe-state-" + sourceTag))
            {
                return;
            }
            bool hadTrackedWarState = HasTrackedCopWarState(humanPlayer);
            if (!hadTrackedWarState && !HasAnyCopKillWarBuffActive(humanPlayer))
            {
                return;
            }

            bool hasAnyCopKillBuff = HasTrackedCopWarState(humanPlayer) || HasAnyCopKillWarBuffActive(humanPlayer);
            bool anyAggro = AnyCopAggroAgainstHuman(humanPlayer);
            if (hasAnyCopKillBuff || anyAggro)
            {
                return;
            }
            if (!hadTrackedWarState)
            {
                return;
            }

            ResetExplicitCopWarState();
            VerificationLog($"safe-hook war-state reset source={sourceTag} frame={frame}");
            RefreshCopAggroUi("safe-state-" + sourceTag);
        }

        internal static void NotifySafeInteractionMaintenance(string sourceTag)
        {
            if (_safeInteractionMaintenanceDepth > 0)
            {
                return;
            }
            try
            {
                _safeInteractionMaintenanceDepth++;
                PlayerInfo humanPlayer = GameplayTweaks.G.GetHumanPlayer();
                if (humanPlayer == null)
                {
                    return;
                }
                TryRelaxBribedPrecinctAggroFromSafeHooks(humanPlayer, sourceTag);
                TryNormalizeSafeHookCopAggro(humanPlayer, sourceTag);
                TryReconcileSafeHookCopWarState(humanPlayer, sourceTag);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[CopKilling] NotifySafeInteractionMaintenance failed source={sourceTag}: {ex.Message}");
            }
            finally
            {
                _safeInteractionMaintenanceDepth = Mathf.Max(0, _safeInteractionMaintenanceDepth - 1);
            }
        }

        private static void CacheJailSystemMethods()
        {
            if (_jailSystemType != null)
            {
                return;
            }
            try
            {
                Type pluginType = typeof(GameplayTweaks.GameplayTweaksPlugin);
                _jailSystemType = pluginType.GetNestedType("JailSystem", BindingFlags.NonPublic | BindingFlags.Public)
                    ?? pluginType.Assembly.GetType("GameplayTweaks.JailSystem");
                if (_jailSystemType == null)
                {
                    return;
                }
                _jailArrestCrewMethod = _jailSystemType.GetMethod("ArrestCrew", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[2] { typeof(PlayerInfo), typeof(Entity) }, null);
                _jailIsInJailMethod = _jailSystemType.GetMethod("IsInJail", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[1] { typeof(EntityID) }, null);
            }
            catch
            {
            }
        }

        private static bool TryArrestCrewViaGameplayTweaks(PlayerInfo humanPlayer, Entity killer)
        {
            if (humanPlayer == null || killer == null)
            {
                return false;
            }
            try
            {
                CacheJailSystemMethods();
                if (_jailArrestCrewMethod == null)
                {
                    return false;
                }
                object result = _jailArrestCrewMethod.Invoke(null, new object[2] { humanPlayer, killer });
                return result is bool b && b;
            }
            catch
            {
                return false;
            }
        }

        private static bool IsPeepInJailViaGameplayTweaks(EntityID peepId)
        {
            if (peepId.IsNotValid)
            {
                return false;
            }
            try
            {
                CacheJailSystemMethods();
                if (_jailIsInJailMethod == null)
                {
                    return false;
                }
                object result = _jailIsInJailMethod.Invoke(null, new object[1] { peepId });
                return result is bool b && b;
            }
            catch
            {
                return false;
            }
        }

        private static string GetCopDisplayName(PlayerInfo cop)
        {
            return cop?.social?.PlayerGroupName ?? ("Precinct#" + (cop?.PID.id ?? -1));
        }

        private static bool RemoveDirectedRelationshipBuff(PlayerInfo source, PlayerInfo target, string buffId)
        {
            return GameplayTweaks.GameplayTweaksPlugin.RemoveDirectedRelationshipBuff(source, target, buffId);
        }

        private static void RemoveCopKillRelationshipBuffs(PlayerInfo humanPlayer)
        {
            if (humanPlayer == null)
            {
                return;
            }
            foreach (PlayerInfo cop in GetMunicipalCopPlayers())
            {
                if (cop == null || cop.PID.id == humanPlayer.PID.id)
                {
                    continue;
                }
                foreach (string buffId in CopKillWarBuffIds)
                {
                    RemoveDirectedRelationshipBuff(cop, humanPlayer, buffId);
                    RemoveDirectedRelationshipBuff(humanPlayer, cop, buffId);
                }
            }
        }

        private static bool HasActiveWitnessEvidence(EntityID killerId)
        {
            if (killerId.IsNotValid)
            {
                return false;
            }
            try
            {
                var state = GameplayTweaks.GameplayTweaksPlugin.GetCrewStateOrNull(killerId);
                return state != null && (state.HasWitness || state.WitnessCount > 0);
            }
            catch
            {
                return false;
            }
        }

        private static void EnsureWitnessEvidence(EntityID killerId, int minimumWitnessCount = 1)
        {
            if (killerId.IsNotValid)
            {
                return;
            }
            try
            {
                var state = GameplayTweaks.GameplayTweaksPlugin.GetOrCreateCrewState(killerId);
                if (state == null)
                {
                    return;
                }
                state.HasWitness = true;
                state.WitnessCount = Mathf.Max(minimumWitnessCount, state.WitnessCount);
            }
            catch
            {
            }
        }

        private static PendingCopWitnessCase QueuePendingWitnessCase(Entity killer, PlayerInfo victimCopGang)
        {
            if (killer == null || killer.Id.IsNotValid)
            {
                return null;
            }
            PendingCopWitnessCase existing = _pendingWitnessCases.FirstOrDefault(x => x.KillerPeepId == killer.Id.id);
            if (existing == null)
            {
                existing = new PendingCopWitnessCase();
                _pendingWitnessCases.Add(existing);
            }
            existing.KillerPeepId = killer.Id.id;
            existing.VictimPrecinctKey = (victimCopGang != null) ? GetPrecinctKey(victimCopGang) : -1;
            existing.VictimCopPlayerId = victimCopGang?.PID.id ?? -1;
            existing.DayCreated = GameplayTweaks.G.GetNow().days;
            existing.WitnessEvidenceConfirmed = false;
            existing.HardMaxApplied = false;
            existing.PoliticalFavorGrandfathered = HasActivePoliticalProtection(GameplayTweaks.G.GetHumanPlayer());
            return existing;
        }

        private static int ResolveVictimPrecinctKey(PlayerInfo victimCopGang, out int victimCopPlayerId)
        {
            victimCopPlayerId = -1;
            try
            {
                if (victimCopGang != null)
                {
                    victimCopPlayerId = victimCopGang.PID.id;
                    return GetPrecinctKey(victimCopGang);
                }
                if (IsValidPrecinctKey(_activeWarPrecinctKey))
                {
                    victimCopPlayerId = _activeWarVictimCopPlayerId;
                    return _activeWarPrecinctKey;
                }
                PlayerInfo fallbackCop = GetMunicipalCopPlayers().FirstOrDefault(c => c != null && c.PID.id != GameplayTweaks.G.GetHumanPlayer()?.PID.id && (c.crew == null || !c.crew.IsCrewDefeated));
                if (fallbackCop != null)
                {
                    victimCopPlayerId = fallbackCop.PID.id;
                    return GetPrecinctKey(fallbackCop);
                }
            }
            catch
            {
            }
            return -1;
        }

        private static void ForceHardMaxWantedAndFeds(EntityID killerId)
        {
            if (killerId.IsNotValid)
            {
                return;
            }
            try
            {
                GameplayTweaks.GameplayTweaksPlugin.ForceHardMaxLocalHeatAndFeds(killerId, 3, FedArrestCountdownDays);
            }
            catch
            {
            }
        }

        private static void ForceHardMaxWantedWithoutFeds(EntityID attackerId)
        {
            if (attackerId.IsNotValid)
            {
                return;
            }
            try
            {
                GameplayTweaks.GameplayTweaksPlugin.ForceHardMaxLocalHeat(attackerId, 1);
            }
            catch
            {
            }
        }

        private static void ApplyImmediateCopKillRetaliation(PlayerInfo humanPlayer, Entity killer, PlayerInfo victimCopGang, bool applyHeatAndFeds = true, bool diagnosticLogs = false, bool stopAfterSetup = false, bool stopAfterFreshWindow = false)
        {
            if (humanPlayer == null || killer == null)
            {
                return;
            }
            if (applyHeatAndFeds)
            {
                ForceHardMaxWantedAndFeds(killer.Id);
            }
            int victimCopPlayerId;
            int victimPrecinctKey = ResolveVictimPrecinctKey(victimCopGang, out victimCopPlayerId);
            if (IsValidPrecinctKey(victimPrecinctKey))
            {
                _activeWarPrecinctKey = victimPrecinctKey;
                _activeWarVictimCopPlayerId = victimCopPlayerId;
            }
            _activeWarKillerPeepId = killer.Id.id;
            _activeIncidentScope = CopIncidentScope.Kill;
            var saveData = GameplayTweaks.GameplayTweaksPlugin.SaveData;
            saveData.CopWarActive = true;
            saveData.CopWarWitnessCount = Mathf.Max(saveData.CopWarWitnessCount, 2);
            saveData.LastCopKillDay = GameplayTweaks.G.GetNow().days;
            BeginKillIncidentPoliticalGate(humanPlayer, HasActivePoliticalProtection(humanPlayer), "kill-immediate");
            _resolvedWarPrecinctKeys.Clear();
            if (stopAfterSetup)
            {
                return;
            }
            MarkFreshRetaliationWindow(killer, victimPrecinctKey);
            if (stopAfterFreshWindow)
            {
                return;
            }
            ApplyExplicitPrecinctHostilityState(humanPlayer, "kill-immediate", refreshUi: false);
            VerificationLog("Cop kill immediate retaliation applied.");
            VerificationLog("Cop kill explicit hostility queued + precinct key=" + victimPrecinctKey + ".");
            VerificationLog("Cop war activated (immediate).");
            LogCopWarStateSummary(humanPlayer, "kill-immediate");
        }

        private static bool DebugAssaultDiagnosticStopAfterRelationshipBuffs => false;

        private static bool DebugAssaultDiagnosticStopAfterFreshWindow => false;

        private static bool DebugAssaultDiagnosticStopAfterExplicitHostility => false;

        private static void ApplyImmediateCopAssaultRetaliation(PlayerInfo humanPlayer, Entity attacker, PlayerInfo victimCopGang, bool diagnosticLogs = false, bool stopAfterRelationshipBuffs = false)
        {
            if (humanPlayer == null || attacker == null)
            {
                return;
            }
            if (diagnosticLogs)
            {
                VerificationLog($"assault-stage stage=setup-enter peep={attacker.Id.id}");
            }
            ForceHardMaxWantedWithoutFeds(attacker.Id);
            int victimCopPlayerId;
            int victimPrecinctKey = ResolveVictimPrecinctKey(victimCopGang, out victimCopPlayerId);
            if (IsValidPrecinctKey(victimPrecinctKey))
            {
                _activeWarPrecinctKey = victimPrecinctKey;
                _activeWarVictimCopPlayerId = victimCopPlayerId;
            }
            _activeWarKillerPeepId = attacker.Id.id;
            _activeIncidentScope = CopIncidentScope.Assault;
            var saveData = GameplayTweaks.GameplayTweaksPlugin.SaveData;
            saveData.CopWarActive = true;
            saveData.CopWarWitnessCount = Mathf.Max(saveData.CopWarWitnessCount, 1);
            _resolvedWarPrecinctKeys.Clear();
            if (diagnosticLogs)
            {
                VerificationLog($"assault-stage stage=setup-ready precinct={victimPrecinctKey} victimPid={victimCopPlayerId}");
            }
            if (diagnosticLogs)
            {
                VerificationLog($"assault-stage stage=after-relationship-buffs precinct={victimPrecinctKey}");
            }
            if (stopAfterRelationshipBuffs)
            {
                VerificationLog($"assault-stage stage=diagnostic-stop-after-buffs precinct={victimPrecinctKey}");
                return;
            }
            MarkFreshRetaliationWindow(attacker, victimPrecinctKey);
            if (diagnosticLogs)
            {
                VerificationLog($"assault-stage stage=after-fresh-window precinct={victimPrecinctKey}");
            }
            if (stopAfterRelationshipBuffs || (diagnosticLogs && DebugAssaultDiagnosticStopAfterFreshWindow))
            {
                VerificationLog($"assault-stage stage=diagnostic-stop-after-fresh-window precinct={victimPrecinctKey}");
                return;
            }
            ApplyExplicitPrecinctHostilityState(humanPlayer, "assault-immediate", refreshUi: false);
            if (diagnosticLogs)
            {
                VerificationLog($"assault-stage stage=after-explicit-hostility precinct={victimPrecinctKey}");
            }
            if (diagnosticLogs && DebugAssaultDiagnosticStopAfterExplicitHostility)
            {
                VerificationLog($"assault-stage stage=diagnostic-stop-after-explicit-hostility precinct={victimPrecinctKey}");
                return;
            }
            VerificationLog("Cop assault immediate retaliation applied.");
            VerificationLog("Cop assault buffs placed + precinct key=" + victimPrecinctKey + ".");
            VerificationLog("Cop war activated (assault).");
            LogCopWarStateSummary(humanPlayer, "assault-immediate");
        }

        private static bool ApplyHighHeatAndFedArrestPressure(PlayerInfo humanPlayer, Entity killer, bool tryImmediateArrest = true)
        {
            if (humanPlayer == null || killer == null)
            {
                return false;
            }
            bool arrested = false;
            ForceHardMaxWantedAndFeds(killer.Id);
            if (!tryImmediateArrest)
            {
                return false;
            }
            try
            {
                arrested = TryArrestCrewViaGameplayTweaks(humanPlayer, killer);
            }
            catch
            {
                arrested = false;
            }
            return arrested;
        }

        private static bool IsWitnessCaseCalled(PendingCopWitnessCase pendingCase)
        {
            if (pendingCase == null || pendingCase.KillerPeepId <= 0UL)
            {
                return false;
            }
            try
            {
                EntityID killerId = EntityID.FromID(pendingCase.KillerPeepId);
                if (!pendingCase.WitnessEvidenceConfirmed && !HasActiveWitnessEvidence(killerId))
                {
                    return false;
                }
                if (IsPeepInJailViaGameplayTweaks(killerId))
                {
                    return true;
                }
                var state = GameplayTweaks.GameplayTweaksPlugin.GetCrewStateOrNull(killerId);
                if (state != null && state.InJail)
                {
                    return true;
                }
                if (state != null && state.FedsIncoming && GameplayTweaks.G.GetNow().days > pendingCase.DayCreated)
                {
                    return true;
                }
            }
            catch
            {
            }
            return false;
        }

        private static bool DidWitnessDisappearWithoutCase(PendingCopWitnessCase pendingCase)
        {
            if (pendingCase == null || pendingCase.KillerPeepId <= 0UL)
            {
                return true;
            }
            try
            {
                EntityID killerId = EntityID.FromID(pendingCase.KillerPeepId);
                var state = GameplayTweaks.GameplayTweaksPlugin.GetCrewStateOrNull(killerId);
                if (state == null)
                {
                    return false;
                }
                return !state.HasWitness && state.WitnessCount <= 0 && !state.InJail;
            }
            catch
            {
                return false;
            }
        }

        private static void EnsureActiveWarPrecinctFromBuffs(PlayerInfo humanPlayer)
        {
            if (humanPlayer == null)
            {
                return;
            }
            if (IsValidPrecinctKey(_activeWarPrecinctKey) && HasLivingCopInPrecinct(_activeWarPrecinctKey))
            {
                return;
            }
            _activeWarPrecinctKey = InvalidPrecinctKey;
            PlayerInfo victimCopPlayer = GameplayTweaks.G.FindPlayerById(_activeWarVictimCopPlayerId);
            if (victimCopPlayer != null && IsCopPlayer(victimCopPlayer))
            {
                _activeWarPrecinctKey = GetPrecinctKey(victimCopPlayer);
                if (IsValidPrecinctKey(_activeWarPrecinctKey))
                {
                    GameplayTweaks.GameplayTweaksPlugin.SaveData.CopWarActive = true;
                    return;
                }
            }
            HashSet<int> desiredPrecincts = GetDesiredHostilePrecinctKeys(humanPlayer);
            if (desiredPrecincts.Count > 0)
            {
                _activeWarPrecinctKey = desiredPrecincts.OrderBy(v => v).First();
                GameplayTweaks.GameplayTweaksPlugin.SaveData.CopWarActive = true;
            }
        }

        private static bool HasLivingCopInPrecinct(int precinctKey)
        {
            if (precinctKey == InvalidPrecinctKey)
            {
                return false;
            }
            foreach (PlayerInfo cop in GetMunicipalCopPlayers())
            {
                if (cop == null || GetPrecinctKey(cop) != precinctKey)
                {
                    continue;
                }
                if (cop.crew == null || !cop.crew.IsCrewDefeated)
                {
                    return true;
                }
            }
            return false;
        }

        private static void TryResolveCopWarFromPrecinctBribes(PlayerInfo humanPlayer)
        {
            if (humanPlayer == null)
            {
                return;
            }
            HashSet<int> bribedPrecincts = GetBribedPrecinctKeys(humanPlayer);
            if (bribedPrecincts.Count == 0)
            {
                return;
            }
            if (_activeIncidentScope == CopIncidentScope.None && !GameplayTweaks.GameplayTweaksPlugin.SaveData.CopWarActive)
            {
                return;
            }
            if (_activeIncidentScope == CopIncidentScope.Kill && !IsActiveWarCulpritInJail())
            {
                string reason = IsActiveWarCulpritHidden() ? "culprit-hidden" : "awaiting-explicit-truce";
                VerificationLog($"cop-war passive precinct truce deferred source=precinct-bribe reason={reason} precinct={_activeWarPrecinctKey}");
                return;
            }

            bool victimPrecinctBribed = IsValidPrecinctKey(_activeWarPrecinctKey) && bribedPrecincts.Contains(_activeWarPrecinctKey);
            if (!victimPrecinctBribed && IsValidPrecinctKey(_activeWarPrecinctKey))
            {
                return;
            }
            if (victimPrecinctBribed && IsFreshRetaliationWindowActive(humanPlayer, out string freshReason))
            {
                VerificationLog($"cop-war explicit precinct truce deferred source=precinct-bribe reason={freshReason} precinct={_activeWarPrecinctKey}");
                return;
            }

            if (IsValidPrecinctKey(_activeWarPrecinctKey))
            {
                _resolvedWarPrecinctKeys.Add(_activeWarPrecinctKey);
            }
            VerificationLog($"cop-war explicit precinct truce resolvedPrecincts={(_resolvedWarPrecinctKeys.Count > 0 ? string.Join(",", _resolvedWarPrecinctKeys.OrderBy(v => v)) : "none")}");

            ApplyExplicitPrecinctHostilityState(humanPlayer, "precinct-bribe", refreshUi: true);
            LogCopWarStateSummary(humanPlayer, "precinct-bribe");

            HashSet<int> remainingPrecincts = GetDesiredHostilePrecinctKeys(humanPlayer);
            if (remainingPrecincts.Count <= 0)
            {
                GameplayTweaks.GameplayTweaksPlugin.LogGrapevine("COP TRUCE: Bribes cleared the remaining precinct hostility.");
                return;
            }

            if (_activeIncidentScope == CopIncidentScope.Assault)
            {
                GameplayTweaks.GameplayTweaksPlugin.LogGrapevine("COP TRUCE: The assaulted precinct stood down.");
                return;
            }

            if (IsActiveWarCulpritHidden())
            {
                GameplayTweaks.GameplayTweaksPlugin.LogGrapevine("COP TRUCE: The victim precinct still needs a payoff while the killer remains hidden.");
                return;
            }

            if (IsActiveWarCulpritInJail())
            {
                GameplayTweaks.GameplayTweaksPlugin.LogGrapevine("COP TRUCE: The victim precinct still needs a payoff, but the other precincts have stood down.");
                return;
            }

            GameplayTweaks.GameplayTweaksPlugin.LogGrapevine("COP TRUCE: The victim precinct stood down. Other precincts stay aggressive until the killer goes to jail.");
        }

        private static void RefreshCopAggroUi(string sourceTag)
        {
            if (_isRefreshingCopAggroUi)
            {
                return;
            }
            try
            {
                _isRefreshingCopAggroUi = true;
                PlayerInfo humanPlayer = GameplayTweaks.G.GetHumanPlayer();
                List<PlayerInfo> copPlayers = GetMunicipalCopPlayers();
                HashSet<int> desiredPrecincts = humanPlayer != null
                    ? GetDesiredHostilePrecinctKeys(humanPlayer)
                    : new HashSet<int>();
                List<PlayerInfo> refreshCopPlayers = copPlayers
                    .Where(cop => ShouldRefreshCopAggroUiForPlayer(cop, humanPlayer, desiredPrecincts))
                    .ToList();
                int dirtyPicks = 0;
                foreach (PlayerInfo copPlayer in refreshCopPlayers)
                {
                    if (copPlayer == null)
                    {
                        continue;
                    }

                    GameplayTweaks.GameplayTweaksPlugin.MarkCrewPickAggroDirty(copPlayer.PID, "cop-war-" + sourceTag);
                    dirtyPicks++;
                }

                GameplayTweaks.GameplayTweaksPlugin.FlushCrewPickAggroRefreshes("cop-war-" + sourceTag);
                GameplayTweaks.GameplayTweaksPlugin.RefreshCrewHudUi("cop-war-" + sourceTag);
                VerificationLog($"cop-war-ui-refresh source={sourceTag} cops={copPlayers.Count} refreshed={refreshCopPlayers.Count} picks={dirtyPicks}");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[CopKilling] RefreshCopAggroUi failed: {ex.Message}");
            }
            finally
            {
                _isRefreshingCopAggroUi = false;
            }
        }

        private static bool ShouldRefreshCopAggroUiForPlayer(PlayerInfo copPlayer, PlayerInfo humanPlayer, HashSet<int> desiredPrecincts)
        {
            if (copPlayer == null)
            {
                return false;
            }

            if (humanPlayer == null)
            {
                return true;
            }

            if (copPlayer.PID.id == humanPlayer.PID.id)
            {
                return false;
            }

            int precinctKey = GetPrecinctKey(copPlayer);
            if (desiredPrecincts != null && desiredPrecincts.Contains(precinctKey))
            {
                return true;
            }

            return IsAggroOn(copPlayer, humanPlayer.PID) || IsAggroOn(humanPlayer, copPlayer.PID);
        }

        private static void EscalateCaseAfterWitnessCalled(PlayerInfo humanPlayer, PendingCopWitnessCase pendingCase)
        {
            if (humanPlayer == null || pendingCase == null)
            {
                return;
            }
            _activeWarPrecinctKey = pendingCase.VictimPrecinctKey;
            _activeWarVictimCopPlayerId = pendingCase.VictimCopPlayerId;
            _activeWarKillerPeepId = pendingCase.KillerPeepId;
            var saveData = GameplayTweaks.GameplayTweaksPlugin.SaveData;
            saveData.CopWarActive = true;
            saveData.CopWarWitnessCount = Mathf.Max(saveData.CopWarWitnessCount, 2);
            saveData.LastCopKillDay = GameplayTweaks.G.GetNow().days;
            BeginKillIncidentPoliticalGate(humanPlayer, pendingCase.PoliticalFavorGrandfathered, "witness-escalation");
            Entity killer = null;
            try
            {
                killer = EntityID.FromID(pendingCase.KillerPeepId).FindEntity();
            }
            catch
            {
            }
            _activeIncidentScope = CopIncidentScope.Kill;
            ApplyExplicitPrecinctHostilityState(humanPlayer, "witness-escalation", refreshUi: true);
            string precinctName = (_activeWarVictimCopPlayerId >= 0) ? GetCopDisplayName(GameplayTweaks.G.FindPlayerById(_activeWarVictimCopPlayerId)) : "the precinct";
            VerificationLog("Witness case called; precinct retaliation escalated citywide.");
            GameplayTweaks.GameplayTweaksPlugin.LogGrapevine("COP WAR: Witness testimony was called. " + precinctName + " escalated citywide retaliation.");
        }

        private static void ProcessPendingWitnessCases(PlayerInfo humanPlayer)
        {
            if (humanPlayer == null || _pendingWitnessCases.Count == 0)
            {
                return;
            }
            for (int i = _pendingWitnessCases.Count - 1; i >= 0; i--)
            {
                PendingCopWitnessCase pendingCase = _pendingWitnessCases[i];
                if (pendingCase == null)
                {
                    _pendingWitnessCases.RemoveAt(i);
                    continue;
                }
                EntityID killerId = EntityID.INVALID;
                try
                {
                    killerId = EntityID.FromID(pendingCase.KillerPeepId);
                }
                catch
                {
                }
                bool hasWitnessEvidence = HasActiveWitnessEvidence(killerId);
                if (hasWitnessEvidence)
                {
                    pendingCase.WitnessEvidenceConfirmed = true;
                }
                if (pendingCase.WitnessEvidenceConfirmed && !pendingCase.HardMaxApplied)
                {
                    pendingCase.HardMaxApplied = true;
                    GameplayTweaks.GameplayTweaksPlugin.SaveData.CopWarWitnessCount = Mathf.Max(GameplayTweaks.GameplayTweaksPlugin.SaveData.CopWarWitnessCount, 2);
                    ForceHardMaxWantedAndFeds(killerId);
                    VerificationLog("Delayed witness evidence confirmed; forced hard-max wanted + fed pressure.");
                }
                if (IsWitnessCaseCalled(pendingCase))
                {
                    EscalateCaseAfterWitnessCalled(humanPlayer, pendingCase);
                    _pendingWitnessCases.RemoveAt(i);
                    continue;
                }
                if (DidWitnessDisappearWithoutCase(pendingCase))
                {
                    _pendingWitnessCases.RemoveAt(i);
                }
            }
        }

        private static bool IsEntityAlive(EntityID peepId)
        {
            if (peepId.IsNotValid)
            {
                return false;
            }
            try
            {
                Entity peep = peepId.FindEntity();
                return peep != null && peep.data?.person != null && peep.data.person.IsAlive;
            }
            catch
            {
                return false;
            }
        }

        private static EntityID GetCrewPeepForPlayer(PlayerInfo player, Entity fallback = null)
        {
            if (fallback != null && !fallback.Id.IsNotValid && IsEntityAlive(fallback.Id))
            {
                return fallback.Id;
            }
            if (player?.crew != null)
            {
                try
                {
                    foreach (CrewAssignment crew in player.crew.GetLiving())
                    {
                        if (crew.IsValid && !crew.peepId.IsNotValid && IsEntityAlive(crew.peepId))
                        {
                            return crew.peepId;
                        }
                    }
                }
                catch
                {
                }
            }
            if (player?.social != null && !player.social.PlayerPeepId.IsNotValid && IsEntityAlive(player.social.PlayerPeepId))
            {
                return player.social.PlayerPeepId;
            }
            if (player?.crew != null)
            {
                try
                {
                    CrewAssignment boss = player.crew.GetCrewForIndex(0);
                    if (boss.IsValid && !boss.peepId.IsNotValid)
                    {
                        return boss.peepId;
                    }
                }
                catch
                {
                }
            }
            if (player?.social != null && !player.social.PlayerPeepId.IsNotValid)
            {
                return player.social.PlayerPeepId;
            }
            if (fallback != null && !fallback.Id.IsNotValid)
            {
                return fallback.Id;
            }
            return EntityID.INVALID;
        }

        private static bool AddDirectedRelationshipBuff(PlayerInfo source, PlayerInfo target, string buffId, EntityID crewPeep = default(EntityID))
        {
            return GameplayTweaks.GameplayTweaksPlugin.AddDirectedRelationshipBuff(source, target, buffId, crewPeep);
        }

        private static void AddMutualRelationshipBuff(PlayerInfo a, PlayerInfo b, string buffId, EntityID aCrewPeep = default(EntityID), EntityID bCrewPeep = default(EntityID))
        {
            if (a == null || b == null || a.PID.id == b.PID.id || string.IsNullOrEmpty(buffId))
            {
                return;
            }
            AddDirectedRelationshipBuff(a, b, buffId, aCrewPeep);
            AddDirectedRelationshipBuff(b, a, buffId, bCrewPeep);
        }

        private static void ApplyCopIncidentRelationshipBuffs(PlayerInfo humanPlayer, int victimPrecinctKey, Entity killer, CopIncidentScope scope)
        {
            if (humanPlayer == null)
            {
                return;
            }

            EntityID humanPeepId = GetCrewPeepForPlayer(humanPlayer, killer);
            int precinctBuffCount = 0;
            int citywideBuffCount = 0;
            foreach (PlayerInfo cop in GetMunicipalCopPlayers())
            {
                if (cop == null || cop.PID.id == humanPlayer.PID.id)
                {
                    continue;
                }

                EntityID copPeepId = GetCrewPeepForPlayer(cop);
                if (victimPrecinctKey != InvalidPrecinctKey && GetPrecinctKey(cop) == victimPrecinctKey)
                {
                    AddMutualRelationshipBuff(cop, humanPlayer, "relbuff-cop-killed-precinct", copPeepId, humanPeepId);
                    precinctBuffCount++;
                    continue;
                }

                if (scope == CopIncidentScope.Kill)
                {
                    AddMutualRelationshipBuff(cop, humanPlayer, "relbuff-cop-killed-citywide", copPeepId, humanPeepId);
                    citywideBuffCount++;
                }
            }
            VerificationLog($"cop-war-buffs-applied scope={scope} precinct={victimPrecinctKey} precinctBuffs={precinctBuffCount} citywideBuffs={citywideBuffCount} killer={(killer?.Id.id ?? 0UL)}");
        }

        private static void ActivateWarBetweenPlayers(PlayerInfo a, PlayerInfo b)
        {
            if (a == null || b == null || a.PID.id == b.PID.id)
            {
                return;
            }
            TryAddAggroOn(a, b.PID);
            TryAddAggroOn(b, a.PID);
        }

        private static void EnforceCopWarFromRelationshipBuffs(PlayerInfo humanPlayer)
        {
            if (humanPlayer == null)
            {
                return;
            }
            if (IsFreshRetaliationWindowActive(humanPlayer, out string freshReason))
            {
                RelaxBribedPrecinctAggroDuringFreshRetaliation(humanPlayer);
                VerificationLog($"cop-war-buff-grace-active reason={freshReason} day={GameplayTweaks.G.GetNow().days} precinct={_freshRetaliationPrecinctKey} killer={_freshRetaliationKillerPeepId}");
                VerificationLog("cop-war-cleanup-skipped-fresh-retaliation");
                if (AnyCopAggroAgainstHuman(humanPlayer))
                {
                    VerificationLog("cop-war-aggro-preserved");
                }
                return;
            }

            if (_activeIncidentScope == CopIncidentScope.None)
            {
                bool hasAnyCopKillBuff = HasTrackedCopWarState(humanPlayer) || HasAnyCopKillWarBuffActive(humanPlayer);
                bool culpritHidden = IsActiveWarCulpritHidden();
                if (!hasAnyCopKillBuff)
                {
                    bool silentNormalization = !GameplayTweaks.GameplayTweaksPlugin.SaveData.CopWarActive;
                    AggroCleanupResult cleanupResult = ClearResidualCopAggroWithoutBuffs(humanPlayer, silentNormalization);
                    int nowDay = GameplayTweaks.G.GetNow().days;
                    if (_lastNoBuffAttackLogDay != nowDay)
                    {
                        _lastNoBuffAttackLogDay = nowDay;
                        _noBuffAttackLoggedPids.Clear();
                    }
                    foreach (PlayerInfo cop in GetMunicipalCopPlayers())
                    {
                        if (cop == null || cop.PID.id == humanPlayer.PID.id)
                        {
                            continue;
                        }
                        if (HasCopKillBuffBetween(cop, humanPlayer))
                        {
                            continue;
                        }
                        if (IsAttackAllowedOneWay(cop, humanPlayer.PID) && _noBuffAttackLoggedPids.Add(cop.PID.id))
                        {
                            Debug.Log($"{VerificationLogPrefix} [CopKilling] no-buff cop still attack-allowed pid={cop.PID.id}");
                        }
                    }
                    if (GameplayTweaks.GameplayTweaksPlugin.SaveData.CopWarActive)
                    {
                        ResetExplicitCopWarState();
                        Debug.Log($"{VerificationLogPrefix} [CopKilling] stale cop war state cleared (no explicit incident)");
                    }
                    if (culpritHidden)
                    {
                        VerificationLog("Cop war re-aggro skipped while culprit is hidden and no cop-kill buffs are active.");
                    }
                    if (cleanupResult.ClearedCount > 0)
                    {
                        VerificationLog($"no-buff aggro normalized silent={cleanupResult.Silent} hadAggro={cleanupResult.HadAggroCount} cleared={cleanupResult.ClearedCount}");
                    }
                    ClearFreshRetaliationWindow();
                    return;
                }
            }

            ApplyExplicitPrecinctHostilityState(humanPlayer, "enforce-explicit", refreshUi: false);
        }

        public static float GetWitnessChanceForVictim(Entity victim)
        {
            return IsCop(victim) ? 0.5f : 0.3f;
        }

        private static int GetActiveNationalWitnessCount()
        {
            try
            {
                var nationalHeat = GameplayTweaks.GameplayTweaksPlugin.SaveData?.NationalHeat;
                if (nationalHeat?.WitnessEntries == null)
                {
                    return 0;
                }
                return nationalHeat.WitnessEntries.Count(e => e != null && !e.ArrestProcessed);
            }
            catch
            {
                return 0;
            }
        }

        private static bool HasActiveNationalWitnessForKiller(EntityID killerId)
        {
            if (killerId.IsNotValid)
            {
                return false;
            }
            try
            {
                long killerLongId = (long)killerId.id;
                var nationalHeat = GameplayTweaks.GameplayTweaksPlugin.SaveData?.NationalHeat;
                if (nationalHeat?.WitnessEntries == null)
                {
                    return false;
                }
                return nationalHeat.WitnessEntries.Any(e =>
                    e != null
                    && !e.ArrestProcessed
                    && e.CrewPeepId == killerLongId
                    && e.SourceType == GameplayTweaks.GameplayTweaksPlugin.NATIONAL_HEAT_SOURCE_COP_KILL);
            }
            catch
            {
                return false;
            }
        }

        private static bool EnsureNationalHeatFallbackForCopKill(EntityID killerId, int dueDay, int nowDay)
        {
            if (killerId.IsNotValid)
            {
                return false;
            }
            try
            {
                var saveData = GameplayTweaks.GameplayTweaksPlugin.SaveData;
                if (saveData.NationalHeat == null)
                {
                    saveData.NationalHeat = new GameplayTweaks.NationalHeatState();
                }
                if (saveData.NationalHeat.WitnessEntries == null)
                {
                    saveData.NationalHeat.WitnessEntries = new List<GameplayTweaks.ImportantWitnessEntry>();
                }
                long killerLongId = (long)killerId.id;
                GameplayTweaks.ImportantWitnessEntry existing = saveData.NationalHeat.WitnessEntries.FirstOrDefault(e =>
                    e != null
                    && !e.ArrestProcessed
                    && e.CrewPeepId == killerLongId
                    && e.SourceType == GameplayTweaks.GameplayTweaksPlugin.NATIONAL_HEAT_SOURCE_COP_KILL);
                if (existing == null)
                {
                    existing = new GameplayTweaks.ImportantWitnessEntry
                    {
                        CrewPeepId = killerLongId,
                        SourceType = GameplayTweaks.GameplayTweaksPlugin.NATIONAL_HEAT_SOURCE_COP_KILL,
                        AddedDay = nowDay,
                        ArrestDueDay = dueDay,
                        ArrestProcessed = false,
                        SentenceBoostApplied = false
                    };
                    saveData.NationalHeat.WitnessEntries.Add(existing);
                }
                else
                {
                    existing.AddedDay = nowDay;
                    existing.ArrestDueDay = dueDay;
                    existing.ArrestProcessed = false;
                }
                saveData.NationalHeat.Active = true;
                saveData.NationalHeat.Level = Mathf.Max(1, saveData.NationalHeat.Level);
                saveData.NationalHeat.LastEscalationDay = Mathf.Max(saveData.NationalHeat.LastEscalationDay, nowDay);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryRegisterCopKillWitnessCase(PlayerInfo humanPlayer, EntityID killerId, int dueDay, int nowDay, string sourceTag)
        {
            if (killerId.IsNotValid)
            {
                return false;
            }

            if (!ShouldCreateCopKillWitness(killerId, sourceTag))
            {
                VerificationLog($"cop-kill-witness-not-called peep={killerId.id} dueDay={dueDay} source={sourceTag}");
                return false;
            }

            EnsureWitnessEvidence(killerId, 3);
            if (humanPlayer != null)
            {
                GameplayTweaks.GameplayTweaksPlugin.SpreadLowLocalHeatToCrew(humanPlayer, killerId);
            }

            bool registerCalled = false;
            try
            {
                GameplayTweaks.GameplayTweaksPlugin.RegisterImportantWitness(
                    killerId,
                    GameplayTweaks.GameplayTweaksPlugin.NATIONAL_HEAT_SOURCE_COP_KILL,
                    dueDay);
                registerCalled = true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[CopKilling] RegisterImportantWitness failed for killer {killerId.id}: {ex.Message}");
            }

            bool hasKillerEntry = HasActiveNationalWitnessForKiller(killerId);
            bool fallbackApplied = false;
            if (!registerCalled || !hasKillerEntry)
            {
                fallbackApplied = EnsureNationalHeatFallbackForCopKill(killerId, dueDay, nowDay);
                hasKillerEntry = HasActiveNationalWitnessForKiller(killerId);
            }

            if (hasKillerEntry)
            {
                VerificationLog($"cop-kill-witness-created peep={killerId.id} dueDay={dueDay} source={sourceTag} fallback={fallbackApplied}");
            }
            VerificationLog($"grouped-cop-attacker-case-created peep={killerId.id} dueDay={dueDay} source={sourceTag} fallback={fallbackApplied} activeEntry={hasKillerEntry}");
            return hasKillerEntry;
        }

        private static bool ShouldCreateCopKillWitness(EntityID killerId, string sourceTag)
        {
            const double killWitnessChance = 0.65;
            double roll = GameplayTweaks.GameplayTweaksPlugin.SharedRng.NextDouble();
            bool witnessCalled = roll < killWitnessChance;
            VerificationLog($"cop-kill-witness-roll peep={killerId.id} source={sourceTag} roll={roll:0.000} chance={killWitnessChance:0.000} witnessCalled={witnessCalled}");
            return witnessCalled;
        }

        private static string BuildGroupedCopIncidentBaseKey(string snapshotSource, IEnumerable<EntityID> attackerIds, Entity victimPeep)
        {
            int nowDay = GameplayTweaks.G.GetNow().days;
            string sourceKey = string.IsNullOrWhiteSpace(snapshotSource) ? "grouped-unknown" : snapshotSource;
            ulong victimPeepId = victimPeep?.Id.id ?? 0UL;
            string attackerKey = string.Join("-",
                (attackerIds ?? Enumerable.Empty<EntityID>())
                    .Where(id => id.IsValid)
                    .Select(id => id.id)
                    .Distinct()
                    .OrderBy(id => id));
            return $"{nowDay}:{sourceKey}:{attackerKey}:{victimPeepId}";
        }

        private static string BuildGroupedCopIncidentKey(string snapshotSource, IEnumerable<EntityID> attackerIds, Entity victimPeep, bool lethal)
        {
            return BuildGroupedCopIncidentBaseKey(snapshotSource, attackerIds, victimPeep) + ":" + (lethal ? 1 : 0);
        }

        private static EntityID ChooseGroupedCopKillFallGuy(PlayerInfo humanPlayer, Entity representativeAttacker, List<EntityID> attackerIds)
        {
            if (representativeAttacker != null && representativeAttacker.Id.IsValid && attackerIds.Contains(representativeAttacker.Id))
            {
                return representativeAttacker.Id;
            }

            if (humanPlayer?.crew != null)
            {
                foreach (EntityID attackerId in attackerIds)
                {
                    CrewAssignment assignment = humanPlayer.crew.GetCrewForPeep(attackerId);
                    if (!assignment.IsValid || assignment.VehicleID.IsNotValid)
                    {
                        continue;
                    }

                    EntityID driverId = GameplayTweaks.MultiCrewVehicleHelper.GetDriverPeepId(humanPlayer.crew, assignment.VehicleID);
                    if (driverId.IsValid && attackerIds.Contains(driverId))
                    {
                        return driverId;
                    }
                }
            }

            return attackerIds.FirstOrDefault(id => id.IsValid);
        }

        private static bool TryGetGroupedCombatAttackerSnapshot(string source, out string snapshotSource, out List<EntityID> attackerIds)
        {
            snapshotSource = string.Empty;
            attackerIds = new List<EntityID>();
            if (string.IsNullOrWhiteSpace(source) || !source.StartsWith("grouped-", StringComparison.Ordinal))
            {
                return false;
            }

            try
            {
                if (!GameplayTweaks.GameplayTweaksPlugin.TryGetActiveGroupedCombatAttackerSnapshot(out attackerIds, out snapshotSource))
                {
                    attackerIds = new List<EntityID>();
                    snapshotSource = string.Empty;
                    return false;
                }
            }
            catch
            {
                attackerIds = new List<EntityID>();
                snapshotSource = string.Empty;
                return false;
            }

            attackerIds = attackerIds
                .Where(id => id.IsValid)
                .Distinct()
                .ToList();
            return attackerIds.Count > 0;
        }

        private static void RegisterGroupedCopKillWitnessCases(PlayerInfo humanPlayer, Entity representativeAttacker, Entity victimPeep, PlayerInfo victimCopGang, string source, bool lethal)
        {
            if (representativeAttacker == null || victimPeep == null)
            {
                return;
            }

            int nowDay = GameplayTweaks.G.GetNow().days;
            if (!TryGetGroupedCombatAttackerSnapshot(source, out string snapshotSource, out List<EntityID> attackerIds))
            {
                if (lethal)
                {
                    OnCopKilled(representativeAttacker, victimCopGang, witnessed: true);
                }
                else
                {
                    OnCopAssaulted(representativeAttacker, victimCopGang);
                }
                return;
            }

            string groupedIncidentKey = BuildGroupedCopIncidentKey(snapshotSource, attackerIds, victimPeep, lethal);
            string groupedIncidentBaseKey = BuildGroupedCopIncidentBaseKey(snapshotSource, attackerIds, victimPeep);
            if (!_processedGroupedCopAttackerCaseKeys.Add(groupedIncidentKey))
            {
                VerificationLog($"national-heat-deduped source={source} key={groupedIncidentKey} representative={representativeAttacker.Id.id} victim={victimPeep.Id.id}");
                return;
            }

            EntityID fallGuyId = ChooseGroupedCopKillFallGuy(humanPlayer, representativeAttacker, attackerIds);
            if (fallGuyId.IsNotValid)
            {
                OnCopKilled(representativeAttacker, victimCopGang, witnessed: true);
                return;
            }

            List<EntityID> companionIds = attackerIds
                .Where(id => id.IsValid && id != fallGuyId)
                .Distinct()
                .ToList();

            try
            {
                _pendingWitnessCases.RemoveAll(x => x != null && (x.KillerPeepId == fallGuyId.id || companionIds.Any(companionId => companionId.id == x.KillerPeepId)));
            }
            catch
            {
            }

            int witnessSource = lethal
                ? GameplayTweaks.GameplayTweaksPlugin.NATIONAL_HEAT_SOURCE_COP_KILL
                : GameplayTweaks.GameplayTweaksPlugin.NATIONAL_HEAT_SOURCE_COP_ASSAULT;
            int baseDueDay = GameplayTweaks.GameplayTweaksPlugin.GetImportantWitnessArrestDueDay(witnessSource, nowDay);

            if (lethal)
            {
                ApplyImmediateCopKillRetaliation(humanPlayer, representativeAttacker, victimCopGang);
                var saveData = GameplayTweaks.GameplayTweaksPlugin.SaveData;
                saveData.LastCopKillDay = nowDay;

                if (!EnableCopWitnessArrestEscalation)
                {
                    VerificationLog($"cop-kill-witness-skipped peep={fallGuyId.id} dueDay={baseDueDay} source={snapshotSource} incident={groupedIncidentBaseKey} reason=disabled");
                    string representativeNameNoWitness = GameplayTweaks.GameplayTweaksPlugin.NormalizeRuntimePersonName(representativeAttacker.data?.person?.FullName);
                    if (string.IsNullOrWhiteSpace(representativeNameNoWitness))
                    {
                        representativeNameNoWitness = "Unknown";
                    }
                    VerificationLog("Grouped cop incident applied hard-max wanted state without witness.");
                    Debug.Log("[CopKilling] " + representativeNameNoWitness + " harmed a cop in grouped combat. Heat maxed and precinct retaliation started.");
                    GameplayTweaks.GameplayTweaksPlugin.LogGrapevine("LAW: " + representativeNameNoWitness + " harmed a cop. Heat maxed and precinct retaliation started.");
                    return;
                }

                if (!ShouldCreateCopKillWitness(fallGuyId, "grouped"))
                {
                    VerificationLog($"cop-kill-witness-not-called peep={fallGuyId.id} dueDay={baseDueDay} source={snapshotSource} incident={groupedIncidentBaseKey}");
                    string representativeNameNoWitness = GameplayTweaks.GameplayTweaksPlugin.NormalizeRuntimePersonName(representativeAttacker.data?.person?.FullName);
                    if (string.IsNullOrWhiteSpace(representativeNameNoWitness))
                    {
                        representativeNameNoWitness = "Unknown";
                    }
                    VerificationLog("Grouped cop incident applied hard-max wanted state without witness.");
                    Debug.Log("[CopKilling] " + representativeNameNoWitness + " harmed a cop in grouped combat. Heat maxed and precinct retaliation started.");
                    GameplayTweaks.GameplayTweaksPlugin.LogGrapevine("LAW: " + representativeNameNoWitness + " harmed a cop. Heat maxed and precinct retaliation started.");
                    return;
                }

                GameplayTweaks.GameplayTweaksPlugin.PromoteGroupedCopAssaultIncidentToKill(groupedIncidentBaseKey, fallGuyId);
                if (EnableCopWitnessArrestEscalation)
                {
                    EnsureWitnessEvidence(fallGuyId, 3);
                }
            }
            else
            {
                ApplyImmediateCopAssaultRetaliation(humanPlayer, representativeAttacker, victimCopGang);
                if (EnableCopWitnessArrestEscalation)
                {
                    EnsureWitnessEvidence(fallGuyId, 1);
                }
            }
            if (humanPlayer != null)
            {
                GameplayTweaks.GameplayTweaksPlugin.SpreadLowLocalHeatToCrew(humanPlayer, fallGuyId);
            }
            if (lethal)
            {
                GameplayTweaks.GameplayTweaksPlugin.RegisterGroupedCopKillWitness(fallGuyId, baseDueDay, groupedIncidentBaseKey, companionIds);
                VerificationLog($"grouped-cop-fallguy-selected peep={fallGuyId.id} dueDay={baseDueDay} companions={companionIds.Count} source={snapshotSource} incident={groupedIncidentBaseKey}");
                VerificationLog($"cop-kill-witness-created peep={fallGuyId.id} dueDay={baseDueDay} source={snapshotSource} incident={groupedIncidentBaseKey}");
            }
            else
            {
                if (EnableCopWitnessArrestEscalation)
                {
                    GameplayTweaks.GameplayTweaksPlugin.RegisterGroupedCopAssaultWitness(fallGuyId, baseDueDay, groupedIncidentBaseKey, companionIds);
                    VerificationLog($"grouped-cop-assault-fallguy-selected peep={fallGuyId.id} dueDay={baseDueDay} companions={companionIds.Count} source={snapshotSource} incident={groupedIncidentBaseKey}");
                    VerificationLog($"cop-assault-witness-created peep={fallGuyId.id} dueDay={baseDueDay} source={snapshotSource} incident={groupedIncidentBaseKey}");
                }
                else
                {
                    VerificationLog($"cop-assault-witness-skipped peep={fallGuyId.id} dueDay={baseDueDay} source={snapshotSource} incident={groupedIncidentBaseKey} reason=disabled");
                }
            }

            string representativeName = GameplayTweaks.GameplayTweaksPlugin.NormalizeRuntimePersonName(representativeAttacker.data?.person?.FullName);
            if (string.IsNullOrWhiteSpace(representativeName))
            {
                representativeName = "Unknown";
            }
            if (lethal)
            {
                VerificationLog("Witness-backed grouped cop incident applied hard-max wanted state.");
                Debug.Log("[CopKilling] " + representativeName + " harmed a cop in grouped combat. Heat maxed and precinct retaliation started.");
                GameplayTweaks.GameplayTweaksPlugin.LogGrapevine("LAW: " + representativeName + " harmed a cop. Heat maxed and precinct retaliation started.");
            }
            else
            {
                VerificationLog("Witness-backed grouped cop assault incident applied max local heat state.");
                Debug.Log("[CopKilling] " + representativeName + " assaulted a cop in grouped combat. Local heat maxed and precinct retaliation started.");
                GameplayTweaks.GameplayTweaksPlugin.LogGrapevine("LAW: " + representativeName + " assaulted a cop. Heat spiked and precinct retaliation started.");
            }
        }

        private static void OnCopAssaulted(Entity attacker, PlayerInfo victimCopGang)
        {
            PlayerInfo humanPlayer = GameplayTweaks.G.GetHumanPlayer();
            if (humanPlayer == null || attacker == null)
            {
                return;
            }

            string attackerName = attacker?.data?.person != null
                ? GameplayTweaks.GameplayTweaksPlugin.NormalizeRuntimePersonName(attacker.data.person.FullName)
                : "Unknown";
            if (string.IsNullOrWhiteSpace(attackerName))
            {
                attackerName = "Unknown";
            }

            ApplyImmediateCopAssaultRetaliation(humanPlayer, attacker, victimCopGang);
            if (EnableCopWitnessArrestEscalation)
            {
                EnsureWitnessEvidence(attacker.Id, 1);
            }
            GameplayTweaks.GameplayTweaksPlugin.SpreadLowLocalHeatToCrew(humanPlayer, attacker.Id);
            if (EnableCopWitnessArrestEscalation)
            {
                int dueDay = GameplayTweaks.GameplayTweaksPlugin.GetImportantWitnessArrestDueDay(
                    GameplayTweaks.GameplayTweaksPlugin.NATIONAL_HEAT_SOURCE_COP_ASSAULT,
                    GameplayTweaks.G.GetNow().days);
                GameplayTweaks.GameplayTweaksPlugin.RegisterImportantWitness(attacker.Id, GameplayTweaks.GameplayTweaksPlugin.NATIONAL_HEAT_SOURCE_COP_ASSAULT, dueDay);
                VerificationLog($"cop-assault-witness-created peep={attacker.Id.id} dueDay={dueDay} source=solo");
                VerificationLog($"National heat assault entry created peep={attacker.Id.id} dueDay={dueDay}.");
                VerificationLog("Witness-backed cop assault incident applied max local heat state.");
            }
            else
            {
                VerificationLog($"cop-assault-witness-skipped peep={attacker.Id.id} source=solo reason=disabled");
                VerificationLog("Cop assault incident applied without witness/arrest escalation.");
            }
            Debug.Log("[CopKilling] " + attackerName + " assaulted a cop. Immediate max local heat + cop war retaliation applied.");
            GameplayTweaks.GameplayTweaksPlugin.LogGrapevine("LAW: " + attackerName + " assaulted a cop. Heat spiked and precinct retaliation started.");
        }

        private static void ApplyDebugCopAssaultRetaliation(Entity attacker, PlayerInfo victimCopGang)
        {
            PlayerInfo humanPlayer = GameplayTweaks.G.GetHumanPlayer();
            if (humanPlayer == null || attacker == null)
            {
                return;
            }

            string attackerName = attacker?.data?.person != null
                ? GameplayTweaks.GameplayTweaksPlugin.NormalizeRuntimePersonName(attacker.data.person.FullName)
                : "Unknown";
            if (string.IsNullOrWhiteSpace(attackerName))
            {
                attackerName = "Unknown";
            }

            ApplyImmediateCopAssaultRetaliation(humanPlayer, attacker, victimCopGang, diagnosticLogs: true, stopAfterRelationshipBuffs: DebugAssaultDiagnosticStopAfterRelationshipBuffs);
            int precinctKey = ResolveVictimPrecinctKey(victimCopGang, out _);
            if (DebugAssaultDiagnosticStopAfterRelationshipBuffs || DebugAssaultDiagnosticStopAfterFreshWindow || DebugAssaultDiagnosticStopAfterExplicitHostility)
            {
                VerificationLog($"debug cop assault diagnostic-stop peep={attacker.Id.id} precinct={precinctKey}");
                string stopStage = DebugAssaultDiagnosticStopAfterRelationshipBuffs
                    ? "relationship buffs"
                    : (DebugAssaultDiagnosticStopAfterFreshWindow ? "fresh retaliation window" : "explicit hostility");
                Debug.Log("[CopKilling] " + attackerName + " debug-assaulted a cop. Diagnostic mode stopped after " + stopStage + ".");
                return;
            }
            VerificationLog($"debug cop assault applied peep={attacker.Id.id} precinct={precinctKey}");
            Debug.Log("[CopKilling] " + attackerName + " debug-assaulted a cop. Precinct retaliation applied without witness/trial side-effects.");
        }

        private static void ApplyDebugCopKillRetaliation(Entity killer, PlayerInfo victimCopGang)
        {
            PlayerInfo humanPlayer = GameplayTweaks.G.GetHumanPlayer();
            if (humanPlayer == null || killer == null)
            {
                return;
            }

            string killerName = killer?.data?.person != null
                ? GameplayTweaks.GameplayTweaksPlugin.NormalizeRuntimePersonName(killer.data.person.FullName)
                : "Unknown";
            if (string.IsNullOrWhiteSpace(killerName))
            {
                killerName = "Unknown";
            }

            try
            {
                _pendingWitnessCases.RemoveAll(x => x != null && x.KillerPeepId == killer.Id.id);
            }
            catch
            {
            }

            ApplyImmediateCopKillRetaliation(
                humanPlayer,
                killer,
                victimCopGang,
                applyHeatAndFeds: false,
                diagnosticLogs: false,
                stopAfterSetup: false,
                stopAfterFreshWindow: false);
            VerificationLog($"debug cop kill applied peep={killer.Id.id} precinct={ResolveVictimPrecinctKey(victimCopGang, out _)}");
            Debug.Log("[CopKilling] " + killerName + " debug-killed a cop. Citywide retaliation applied without witness/trial/raid side-effects.");
        }

        public static void OnCopKilled(Entity killer, PlayerInfo victimCopGang, bool witnessed)
        {
            var saveData = GameplayTweaks.GameplayTweaksPlugin.SaveData;
            int nowDay = GameplayTweaks.G.GetNow().days;
            saveData.LastCopKillDay = nowDay;
            PlayerInfo humanPlayer = GameplayTweaks.G.GetHumanPlayer();
            if (humanPlayer == null || killer == null)
            {
                return;
            }

            string killerName;
            if (killer?.data?.person != null)
                killerName = GameplayTweaks.GameplayTweaksPlugin.NormalizeRuntimePersonName(killer.data.person.FullName);
            else
                killerName = "Unknown";
            if (string.IsNullOrWhiteSpace(killerName))
            {
                killerName = "Unknown";
            }

            try
            {
                _pendingWitnessCases.RemoveAll(x => x != null && x.KillerPeepId == killer.Id.id);
            }
            catch
            {
            }

            ApplyImmediateCopKillRetaliation(humanPlayer, killer, victimCopGang);
            if (EnableCopWitnessArrestEscalation)
            {
                int dueDay = GameplayTweaks.GameplayTweaksPlugin.GetImportantWitnessArrestDueDay(
                    GameplayTweaks.GameplayTweaksPlugin.NATIONAL_HEAT_SOURCE_COP_KILL,
                    nowDay);
                bool beforeActive = saveData.NationalHeat != null && saveData.NationalHeat.Active;
                int beforeLevel = saveData.NationalHeat?.Level ?? 0;
                int beforeWitnessCount = GetActiveNationalWitnessCount();
                GameplayTweaks.GameplayTweaksPlugin.VerificationLog(
                    "NationalHeat",
                    $"Cop kill registration before active={beforeActive} level={beforeLevel} witnessCount={beforeWitnessCount} killer={killer.Id.id}");
                bool registerCalled;
                if (witnessed)
                {
                    EnsureWitnessEvidence(killer.Id, 3);
                    GameplayTweaks.GameplayTweaksPlugin.SpreadLowLocalHeatToCrew(humanPlayer, killer.Id);
                    try
                    {
                        GameplayTweaks.GameplayTweaksPlugin.RegisterImportantWitness(
                            killer.Id,
                            GameplayTweaks.GameplayTweaksPlugin.NATIONAL_HEAT_SOURCE_COP_KILL,
                            dueDay);
                        registerCalled = HasActiveNationalWitnessForKiller(killer.Id);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[CopKilling] Forced RegisterImportantWitness failed for killer {killer.Id.id}: {ex.Message}");
                        registerCalled = false;
                    }
                    if (!registerCalled)
                    {
                        registerCalled = EnsureNationalHeatFallbackForCopKill(killer.Id, dueDay, nowDay);
                    }
                    VerificationLog($"cop-kill-witness-forced peep={killer.Id.id} dueDay={dueDay} source=solo activeEntry={HasActiveNationalWitnessForKiller(killer.Id)}");
                }
                else
                {
                    registerCalled = TryRegisterCopKillWitnessCase(humanPlayer, killer.Id, dueDay, nowDay, sourceTag: "solo");
                }
                bool afterActive = saveData.NationalHeat != null && saveData.NationalHeat.Active;
                int afterLevel = saveData.NationalHeat?.Level ?? 0;
                int afterWitnessCount = GetActiveNationalWitnessCount();
                bool hasKillerEntry = HasActiveNationalWitnessForKiller(killer.Id);
                GameplayTweaks.GameplayTweaksPlugin.VerificationLog(
                    "NationalHeat",
                    $"Cop kill registered active={afterActive} level={afterLevel} witnessCount={afterWitnessCount} hasKillerEntry={hasKillerEntry} fallback={!registerCalled}");
                if (registerCalled)
                {
                    VerificationLog($"National heat entry created peep={killer.Id.id} dueDay={dueDay} witnessCountBefore={beforeWitnessCount} witnessCountAfter={afterWitnessCount}.");
                    VerificationLog("Witness-backed cop incident applied hard-max wanted state.");
                }
                else
                {
                    VerificationLog($"National heat entry skipped peep={killer.Id.id} dueDay={dueDay} witnessCountBefore={beforeWitnessCount} witnessCountAfter={afterWitnessCount}.");
                    VerificationLog("Cop incident applied hard-max wanted state without witness.");
                }
            }
            else
            {
                VerificationLog($"cop-kill-witness-skipped peep={killer.Id.id} source=solo reason=disabled");
                VerificationLog("Cop incident applied hard-max wanted state without witness.");
            }
            Debug.Log("[CopKilling] " + killerName + " harmed a cop. Immediate max heat + cop war retaliation applied.");
            GameplayTweaks.GameplayTweaksPlugin.LogGrapevine("LAW: " + killerName + " harmed a cop. Heat maxed and precinct retaliation started.");
        }

        public static bool CanCallCopTruce()
        {
            try
            {
                PlayerInfo humanPlayer = GameplayTweaks.G.GetHumanPlayer();
                if (humanPlayer == null)
                    return false;
                bool hasTrackedState = HasTrackedCopWarState(humanPlayer);
                if (!hasTrackedState)
                    return false;
                return GetBribedPrecinctKeys(humanPlayer).Count > 0;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CopKilling] CanCallCopTruce failed: {ex}");
            }
            return false;
        }

        private static bool TryResetResolvedJailedCopWarState(PlayerInfo humanPlayer, string sourceTag)
        {
            if (humanPlayer == null || !IsActiveWarCulpritInJail())
            {
                return false;
            }

            if (HasActivePoliticalProtection(humanPlayer))
            {
                return false;
            }

            EnsureActiveWarPrecinctFromBuffs(humanPlayer);
            if (!IsCurrentVictimPrecinctPoliticallyResolved())
            {
                return false;
            }

            if (GetDesiredHostilePrecinctKeys(humanPlayer).Count > 0)
            {
                return false;
            }

            AggroCleanupResult cleanupResult = ClearResidualCopAggroWithoutBuffs(humanPlayer, silent: true);
            RemoveCopKillRelationshipBuffs(humanPlayer);
            RemovePoliticalProtectionRelationshipBuffs(humanPlayer);
            GameplayTweaks.GameplayTweaksPlugin.SaveData.CopWarActive = false;
            GameplayTweaks.GameplayTweaksPlugin.SaveData.CopWarWitnessCount = 0;
            ResetExplicitCopWarState();
            VerificationLog($"jailed-resolved-war-reset source={sourceTag} hadAggro={cleanupResult.HadAggroCount} cleared={cleanupResult.ClearedCount}");
            RefreshCopAggroUi("jailed-reset-" + sourceTag);
            return true;
        }

        private static bool IsPoliticalFavorResidualAggroCleanupEligible(PlayerInfo humanPlayer, bool hasTrackedState, bool anyAggro, bool victimResolved, HashSet<int> desiredPrecincts, bool hasCopKillBuffs)
        {
            if (humanPlayer == null || !anyAggro)
            {
                return false;
            }

            if (!hasTrackedState)
            {
                return true;
            }

            if (!victimResolved)
            {
                return false;
            }

            bool hasDesiredPrecincts = desiredPrecincts != null && desiredPrecincts.Count > 0;
            return !hasDesiredPrecincts
                && !hasCopKillBuffs
                && !GameplayTweaks.GameplayTweaksPlugin.SaveData.CopWarActive;
        }

        private static bool TryResolvePoliticalFavorResidualAggro(PlayerInfo humanPlayer, string sourceTag)
        {
            if (humanPlayer == null || !AnyCopAggroAgainstHuman(humanPlayer))
            {
                return false;
            }

            ActivatePoliticalFavorSuppressionWindow();
            AggroCleanupResult cleanupResult = ClearResidualCopAggroWithoutBuffs(humanPlayer, silent: false);
            VerificationLog($"political-favor residual-aggro cleanup source={sourceTag} hadAggro={cleanupResult.HadAggroCount} cleared={cleanupResult.ClearedCount}");
            RefreshCopAggroUi("political-favor-residual-" + sourceTag);
            return cleanupResult.HadAggroCount > 0;
        }

        public static bool CanUsePoliticalFavorForHumanPlayer()
        {
            try
            {
                PlayerInfo humanPlayer = GameplayTweaks.G.GetHumanPlayer();
                if (humanPlayer == null || !HasActivePoliticalProtection(humanPlayer))
                {
                    return false;
                }

                if (TryResetResolvedJailedCopWarState(humanPlayer, "can-use-political-favor"))
                {
                    return false;
                }
                if (IsPoliticalFavorLockedBehindPreKillBribe(humanPlayer, out int outstandingTargetCount))
                {
                    VerificationLog($"political-favor locked source=prekill-bribe outstandingTargets={outstandingTargetCount}");
                    return false;
                }

                bool hasTrackedState = HasTrackedCopWarState(humanPlayer);
                bool anyAggro = AnyCopAggroAgainstHuman(humanPlayer);
                EnsureActiveWarPrecinctFromBuffs(humanPlayer);
                HashSet<int> desiredPrecincts = GetDesiredHostilePrecinctKeys(humanPlayer);
                bool victimResolved = IsCurrentVictimPrecinctPoliticallyResolved();
                bool hasCopKillBuffs = HasAnyCopKillWarBuffActive(humanPlayer);
                if (IsPoliticalFavorResidualAggroCleanupEligible(humanPlayer, hasTrackedState, anyAggro, victimResolved, desiredPrecincts, hasCopKillBuffs))
                {
                    VerificationLog($"political-favor use-enabled source=cleanup-only tracked={hasTrackedState} aggro={anyAggro} resolved={victimResolved} desired={desiredPrecincts.Count} buffs={hasCopKillBuffs}");
                    return true;
                }

                if (!hasTrackedState || victimResolved)
                {
                    return false;
                }

                return desiredPrecincts.Count > 0 || anyAggro;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CopKilling] CanUsePoliticalFavorForHumanPlayer failed: {ex}");
                return false;
            }
        }

        public static bool IsPoliticalFavorAlreadyUsedForCurrentIncident()
        {
            try
            {
                PlayerInfo humanPlayer = GameplayTweaks.G.GetHumanPlayer();
                if (humanPlayer == null || !HasActivePoliticalProtection(humanPlayer))
                {
                    return false;
                }

                if (TryResetResolvedJailedCopWarState(humanPlayer, "political-favor-used-state"))
                {
                    return false;
                }
                if (IsPoliticalFavorLockedBehindPreKillBribe(humanPlayer, out int outstandingTargetCount))
                {
                    VerificationLog($"political-favor already-used suppressed source=prekill-bribe outstandingTargets={outstandingTargetCount}");
                    return false;
                }

                bool hasTrackedState = HasTrackedCopWarState(humanPlayer);
                if (!hasTrackedState)
                {
                    return false;
                }

                EnsureActiveWarPrecinctFromBuffs(humanPlayer);
                bool victimResolved = IsCurrentVictimPrecinctPoliticallyResolved();
                if (!victimResolved)
                {
                    return false;
                }

                HashSet<int> desiredPrecincts = GetDesiredHostilePrecinctKeys(humanPlayer);
                bool hasCopKillBuffs = HasAnyCopKillWarBuffActive(humanPlayer);
                bool anyAggro = AnyCopAggroAgainstHuman(humanPlayer);
                if (IsPoliticalFavorResidualAggroCleanupEligible(humanPlayer, hasTrackedState, anyAggro, victimResolved, desiredPrecincts, hasCopKillBuffs))
                {
                    VerificationLog($"political-favor already-used suppressed source=cleanup-only aggro={anyAggro} desired={desiredPrecincts.Count} buffs={hasCopKillBuffs}");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CopKilling] IsPoliticalFavorAlreadyUsedForCurrentIncident failed: {ex}");
                return false;
            }
        }

        public static bool TryUsePoliticalFavorForHumanPlayer(string sourceTag)
        {
            return CanUsePoliticalFavorForHumanPlayer() && TryResolvePoliticalProtectionForHumanPlayer(sourceTag);
        }

        public static bool HasPoliticalFavorRelationship(int aPid, int bPid)
        {
            PlayerInfo a = GameplayTweaks.G.FindPlayerById(aPid);
            PlayerInfo b = GameplayTweaks.G.FindPlayerById(bPid);
            if (a == null || b == null)
            {
                return false;
            }

            PlayerInfo humanPlayer = null;
            PlayerInfo copPlayer = null;
            if (a.PID.IsHumanPlayer && IsCopPlayer(b))
            {
                humanPlayer = a;
                copPlayer = b;
            }
            else if (b.PID.IsHumanPlayer && IsCopPlayer(a))
            {
                humanPlayer = b;
                copPlayer = a;
            }
            if (humanPlayer == null || copPlayer == null || !HasActivePoliticalProtection(humanPlayer))
            {
                return false;
            }

            int precinctKey = GetPrecinctKey(copPlayer);
            return _resolvedWarPrecinctKeys.Contains(precinctKey) && !GetDesiredHostilePrecinctKeys(humanPlayer).Contains(precinctKey);
        }

        private static bool IsCurrentVictimPrecinctPoliticallyResolved()
        {
            return IsValidPrecinctKey(_activeWarPrecinctKey) && _resolvedWarPrecinctKeys.Contains(_activeWarPrecinctKey);
        }

        public static bool TryResolvePoliticalProtectionForHumanPlayer(string sourceTag)
        {
            PlayerInfo humanPlayer = GameplayTweaks.G.GetHumanPlayer();
            if (humanPlayer == null || !HasActivePoliticalProtection(humanPlayer))
            {
                return false;
            }
            if (IsPoliticalFavorLockedBehindPreKillBribe(humanPlayer, out int outstandingTargetCount))
            {
                VerificationLog($"political-favor blocked source={sourceTag} reason=prekill-bribe outstandingTargets={outstandingTargetCount}");
                return false;
            }
            bool hasTrackedState = HasTrackedCopWarState(humanPlayer);
            bool anyAggro = AnyCopAggroAgainstHuman(humanPlayer);
            EnsureActiveWarPrecinctFromBuffs(humanPlayer);
            HashSet<int> desiredPrecincts = GetDesiredHostilePrecinctKeys(humanPlayer);
            bool victimResolved = IsCurrentVictimPrecinctPoliticallyResolved();
            bool hasCopKillBuffs = HasAnyCopKillWarBuffActive(humanPlayer);

            if (IsPoliticalFavorResidualAggroCleanupEligible(humanPlayer, hasTrackedState, anyAggro, victimResolved, desiredPrecincts, hasCopKillBuffs))
            {
                return TryResolvePoliticalFavorResidualAggro(humanPlayer, sourceTag);
            }

            if (!hasTrackedState)
            {
                return false;
            }

            bool resolveAllActivePrecincts = _activeIncidentScope == CopIncidentScope.Kill && desiredPrecincts.Count > 0;
            if (resolveAllActivePrecincts)
            {
                foreach (int precinctKey in desiredPrecincts)
                {
                    if (IsValidPrecinctKey(precinctKey))
                    {
                        _resolvedWarPrecinctKeys.Add(precinctKey);
                    }
                }
                VerificationLog($"political-favor full-precinct-resolution source={sourceTag} desiredPrecincts={desiredPrecincts.Count}");
            }
            else if (IsValidPrecinctKey(_activeWarPrecinctKey))
            {
                _resolvedWarPrecinctKeys.Add(_activeWarPrecinctKey);
            }
            ActivatePoliticalFavorSuppressionWindow();
            HashSet<int> remainingPrecincts = GetDesiredHostilePrecinctKeys(humanPlayer);
            ApplyExplicitPrecinctHostilityState(humanPlayer, "political-favor", refreshUi: true);
            if (remainingPrecincts.Count <= 0)
            {
                AggroCleanupResult cleanupResult = ForceClearAllCopAggro(humanPlayer, silent: false);
                RemoveCopKillRelationshipBuffs(humanPlayer);
                RemovePoliticalProtectionRelationshipBuffs(humanPlayer);
                GameplayTweaks.GameplayTweaksPlugin.SaveData.CopWarActive = false;
                GameplayTweaks.GameplayTweaksPlugin.SaveData.CopWarWitnessCount = 0;
                ResetExplicitCopWarState();
                RefreshCopAggroUi("political-favor-full-" + sourceTag);
                VerificationLog($"political-favor full cleanup source={sourceTag} hadAggro={cleanupResult.HadAggroCount} cleared={cleanupResult.ClearedCount}");
            }
            else
            {
                GameplayTweaks.GameplayTweaksPlugin.SaveData.CopWarActive = true;
                TryRelaxBribedPrecinctAggroFromSafeHooks(humanPlayer, "political-favor-" + sourceTag);
            }
            VerificationLog($"political-favor state-only source={sourceTag} remainingPrecincts={remainingPrecincts.Count} resolvedPrecincts={_resolvedWarPrecinctKeys.Count} suppressionFrameUntil={_politicalFavorSuppressionUntilFrame}");
            LogCopWarStateSummary(humanPlayer, sourceTag);

            if (remainingPrecincts.Count <= 0)
            {
                Debug.Log("[CopKilling] Political favor cooled the remaining precinct hostility.");
            }
            else if (_activeIncidentScope == CopIncidentScope.Assault)
            {
                Debug.Log("[CopKilling] Political favor cooled the assaulted precinct.");
            }
            else
            {
                Debug.Log("[CopKilling] Political favor cooled the victim precinct while other precincts remain hostile.");
            }
            return true;
        }

        public static bool ShouldSuppressIncomingAiCopCombat(PlayerID attackerPid, PlayerID targetPid)
        {
            try
            {
                if (!targetPid.IsHumanPlayer || !attackerPid.IsAIPlayer)
                {
                    return false;
                }

                PlayerInfo humanPlayer = GameplayTweaks.G.GetHumanPlayer();
                PlayerInfo attackerPlayer = PlayerIDExtensions.FindPlayer(attackerPid);
                if (humanPlayer == null || attackerPlayer == null || !IsCopPlayer(attackerPlayer))
                {
                    return false;
                }

                TryNormalizeSafeHookCopAggro(humanPlayer, "incoming-ai-combat");
                TryReconcileSafeHookCopWarState(humanPlayer, "incoming-ai-combat");
                return ShouldSuppressCopAttack(attackerPlayer, humanPlayer);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[CopKilling] ShouldSuppressIncomingAiCopCombat failed: " + ex.Message);
                return false;
            }
        }

        public static bool CallCopTruce()
        {
            PlayerInfo humanPlayer = GameplayTweaks.G.GetHumanPlayer();
            if (humanPlayer == null)
                return false;
            TryNormalizeSafeHookCopAggro(humanPlayer, "call-truce");
            HashSet<int> bribedPrecincts = GetBribedPrecinctKeys(humanPlayer);
            if (bribedPrecincts.Count == 0)
                return false;
            bool hasTrackedState = HasTrackedCopWarState(humanPlayer);
            if (!hasTrackedState)
                return false;
            EnsureActiveWarPrecinctFromBuffs(humanPlayer);
            ClearCopTruceForPrecincts(humanPlayer, bribedPrecincts);
            var saveData = GameplayTweaks.GameplayTweaksPlugin.SaveData;
            bool includesRequiredPrecinct = !IsValidPrecinctKey(_activeWarPrecinctKey) || !HasLivingCopInPrecinct(_activeWarPrecinctKey) || bribedPrecincts.Contains(_activeWarPrecinctKey);
            string requiredPrecinctName = (_activeWarVictimCopPlayerId >= 0)
                ? GetCopDisplayName(GameplayTweaks.G.FindPlayerById(_activeWarVictimCopPlayerId))
                : "the precinct that lost the officer";
            if (!includesRequiredPrecinct)
            {
                saveData.CopWarWitnessCount = Mathf.Max(0, saveData.CopWarWitnessCount - 1);
                RefreshCopAggroUi("partial-truce");
                Debug.Log("[CopKilling] COP TRUCE: Wrong precinct bribed for case resolution.");
                GameplayTweaks.GameplayTweaksPlugin.LogGrapevine("COP TRUCE: You must secure truce with " + requiredPrecinctName + " to clear cop-kill buffs.");
                return true;
            }
            RemoveCopKillRelationshipBuffs(humanPlayer);
            HashSet<int> allPrecinctKeys = new HashSet<int>(GetMunicipalCopPlayers().Select(GetPrecinctKey));
            ClearCopTruceForPrecincts(humanPlayer, allPrecinctKeys);
            AggroCleanupResult cleanupResult = ClearResidualCopAggroWithoutBuffs(humanPlayer, silent: false);
            bool anyCopKillBuffsRemaining = HasTrackedCopWarState(humanPlayer) && GetDesiredHostilePrecinctKeys(humanPlayer).Count > 0;
            if (!anyCopKillBuffsRemaining)
            {
                ResetExplicitCopWarState();
                RefreshCopAggroUi("full-truce");
                Debug.Log("[CopKilling] COP TRUCE: Peace has been negotiated across all hostile precincts.");
                GameplayTweaks.GameplayTweaksPlugin.LogGrapevine("COP TRUCE: Bribes calmed every hostile precinct.");
                VerificationLog($"post-truce residual aggro cleanup hadAggro={cleanupResult.HadAggroCount} cleared={cleanupResult.ClearedCount}");
            }
            else
            {
                saveData.CopWarWitnessCount = Mathf.Max(0, saveData.CopWarWitnessCount - 1);
                RefreshCopAggroUi("precinct-stand-down");
                Debug.Log("[CopKilling] COP TRUCE: One precinct stood down, but others remain hostile.");
                GameplayTweaks.GameplayTweaksPlugin.LogGrapevine("COP TRUCE: One bribed precinct stood down, but other precincts remain aggressive.");
            }
            LogCopWarStateSummary(humanPlayer, "call-truce");
            return true;
        }

        internal static void TriggerDebugCopIncident(bool lethal)
        {
            try
            {
                int currentFrame = Time.frameCount;
                if (_lastDebugIncidentFrame >= 0 && currentFrame - _lastDebugIncidentFrame < 120)
                {
                    Debug.Log($"[CopKilling] Debug cop incident skipped lethal={lethal}: cooldown active frameDelta={currentFrame - _lastDebugIncidentFrame}");
                    return;
                }
                PlayerInfo humanPlayer = GameplayTweaks.G.GetHumanPlayer();
                if (humanPlayer == null)
                {
                    Debug.LogWarning("[CopKilling] Debug cop incident skipped: missing human player.");
                    return;
                }
                if (_activeIncidentScope != CopIncidentScope.None || GameplayTweaks.GameplayTweaksPlugin.SaveData.CopWarActive)
                {
                    Debug.Log($"[CopKilling] Debug cop incident replacing existing debug state lethal={lethal} scope={_activeIncidentScope} precinct={_activeWarPrecinctKey}");
                    PrepareForDebugIncidentTransition(humanPlayer, lethal ? "debug-kill-replace" : "debug-assault-replace");
                }

                Entity killer = GetCrewPeepForPlayer(humanPlayer).FindEntity();
                if (killer == null)
                {
                    Debug.LogWarning("[CopKilling] Debug cop incident skipped: missing human crew peep.");
                    return;
                }

                PlayerInfo victimCopGang = FindPreferredDebugVictimCopGang(humanPlayer);
                if (victimCopGang == null)
                {
                    Debug.LogWarning("[CopKilling] Debug cop incident skipped: no living cop precinct found.");
                    return;
                }

                if (lethal)
                {
                    ApplyDebugCopKillRetaliation(killer, victimCopGang);
                }
                else
                {
                    ApplyDebugCopAssaultRetaliation(killer, victimCopGang);
                }
                _lastDebugIncidentFrame = currentFrame;
                ActivateDebugResetSuppressionWindow();
                VerificationLog($"debug-incident stabilization lethal={lethal} suppressionUntilFrame={_debugResetSuppressionUntilFrame} suppressionUntilDay={_debugResetSuppressionUntilDay}");
                RefreshCopAggroUi(lethal ? "debug-kill" : "debug-assault");
                Debug.Log($"[CopKilling] Debug {(lethal ? "cop kill" : "cop assault")} incident triggered precinct={GetPrecinctKey(victimCopGang)} copPid={victimCopGang.PID.id} copName={GetCopDisplayName(victimCopGang)} municipalPreferred={IsMunicipalCopPlayer(victimCopGang)} killer={killer.Id.id}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CopKilling] TriggerDebugCopIncident failed lethal={lethal}: {ex}");
            }
        }

        public static void ApplyPatch(Harmony harmony)
        {
            try
            {
                Type combatAdvisorType = typeof(GameClock).Assembly.GetType("Game.Session.Player.AI.CombatAdvisor");
                ApplyAttackSuppressionPatch(harmony, combatAdvisorType);

                Type combatType = typeof(GameClock).Assembly.GetType("Game.Session.Sim.CombatManager");
                ApplyCombatIncidentRuntimePatches(harmony, combatType);
                ApplyHumanCopTargetingPatch(harmony, combatType);
                ApplyCrewPickTintPatch(harmony);
                ApplyTurnStartReconciliationPatch(harmony);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CopKilling] CopWarSystem patch failed: {ex}");
            }
        }

        public static void ApplyCombatIncidentRuntimePatches(Harmony harmony)
        {
            try
            {
                Type combatType = typeof(GameClock).Assembly.GetType("Game.Session.Sim.CombatManager");
                ApplyCombatIncidentRuntimePatches(harmony, combatType);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CopKilling] ApplyCombatIncidentRuntimePatches failed: {ex}");
            }
        }

        public static void ApplyAttackSuppressionPatch(Harmony harmony)
        {
            try
            {
                Type combatAdvisorType = typeof(GameClock).Assembly.GetType("Game.Session.Player.AI.CombatAdvisor");
                ApplyAttackSuppressionPatch(harmony, combatAdvisorType);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CopKilling] ApplyAttackSuppressionPatch failed: {ex}");
            }
        }

        public static void ApplyCrewPickTintPatch(Harmony harmony)
        {
            try
            {
                if (harmony == null)
                {
                    return;
                }

                Type crewPickType = typeof(GameClock).Assembly.GetType("Game.UI.Session.Picks.CrewPick");
                if (crewPickType == null)
                {
                    return;
                }

                MethodInfo crewPickRefresh = crewPickType.GetMethod("RefreshContents", BindingFlags.Instance | BindingFlags.Public);
                if (crewPickRefresh != null)
                {
                    harmony.Patch(crewPickRefresh, null, new HarmonyMethod(typeof(CopWarSystem), "CrewPickRefreshPostfix", null), null, null, null);
                    Debug.Log("[CopKilling] Cop aggro crew pick tint patch applied");
                }
                else
                {
                    Debug.LogWarning("[CopKilling] Missing CrewPick.RefreshContents patch target");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CopKilling] ApplyCrewPickTintPatch failed: {ex}");
            }
        }

        public static void ApplyHumanCopTargetingPatch(Harmony harmony)
        {
            try
            {
                if (harmony == null)
                {
                    return;
                }

                Type combatType = typeof(GameClock).Assembly.GetType("Game.Session.Sim.CombatManager");
                ApplyHumanCopTargetingPatch(harmony, combatType);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CopKilling] ApplyHumanCopTargetingPatch failed: {ex}");
            }
        }

        private static void ApplyHumanCopTargetingPatch(Harmony harmony, Type combatType)
        {
            if (harmony == null || combatType == null)
            {
                return;
            }

            MethodInfo canAttack = combatType.GetMethod("CanAttackTarget", BindingFlags.Static | BindingFlags.NonPublic);
            if (canAttack != null)
            {
                harmony.Patch(canAttack, null, new HarmonyMethod(typeof(CopWarSystem), nameof(CanAttackTargetPostfix)), null, null, null);
                Debug.Log("[CopKilling] Cop attack target patch applied - cops can now be attacked!");
            }
            else
            {
                Debug.LogWarning("[CopKilling] Could not find CanAttackTarget method");
            }
        }

        private static void ApplyCombatIncidentRuntimePatches(Harmony harmony, Type combatType)
        {
            if (harmony == null)
            {
                return;
            }

            if (combatType != null)
            {
                MethodInfo[] combatMethods = combatType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                MethodInfo performCombat = combatMethods.FirstOrDefault(m => MethodMatchesSignature(m, "PerformCombat", new string[4] { "CrewAssignment", "CrewAssignment", "WeaponConfig", "WeaponConfig" }));
                MethodInfo performHumanCombat = combatMethods.FirstOrDefault(m => MethodMatchesSignature(m, "PerformHumanCombat", new string[3] { "Entity", "Entity", "WeaponConfig" }));
                if (performCombat != null)
                {
                    harmony.Patch(performCombat, null, new HarmonyMethod(typeof(CopWarSystem), nameof(PerformCombatPostfix)), null, null, null);
                    Debug.Log("[CopKilling] Runtime combat incident patch applied: " + DescribeMethod(performCombat));
                }
                else
                {
                    Debug.LogWarning("[CopKilling] Missing runtime combat incident patch: PerformCombat(CrewAssignment,CrewAssignment,WeaponConfig,WeaponConfig)");
                }

                if (performHumanCombat != null)
                {
                    harmony.Patch(performHumanCombat, new HarmonyMethod(typeof(CopWarSystem), nameof(PerformHumanCombatPrefix)), new HarmonyMethod(typeof(CopWarSystem), nameof(PerformHumanCombatPostfix)), null, null, null);
                    Debug.Log("[CopKilling] Runtime combat incident patch applied: " + DescribeMethod(performHumanCombat));
                }
                else
                {
                    Debug.LogWarning("[CopKilling] Missing runtime combat incident patch: PerformHumanCombat(Entity,Entity,WeaponConfig)");
                }
            }
            else
            {
                Debug.LogWarning("[CopKilling] Missing runtime combat incident type: Game.Session.Sim.CombatManager");
            }

            Type combatPopupPlanningType = typeof(GameClock).Assembly.GetType("Game.UI.Session.Combat.CombatPopupPlanning");
            MethodInfo popupDoCombat = combatPopupPlanningType?.GetMethod("DoCombat", BindingFlags.Instance | BindingFlags.NonPublic, null, new Type[] { typeof(Entity) }, null);
            if (popupDoCombat != null)
            {
                harmony.Patch(popupDoCombat, new HarmonyMethod(typeof(CopWarSystem), nameof(CombatPopupDoCombatPrefix)), new HarmonyMethod(typeof(CopWarSystem), nameof(CombatPopupDoCombatPostfix)), null, null, null);
                Debug.Log("[CopKilling] Runtime combat incident popup patch applied: " + DescribeMethod(popupDoCombat));
            }
            else
            {
                Debug.LogWarning("[CopKilling] Missing runtime combat incident popup patch: CombatPopupPlanning.DoCombat(Entity)");
            }

            ApplyCopKillStatFallbackPatch(harmony);
        }

        private static void ApplyCopKillStatFallbackPatch(Harmony harmony)
        {
            if (harmony == null)
            {
                return;
            }

            MethodInfo[] statMethods = typeof(AgentComponent).GetMethods(BindingFlags.Instance | BindingFlags.Public);
            foreach (MethodInfo statMethod in statMethods)
            {
                if (statMethod.Name != "IncrementStat")
                {
                    continue;
                }
                ParameterInfo[] pars = statMethod.GetParameters();
                if (pars.Length >= 2 && pars[0].ParameterType == typeof(CrewStats))
                {
                    harmony.Patch(statMethod, null, new HarmonyMethod(typeof(CopWarSystem), nameof(CopKillStatIncrementPostfix)), null, null, null);
                    Debug.Log("[CopKilling] Cop kill fallback stat patch applied");
                    break;
                }
            }
        }

        public static void ApplyTurnStartReconciliationPatch(Harmony harmony)
        {
            try
            {
                if (harmony == null)
                {
                    return;
                }

                MethodInfo onPlayerTurnStarted = typeof(PlayerCrew).GetMethod("OnPlayerTurnStarted", BindingFlags.Instance | BindingFlags.Public);
                if (onPlayerTurnStarted != null)
                {
                    harmony.Patch(onPlayerTurnStarted, null, new HarmonyMethod(typeof(CopWarSystem), "OnPlayerTurnStartedPostfix", null), null, null, null);
                    Debug.Log("[CopKilling] Cop war turn reconciliation patch applied");
                }
                else
                {
                    Debug.LogWarning("[CopKilling] Missing PlayerCrew.OnPlayerTurnStarted patch target");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CopKilling] ApplyTurnStartReconciliationPatch failed: {ex}");
            }
        }

        public static void ApplyCombatPostfixDiagnosticPatches(Harmony harmony, bool includePopupRepairDiagnostics)
        {
            try
            {
                if (harmony == null)
                {
                    return;
                }

                Type combatType = typeof(GameClock).Assembly.GetType("Game.Session.Sim.CombatManager");
                if (combatType == null)
                {
                    return;
                }

                MethodInfo[] combatMethods = combatType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                MethodInfo performCombat = combatMethods.FirstOrDefault(m => MethodMatchesSignature(m, "PerformCombat", new string[4] { "CrewAssignment", "CrewAssignment", "WeaponConfig", "WeaponConfig" }));
                MethodInfo performHumanCombat = combatMethods.FirstOrDefault(m => MethodMatchesSignature(m, "PerformHumanCombat", new string[3] { "Entity", "Entity", "WeaponConfig" }));
                if (performCombat != null)
                {
                    harmony.Patch(performCombat, null, new HarmonyMethod(typeof(CopWarSystem), nameof(PerformCombatDiagnosticPostfix)), null, null, null);
                    Debug.Log("[CopKilling] Diagnostic combat entrypoint patch applied: " + DescribeMethod(performCombat));
                }
                else
                {
                    Debug.LogWarning("[CopKilling] Missing diagnostic combat entrypoint patch: PerformCombat(CrewAssignment,CrewAssignment,WeaponConfig,WeaponConfig)");
                }

                if (performHumanCombat != null)
                {
                    harmony.Patch(performHumanCombat, null, new HarmonyMethod(typeof(CopWarSystem), nameof(PerformHumanCombatDiagnosticPostfix)), null, null, null);
                    Debug.Log("[CopKilling] Diagnostic combat entrypoint patch applied: " + DescribeMethod(performHumanCombat));
                }
                else
                {
                    Debug.LogWarning("[CopKilling] Missing diagnostic combat entrypoint patch: PerformHumanCombat(Entity,Entity,WeaponConfig)");
                }

                Type combatPopupPlanningType = typeof(GameClock).Assembly.GetType("Game.UI.Session.Combat.CombatPopupPlanning");
                MethodInfo popupDoCombat = combatPopupPlanningType?.GetMethod("DoCombat", BindingFlags.Instance | BindingFlags.NonPublic, null, new Type[] { typeof(Entity) }, null);
                if (popupDoCombat != null)
                {
                    harmony.Patch(popupDoCombat, new HarmonyMethod(typeof(CopWarSystem), nameof(CombatPopupDoCombatDiagnosticPrefix)), new HarmonyMethod(typeof(CopWarSystem), nameof(CombatPopupDoCombatDiagnosticPostfix)), null, null, null);
                    Debug.Log("[CopKilling] Diagnostic combat popup-do-combat patch applied: " + DescribeMethod(popupDoCombat));
                }
                else
                {
                    Debug.LogWarning("[CopKilling] Missing diagnostic combat popup patch: CombatPopupPlanning.DoCombat(Entity)");
                }

                MethodInfo popupRefreshContents = combatPopupPlanningType?.GetMethod("RefreshContents", BindingFlags.Instance | BindingFlags.NonPublic);
                if (includePopupRepairDiagnostics && popupRefreshContents != null)
                {
                    harmony.Patch(popupRefreshContents, new HarmonyMethod(typeof(CopWarSystem), nameof(CombatPopupRefreshContentsDiagnosticPrefix)), null, null, null, null);
                    Debug.Log("[CopKilling] Diagnostic combat popup patch applied: " + DescribeMethod(popupRefreshContents));
                }
                else if (!includePopupRepairDiagnostics)
                {
                    Debug.Log("[CopKilling] Diagnostic combat popup repair patch skipped while human cop targeting slice is disabled");
                }
                else
                {
                    Debug.LogWarning("[CopKilling] Missing diagnostic combat popup patch: CombatPopupPlanning.RefreshContents()");
                }

                Type convoCallbacksType = typeof(GameClock).Assembly.GetType("Game.UI.Session.Convo.ConvoCallbacks");
                if (convoCallbacksType != null)
                {
                    MethodInfo[] convoMethods = convoCallbacksType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    MethodInfo storeCombat = convoMethods.FirstOrDefault(m => MethodMatchesSignature(m, "StoreCombat", new string[1] { "ConvoButton" }));
                    MethodInfo executeCombat = convoMethods.FirstOrDefault(m => MethodMatchesSignature(m, "ExecuteCombat", new string[1] { "ConvoButton" }));
                    if (storeCombat != null)
                    {
                        harmony.Patch(
                            storeCombat,
                            new HarmonyMethod(typeof(CopWarSystem), nameof(StoreCombatDiagnosticPrefix)),
                            new HarmonyMethod(typeof(CopWarSystem), nameof(StoreCombatDiagnosticPostfix)),
                            null,
                            new HarmonyMethod(typeof(CopWarSystem), nameof(StoreCombatDiagnosticFinalizer)),
                            null);
                        Debug.Log("[CopKilling] Diagnostic convo combat patch applied: " + DescribeMethod(storeCombat));
                    }
                    else
                    {
                        Debug.LogWarning("[CopKilling] Missing diagnostic convo combat patch: StoreCombat(ConvoButton)");
                    }

                    if (executeCombat != null)
                    {
                        harmony.Patch(
                            executeCombat,
                            new HarmonyMethod(typeof(CopWarSystem), nameof(ExecuteCombatDiagnosticPrefix)),
                            new HarmonyMethod(typeof(CopWarSystem), nameof(ExecuteCombatDiagnosticPostfix)),
                            null,
                            new HarmonyMethod(typeof(CopWarSystem), nameof(ExecuteCombatDiagnosticFinalizer)),
                            null);
                        Debug.Log("[CopKilling] Diagnostic convo combat patch applied: " + DescribeMethod(executeCombat));
                    }
                    else
                    {
                        Debug.LogWarning("[CopKilling] Missing diagnostic convo combat patch: ExecuteCombat(ConvoButton)");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CopKilling] ApplyCombatPostfixDiagnosticPatches failed: {ex}");
            }
        }

        private static void ApplyAttackSuppressionPatch(Harmony harmony, Type combatAdvisorType)
        {
            if (harmony == null || combatAdvisorType == null)
            {
                return;
            }

            MethodInfo isAttackAllowed = combatAdvisorType.GetMethod("IsAttackAllowed", BindingFlags.Instance | BindingFlags.Public, null, new Type[] { typeof(PlayerID) }, null);
            if (CopKillingPlugin.IsAttackSuppressionIsAttackAllowedEnabled() && isAttackAllowed != null)
            {
                harmony.Patch(isAttackAllowed, new HarmonyMethod(typeof(CopWarSystem), nameof(IsAttackAllowedPrefix)), null, null, null, null);
                Debug.Log("[CopKilling] CombatAdvisor.IsAttackAllowed suppression patch applied");
            }
            else if (!CopKillingPlugin.IsAttackSuppressionIsAttackAllowedEnabled())
            {
                Debug.Log("[CopKilling] CombatAdvisor.IsAttackAllowed suppression patch skipped by config");
            }
            else
            {
                Debug.LogWarning("[CopKilling] Missing CombatAdvisor.IsAttackAllowed patch target");
            }

            Type attackAdvisorType = typeof(GameClock).Assembly.GetType("Game.Session.Player.AI.AttackAdvisor");
            MethodInfo tryPickBuilding = attackAdvisorType?.GetMethod("TryPickBuilding", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
            if (CopKillingPlugin.IsAttackSuppressionTryPickBuildingEnabled() && tryPickBuilding != null)
            {
                harmony.Patch(
                    tryPickBuilding,
                    new HarmonyMethod(typeof(CopWarSystem), nameof(AttackAdvisorTryPickBuildingPrefix)),
                    null,
                    null,
                    new HarmonyMethod(typeof(CopWarSystem), nameof(AttackAdvisorTryPickBuildingFinalizer)),
                    null);
                Debug.Log("[CopKilling] AttackAdvisor.TryPickBuilding guard patch applied");
            }
            else if (!CopKillingPlugin.IsAttackSuppressionTryPickBuildingEnabled())
            {
                Debug.Log("[CopKilling] AttackAdvisor.TryPickBuilding guard patch skipped by config");
            }
            else
            {
                Debug.LogWarning("[CopKilling] Missing AttackAdvisor.TryPickBuilding guard patch target");
            }

            MethodInfo tryForceClosure = attackAdvisorType?.GetMethod("TryForceClosure", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
            if (CopKillingPlugin.IsAttackSuppressionTryPickBuildingEnabled() && tryForceClosure != null)
            {
                harmony.Patch(
                    tryForceClosure,
                    new HarmonyMethod(typeof(CopWarSystem), nameof(AttackAdvisorTryForceClosurePrefix)),
                    null,
                    null,
                    null,
                    null);
                Debug.Log("[CopKilling] AttackAdvisor.TryForceClosure guard patch applied");
            }
            else if (!CopKillingPlugin.IsAttackSuppressionTryPickBuildingEnabled())
            {
                Debug.Log("[CopKilling] AttackAdvisor.TryForceClosure guard patch skipped by config");
            }
            else
            {
                Debug.LogWarning("[CopKilling] Missing AttackAdvisor.TryForceClosure guard patch target");
            }

            MethodInfo onTurnUpdate = attackAdvisorType?.GetMethod("OnTurnUpdate", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
            if (CopKillingPlugin.IsAttackSuppressionAttackAdvisorOnTurnUpdateEnabled() && onTurnUpdate != null)
            {
                harmony.Patch(
                    onTurnUpdate,
                    new HarmonyMethod(typeof(CopWarSystem), nameof(AttackAdvisorOnTurnUpdatePrefix)),
                    null,
                    null,
                    new HarmonyMethod(typeof(CopWarSystem), nameof(AttackAdvisorOnTurnUpdateFinalizer)),
                    null);
                Debug.Log("[CopKilling] AttackAdvisor.OnTurnUpdate guard patch applied");
            }
            else if (!CopKillingPlugin.IsAttackSuppressionAttackAdvisorOnTurnUpdateEnabled())
            {
                Debug.Log("[CopKilling] AttackAdvisor.OnTurnUpdate guard patch skipped by config");
            }
            else
            {
                Debug.LogWarning("[CopKilling] Missing AttackAdvisor.OnTurnUpdate guard patch target");
            }

            MethodInfo trySellOutToFeds = attackAdvisorType?.GetMethod("TrySellOutToFeds", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
            if (CopKillingPlugin.IsAttackSuppressionTryPickBuildingEnabled() && trySellOutToFeds != null)
            {
                harmony.Patch(
                    trySellOutToFeds,
                    new HarmonyMethod(typeof(CopWarSystem), nameof(AttackAdvisorTrySellOutToFedsPrefix)),
                    null,
                    null,
                    null,
                    null);
                Debug.Log("[CopKilling] AttackAdvisor.TrySellOutToFeds guard patch applied");
            }
            else if (!CopKillingPlugin.IsAttackSuppressionTryPickBuildingEnabled())
            {
                Debug.Log("[CopKilling] AttackAdvisor.TrySellOutToFeds guard patch skipped by config");
            }
            else
            {
                Debug.LogWarning("[CopKilling] Missing AttackAdvisor.TrySellOutToFeds guard patch target");
            }

            MethodInfo tryPickCoord = attackAdvisorType?.GetMethod("TryPickCoord", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
            if (CopKillingPlugin.IsAttackSuppressionTryPickBuildingEnabled() && tryPickCoord != null)
            {
                harmony.Patch(
                    tryPickCoord,
                    new HarmonyMethod(typeof(CopWarSystem), nameof(AttackAdvisorTryPickCoordPrefix)),
                    null,
                    null,
                    null,
                    null);
                Debug.Log("[CopKilling] AttackAdvisor.TryPickCoord guard patch applied");
            }
            else if (!CopKillingPlugin.IsAttackSuppressionTryPickBuildingEnabled())
            {
                Debug.Log("[CopKilling] AttackAdvisor.TryPickCoord guard patch skipped by config");
            }
            else
            {
                Debug.LogWarning("[CopKilling] Missing AttackAdvisor.TryPickCoord guard patch target");
            }

            MethodInfo maybeAskForTruce = combatAdvisorType.GetMethod("MaybeAskForTruce", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (CopKillingPlugin.IsAttackSuppressionMaybeAskForTruceEnabled() && maybeAskForTruce != null)
            {
                harmony.Patch(
                    maybeAskForTruce,
                    new HarmonyMethod(typeof(CopWarSystem), nameof(CombatAdvisorMaybeAskForTrucePrefix)),
                    null,
                    null,
                    new HarmonyMethod(typeof(CopWarSystem), nameof(CombatAdvisorMaybeAskForTruceFinalizer)),
                    null);
                Debug.Log("[CopKilling] CombatAdvisor.MaybeAskForTruce guard patch applied");
            }
            else if (!CopKillingPlugin.IsAttackSuppressionMaybeAskForTruceEnabled())
            {
                Debug.Log("[CopKilling] CombatAdvisor.MaybeAskForTruce guard patch skipped by config");
            }
            else
            {
                Debug.LogWarning("[CopKilling] Missing CombatAdvisor.MaybeAskForTruce guard patch target");
            }

            MethodInfo combatAdvisorOnTurnUpdate = combatAdvisorType.GetMethod("OnTurnUpdate", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
            if (CopKillingPlugin.IsAttackSuppressionCombatAdvisorOnTurnUpdateEnabled() && combatAdvisorOnTurnUpdate != null)
            {
                harmony.Patch(
                    combatAdvisorOnTurnUpdate,
                    new HarmonyMethod(typeof(CopWarSystem), nameof(CombatAdvisorOnTurnUpdatePrefix)),
                    null,
                    null,
                    new HarmonyMethod(typeof(CopWarSystem), nameof(CombatAdvisorOnTurnUpdateFinalizer)),
                    null);
                Debug.Log("[CopKilling] CombatAdvisor.OnTurnUpdate guard patch applied");
            }
            else if (!CopKillingPlugin.IsAttackSuppressionCombatAdvisorOnTurnUpdateEnabled())
            {
                Debug.Log("[CopKilling] CombatAdvisor.OnTurnUpdate guard patch skipped by config");
            }
            else
            {
                Debug.LogWarning("[CopKilling] Missing CombatAdvisor.OnTurnUpdate guard patch target");
            }

            MethodInfo updateRequestsAfterAggro = combatAdvisorType.GetMethod("UpdateRequestsAfterAggro", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
            if (CopKillingPlugin.IsAttackSuppressionUpdateRequestsAfterAggroEnabled() && updateRequestsAfterAggro != null)
            {
                harmony.Patch(
                    updateRequestsAfterAggro,
                    new HarmonyMethod(typeof(CopWarSystem), nameof(CombatAdvisorUpdateRequestsAfterAggroPrefix)),
                    null,
                    null,
                    new HarmonyMethod(typeof(CopWarSystem), nameof(CombatAdvisorUpdateRequestsAfterAggroFinalizer)),
                    null);
                Debug.Log("[CopKilling] CombatAdvisor.UpdateRequestsAfterAggro guard patch applied");
            }
            else if (!CopKillingPlugin.IsAttackSuppressionUpdateRequestsAfterAggroEnabled())
            {
                Debug.Log("[CopKilling] CombatAdvisor.UpdateRequestsAfterAggro guard patch skipped by config");
            }
            else
            {
                Debug.LogWarning("[CopKilling] Missing CombatAdvisor.UpdateRequestsAfterAggro guard patch target");
            }
        }

        private static bool MethodMatchesSignature(MethodInfo method, string name, string[] parameterTypeNames)
        {
            if (method == null || method.Name != name)
            {
                return false;
            }
            ParameterInfo[] parameters = method.GetParameters();
            if (parameters.Length != parameterTypeNames.Length)
            {
                return false;
            }
            for (int i = 0; i < parameters.Length; i++)
            {
                if (!string.Equals(parameters[i].ParameterType.Name, parameterTypeNames[i], StringComparison.Ordinal))
                {
                    return false;
                }
            }
            return true;
        }

        private static string DescribeMethod(MethodInfo method)
        {
            if (method == null)
            {
                return "<null>";
            }
            string pars = string.Join(",", method.GetParameters().Select(p => p.ParameterType.Name));
            string owner = method.DeclaringType?.FullName ?? "<unknown>";
            return owner + "." + method.Name + "(" + pars + ")";
        }

        private static bool IsAttackAllowedPrefix(object __instance, PlayerID pid, ref bool __result)
        {
            try
            {
                FieldInfo playerField = __instance?.GetType().GetField("_player", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                PlayerInfo owner = playerField?.GetValue(__instance) as PlayerInfo;
                PlayerInfo target = GameplayTweaks.G.FindPlayerById(pid.id);
                if (IsApplyingExplicitHostilityState || IsAiStabilizationSuppressionActive())
                {
                    if (ShouldForceCopAttack(owner, target))
                    {
                        __result = true;
                        return false;
                    }
                    if (ShouldSuppressCopAttack(owner, target))
                    {
                        __result = false;
                        return false;
                    }
                    return true;
                }
                if (ShouldForceCopAttack(owner, target))
                {
                    TryAddAggroOn(owner, pid);
                    if (target != null)
                    {
                        TryAddAggroOn(target, owner.PID);
                    }
                    __result = true;
                    return false;
                }
                if (ShouldSuppressCopAttack(owner, target))
                {
                    ForceClearAggroPair(owner, pid);
                    if (target != null)
                    {
                        ForceClearAggroPair(target, owner.PID);
                        TryNormalizeSafeHookCopAggro(target, "is-attack-allowed");
                        TryReconcileSafeHookCopWarState(target, "is-attack-allowed");
                    }
                    __result = false;
                    return false;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[CopKilling] IsAttackAllowedPrefix failed: " + ex.Message);
            }
            return true;
        }

        private static bool AttackAdvisorTryPickBuildingPrefix(object __instance)
        {
            if (TrySkipAttackAdvisorTurnActionIfInvalid(__instance, "TryPickBuilding"))
            {
                return false;
            }
            if (!IsAiStabilizationSuppressionActive())
            {
                return true;
            }

            try
            {
                if (ShouldLogSuppressionSkip(ref _lastAttackAdvisorTryPickSuppressionLogFrame))
                {
                    Debug.Log($"[CopKilling] AttackAdvisor.TryPickBuilding skipped during cop-war stabilization frame={Time.frameCount} resetUntil={_debugResetSuppressionUntilFrame} favorUntil={_politicalFavorSuppressionUntilFrame}");
                }
            }
            catch
            {
            }
            return false;
        }

        private static bool AttackAdvisorTryForceClosurePrefix(object __instance)
        {
            if (TrySkipAttackAdvisorTurnActionIfInvalid(__instance, "TryForceClosure"))
            {
                return false;
            }
            if (!IsAiStabilizationSuppressionActive())
            {
                return true;
            }

            try
            {
                if (ShouldLogSuppressionSkip(ref _lastAttackAdvisorTryPickSuppressionLogFrame))
                {
                    Debug.Log($"[CopKilling] AttackAdvisor.TryForceClosure skipped during cop-war stabilization frame={Time.frameCount} resetUntil={_debugResetSuppressionUntilFrame} favorUntil={_politicalFavorSuppressionUntilFrame}");
                }
            }
            catch
            {
            }
            return false;
        }

        private static bool AttackAdvisorTrySellOutToFedsPrefix(object __instance)
        {
            if (TrySkipAttackAdvisorTurnActionIfInvalid(__instance, "TrySellOutToFeds"))
            {
                return false;
            }
            if (!IsAiStabilizationSuppressionActive())
            {
                return true;
            }

            try
            {
                if (ShouldLogSuppressionSkip(ref _lastAttackAdvisorTryPickSuppressionLogFrame))
                {
                    Debug.Log($"[CopKilling] AttackAdvisor.TrySellOutToFeds skipped during cop-war stabilization frame={Time.frameCount} resetUntil={_debugResetSuppressionUntilFrame} favorUntil={_politicalFavorSuppressionUntilFrame}");
                }
            }
            catch
            {
            }
            return false;
        }

        private static bool AttackAdvisorTryPickCoordPrefix(object __instance)
        {
            if (TrySkipAttackAdvisorTurnActionIfInvalid(__instance, "TryPickCoord"))
            {
                return false;
            }
            if (!IsAiStabilizationSuppressionActive())
            {
                return true;
            }

            try
            {
                if (ShouldLogSuppressionSkip(ref _lastAttackAdvisorTryPickSuppressionLogFrame))
                {
                    Debug.Log($"[CopKilling] AttackAdvisor.TryPickCoord skipped during cop-war stabilization frame={Time.frameCount} resetUntil={_debugResetSuppressionUntilFrame} favorUntil={_politicalFavorSuppressionUntilFrame}");
                }
            }
            catch
            {
            }
            return false;
        }

        private static bool AttackAdvisorOnTurnUpdatePrefix(object __instance)
        {
            TryRepairAttackAdvisorCoordState(__instance);
            if (!TryGetAdvisorSuppressionReason(out string reason))
            {
                return true;
            }

            try
            {
                if (ShouldLogSuppressionSkip(ref _lastAttackAdvisorTurnSuppressionLogFrame))
                {
                    Debug.Log($"[CopKilling] AttackAdvisor.OnTurnUpdate skipped reason={reason} frame={Time.frameCount} resetUntil={_debugResetSuppressionUntilFrame} favorUntil={_politicalFavorSuppressionUntilFrame}");
                }
            }
            catch
            {
            }
            return false;
        }

        private static bool TrySkipAttackAdvisorTurnActionIfInvalid(object attackAdvisor, string action)
        {
            try
            {
                if (attackAdvisor == null)
                {
                    return false;
                }
                if (_attackAdvisorPlayerField == null)
                {
                    _attackAdvisorPlayerField = attackAdvisor.GetType().GetField("_player", BindingFlags.Instance | BindingFlags.NonPublic);
                }
                PlayerInfo player = _attackAdvisorPlayerField?.GetValue(attackAdvisor) as PlayerInfo;
                if (player == null || player.territory == null)
                {
                    LogAttackAdvisorInvalidSkip(action, "missing-player-territory");
                    return true;
                }
                Node hqNode = null;
                try
                {
                    hqNode = player.territory.GetHeadquartersNode();
                }
                catch
                {
                    hqNode = null;
                }
                if (hqNode == null)
                {
                    LogAttackAdvisorInvalidSkip(action, "missing-headquarters-node");
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                LogAttackAdvisorInvalidSkip(action, ex.GetType().Name + ":" + ex.Message);
                return true;
            }
        }

        private static void LogAttackAdvisorInvalidSkip(string action, string reason)
        {
            try
            {
                if (ShouldLogSuppressionSkip(ref _lastAttackAdvisorTryPickSuppressionLogFrame))
                {
                    Debug.Log($"[CopKilling] AttackAdvisor.{action} skipped due to invalid state reason={reason} frame={Time.frameCount}");
                }
            }
            catch
            {
            }
        }

        private static bool TryRepairAttackAdvisorCoordState(object attackAdvisor)
        {
            try
            {
                if (attackAdvisor == null)
                {
                    return false;
                }
                if (_attackAdvisorDataField == null)
                {
                    _attackAdvisorDataField = attackAdvisor.GetType().GetField("_data", BindingFlags.Instance | BindingFlags.NonPublic);
                }
                object data = _attackAdvisorDataField?.GetValue(attackAdvisor);
                if (data == null)
                {
                    return false;
                }
                if (_attackAdvisorCoordStateField == null)
                {
                    _attackAdvisorCoordStateField = data.GetType().GetField("coordState", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                }
                object coordState = _attackAdvisorCoordStateField?.GetValue(data);
                if (coordState == null)
                {
                    return false;
                }
                Type coordType = coordState.GetType();
                if (_attackAdvisorCoordStateAttackingCrewField == null)
                {
                    _attackAdvisorCoordStateAttackingCrewField = coordType.GetField("attackingCrew", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                }
                if (_attackAdvisorCoordStateTargetPlayerField == null)
                {
                    _attackAdvisorCoordStateTargetPlayerField = coordType.GetField("targetPlayer", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                }
                if (_attackAdvisorCoordStateRallyPointField == null)
                {
                    _attackAdvisorCoordStateRallyPointField = coordType.GetField("rallyPoint", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                }
                if (_attackAdvisorCoordStateRallyPointNodeIdField == null)
                {
                    _attackAdvisorCoordStateRallyPointNodeIdField = coordType.GetField("rallyPointNodeId", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                }
                if (_attackAdvisorCoordStateRallyExpiresField == null)
                {
                    _attackAdvisorCoordStateRallyExpiresField = coordType.GetField("rallyExpires", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                }
                bool changed = false;
                if (_attackAdvisorCoordStateAttackingCrewField?.GetValue(coordState) is IEnumerable<EntityID> attackingCrew)
                {
                    List<EntityID> validCrew = new List<EntityID>();
                    foreach (EntityID peepId in attackingCrew)
                    {
                        if (IsValidAttackAdvisorCoordCrew(peepId))
                        {
                            validCrew.Add(peepId);
                        }
                    }
                    if (validCrew.Count != attackingCrew.Count())
                    {
                        _attackAdvisorCoordStateAttackingCrewField.SetValue(coordState, validCrew);
                        changed = true;
                    }
                }
                bool invalidState = false;
                PlayerID targetPlayer = default(PlayerID);
                if (_attackAdvisorCoordStateTargetPlayerField?.GetValue(coordState) is PlayerID targetPlayerValue)
                {
                    targetPlayer = targetPlayerValue;
                }
                EntityID rallyPoint = default(EntityID);
                if (_attackAdvisorCoordStateRallyPointField?.GetValue(coordState) is EntityID rallyPointValue)
                {
                    rallyPoint = rallyPointValue;
                }
                NodeID rallyPointNodeId = default(NodeID);
                if (_attackAdvisorCoordStateRallyPointNodeIdField?.GetValue(coordState) is NodeID rallyPointNodeValue)
                {
                    rallyPointNodeId = rallyPointNodeValue;
                }
                PlayerInfo targetPlayerInfo = null;
                try
                {
                    if (targetPlayer.IsAnyPlayer)
                    {
                        targetPlayerInfo = targetPlayer.FindPlayer();
                    }
                }
                catch
                {
                    targetPlayerInfo = null;
                }
                if (!targetPlayer.IsAnyPlayer || targetPlayerInfo?.crew == null)
                {
                    invalidState = true;
                }
                if (!rallyPoint.IsValid || rallyPoint.FindEntity() == null)
                {
                    invalidState = true;
                }
                if (!rallyPointNodeId.IsValid || rallyPointNodeId.FindNode() == null)
                {
                    invalidState = true;
                }
                if (!changed && _attackAdvisorCoordStateAttackingCrewField?.GetValue(coordState) is IEnumerable<EntityID> crewAfter && !crewAfter.Any())
                {
                    invalidState = true;
                }
                if (!invalidState && targetPlayerInfo?.crew != null)
                {
                    try
                    {
                        IEnumerable<CrewAssignment> livingCrew = targetPlayerInfo.crew.GetLiving();
                        if (livingCrew == null || livingCrew.Any(item => !IsValidAttackAdvisorTargetCrew(item)))
                        {
                            invalidState = true;
                        }
                    }
                    catch
                    {
                        invalidState = true;
                    }
                }
                if (invalidState)
                {
                    _attackAdvisorCoordStateField.SetValue(data, null);
                    if (_lastAttackAdvisorCoordStateFixLogFrame != Time.frameCount)
                    {
                        _lastAttackAdvisorCoordStateFixLogFrame = Time.frameCount;
                        Debug.LogWarning($"[CopKilling] AttackAdvisor.OnTurnUpdate cleared invalid coordinated attack state frame={Time.frameCount} target={targetPlayer} rally={rallyPoint}");
                    }
                    return true;
                }
                return changed;
            }
            catch (Exception ex)
            {
                if (_lastAttackAdvisorCoordStateFixLogFrame != Time.frameCount)
                {
                    _lastAttackAdvisorCoordStateFixLogFrame = Time.frameCount;
                    Debug.LogWarning($"[CopKilling] AttackAdvisor.OnTurnUpdate coord-state repair failed: {ex.GetType().Name}: {ex.Message}");
                }
                return false;
            }
        }

        private static bool IsValidAttackAdvisorTargetCrew(CrewAssignment assignment)
        {
            if (assignment.IsNotValid)
            {
                return false;
            }
            try
            {
                Entity peep = assignment.GetPeep();
                if (peep == null || peep.data?.agent == null)
                {
                    return false;
                }
                NodeID nid = peep.data.agent.nid;
                return nid.IsValid && nid.FindNode() != null;
            }
            catch
            {
                return false;
            }
        }

        private static bool IsValidAttackAdvisorCoordCrew(EntityID peepId)
        {
            if (!peepId.IsValid)
            {
                return false;
            }
            try
            {
                Entity peep = peepId.FindEntity();
                return peep != null && peep.data?.agent != null;
            }
            catch
            {
                return false;
            }
        }

        private static bool ForceClearAttackAdvisorCoordState(object attackAdvisor, string reason)
        {
            try
            {
                if (attackAdvisor == null)
                {
                    return false;
                }
                if (_attackAdvisorDataField == null)
                {
                    _attackAdvisorDataField = attackAdvisor.GetType().GetField("_data", BindingFlags.Instance | BindingFlags.NonPublic);
                }
                object data = _attackAdvisorDataField?.GetValue(attackAdvisor);
                if (data == null)
                {
                    return false;
                }
                if (_attackAdvisorCoordStateField == null)
                {
                    _attackAdvisorCoordStateField = data.GetType().GetField("coordState", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                }
                object coordState = _attackAdvisorCoordStateField?.GetValue(data);
                if (coordState == null)
                {
                    return false;
                }
                _attackAdvisorCoordStateField.SetValue(data, null);
                if (_lastAttackAdvisorCoordStateFixLogFrame != Time.frameCount)
                {
                    _lastAttackAdvisorCoordStateFixLogFrame = Time.frameCount;
                    Debug.LogWarning($"[CopKilling] AttackAdvisor.OnTurnUpdate force-cleared coordinated attack state reason={reason} frame={Time.frameCount}");
                }
                return true;
            }
            catch (Exception ex)
            {
                if (_lastAttackAdvisorCoordStateFixLogFrame != Time.frameCount)
                {
                    _lastAttackAdvisorCoordStateFixLogFrame = Time.frameCount;
                    Debug.LogWarning($"[CopKilling] AttackAdvisor.OnTurnUpdate force-clear failed: {ex.GetType().Name}: {ex.Message}");
                }
                return false;
            }
        }

        private static Exception AttackAdvisorTryPickBuildingFinalizer(Exception __exception)
        {
            if (__exception == null)
            {
                return null;
            }
            int frame = Time.frameCount;
            if (_lastAttackAdvisorFinalizerLogFrame != frame)
            {
                _lastAttackAdvisorFinalizerLogFrame = frame;
                Debug.LogWarning($"[CopKilling] AttackAdvisor.TryPickBuilding swallowed exception during cop-war stabilization: {__exception.GetType().Name}: {__exception.Message}");
            }
            return null;
        }

        private static Exception AttackAdvisorOnTurnUpdateFinalizer(object __instance, Exception __exception)
        {
            if (__exception == null)
            {
                return null;
            }
            ForceClearAttackAdvisorCoordState(__instance, __exception.GetType().Name);
            int frame = Time.frameCount;
            if (_lastAttackAdvisorTurnFinalizerLogFrame != frame)
            {
                _lastAttackAdvisorTurnFinalizerLogFrame = frame;
                string stack = __exception.StackTrace ?? string.Empty;
                Debug.LogWarning($"[CopKilling] AttackAdvisor.OnTurnUpdate swallowed exception during cop-war stabilization: {__exception.GetType().Name}: {__exception.Message}\n{stack}");
            }
            return null;
        }

        private static bool CombatAdvisorMaybeAskForTrucePrefix(PlayerID other)
        {
            if (!TryGetAdvisorSuppressionReason(out string reason))
            {
                return true;
            }

            try
            {
                if (ShouldLogSuppressionSkip(ref _lastCombatAdvisorTruceSuppressionLogFrame))
                {
                    Debug.Log($"[CopKilling] CombatAdvisor.MaybeAskForTruce allowed-through reason={reason} frame={Time.frameCount} resetUntil={_debugResetSuppressionUntilFrame} favorUntil={_politicalFavorSuppressionUntilFrame} other={other.id}");
                }
            }
            catch
            {
            }
            return true;
        }

        private static bool CombatAdvisorOnTurnUpdatePrefix()
        {
            if (!TryGetAdvisorSuppressionReason(out string reason))
            {
                return true;
            }

            try
            {
                if (ShouldLogSuppressionSkip(ref _lastCombatAdvisorTurnSuppressionLogFrame))
                {
                    Debug.Log($"[CopKilling] CombatAdvisor.OnTurnUpdate skipped reason={reason} frame={Time.frameCount} resetUntilFrame={_debugResetSuppressionUntilFrame} resetUntilDay={_debugResetSuppressionUntilDay} favorUntilFrame={_politicalFavorSuppressionUntilFrame} favorUntilDay={_politicalFavorSuppressionUntilDay}");
                }
            }
            catch
            {
            }
            return false;
        }

        private static Exception CombatAdvisorMaybeAskForTruceFinalizer(Exception __exception)
        {
            if (__exception == null)
            {
                return null;
            }
            int frame = Time.frameCount;
            if (_lastCombatAdvisorTruceFinalizerLogFrame != frame)
            {
                _lastCombatAdvisorTruceFinalizerLogFrame = frame;
                Debug.LogWarning($"[CopKilling] CombatAdvisor.MaybeAskForTruce swallowed exception during cop-war stabilization: {__exception.GetType().Name}: {__exception.Message}");
            }
            return null;
        }

        private static Exception CombatAdvisorOnTurnUpdateFinalizer(Exception __exception)
        {
            if (__exception == null)
            {
                return null;
            }
            int frame = Time.frameCount;
            if (_lastCombatAdvisorUpdateFinalizerLogFrame != frame)
            {
                _lastCombatAdvisorUpdateFinalizerLogFrame = frame;
                Debug.LogWarning($"[CopKilling] CombatAdvisor.OnTurnUpdate swallowed exception during cop-war stabilization: {__exception.GetType().Name}: {__exception.Message}");
            }
            return null;
        }

        private static bool CombatAdvisorUpdateRequestsAfterAggroPrefix()
        {
            if (!TryGetAdvisorSuppressionReason(out string reason))
            {
                return true;
            }

            try
            {
                if (ShouldLogSuppressionSkip(ref _lastCombatAdvisorUpdateSuppressionLogFrame))
                {
                    Debug.Log($"[CopKilling] CombatAdvisor.UpdateRequestsAfterAggro skipped reason={reason} frame={Time.frameCount} resetUntilFrame={_debugResetSuppressionUntilFrame} resetUntilDay={_debugResetSuppressionUntilDay} favorUntilFrame={_politicalFavorSuppressionUntilFrame} favorUntilDay={_politicalFavorSuppressionUntilDay}");
                }
            }
            catch
            {
            }
            return false;
        }

        private static Exception CombatAdvisorUpdateRequestsAfterAggroFinalizer(Exception __exception)
        {
            if (__exception == null)
            {
                return null;
            }
            int frame = Time.frameCount;
            if (_lastCombatAdvisorUpdateFinalizerLogFrame != frame)
            {
                _lastCombatAdvisorUpdateFinalizerLogFrame = frame;
                Debug.LogWarning($"[CopKilling] CombatAdvisor.UpdateRequestsAfterAggro swallowed exception during cop-war stabilization: {__exception.GetType().Name}: {__exception.Message}");
            }
            return null;
        }

        private static void EnsureCombatHookMonitorDay(int nowDay)
        {
            if (_combatHookMonitorDay == nowDay)
            {
                return;
            }
            if (_combatHookMonitorDay >= 0 && _combatHookMonitorDay < nowDay && _sawCommittedCopCombatThisDay && !_sawCombatIncidentHookThisDay && !_loggedMissingCombatHookWarningThisDay)
            {
                _loggedMissingCombatHookWarningThisDay = true;
                Debug.LogWarning("[CopKilling] A committed cop combat did not reach a runtime combat hook before day rollover; verify combat method patching.");
            }
            _combatHookMonitorDay = nowDay;
            _sawCommittedCopCombatThisDay = false;
            _sawCombatIncidentHookThisDay = false;
            _loggedMissingCombatHookWarningThisDay = false;
        }

        private static void MarkCommittedCopCombatObserved()
        {
            int nowDay = GameplayTweaks.G.GetNow().days;
            EnsureCombatHookMonitorDay(nowDay);
            _sawCommittedCopCombatThisDay = true;
        }

        internal static void ProcessPendingCommittedCopCombatFallbacks()
        {
            if (_pendingCommittedCopCombats.Count == 0)
            {
                return;
            }

            try
            {
                int currentFrame = Time.frameCount;
                int nowDay = GameplayTweaks.G.GetNow().days;
                CleanupCopKillTrackingForDay(nowDay);
                for (int i = _pendingCommittedCopCombats.Count - 1; i >= 0; i--)
                {
                    PendingCommittedCopCombat pending = _pendingCommittedCopCombats[i];
                    if (pending == null)
                    {
                        _pendingCommittedCopCombats.RemoveAt(i);
                        continue;
                    }

                    if (pending.DayCreated >= 0 && pending.DayCreated < nowDay - 1)
                    {
                        VerificationLog($"combat-commit-fallback dropped attacker={pending.AttackerPeepId} target={pending.TargetPeepId} source={pending.SourceTag} reason=stale day={nowDay}");
                        _pendingCommittedCopCombats.RemoveAt(i);
                        continue;
                    }

                    if (currentFrame <= pending.CommitFrame + 1)
                    {
                        continue;
                    }

                    if (!TryFindEntityById(pending.AttackerPeepId, out Entity attackerPeep))
                    {
                        VerificationLog($"combat-commit-fallback dropped attacker={pending.AttackerPeepId} target={pending.TargetPeepId} source={pending.SourceTag} reason=missing-attacker");
                        _pendingCommittedCopCombats.RemoveAt(i);
                        continue;
                    }

                    if (!TryFindEntityById(pending.TargetPeepId, out Entity targetPeep))
                    {
                        VerificationLog($"combat-commit-fallback dropped attacker={pending.AttackerPeepId} target={pending.TargetPeepId} source={pending.SourceTag} reason=missing-target");
                        _pendingCommittedCopCombats.RemoveAt(i);
                        continue;
                    }

                    PlayerInfo humanPlayer = GameplayTweaks.G.GetHumanPlayer();
                    if (humanPlayer == null || attackerPeep.data?.agent?.pid != humanPlayer.PID)
                    {
                        _pendingCommittedCopCombats.RemoveAt(i);
                        continue;
                    }

                    PlayerID? targetPid = targetPeep.data?.agent?.pid;
                    PlayerInfo targetPlayer = targetPid.HasValue ? PlayerIDExtensions.FindPlayer(targetPid.Value) : null;
                    if (!IsCop(targetPeep) && !IsCopPlayer(targetPlayer))
                    {
                        VerificationLog($"combat-commit-fallback dropped attacker={pending.AttackerPeepId} target={pending.TargetPeepId} source={pending.SourceTag} reason=target-no-longer-cop");
                        _pendingCommittedCopCombats.RemoveAt(i);
                        continue;
                    }

                    bool lethal = targetPeep.data?.person != null && !targetPeep.data.person.IsAlive;
                    string fallbackSource = pending.SourceTag + "-commit-fallback";
                    MarkCombatIncidentHookFired(fallbackSource, attackerPeep, targetPeep);
                    RecordObservedCopTarget(attackerPeep.Id, targetPeep.Id, fallbackSource);
                    bool accepted = TryHandleCopIncident(attackerPeep, targetPeep, targetPlayer, fallbackSource, lethal);
                    VerificationLog($"combat-commit-fallback applied attacker={attackerPeep.Id.id} target={targetPeep.Id.id} lethal={lethal} source={pending.SourceTag} frame={currentFrame} accepted={accepted}");
                    _pendingCommittedCopCombats.RemoveAt(i);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[CopKilling] ProcessPendingCommittedCopCombatFallbacks failed: {ex.Message}");
            }
        }

        private static bool TryFindEntityById(ulong rawEntityId, out Entity entity)
        {
            entity = null;
            if (rawEntityId == 0UL)
            {
                return false;
            }

            try
            {
                EntityID entityId = EntityID.FromID(rawEntityId);
                if (entityId.IsNotValid)
                {
                    return false;
                }

                entity = entityId.FindEntity();
                return entity != null;
            }
            catch
            {
                entity = null;
                return false;
            }
        }

        private static void QueueCommittedCopCombatFallback(Entity attackerPeep, Entity targetPeep, string source)
        {
            if (attackerPeep == null || targetPeep == null || attackerPeep.Id.IsNotValid || targetPeep.Id.IsNotValid)
            {
                return;
            }

            PlayerInfo humanPlayer = GameplayTweaks.G.GetHumanPlayer();
            if (humanPlayer == null || attackerPeep.data?.agent?.pid != humanPlayer.PID)
            {
                return;
            }

            PlayerID? targetPid = targetPeep.data?.agent?.pid;
            PlayerInfo targetPlayer = targetPid.HasValue ? PlayerIDExtensions.FindPlayer(targetPid.Value) : null;
            if (!IsCop(targetPeep) && !IsCopPlayer(targetPlayer))
            {
                return;
            }

            int nowDay = GameplayTweaks.G.GetNow().days;
            CleanupCopKillTrackingForDay(nowDay);
            MarkCommittedCopCombatObserved();
            PendingCommittedCopCombat pending = _pendingCommittedCopCombats.FirstOrDefault(item => item.AttackerPeepId == attackerPeep.Id.id && item.TargetPeepId == targetPeep.Id.id);
            if (pending == null)
            {
                pending = new PendingCommittedCopCombat
                {
                    AttackerPeepId = attackerPeep.Id.id,
                    TargetPeepId = targetPeep.Id.id
                };
                _pendingCommittedCopCombats.Add(pending);
            }

            pending.DayCreated = nowDay;
            pending.CommitFrame = Time.frameCount;
            pending.SourceTag = source ?? string.Empty;
            RecordObservedCopTarget(attackerPeep.Id, targetPeep.Id, source + "-queued");
            VerificationLog($"combat-commit-queued attacker={attackerPeep.Id.id} target={targetPeep.Id.id} source={source} day={nowDay} frame={pending.CommitFrame}");
        }

        private static void ClearPendingCommittedCopCombatFallbacks(ulong attackerPeepId, ulong targetPeepId, string source)
        {
            if (attackerPeepId == 0UL)
            {
                return;
            }

            int removed = _pendingCommittedCopCombats.RemoveAll(item =>
                item != null
                && item.AttackerPeepId == attackerPeepId
                && (targetPeepId == 0UL || item.TargetPeepId == targetPeepId));
            if (removed > 0)
            {
                VerificationLog($"combat-commit-cleared attacker={attackerPeepId} target={targetPeepId} source={source} cleared={removed}");
            }
        }

        private static void MarkCopAttackCandidateObserved()
        {
            int nowDay = GameplayTweaks.G.GetNow().days;
            EnsureCombatHookMonitorDay(nowDay);
        }

        private static bool ShouldLogCombatTraceSource(string source)
        {
            if (string.IsNullOrWhiteSpace(source))
            {
                return false;
            }

            if (string.Equals(source, "perform-combat", StringComparison.Ordinal))
            {
                return true;
            }

            if (source.StartsWith("grouped-", StringComparison.Ordinal))
            {
                return true;
            }

            if (source.EndsWith("-commit-fallback", StringComparison.Ordinal))
            {
                return true;
            }

            if (source.IndexOf("diagnostic", StringComparison.Ordinal) >= 0)
            {
                return true;
            }

            return false;
        }

        private static void MarkCombatIncidentHookFired(string source, Entity attackerPeep, Entity targetPeep)
        {
            int nowDay = GameplayTweaks.G.GetNow().days;
            EnsureCombatHookMonitorDay(nowDay);
            _sawCombatIncidentHookThisDay = true;
            ulong attackerId = attackerPeep?.Id.id ?? 0UL;
            ulong targetId = targetPeep?.Id.id ?? 0UL;
            ClearPendingCommittedCopCombatFallbacks(attackerId, targetId, source);
            if (ShouldLogCombatTraceSource(source))
            {
                VerificationLog($"Combat postfix fired source={source} attacker={attackerId} target={targetId} day={nowDay}");
            }
        }

        private static bool ShouldTrackHumanCopCombat(Entity attackerPeep, Entity targetPeep)
        {
            if (attackerPeep?.data?.agent?.pid.IsHumanPlayer != true)
            {
                return false;
            }

            PlayerID? targetPid = targetPeep?.data?.agent?.pid;
            PlayerInfo targetPlayer = targetPid.HasValue ? PlayerIDExtensions.FindPlayer(targetPid.Value) : null;
            return IsCop(targetPeep) || IsCopPlayer(targetPlayer);
        }

        private static bool TryGetCombatPopupSelectedTarget(object popupInstance, Entity peep, out Entity targetPeep, out bool targetIsCop)
        {
            targetPeep = null;
            targetIsCop = false;
            if (popupInstance == null || peep == null)
            {
                return false;
            }

            try
            {
                if (_combatPopupFindTargetForMethod == null || _combatPopupFindTargetForMethod.DeclaringType != popupInstance.GetType())
                {
                    _combatPopupFindTargetForMethod = popupInstance.GetType().GetMethod("FindTargetFor", BindingFlags.Instance | BindingFlags.NonPublic, null, new Type[] { typeof(Entity) }, null);
                }
                if (_combatPopupFindTargetForMethod == null)
                {
                    return false;
                }

                targetPeep = _combatPopupFindTargetForMethod.Invoke(popupInstance, new object[] { peep }) as Entity;
                if (targetPeep == null)
                {
                    return false;
                }

                PlayerID? targetPid = targetPeep.data?.agent?.pid;
                PlayerInfo targetPlayer = targetPid.HasValue ? PlayerIDExtensions.FindPlayer(targetPid.Value) : null;
                targetIsCop = IsCop(targetPeep) || IsCopPlayer(targetPlayer);
                return true;
            }
            catch
            {
                targetPeep = null;
                targetIsCop = false;
                return false;
            }
        }

        private static void LogCombatPopupDoCombatContext(string source, object popupInstance, Entity peep)
        {
            try
            {
                if (peep == null)
                {
                    return;
                }

                if (!TryGetCombatPopupSelectedTarget(popupInstance, peep, out Entity targetPeep, out bool targetIsCop))
                {
                    VerificationLog($"{source} attacker={peep.Id.id} target=0 targetIsCop=False unresolved=True");
                    return;
                }

                VerificationLog($"{source} attacker={peep.Id.id} target={targetPeep.Id.id} targetIsCop={targetIsCop}");
                if (targetIsCop)
                {
                    MarkCopAttackCandidateObserved();
                    RecordObservedCopTarget(peep.Id, targetPeep.Id, source);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[CopKilling] LogCombatPopupDoCombatContext failed source={source}: {ex.Message}");
            }
        }

        private static void OnPlayerTurnStartedPostfix(PlayerCrew __instance)
        {
            try
            {
                PlayerInfo humanPlayer = GameplayTweaks.G.GetHumanPlayer();
                if (humanPlayer == null || __instance != humanPlayer.crew)
                {
                    return;
                }
                int nowDay = GameplayTweaks.G.GetNow().days;
                if (ShouldDeferTurnStartReconciliation(nowDay))
                {
                    return;
                }
                EnsureCombatHookMonitorDay(nowDay);
                CleanupCopKillTrackingForDay(nowDay);
                ProcessPendingWitnessCases(humanPlayer);
                GameplayTweaks.GameplayTweaksPlugin.ReconcilePersistentGangRelationshipBuffs("cop-turn-start");
                EnsureActiveWarPrecinctFromBuffs(humanPlayer);
                EnforceCopWarFromRelationshipBuffs(humanPlayer);
                TryResolveCopWarFromPrecinctBribes(humanPlayer);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CopKilling] OnPlayerTurnStartedPostfix error: {ex}");
            }
        }

        private static bool ShouldDeferTurnStartReconciliation(int nowDay)
        {
            int currentFrame = Time.frameCount;
            int earliestFrame = Mathf.Max(240, GameplayTweaks.GameplayTweaksPlugin.RuntimePromptEarliestFrame);
            if (currentFrame < earliestFrame)
            {
                if (_lastDeferredTurnStartLogFrame != currentFrame)
                {
                    _lastDeferredTurnStartLogFrame = currentFrame;
                    VerificationLog($"turn-start reconciliation deferred day={nowDay} frame={currentFrame} earliestFrame={earliestFrame}");
                }
                return true;
            }
            return false;
        }

        private static void CanAttackTargetPostfix(PlayerID pid, EntityID target, ref bool __result)
        {
            try
            {
                if (__result || !pid.IsHumanPlayer)
                    return;
                Entity targetEntity = EntityIDExtensions.FindEntity(target);
                if (targetEntity == null)
                    return;
                PlayerID? targetPid = targetEntity.data?.agent?.pid;
                if (!targetPid.HasValue)
                    return;
                PlayerInfo targetPlayer = PlayerIDExtensions.FindPlayer(targetPid.Value);
                if (targetPlayer == null)
                    return;

                bool isCopOrFed = false;
                PropertyInfo prop = targetPlayer.GetType().GetProperty("IsCopOrFed");
                if (prop != null)
                    isCopOrFed = (bool)prop.GetValue(targetPlayer);
                else
                {
                    FieldInfo field = targetPlayer.GetType().GetField("IsCopOrFed");
                    if (field != null)
                        isCopOrFed = (bool)field.GetValue(targetPlayer);
                }
                if (!isCopOrFed)
                    return;

                PlayerInfo humanPlayer = GameplayTweaks.G.GetHumanPlayer();
                if (humanPlayer != null)
                {
                    TryNormalizeSafeHookCopAggro(humanPlayer, "can-attack-target");
                    TryReconcileSafeHookCopWarState(humanPlayer, "can-attack-target");
                }

                CrewAssignment crewForPeep = targetPlayer.crew.GetCrewForPeep(target);
                Entity peep = crewForPeep.GetPeep();
                bool isAlive = peep?.data?.person?.IsAlive == true;
                bool isInVehicle = crewForPeep.IsInVehicle;
                if (isAlive && isInVehicle)
                {
                    __result = true;
                    MarkCopAttackCandidateObserved();
                    string name = GameplayTweaks.GameplayTweaksPlugin.NormalizeRuntimePersonName(targetEntity.data?.person?.FullName);
                    if (string.IsNullOrWhiteSpace(name))
                    {
                        name = "Unknown";
                    }
                    Debug.Log("[CopKilling] Allowing attack on cop: " + name);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CopKilling] CanAttackTargetPostfix error: {ex}");
            }
        }

        private static void CleanupCopKillTrackingForDay(int nowDay)
        {
            if (_lastProcessedCopKillDay == nowDay)
            {
                return;
            }
            _lastProcessedCopKillDay = nowDay;
            _processedCopKillKeys.Clear();
            _processedGroupedCopAttackerCaseKeys.Clear();
            List<ulong> staleAttackers = _observedCopTargetDayByAttacker
                .Where(kv => kv.Value < nowDay - 1)
                .Select(kv => kv.Key)
                .ToList();
            foreach (ulong attackerId in staleAttackers)
            {
                _observedCopTargetDayByAttacker.Remove(attackerId);
                _observedCopTargetByAttacker.Remove(attackerId);
            }
            _lastIncomingAttackDiagnosticDay = nowDay;
            _incomingAttackDiagnosticKeys.Clear();
        }

        private static void OnExternalCombatResolved(CombatResults result, string source)
        {
            if (result == null || result.attacker.peep == null || result.target.peep == null)
            {
                return;
            }
            try
            {
                Entity attackerPeep = result.attacker.peep;
                Entity targetPeep = result.target.peep;
                PlayerID? attackerPid = attackerPeep.data?.agent?.pid;
                PlayerID? targetPid = targetPeep.data?.agent?.pid;
                if (attackerPid.HasValue && attackerPid.Value.IsHumanPlayer)
                {
                    PlayerInfo targetPlayer = targetPid.HasValue ? PlayerIDExtensions.FindPlayer(targetPid.Value) : null;
                    if (IsCop(targetPeep) || IsCopPlayer(targetPlayer))
                    {
                        TryProcessCopCombatIncident("grouped-" + source, result, attackerPeep, targetPeep);
                    }
                    return;
                }
                if (targetPid.HasValue && targetPid.Value.IsHumanPlayer && attackerPid.HasValue && attackerPid.Value.IsAIPlayer)
                {
                    LogIncomingAttackDiagnostic(result, source, attackerPeep, targetPeep, attackerPid.Value, targetPid.Value);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[CopKilling] External combat observer failed source={source}: {ex.Message}");
            }
        }

        private static void LogIncomingAttackDiagnostic(CombatResults result, string source, Entity attackerPeep, Entity targetPeep, PlayerID attackerPid, PlayerID targetPid)
        {
            int nowDay = GameplayTweaks.G.GetNow().days;
            CleanupCopKillTrackingForDay(nowDay);
            bool lethal = result != null && result.target.IsDead;
            string key = $"{nowDay}:{source}:{attackerPeep.Id.id}:{targetPeep.Id.id}:{lethal}";
            if (!_incomingAttackDiagnosticKeys.Add(key))
            {
                return;
            }
            PlayerInfo humanPlayer = GameplayTweaks.G.GetHumanPlayer();
            PlayerInfo attackerPlayer = PlayerIDExtensions.FindPlayer(attackerPid);
            bool attackerIsCop = IsCop(attackerPeep) || IsCopPlayer(attackerPlayer);
            bool humanHasCopKillBuff = humanPlayer != null
                && (HasTrackedCopWarState(humanPlayer) || HasAnyCopKillWarBuffActive(humanPlayer) || (attackerPlayer != null && HasCopKillBuffBetween(attackerPlayer, humanPlayer)));
            bool attackerAggroOnHuman = attackerPlayer != null && humanPlayer != null && IsAggroOn(attackerPlayer, humanPlayer.PID);
            VerificationLog($"incoming-attack source={source} attackerPid={attackerPid.id} attackerPeep={attackerPeep.Id.id} targetPid={targetPid.id} targetPeep={targetPeep.Id.id} lethal={lethal} attackerIsCop={attackerIsCop} humanHasCopKillBuff={humanHasCopKillBuff} attackerAggroOnHuman={attackerAggroOnHuman}");
        }

        private static void RecordObservedCopTarget(EntityID attackerId, EntityID targetId, string source)
        {
            if (attackerId.IsNotValid || targetId.IsNotValid)
            {
                return;
            }
            int nowDay = GameplayTweaks.G.GetNow().days;
            CleanupCopKillTrackingForDay(nowDay);
            _observedCopTargetByAttacker[attackerId.id] = targetId.id;
            _observedCopTargetDayByAttacker[attackerId.id] = nowDay;
            if (ShouldLogCombatTraceSource(source))
            {
                VerificationLog($"Observed cop combat target source={source} attacker={attackerId.id} target={targetId.id} day={nowDay}");
            }
        }

        private static bool TryConsumeObservedCopKill(Entity attackerPeep, out Entity victimPeep, out PlayerInfo victimPlayer, out string reason)
        {
            victimPeep = null;
            victimPlayer = null;
            reason = "no-observed-target";
            if (attackerPeep == null || attackerPeep.Id.IsNotValid)
            {
                reason = "invalid-attacker";
                return false;
            }
            int nowDay = GameplayTweaks.G.GetNow().days;
            CleanupCopKillTrackingForDay(nowDay);
            if (!_observedCopTargetByAttacker.TryGetValue(attackerPeep.Id.id, out ulong targetId) || targetId == 0UL)
            {
                return false;
            }
            EntityID victimId = EntityID.INVALID;
            try
            {
                victimId = EntityID.FromID(targetId);
            }
            catch
            {
                _observedCopTargetByAttacker.Remove(attackerPeep.Id.id);
                _observedCopTargetDayByAttacker.Remove(attackerPeep.Id.id);
                reason = "invalid-target-id";
                return false;
            }
            victimPeep = victimId.FindEntity();
            if (victimPeep == null)
            {
                _observedCopTargetByAttacker.Remove(attackerPeep.Id.id);
                _observedCopTargetDayByAttacker.Remove(attackerPeep.Id.id);
                reason = "target-entity-missing";
                return false;
            }
            PlayerID? pid = victimPeep.data?.agent?.pid;
            victimPlayer = pid.HasValue ? PlayerIDExtensions.FindPlayer(pid.Value) : null;
            bool isCopTarget = IsCop(victimPeep) || IsCopPlayer(victimPlayer);
            if (!isCopTarget)
            {
                _observedCopTargetByAttacker.Remove(attackerPeep.Id.id);
                _observedCopTargetDayByAttacker.Remove(attackerPeep.Id.id);
                reason = "observed-target-not-cop";
                return false;
            }
            bool dead = victimPeep.data?.person != null && !victimPeep.data.person.IsAlive;
            if (!dead)
            {
                reason = "observed-target-still-alive";
                return false;
            }
            _observedCopTargetByAttacker.Remove(attackerPeep.Id.id);
            _observedCopTargetDayByAttacker.Remove(attackerPeep.Id.id);
            reason = "observed-dead-cop";
            return true;
        }

        private static string BuildCopIncidentKey(Entity attackerPeep, Entity victimPeep, string source, bool lethal, out bool groupedVehicleIncident)
        {
            int nowDay = GameplayTweaks.G.GetNow().days;
            EntityID attackerId = attackerPeep != null ? attackerPeep.Id : EntityID.INVALID;
            groupedVehicleIncident = false;
            string attackerKey = attackerId.IsValid
                ? "peep:" + attackerId.id
                : "peep:0";

            if (TryResolveGroupedCombatAttackerVehicleId(source, out EntityID groupedVehicleId))
            {
                attackerKey = "vehicle:" + groupedVehicleId.id;
                groupedVehicleIncident = true;
                VerificationLog($"grouped-cop-shared-key source={source} vehicle={groupedVehicleId.id} attacker={(attackerId.IsValid ? attackerId.id : 0UL)} victim={(victimPeep?.Id.id ?? 0UL)} lethal={lethal}");
            }
            else
            {
                PlayerInfo humanPlayer = GameplayTweaks.G.GetHumanPlayer();
                CrewAssignment attackerCrew = humanPlayer?.crew?.GetCrewForPeep(attackerId) ?? CrewAssignment.EMPTY;
                if (attackerCrew.IsValid && attackerCrew.IsInVehicle && attackerCrew.VehicleID.IsValid)
                {
                    attackerKey = "vehicle:" + attackerCrew.VehicleID.id;
                }
            }

            ulong victimId = victimPeep?.Id.id ?? 0UL;
            string incidentSuffix = (lethal ? 1 : 0).ToString();
            return $"{nowDay}:{attackerKey}:{victimId}:{incidentSuffix}";
        }

        private static bool TryHandleCopIncident(Entity attackerPeep, Entity victimPeep, PlayerInfo victimPlayer, string source, bool lethal)
        {
            if (attackerPeep == null || victimPeep == null || attackerPeep.Id.IsNotValid || victimPeep.Id.IsNotValid)
            {
                return false;
            }
            int nowDay = GameplayTweaks.G.GetNow().days;
            CleanupCopKillTrackingForDay(nowDay);
            string killKey = BuildCopIncidentKey(attackerPeep, victimPeep, source, lethal, out bool groupedVehicleIncident);
            if (!_processedCopKillKeys.Add(killKey))
            {
                if (groupedVehicleIncident)
                {
                    VerificationLog($"cop-incident-deduped source={source} key={killKey} representative={attackerPeep.Id.id} victim={victimPeep.Id.id}");
                    VerificationLog($"national-heat-deduped source={source} key={killKey} representative={attackerPeep.Id.id} victim={victimPeep.Id.id}");
                }
                return false;
            }
            string incidentType = lethal ? "lethal" : "assault";
            VerificationLog($"Cop incident accepted source={source} type={incidentType} key={killKey} groupedShared={groupedVehicleIncident}");
            if (groupedVehicleIncident)
            {
                RegisterGroupedCopKillWitnessCases(GameplayTweaks.G.GetHumanPlayer(), attackerPeep, victimPeep, victimPlayer, source, lethal);
            }
            else
            {
                if (lethal)
                {
                    OnCopKilled(attackerPeep, victimPlayer, witnessed: true);
                }
                else
                {
                    OnCopAssaulted(attackerPeep, victimPlayer);
                }
            }
            return true;
        }

        private static bool TryResolveGroupedCombatAttackerVehicleId(string source, out EntityID vehicleId)
        {
            vehicleId = EntityID.INVALID;
            if (string.IsNullOrWhiteSpace(source) || !source.StartsWith("grouped-", StringComparison.Ordinal))
            {
                return false;
            }

            try
            {
                if (GameplayTweaks.GameplayTweaksPlugin.TryGetActiveGroupedCombatAttackerSnapshot(out _, out string snapshotSource)
                    && !string.IsNullOrWhiteSpace(snapshotSource))
                {
                    const string liveMarker = "attackerVehicle=";
                    int liveMarkerIndex = snapshotSource.IndexOf(liveMarker, StringComparison.Ordinal);
                    if (liveMarkerIndex >= 0)
                    {
                        int liveValueStart = liveMarkerIndex + liveMarker.Length;
                        int liveValueEnd = snapshotSource.IndexOf(':', liveValueStart);
                        string liveRawVehicleId = liveValueEnd >= 0
                            ? snapshotSource.Substring(liveValueStart, liveValueEnd - liveValueStart)
                            : snapshotSource.Substring(liveValueStart);
                        if (ulong.TryParse(liveRawVehicleId, out ulong liveParsedVehicleId) && liveParsedVehicleId != 0UL)
                        {
                            vehicleId = EntityID.FromID(liveParsedVehicleId);
                            if (vehicleId.IsValid)
                            {
                                return true;
                            }
                        }
                    }
                }

                if (_groupedCombatTransactionSourceField == null)
                {
                    Type patchType = typeof(GameplayTweaks.GameplayTweaksPlugin).GetNestedType("VehicleGroupCombatPatch", BindingFlags.NonPublic);
                    _groupedCombatTransactionSourceField = patchType?.GetField("_groupedCombatTransactionSource", BindingFlags.Static | BindingFlags.NonPublic);
                }

                string transactionSource = _groupedCombatTransactionSourceField?.GetValue(null) as string;
                if (string.IsNullOrWhiteSpace(transactionSource))
                {
                    return false;
                }

                const string marker = "attackerVehicle=";
                int markerIndex = transactionSource.IndexOf(marker, StringComparison.Ordinal);
                if (markerIndex < 0)
                {
                    return false;
                }

                int valueStart = markerIndex + marker.Length;
                int valueEnd = transactionSource.IndexOf(':', valueStart);
                string rawVehicleId = valueEnd >= 0
                    ? transactionSource.Substring(valueStart, valueEnd - valueStart)
                    : transactionSource.Substring(valueStart);
                if (!ulong.TryParse(rawVehicleId, out ulong parsedVehicleId) || parsedVehicleId == 0UL)
                {
                    return false;
                }

                vehicleId = EntityID.FromID(parsedVehicleId);
                return vehicleId.IsValid;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[CopKilling] TryResolveGroupedCombatAttackerVehicleId failed: {ex.Message}");
                vehicleId = EntityID.INVALID;
                return false;
            }
        }

        private static void CopKillStatIncrementPostfix(object __instance, CrewStats key, int delta)
        {
            if ((int)key != 0 || delta <= 0 || __instance == null)
            {
                return;
            }
            try
            {
                if (_agentEntityField == null)
                {
                    _agentEntityField = typeof(AgentComponent).BaseType?.GetField("_entity", BindingFlags.Instance | BindingFlags.NonPublic);
                }
                Entity attackerPeep = _agentEntityField?.GetValue(__instance) as Entity;
                if (attackerPeep == null || attackerPeep.Id.IsNotValid)
                {
                    return;
                }
                PlayerInfo humanPlayer = GameplayTweaks.G.GetHumanPlayer();
                if (humanPlayer == null || attackerPeep.data?.agent?.pid != humanPlayer.PID)
                {
                    return;
                }
                for (int i = 0; i < delta; i++)
                {
                    if (TryConsumeObservedCopKill(attackerPeep, out Entity victimPeep, out PlayerInfo victimPlayer, out string reason))
                    {
                        TryHandleCopIncident(attackerPeep, victimPeep, victimPlayer, "increment-stat-fallback", lethal: true);
                    }
                    else
                    {
                        VerificationLog($"Cop kill fallback not triggered attacker={attackerPeep.Id.id} reason={reason}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[CopKilling] CopKillStatIncrementPostfix failed: {ex.Message}");
            }
        }

        internal static void NotifyHumanCopCombatEncounter(Entity attackerPeep, Entity targetPeep, string source)
        {
            if (attackerPeep == null || targetPeep == null || attackerPeep.Id.IsNotValid || targetPeep.Id.IsNotValid)
            {
                return;
            }
            try
            {
                PlayerInfo humanPlayer = GameplayTweaks.G.GetHumanPlayer();
                if (humanPlayer == null || attackerPeep.data?.agent?.pid != humanPlayer.PID)
                {
                    return;
                }
                PlayerID? targetPid = targetPeep.data?.agent?.pid;
                PlayerInfo targetPlayer = targetPid.HasValue ? PlayerIDExtensions.FindPlayer(targetPid.Value) : null;
                bool targetIsCop = IsCop(targetPeep) || IsCopPlayer(targetPlayer);
                if (!targetIsCop)
                {
                    return;
                }
                RecordObservedCopTarget(attackerPeep.Id, targetPeep.Id, source + "-observe");
                if (TryHandleCopIncident(attackerPeep, targetPeep, targetPlayer, source, lethal: false))
                {
                    VerificationLog($"Cop combat observed; applying assault retaliation. source={source} target={targetPeep.Id.id} reason=direct-notify");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[CopKilling] NotifyHumanCopCombatEncounter failed: {ex.Message}");
            }
        }

        private static bool TryExtractCombatPeeps(object combatResult, out Entity attackerPeep, out Entity targetPeep, out object targetEntry)
        {
            attackerPeep = null;
            targetPeep = null;
            targetEntry = null;
            if (combatResult == null)
            {
                return false;
            }
            FieldInfo attackerField = combatResult.GetType().GetField("attacker");
            FieldInfo targetField = combatResult.GetType().GetField("target");
            if (attackerField == null || targetField == null)
            {
                return false;
            }
            object attacker = attackerField.GetValue(combatResult);
            targetEntry = targetField.GetValue(combatResult);
            if (attacker == null || targetEntry == null)
            {
                return false;
            }
            FieldInfo attackerPeepField = attacker.GetType().GetField("peep");
            FieldInfo targetPeepField = targetEntry.GetType().GetField("peep");
            if (attackerPeepField == null || targetPeepField == null)
            {
                return false;
            }
            attackerPeep = attackerPeepField.GetValue(attacker) as Entity;
            targetPeep = targetPeepField.GetValue(targetEntry) as Entity;
            return attackerPeep != null && targetPeep != null;
        }

        private static void TryProcessCopCombatIncident(string source, object combatResult, Entity fallbackAttacker = null, Entity fallbackTarget = null)
        {
            try
            {
                Entity attackerPeep = null;
                Entity targetPeep = null;
                object targetEntry = null;
                if (!TryExtractCombatPeeps(combatResult, out attackerPeep, out targetPeep, out targetEntry))
                {
                    attackerPeep = fallbackAttacker;
                    targetPeep = fallbackTarget;
                }
                if (attackerPeep == null || targetPeep == null)
                {
                    return;
                }

                PlayerInfo humanPlayer = GameplayTweaks.G.GetHumanPlayer();
                if (humanPlayer == null)
                {
                    return;
                }
                PlayerID? attackerPid = attackerPeep.data?.agent?.pid;
                if (!attackerPid.HasValue || attackerPid.Value != humanPlayer.PID)
                {
                    return;
                }

                PlayerID? targetPid = targetPeep.data?.agent?.pid;
                PlayerInfo targetPlayer = targetPid.HasValue ? PlayerIDExtensions.FindPlayer(targetPid.Value) : null;
                bool targetIsCop = IsCop(targetPeep) || IsCopPlayer(targetPlayer);
                if (!targetIsCop)
                {
                    return;
                }

                MarkCombatIncidentHookFired(source, attackerPeep, targetPeep);
                RecordObservedCopTarget(attackerPeep.Id, targetPeep.Id, source);
                bool isDead = IsCombatTargetDead(targetEntry, targetPeep, out string deathReason);
                if (!isDead)
                {
                    bool accepted = TryHandleCopIncident(attackerPeep, targetPeep, targetPlayer, source, lethal: false);
                    if (accepted)
                    {
                        VerificationLog($"Cop combat observed; applying assault retaliation. source={source} target={targetPeep.Id.id} reason={deathReason}");
                        if (!string.IsNullOrWhiteSpace(source) && source.StartsWith("grouped-", StringComparison.Ordinal))
                        {
                            VerificationLog($"grouped-cop-context-preserved source={source} attacker={attackerPeep.Id.id} target={targetPeep.Id.id} lethal=False");
                        }
                    }
                    return;
                }
                bool lethalAccepted = TryHandleCopIncident(attackerPeep, targetPeep, targetPlayer, source, lethal: true);
                if (lethalAccepted)
                {
                    VerificationLog($"Cop kill confirmed in combat postfix. source={source} target={targetPeep.Id.id} reason={deathReason}");
                    if (!string.IsNullOrWhiteSpace(source) && source.StartsWith("grouped-", StringComparison.Ordinal))
                    {
                        VerificationLog($"grouped-cop-context-preserved source={source} attacker={attackerPeep.Id.id} target={targetPeep.Id.id} lethal=True");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CopKilling] TryProcessCopCombatIncident error source={source}: {ex}");
            }
        }

        private static void PerformCombatPostfix(object __instance, object __result)
        {
            TryProcessCopCombatIncident("perform-combat", __result);
        }

        private static void PerformHumanCombatPrefix(Entity attacker, Entity target)
        {
            QueueCommittedCopCombatFallback(attacker, target, "perform-human-combat");
        }

        private static void PerformHumanCombatPostfix(Entity attacker, Entity target, object __result)
        {
            TryProcessCopCombatIncident("perform-human-combat", __result, attacker, target);
        }

        private static void CombatPopupDoCombatPostfix(object __instance, Entity peep, object __result)
        {
            Entity targetPeep = null;
            TryGetCombatPopupSelectedTarget(__instance, peep, out targetPeep, out _);
            TryProcessCopCombatIncident("combat-popup-do-combat", __result, peep, targetPeep);
        }

        private static void PerformCombatDiagnosticPostfix(object __instance, object __result)
        {
            try
            {
                if (!TryExtractCombatPeeps(__result, out Entity attackerPeep, out Entity targetPeep, out _))
                {
                    return;
                }

                if (!ShouldTrackHumanCopCombat(attackerPeep, targetPeep))
                {
                    return;
                }

                MarkCombatIncidentHookFired("perform-combat-diagnostic", attackerPeep, targetPeep);
                RecordObservedCopTarget(attackerPeep.Id, targetPeep.Id, "perform-combat-diagnostic");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[CopKilling] PerformCombatDiagnosticPostfix failed: {ex.Message}");
            }
        }

        private static void PerformHumanCombatDiagnosticPostfix(Entity attacker, Entity target, object __result)
        {
            try
            {
                Entity attackerPeep = attacker;
                Entity targetPeep = target;
                if ((attackerPeep == null || targetPeep == null) && TryExtractCombatPeeps(__result, out Entity extractedAttacker, out Entity extractedTarget, out _))
                {
                    attackerPeep = attackerPeep ?? extractedAttacker;
                    targetPeep = targetPeep ?? extractedTarget;
                }
                if (attackerPeep == null || targetPeep == null)
                {
                    return;
                }

                if (!ShouldTrackHumanCopCombat(attackerPeep, targetPeep))
                {
                    return;
                }

                MarkCombatIncidentHookFired("perform-human-combat-diagnostic", attackerPeep, targetPeep);
                RecordObservedCopTarget(attackerPeep.Id, targetPeep.Id, "perform-human-combat-diagnostic");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[CopKilling] PerformHumanCombatDiagnosticPostfix failed: {ex.Message}");
            }
        }

        private static void CombatPopupDoCombatPrefix(object __instance, Entity peep)
        {
            LogCombatPopupDoCombatContext("combat-popup-do-combat-prefix", __instance, peep);
            if (TryGetCombatPopupSelectedTarget(__instance, peep, out Entity targetPeep, out bool targetIsCop) && targetIsCop)
            {
                QueueCommittedCopCombatFallback(peep, targetPeep, "combat-popup-do-combat");
            }
        }

        private static void CombatPopupDoCombatDiagnosticPostfix(object __instance, Entity peep, object __result)
        {
            try
            {
                if (!TryGetCombatPopupSelectedTarget(__instance, peep, out Entity selectedTargetPeep, out bool targetIsCop))
                {
                    return;
                }

                if (__result == null)
                {
                    if (targetIsCop)
                    {
                        VerificationLog($"combat-popup-do-combat-diagnostic result-null attacker={peep?.Id.id ?? 0UL} target={selectedTargetPeep?.Id.id ?? 0UL}");
                    }
                    return;
                }

                if (!TryExtractCombatPeeps(__result, out Entity attackerPeep, out Entity targetPeep, out _))
                {
                    attackerPeep = peep;
                    targetPeep = selectedTargetPeep;
                }
                if (attackerPeep == null || targetPeep == null)
                {
                    return;
                }

                if (!ShouldTrackHumanCopCombat(attackerPeep, targetPeep))
                {
                    return;
                }

                MarkCombatIncidentHookFired("combat-popup-do-combat-diagnostic", attackerPeep, targetPeep);
                RecordObservedCopTarget(attackerPeep.Id, targetPeep.Id, "combat-popup-do-combat-diagnostic");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[CopKilling] CombatPopupDoCombatDiagnosticPostfix failed: {ex.Message}");
            }
        }

        private static void CombatPopupDoCombatDiagnosticPrefix(object __instance, Entity peep)
        {
            LogCombatPopupDoCombatContext("combat-popup-do-combat-diagnostic-prefix", __instance, peep);
        }

        private static void StoreCombatDiagnosticPrefix(object __instance, object __0)
        {
            LogConvoCombatHandoff("prefix", "store-combat", __instance, __0, null);
        }

        private static void StoreCombatDiagnosticPostfix(object __instance, object __0)
        {
            LogConvoCombatHandoff("postfix", "store-combat", __instance, __0, null);
        }

        private static Exception StoreCombatDiagnosticFinalizer(object __instance, object __0, Exception __exception)
        {
            if (__exception != null)
            {
                LogConvoCombatHandoff("finalizer", "store-combat", __instance, __0, __exception);
            }
            return __exception;
        }

        private static void ExecuteCombatDiagnosticPrefix(object __instance, object __0)
        {
            LogConvoCombatHandoff("prefix", "execute-combat", __instance, __0, null);
        }

        private static void ExecuteCombatDiagnosticPostfix(object __instance, object __0)
        {
            LogConvoCombatHandoff("postfix", "execute-combat", __instance, __0, null);
        }

        private static Exception ExecuteCombatDiagnosticFinalizer(object __instance, object __0, Exception __exception)
        {
            if (__exception != null)
            {
                LogConvoCombatHandoff("finalizer", "execute-combat", __instance, __0, __exception);
            }
            return __exception;
        }

        private static void CombatPopupRefreshContentsDiagnosticPrefix(object __instance)
        {
            try
            {
                if (__instance == null)
                {
                    return;
                }

                if (_combatPopupEnemiesField == null || _combatPopupMyCrewField == null)
                {
                    Type popupType = __instance.GetType();
                    if (_combatPopupEnemiesField == null)
                    {
                        _combatPopupEnemiesField = popupType.GetField("_enemies", BindingFlags.Instance | BindingFlags.NonPublic);
                    }
                    if (_combatPopupMyCrewField == null)
                    {
                        _combatPopupMyCrewField = popupType.GetField("_mycrew", BindingFlags.Instance | BindingFlags.NonPublic);
                    }
                }

                List<Entity> enemies = _combatPopupEnemiesField?.GetValue(__instance) as List<Entity>;

                List<Entity> myCrew = _combatPopupMyCrewField?.GetValue(__instance) as List<Entity>;
                if (myCrew == null || myCrew.Count == 0)
                {
                    return;
                }

                var repairedEnemies = enemies != null
                    ? enemies.Where(item => item != null && item.Id.IsValid).Distinct().ToList()
                    : new List<Entity>();
                int originalEnemyCount = repairedEnemies.Count;
                foreach (Entity attackerPeep in myCrew)
                {
                    if (attackerPeep == null || attackerPeep.Id.IsNotValid)
                    {
                        continue;
                    }
                    if (!_observedCopTargetByAttacker.TryGetValue(attackerPeep.Id.id, out ulong targetId) || targetId == 0UL)
                    {
                        continue;
                    }

                    Entity targetPeep = EntityID.FromID(targetId).FindEntity();
                    if (targetPeep == null || targetPeep.Id.IsNotValid || targetPeep.data?.person?.IsAlive != true)
                    {
                        continue;
                    }

                    PlayerID? targetPid = targetPeep.data?.agent?.pid;
                    PlayerInfo targetPlayer = targetPid.HasValue ? PlayerIDExtensions.FindPlayer(targetPid.Value) : null;
                    if (!IsCop(targetPeep) && !IsCopPlayer(targetPlayer))
                    {
                        continue;
                    }

                    if (!repairedEnemies.Any(item => item != null && item.Id == targetPeep.Id))
                    {
                        repairedEnemies.Add(targetPeep);
                    }
                }

                if (repairedEnemies.Count == 0)
                {
                    return;
                }

                if (repairedEnemies.Count == originalEnemyCount)
                {
                    return;
                }

                _combatPopupEnemiesField?.SetValue(__instance, repairedEnemies);
                VerificationLog($"popup-target-repair source=observed-cop-target attackers={myCrew.Count} originalEnemies={originalEnemyCount} repairedEnemies={string.Join(",", repairedEnemies.Select(item => item.Id.id.ToString()))}");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[CopKilling] CombatPopupRefreshContentsDiagnosticPrefix failed: {ex.Message}");
            }
        }

        private static void LogConvoCombatHandoff(string stage, string method, object convoCallbacksInstance, object button, Exception ex)
        {
            try
            {
                if (!TryGetConvoCombatContext(convoCallbacksInstance, out PlayerID visitPid, out Entity attackerPeep, out Entity targetPeep, out bool attackerInVehicle, out PlayerID targetPid, out bool targetIsCop))
                {
                    string unresolvedSuffix = ex == null ? string.Empty : $" exception={ex.GetType().Name}:{ex.Message}";
                    VerificationLog($"convo-combat-handoff stage={stage} method={method} context=unresolved buttonType={button?.GetType().Name ?? "null"}{unresolvedSuffix}");
                    return;
                }

                ulong attackerId = attackerPeep?.Id.id ?? 0UL;
                ulong targetId = targetPeep?.Id.id ?? 0UL;
                string exceptionSuffix = ex == null ? string.Empty : $" exception={ex.GetType().Name}:{ex.Message}";
                VerificationLog($"convo-combat-handoff stage={stage} method={method} visitPid={visitPid.id} human={visitPid.IsHumanPlayer} attacker={attackerId} attackerVehicle={attackerInVehicle} target={targetId} targetPid={targetPid.id} targetIsCop={targetIsCop} buttonType={button?.GetType().Name ?? "null"}{exceptionSuffix}");

                if (stage == "prefix" && method == "execute-combat" && visitPid.IsHumanPlayer && attackerPeep != null && targetPeep != null && targetIsCop)
                {
                    MarkCopAttackCandidateObserved();
                    RecordObservedCopTarget(attackerPeep.Id, targetPeep.Id, "convo-execute-combat");
                }
            }
            catch (Exception loggingEx)
            {
                Debug.LogWarning($"[CopKilling] LogConvoCombatHandoff failed stage={stage} method={method}: {loggingEx.Message}");
            }
        }

        private static bool TryGetConvoCombatContext(object convoCallbacksInstance, out PlayerID visitPid, out Entity attackerPeep, out Entity targetPeep, out bool attackerInVehicle, out PlayerID targetPid, out bool targetIsCop)
        {
            visitPid = PlayerID.INVALID;
            attackerPeep = null;
            targetPeep = null;
            attackerInVehicle = false;
            targetPid = PlayerID.INVALID;
            targetIsCop = false;

            object visit = GetConvoVisitObject(convoCallbacksInstance);
            if (visit == null)
            {
                return false;
            }

            if (TryGetMemberValue(visit, "pid", out PlayerID resolvedVisitPid))
            {
                visitPid = resolvedVisitPid;
            }

            object crewObj = GetMemberValue(visit, "crew");
            if (crewObj != null)
            {
                if (TryGetMemberValue(crewObj, "IsInVehicle", out bool isInVehicle))
                {
                    attackerInVehicle = isInVehicle;
                }

                MethodInfo getPeepMethod = crewObj.GetType().GetMethod("GetPeep", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
                if (getPeepMethod != null)
                {
                    attackerPeep = getPeepMethod.Invoke(crewObj, null) as Entity;
                }
            }

            targetPeep = GetMemberValue(visit, "npc") as Entity ?? GetMemberValue(visit, "peep") as Entity;
            PlayerInfo targetPlayer = null;
            if (targetPeep?.data?.agent?.pid is PlayerID resolvedTargetPid)
            {
                targetPid = resolvedTargetPid;
                targetPlayer = PlayerIDExtensions.FindPlayer(resolvedTargetPid);
            }
            targetIsCop = IsCop(targetPeep) || IsCopPlayer(targetPlayer);
            return true;
        }

        private static object GetConvoVisitObject(object convoCallbacksInstance)
        {
            if (convoCallbacksInstance == null)
            {
                return null;
            }

            if (_convoCallbacksVisitProperty == null)
            {
                _convoCallbacksVisitProperty = convoCallbacksInstance.GetType().GetProperty("Visit", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            }

            return _convoCallbacksVisitProperty?.GetValue(convoCallbacksInstance, null);
        }

        private static object GetMemberValue(object instance, string name)
        {
            if (instance == null || string.IsNullOrEmpty(name))
            {
                return null;
            }

            Type type = instance.GetType();
            PropertyInfo property = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property != null)
            {
                return property.GetValue(instance, null);
            }

            FieldInfo field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return field?.GetValue(instance);
        }

        private static bool TryGetMemberValue<T>(object instance, string name, out T value)
        {
            object raw = GetMemberValue(instance, name);
            if (raw is T typedValue)
            {
                value = typedValue;
                return true;
            }

            value = default;
            return false;
        }

        private static object GetCrewPickGoObject(object crewPickInstance)
        {
            if (crewPickInstance == null)
            {
                return null;
            }
            if (_crewPickGoField == null)
            {
                Type type = crewPickInstance.GetType();
                while (type != null && _crewPickGoField == null)
                {
                    _crewPickGoField = type.GetField("go", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    type = type.BaseType;
                }
            }
            return _crewPickGoField?.GetValue(crewPickInstance);
        }

        private static object GetCrewPickOverlayImage(object goObject)
        {
            if (goObject == null)
            {
                return null;
            }
            if (_crewPickGoGetImageMethod == null)
            {
                _crewPickGoGetImageMethod = goObject.GetType().GetMethod("GetImage", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[] { typeof(string) }, null);
            }
            if (_crewPickGoGetImageMethod == null)
            {
                return null;
            }
            try
            {
                return _crewPickGoGetImageMethod.Invoke(goObject, new object[] { "Button/Color Overlay" });
            }
            catch
            {
                return null;
            }
        }

        private static void SetUiGraphicColor(object graphicObject, Color color)
        {
            if (graphicObject == null)
            {
                return;
            }
            Type type = graphicObject.GetType();
            if (_graphicColorProperty == null || !_graphicColorProperty.DeclaringType.IsAssignableFrom(type))
            {
                _graphicColorProperty = type.GetProperty("color", BindingFlags.Instance | BindingFlags.Public);
            }
            if (_graphicColorProperty == null || !_graphicColorProperty.CanWrite)
            {
                return;
            }
            try
            {
                _graphicColorProperty.SetValue(graphicObject, color, null);
            }
            catch
            {
            }
        }

        private static void CrewPickRefreshPostfix(object __instance)
        {
            if (__instance == null)
            {
                return;
            }
            try
            {
                if (_crewPickPidField == null)
                {
                    _crewPickPidField = __instance.GetType().GetField("_pid", BindingFlags.Instance | BindingFlags.NonPublic);
                }
                if (_crewPickPlayerColorField == null)
                {
                    _crewPickPlayerColorField = __instance.GetType().GetField("_playerColor", BindingFlags.Instance | BindingFlags.NonPublic);
                }
                if (_crewPickPidField == null)
                {
                    return;
                }
                if (!(_crewPickPidField.GetValue(__instance) is PlayerID pid))
                {
                    return;
                }
                PlayerInfo targetPlayer = PlayerIDExtensions.FindPlayer(pid);
                if (targetPlayer == null || !IsCopPlayer(targetPlayer))
                {
                    return;
                }
                bool isAggro = IsCopOrFedAggroOnHuman(targetPlayer);
                Color baseColor = ColorConstants.POLICE_BLUE;
                try
                {
                    if (targetPlayer.IsCopOrFed)
                    {
                        baseColor = ColorConstants.POLICE_BLUE;
                    }
                    else if (_crewPickPlayerColorField != null && _crewPickPlayerColorField.GetValue(__instance) is Color pickColor)
                    {
                        baseColor = pickColor;
                    }
                }
                catch
                {
                    if (_crewPickPlayerColorField != null && _crewPickPlayerColorField.GetValue(__instance) is Color pickColor)
                    {
                        baseColor = pickColor;
                    }
                }
                object goObject = GetCrewPickGoObject(__instance);
                object overlayImage = GetCrewPickOverlayImage(goObject);
                if (overlayImage == null)
                {
                    return;
                }
                SetUiGraphicColor(overlayImage, isAggro ? CopAggroPickTintColor : baseColor);
                if (!isAggro)
                {
                    return;
                }
                int nowDay = GameplayTweaks.G.GetNow().days;
                if (_lastCopPickTintLogDay != nowDay)
                {
                    _lastCopPickTintLogDay = nowDay;
                    _copPickTintLogPids.Clear();
                }
                if (_copPickTintLogPids.Add(pid.id))
                {
                    Debug.Log($"{VerificationLogPrefix} [CopPick] tint applied pid={pid.id} aggro=true");
                }
            }
            catch
            {
            }
        }

        private static bool IsCombatTargetDead(object combatTarget, Entity targetPeep, out string reason)
        {
            reason = "none";
            if (combatTarget == null)
            {
                reason = "combat-target-null";
                return false;
            }
            try
            {
                PropertyInfo isDeadProp = combatTarget.GetType().GetProperty("IsDead");
                if (isDeadProp != null)
                {
                    object val = isDeadProp.GetValue(combatTarget);
                    if (val is bool b && b)
                    {
                        reason = "target-IsDead-prop";
                        return true;
                    }
                }
            }
            catch
            {
            }
            try
            {
                FieldInfo isDeadField = combatTarget.GetType().GetField("IsDead");
                if (isDeadField != null)
                {
                    object val = isDeadField.GetValue(combatTarget);
                    if (val is bool b && b)
                    {
                        reason = "target-IsDead-field";
                        return true;
                    }
                }
            }
            catch
            {
            }
            try
            {
                if (targetPeep?.data?.person != null && !targetPeep.data.person.IsAlive)
                {
                    reason = "peep-person-not-alive";
                    return true;
                }
            }
            catch
            {
            }
            reason = "alive";
            return false;
        }
    }

    internal static class CombatNameDisplayPatch
    {
        public static void ApplyPatch(Harmony harmony)
        {
            try
            {
                Type convoType = typeof(GameClock).Assembly.GetType("Game.UI.Session.Convo.ConvoDataCombat");
                if (convoType != null)
                {
                    MethodInfo method = convoType.GetMethod("MakeReplacements", BindingFlags.Instance | BindingFlags.Public);
                    if (method != null)
                    {
                        harmony.Patch(method, null, new HarmonyMethod(typeof(CombatNameDisplayPatch), "MakeReplacementsPostfix", null), null, null, null);
                        Debug.Log("[CopKilling] Combat name display patch applied");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CopKilling] CombatNameDisplayPatch failed: {ex}");
            }
        }

        private static void MakeReplacementsPostfix(object __instance, ref string[] __result)
        {
            try
            {
                FieldInfo targetField = __instance.GetType().GetField("target");
                if (targetField == null)
                {
                    return;
                }

                object targetObj = targetField.GetValue(__instance);
                if (targetObj == null)
                {
                    return;
                }

                CrewAssignment targetAssignment = targetObj is CrewAssignment assignment ? assignment : CrewAssignment.EMPTY;
                Entity peep = null;
                MethodInfo getPeep = targetObj.GetType().GetMethod("GetPeep");
                if (getPeep != null)
                {
                    peep = getPeep.Invoke(targetObj, null) as Entity;
                }
                if (peep == null && targetAssignment.IsValid && targetAssignment.peepId.IsValid)
                {
                    peep = targetAssignment.peepId.FindEntity();
                }

                PlayerInfo player = ResolveTargetPlayer(peep);
                string name = ResolveTargetName(peep, player);
                string gang = ResolveTargetGang(player);
                if (!string.IsNullOrEmpty(gang))
                {
                    __result = new string[] { name, gang, name + " (" + gang + ")" };
                }
                else
                {
                    __result = new string[] { name, string.Empty, name };
                }

                Debug.Log("[CopKilling] Combat target: " + name + " from " + gang);
            }
            catch { }
        }

        private static PlayerInfo ResolveTargetPlayer(Entity peep)
        {
            try
            {
                PlayerID? pid = peep?.data?.agent?.pid;
                if (pid.HasValue && !pid.Value.IsNotAnyPlayer)
                {
                    return PlayerIDExtensions.FindPlayer(pid.Value);
                }
            }
            catch
            {
            }
            return null;
        }

        private static string ResolveTargetName(Entity peep, PlayerInfo player)
        {
            string peepName = GameplayTweaks.GameplayTweaksPlugin.NormalizeRuntimePersonName(peep?.data?.person?.FullName);
            if (!string.IsNullOrWhiteSpace(peepName))
            {
                return peepName;
            }
            if (IsPolicePlayer(player))
            {
                return "Officer";
            }
            string gangName = player?.social?.PlayerGroupName;
            if (!string.IsNullOrWhiteSpace(gangName))
            {
                return gangName;
            }
            return "Unknown";
        }

        private static string ResolveTargetGang(PlayerInfo player)
        {
            if (IsPolicePlayer(player))
            {
                return "Police";
            }
            return player?.social?.PlayerGroupName ?? string.Empty;
        }

        private static bool IsPolicePlayer(PlayerInfo player)
        {
            if (player == null)
            {
                return false;
            }
            try
            {
                if (player.IsJustCop || player.IsCopOrFed)
                {
                    return true;
                }
                PropertyInfo isJustCop = player.GetType().GetProperty("IsJustCop");
                if (isJustCop != null)
                {
                    object value = isJustCop.GetValue(player);
                    if (value is bool b && b)
                    {
                        return true;
                    }
                }
            }
            catch
            {
            }
            return false;
        }
    }
}
