using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using Game.Core;
using Game.Services;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Player;
using Game.Session.Sim;
using Game.Session.Sim.Modules;
using HarmonyLib;
using MonoMod.RuntimeDetour;
using UnityEngine;
using UnityEngine.UI;
using CrewStats = Game.Core.CrewStats;

namespace GameplayTweaks
{
    internal static class G
    {
        private static readonly Type GameType;
        private static readonly FieldInfo CtxField;
        private static FieldInfo _simmanField;
        private static FieldInfo _relsField;
        private static FieldInfo _peoplegenField;
        private static FieldInfo _playersField;
        private static FieldInfo _clockField;
        private static PropertyInfo _humanProp;
        private static PropertyInfo _nowProp;
        private static MethodInfo _getPoliticianMethod;

        static G()
        {
            GameType = typeof(GameClock).Assembly.GetType("Game.Game");
            if (GameType != null)
                CtxField = GameType.GetField("ctx", BindingFlags.Public | BindingFlags.Static);
        }

        public static dynamic ctx => CtxField?.GetValue(null);
        public static object GetCtx() => CtxField?.GetValue(null);

        private static void EnsureReflectionCached(object ctxObj)
        {
            if (_simmanField != null) return;
            var ctxType = ctxObj.GetType();
            _simmanField = ctxType.GetField("simman");
            _playersField = ctxType.GetField("players");
            _clockField = ctxType.GetField("clock");

            if (_simmanField != null)
            {
                var simType = _simmanField.FieldType;
                _relsField = simType.GetField("rels");
                _peoplegenField = simType.GetField("peoplegen");
            }
        }

        public static RelationshipTracker GetRels()
        {
            var ctxObj = GetCtx();
            if (ctxObj == null) return null;
            EnsureReflectionCached(ctxObj);
            var simman = _simmanField?.GetValue(ctxObj);
            return _relsField?.GetValue(simman) as RelationshipTracker;
        }

        public static PeopleTracker GetPeopleGen()
        {
            var ctxObj = GetCtx();
            if (ctxObj == null) return null;
            EnsureReflectionCached(ctxObj);
            var simman = _simmanField?.GetValue(ctxObj);
            return _peoplegenField?.GetValue(simman) as PeopleTracker;
        }

        public static PlayerCrew GetHumanCrew()
        {
            var ctxObj = GetCtx();
            if (ctxObj == null) return null;
            EnsureReflectionCached(ctxObj);
            var players = _playersField?.GetValue(ctxObj);
            if (players == null) return null;
            if (_humanProp == null) _humanProp = players.GetType().GetProperty("Human");
            var human = _humanProp?.GetValue(players) as PlayerInfo;
            return human?.crew;
        }

        public static SimTime GetNow()
        {
            var ctxObj = GetCtx();
            if (ctxObj == null) return default;
            EnsureReflectionCached(ctxObj);
            var clock = _clockField?.GetValue(ctxObj);
            if (clock == null) return default;
            if (_nowProp == null) _nowProp = clock.GetType().GetProperty("Now");
            return (SimTime)_nowProp.GetValue(clock);
        }

        public static object GetPoliticianData(EntityID id)
        {
            var ctxObj = GetCtx();
            if (ctxObj == null) return null;
            EnsureReflectionCached(ctxObj);
            var simman = _simmanField?.GetValue(ctxObj);
            if (simman == null) return null;
            var politics = simman.GetType().GetField("politics")?.GetValue(simman)
                        ?? simman.GetType().GetProperty("politics")?.GetValue(simman);
            if (politics == null) return null;
            if (_getPoliticianMethod == null)
                _getPoliticianMethod = politics.GetType().GetMethod("GetPoliticianData");
            return _getPoliticianMethod?.Invoke(politics, new object[] { id });
        }

        public static Entity GetManager(Entity building)
        {
            try { return BuildingUtil.FindOwnerOrManagerForAnyBuilding(building); }
            catch { return null; }
        }

        public static object GetPlayers()
        {
            var ctxObj = GetCtx();
            if (ctxObj == null) return null;
            EnsureReflectionCached(ctxObj);
            return _playersField?.GetValue(ctxObj);
        }

        public static IEnumerable<PlayerInfo> GetAllPlayers()
        {
            var players = GetPlayers();
            if (players == null) yield break;
            IEnumerable<PlayerInfo> all = null;
            // Try property first, then field
            var allProp = players.GetType().GetProperty("all", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (allProp != null)
                all = allProp.GetValue(players) as IEnumerable<PlayerInfo>;
            if (all == null)
            {
                var allField = players.GetType().GetField("all", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (allField != null)
                    all = allField.GetValue(players) as IEnumerable<PlayerInfo>;
            }
            if (all == null)
            {
                // Last resort: try "All" with capital A
                var allProp2 = players.GetType().GetProperty("All", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (allProp2 != null)
                    all = allProp2.GetValue(players) as IEnumerable<PlayerInfo>;
            }
            if (all != null)
                foreach (var p in all) yield return p;
        }

        public static PlayerInfo GetHumanPlayer()
        {
            var players = GetPlayers();
            if (players == null) return null;
            if (_humanProp == null) _humanProp = players.GetType().GetProperty("Human");
            return _humanProp?.GetValue(players) as PlayerInfo;
        }
    }

    // =========================================================================
    // Wanted Level Enum
    // =========================================================================
    public enum WantedLevel { None, Low, Medium, High }

    // =========================================================================
    // Crew Member Mod State
    // =========================================================================
    [Serializable]
    public class CrewModState
    {
        public float StreetCreditProgress;
        public int StreetCreditLevel;
        public WantedLevel WantedLevel = WantedLevel.None;
        public float WantedProgress;
        public bool MayorBribeActive;
        public bool JudgeBribeActive;
        public long BribeExpiresRaw;
        public float HappinessValue = 1f;
        public int TurnsUnhappy;
        public bool OnVacation;
        public long VacationReturnsRaw;
        public bool IsUnderboss;
        public bool AwaitingChildBirth;
        public int LastFutureKidsCount;
        public int LastBoozeSoldCount;
        public bool IsDefector;
        public long DefectorIdRaw;

        // Vacation pending system
        public bool VacationPending;
        public int VacationDuration = 1;

        // Fed arrival system
        public int FedArrivalCountdown;
        public bool FedsIncoming;

        // Jail/Trial system
        public bool InJail;
        public int DaysInJail;
        public int TrialDaysRemaining;
        public int LawyerRetainer; // Amount paid for lawyer
        public bool CaseDismissed;

        // Witness system
        public bool HasWitness;
        public bool WitnessThreatenedSuccessfully;
        public bool WitnessThreatAttempted; // Can only threaten once
        public int ExtraJailYears; // From failed witness threat

        // Booze selling cooldown
        public int LastBoozeSellTurn; // Turn number when last sold booze

        public SimTime BribeExpires
        {
            get => new SimTime((int)BribeExpiresRaw);
            set => BribeExpiresRaw = value.days;
        }
        public SimTime VacationReturns
        {
            get => new SimTime((int)VacationReturnsRaw);
            set => VacationReturnsRaw = value.days;
        }
    }

    // =========================================================================
    // Alliance Pact
    // =========================================================================
    [Serializable]
    public class AlliancePact
    {
        public string PactId;
        public string PactName;
        public int ColorIndex; // 0-3, maps to PACT_COLORS slot
        public int LeaderGangId;
        public List<int> MemberIds = new List<int>();
        public float ColorR, ColorG, ColorB;
        public int FormedDays;
        public bool PlayerInvited;
        public bool IsPending;

        public string DisplayName => string.IsNullOrEmpty(PactName) ? $"Pact {ColorIndex + 1}" : PactName;

        public Color SharedColor
        {
            get => new Color(ColorR, ColorG, ColorB);
            set { ColorR = value.r; ColorG = value.g; ColorB = value.b; }
        }
        public SimTime Formed
        {
            get => new SimTime(FormedDays);
            set => FormedDays = value.days;
        }
        public bool IsMember(PlayerID pid) => MemberIds.Contains(pid.id) || LeaderGangId == pid.id;
        public bool IsActive => !IsPending && MemberIds.Count > 0;
    }

    // =========================================================================
    // Mod Save Data
    // =========================================================================
    [Serializable]
    public class ModSaveData
    {
        public Dictionary<long, CrewModState> CrewStates = new Dictionary<long, CrewModState>();
        public List<AlliancePact> Pacts = new List<AlliancePact>();
        public int NextPactId;
        public int PlayerPactId = -1; // -1 means not in a player-created pact
        public int PlayerJoinedPactIndex = -1; // AI pact color index the player joined (-1 = none)
        public int LastPactJoinDay = -1; // Last day player asked to join an AI pact (legacy, kept for compat)
        public Dictionary<int, int> PactJoinCooldowns = new Dictionary<int, int>(); // Per-pact cooldown: slotIndex -> last attempt day
        public bool NeverAcceptPacts; // Toggle to never accept AI pact invitations
        public List<string> GrapevineEvents = new List<string>(); // Gang interaction log
    }

    // =========================================================================
    // Constants
    // =========================================================================
    internal static class ModConstants
    {
        public const float STREET_CREDIT_PER_FIGHT = 0.05f;
        public const float STREET_CREDIT_PER_KILL = 0.30f;
        public const float STREET_CREDIT_PER_25_BOOZE = 0.10f;
        public const int STREET_CREDIT_REWARD_COUNT = 7;
        public const float HAPPINESS_LOSS_PER_LEVEL = 0.10f;
        public const int VACATION_DAYS = 7; // One full turn (game simulates 7 days per turn)
        public const int GIFT_BASE_COST = 50;
        public const int TURNS_FOR_DEFECTION = 5;
        public const float UNHAPPY_THRESHOLD = 0.20f;
        public const int FAMILY_REHIRE_COST = 3500;
        public const int TOP_GANG_COUNT = 6;
        public const int TOP_GANG_CREW_CAP = 40;
        public const float INCOME_SHARE_PERCENT = 0.05f;
        public const int BASE_BRIBE_COST = 520; // Increased by 20
        public const int BRIBE_DURATION_DAYS = 7;
        public const int MAYOR_BRIBE_COST = 10000;
        public const int MAYOR_BRIBE_DURATION_DAYS = 180; // 6 months
        public const int KILLS_FOR_MAX_WANTED = 5;
        public const int MAX_PACTS = 7; // 6 AI + 1 player
        public const int PLAYER_PACT_SLOT = 6; // Index for player's own pact

        // Fed arrival countdown based on wanted level
        public const int FED_ARRIVAL_HIGH = 2;
        public const int FED_ARRIVAL_MEDIUM = 5;
        public const int FED_ARRIVAL_LOW = 10;

        public const int PLAYER_PACT_TERRITORY_COST = 24;
        public const int PLAYER_PACT_SC_COST = 2;
        public const int PACT_JOIN_COOLDOWN_DAYS = 180; // 6 months

        // Pact colors - 7 slots (6 AI + 1 player)
        public static readonly Color[] PACT_COLORS = new Color[]
        {
            new Color(0.9f, 0.2f, 0.2f),   // Red
            new Color(0.2f, 0.5f, 0.9f),   // Blue
            new Color(0.9f, 0.7f, 0.1f),   // Yellow/Gold
            new Color(0.9f, 0.4f, 0.1f),   // Orange
            new Color(0.6f, 0.3f, 0.8f),   // Purple
            new Color(0.2f, 0.7f, 0.7f),   // Teal
            new Color(0.4f, 0.8f, 0.4f),   // Green (Player)
        };

        public static readonly string[] PACT_COLOR_NAMES = { "Red", "Blue", "Gold", "Orange", "Purple", "Teal", "Your Pact" };

        public static readonly string DIRTY_CASH_LABEL = "dirty-cash";

        // Happiness mood tiers (threshold, label, color) - checked top-down
        public static readonly float[] MOOD_THRESHOLDS = { 0.75f, 0.50f, 0.25f, 0.00f };
        public static readonly string[] MOOD_LABELS = { "Happy", "Content", "Unhappy", "Miserable" };
        public static readonly Color[] MOOD_COLORS =
        {
            new Color(0.3f, 0.85f, 0.3f),   // Happy - Green
            new Color(0.7f, 0.8f, 0.3f),    // Content - Yellow-green
            new Color(0.9f, 0.6f, 0.2f),    // Unhappy - Orange
            new Color(0.85f, 0.2f, 0.2f),   // Miserable - Red
        };
    }

    [BepInPlugin("com.mods.gameplaytweaks", "Gameplay Tweaks", "1.0.0")]
    public class GameplayTweaksPlugin : BaseUnityPlugin
    {
        internal static ConfigEntry<bool> EnableSpouseEthnicity;
        internal static ConfigEntry<float> SpouseEthnicityChance;
        internal static ConfigEntry<bool> EnableHireableAge;
        internal static ConfigEntry<float> HireableMinAge;
        internal static ConfigEntry<int> MarriageMinAge;
        internal static ConfigEntry<int> MarriageMaxAgeDiff;
        internal static ConfigEntry<bool> EnableCrewStats;
        internal static ConfigEntry<bool> EnableAIAlliances;
        internal static ConfigEntry<bool> EnableDirtyCash;

        internal static bool ForceSameEthnicity;
        internal static System.Random SharedRng = new System.Random();
        internal static ModSaveData SaveData = new ModSaveData();
        private static string _saveFilePath;
        internal static GameplayTweaksPlugin Instance;

        // Gang tracker - refreshed each turn, used by pact browser
        internal static List<PlayerInfo> TrackedGangs = new List<PlayerInfo>();
        private static int _lastGangTrackDay = -1;

        // Cached pact state to avoid LINQ in hot paths
        internal static bool HasPlayerPact => SaveData.PlayerJoinedPactIndex >= 0 || _hasOwnPact;
        private static bool _hasOwnPact;
        internal static void RefreshPactCache()
        {
            _hasOwnPact = SaveData.Pacts.Any(p => p.ColorIndex == ModConstants.PLAYER_PACT_SLOT);
        }

        internal static void RefreshGangTracker()
        {
            TrackedGangs.Clear();
            try
            {
                foreach (var p in G.GetAllPlayers())
                {
                    if (p == null || p.PID.IsHumanPlayer) continue;
                    if (p.crew == null || p.crew.IsCrewDefeated) continue;
                    if (p.crew.LivingCrewCount <= 0) continue;
                    TrackedGangs.Add(p);
                }
                RefreshPactCache();
            }
            catch (Exception e) { Debug.LogError($"[GameplayTweaks] Gang tracker error: {e}"); }
        }

        // Helper: Read a Fixnum value safely (handles various internal representations)
        internal static int ReadFixnum(object qty)
        {
            if (qty == null) return 0;
            try { return Convert.ToInt32(qty); }
            catch
            {
                try
                {
                    var qtyField = qty.GetType().GetField("v")
                        ?? qty.GetType().GetField("_value")
                        ?? qty.GetType().GetField("raw");
                    if (qtyField != null) return Convert.ToInt32(qtyField.GetValue(qty));
                }
                catch { }
                return 0;
            }
        }

        // Helper: Read resource amount from entity inventory
        internal static int ReadInventoryAmount(Entity entity, string labelName)
        {
            if (entity == null) return 0;
            try
            {
                var inv = ModulesUtil.GetInventory(entity);
                if (inv?.data == null) return 0;
                var label = new Label(labelName);
                var rq = inv.data.Get(label);
                return ReadFixnum(rq.qty);
            }
            catch { return 0; }
        }

        // Helper: Get player's clean cash from the financial system (NOT inventory)
        internal static int GetPlayerCleanCash()
        {
            try
            {
                PlayerInfo human = G.GetHumanPlayer();
                if (human == null) return 0;
                var totalMoney = human.finances.GetMoneyTotal();
                return (int)totalMoney;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[GameplayTweaks] GetPlayerCleanCash failed: {e.Message}");
                return 0;
            }
        }

        // Helper: Add dirty cash to an entity's inventory
        internal static void AddDirtyCash(Entity entity, int amount)
        {
            if (entity == null || amount <= 0) return;
            try
            {
                var inv = ModulesUtil.GetInventory(entity);
                if (inv?.data == null) return;
                var label = new Label(ModConstants.DIRTY_CASH_LABEL);
                inv.data.Increment(label, amount);
            }
            catch (Exception e) { Debug.LogError($"[GameplayTweaks] AddDirtyCash failed: {e}"); }
        }

        // Helper: Remove dirty cash from entity's inventory (returns amount actually removed)
        internal static int RemoveDirtyCash(Entity entity, int amount)
        {
            if (entity == null || amount <= 0) return 0;
            try
            {
                var inv = ModulesUtil.GetInventory(entity);
                if (inv?.data == null) return 0;
                var label = new Label(ModConstants.DIRTY_CASH_LABEL);
                int available = ReadFixnum(inv.data.Get(label).qty);
                int toRemove = Math.Min(available, amount);
                if (toRemove > 0)
                    inv.TryRemoveResourcesUpToAll(label, toRemove);
                return toRemove;
            }
            catch { return 0; }
        }

        // Helper: Get total dirty cash across all player buildings
        internal static int GetTotalDirtyCash()
        {
            int total = 0;
            try
            {
                PlayerInfo human = G.GetHumanPlayer();
                if (human == null) return 0;
                var safehouse = human.territory.Safehouse;
                if (!safehouse.IsNotValid)
                {
                    var safeEntity = safehouse.FindEntity();
                    total += ReadInventoryAmount(safeEntity, ModConstants.DIRTY_CASH_LABEL);
                }
            }
            catch { }
            return total;
        }

        private void Awake()
        {
            Instance = this;

            EnableSpouseEthnicity = Config.Bind("SpouseEthnicity", "Enabled", true, "Spouses share ethnicity.");
            SpouseEthnicityChance = Config.Bind("SpouseEthnicity", "SameEthnicityChance", 0.80f, "Probability same ethnicity.");
            EnableHireableAge = Config.Bind("HireableAge", "Enabled", true, "Change min hire age.");
            HireableMinAge = Config.Bind("HireableAge", "MinAge", 16f, "Minimum age to hire.");
            MarriageMinAge = Config.Bind("Marriage", "MinAge", 18, "Minimum marriage age.");
            MarriageMaxAgeDiff = Config.Bind("Marriage", "MaxAgeDifference", 10, "Max age difference.");
            EnableCrewStats = Config.Bind("CrewStats", "Enabled", true, "Enable crew stat tracking.");
            EnableAIAlliances = Config.Bind("AIAlliances", "Enabled", true, "Enable AI alliances.");
            EnableDirtyCash = Config.Bind("DirtyCash", "Enabled", true, "Enable dirty cash economy.");

            var harmony = new Harmony("com.mods.gameplaytweaks");
            harmony.PatchAll(typeof(SpouseEthnicityLinkPatch));
            harmony.PatchAll(typeof(SpouseEthnicityCandidatePatch));
            FindRandoToMarryPatch.ApplyPatch(harmony);
            HireableAgePatch.ApplyManualDetour();
            CrewRelationshipHandlerPatch.ApplyPatch(harmony);
            StatTrackingPatch.ApplyPatch(harmony);
            TurnUpdatePatch.ApplyPatch(harmony);
            AICrewCapPatch.ApplyPatch(harmony);
            TerritoryColorPatch.ApplyPatch(harmony);
            BossArrestPatch.ApplyPatch(harmony);
            JailSystem.Initialize();
            SaveLoadPatch.ApplyPatch(harmony);
            AttackAdvisorPatch.ApplyPatch(harmony);
            DirtyCashPatches.ApplyPatches(harmony);
            FrontTrackingPatch.ApplyPatch(harmony);

            // Warn if old dirty cash DLLs still present
            CheckForConflictingMods();

            Logger.LogInfo("Gameplay Tweaks Extended loaded");
        }

        private void CheckForConflictingMods()
        {
            string pluginsDir = Path.Combine(Path.GetDirectoryName(Info.Location), "");
            try
            {
                pluginsDir = Path.GetDirectoryName(Info.Location);
            }
            catch { return; }
            string[] conflicting = { "DirtyCashEconomy.dll", "DirtyCashVolumeFix.dll", "Safebox.dll" };
            foreach (string dll in conflicting)
            {
                string path = Path.Combine(pluginsDir, dll);
                if (File.Exists(path))
                    Debug.LogWarning($"[GameplayTweaks] WARNING: '{dll}' detected in plugins folder. This mod now includes dirty cash features. Remove '{dll}' to avoid conflicts.");
            }
        }

        // =====================================================================
        // Mod State Helpers
        // =====================================================================
        internal static CrewModState GetOrCreateCrewState(EntityID crewId)
        {
            long key = crewId.IsNotValid ? -1 : (long)crewId.id;
            if (key < 0) return null;
            if (!SaveData.CrewStates.TryGetValue(key, out CrewModState state))
            {
                state = new CrewModState();
                SaveData.CrewStates[key] = state;
            }
            return state;
        }

        internal static CrewModState GetCrewStateOrNull(EntityID crewId)
        {
            long key = crewId.IsNotValid ? -1 : (long)crewId.id;
            if (key < 0) return null;
            SaveData.CrewStates.TryGetValue(key, out CrewModState state);
            return state;
        }

        internal static AlliancePact GetPactForPlayer(PlayerID pid) =>
            SaveData.Pacts.FirstOrDefault(p => p.IsMember(pid));

        internal static void LogGrapevine(string msg)
        {
            SaveData.GrapevineEvents.Insert(0, msg);
            if (SaveData.GrapevineEvents.Count > 50)
                SaveData.GrapevineEvents.RemoveRange(50, SaveData.GrapevineEvents.Count - 50);
        }

        internal static int GetBribeCost(WantedLevel level)
        {
            int baseCost = ModConstants.BASE_BRIBE_COST;
            return baseCost * (1 << (int)level); // Doubles per level
        }

        internal static void GrantStreetCredit(PlayerInfo player, int amount)
        {
            try
            {
                var safehouse = player.territory.Safehouse;
                if (safehouse.IsNotValid)
                {
                    Debug.LogWarning("[GameplayTweaks] No safehouse found for street credit grant");
                    return;
                }
                var safeEntity = safehouse.FindEntity();
                if (safeEntity == null)
                {
                    Debug.LogWarning("[GameplayTweaks] Safehouse entity not found");
                    return;
                }
                var inv = ModulesUtil.GetInventory(safeEntity);
                if (inv != null)
                {
                    var label = new Label("streetcredit");
                    int added = inv.ForceAddResourcesRegardlessOfSpace(label, amount);
                    Debug.Log($"[GameplayTweaks] Granted {added} streetcredit to safehouse (requested: {amount})");
                }
                else
                {
                    Debug.LogWarning("[GameplayTweaks] Safehouse inventory not found");
                }
            }
            catch (Exception e) { Debug.LogError($"[GameplayTweaks] GrantStreetCredit failed: {e}"); }
        }

        internal static void SaveModData()
        {
            try
            {
                if (string.IsNullOrEmpty(_saveFilePath)) return;
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("{\"NextPactId\":" + SaveData.NextPactId + ",\"PlayerPactId\":" + SaveData.PlayerPactId
                    + ",\"PJI\":" + SaveData.PlayerJoinedPactIndex + ",\"LPJD\":" + SaveData.LastPactJoinDay
                    + ",\"NAP\":" + SaveData.NeverAcceptPacts.ToString().ToLower()
                    + ",\"LOD\":" + CrewRelationshipHandlerPatch._lastOutingDay
                    + ",\"GMB\":" + CrewRelationshipHandlerPatch._globalMayorBribeActive.ToString().ToLower()
                    + ",\"GMBD\":" + CrewRelationshipHandlerPatch._globalMayorBribeExpireDay
                    + ",\"CrewStates\":{");
                bool first = true;
                foreach (var kvp in SaveData.CrewStates)
                {
                    if (!first) sb.Append(",");
                    first = false;
                    var s = kvp.Value;
                    sb.Append($"\"{kvp.Key}\":{{\"SCP\":{s.StreetCreditProgress},\"SCL\":{s.StreetCreditLevel},");
                    sb.Append($"\"WL\":{(int)s.WantedLevel},\"WP\":{s.WantedProgress},\"MBA\":{s.MayorBribeActive.ToString().ToLower()},");
                    sb.Append($"\"JBA\":{s.JudgeBribeActive.ToString().ToLower()},\"BER\":{s.BribeExpiresRaw},\"HV\":{s.HappinessValue},");
                    sb.Append($"\"TU\":{s.TurnsUnhappy},\"OV\":{s.OnVacation.ToString().ToLower()},\"VRR\":{s.VacationReturnsRaw},");
                    sb.Append($"\"IU\":{s.IsUnderboss.ToString().ToLower()},\"ACB\":{s.AwaitingChildBirth.ToString().ToLower()},");
                    sb.Append($"\"LFKC\":{s.LastFutureKidsCount},\"LBSC\":{s.LastBoozeSoldCount},");
                    sb.Append($"\"VP\":{s.VacationPending.ToString().ToLower()},\"VD\":{s.VacationDuration},");
                    sb.Append($"\"FAC\":{s.FedArrivalCountdown},\"FI\":{s.FedsIncoming.ToString().ToLower()},");
                    sb.Append($"\"HW\":{s.HasWitness.ToString().ToLower()},\"WTS\":{s.WitnessThreatenedSuccessfully.ToString().ToLower()},");
                    sb.Append($"\"WTA\":{s.WitnessThreatAttempted.ToString().ToLower()},\"EJY\":{s.ExtraJailYears},");
                    sb.Append($"\"LR\":{s.LawyerRetainer},\"CD\":{s.CaseDismissed.ToString().ToLower()},\"LBST\":{s.LastBoozeSellTurn}}}");
                }
                sb.Append("},\"Pacts\":[");
                bool firstPact = true;
                foreach (var pact in SaveData.Pacts)
                {
                    if (!firstPact) sb.Append(",");
                    firstPact = false;
                    sb.Append($"{{\"PI\":\"{pact.PactId}\",\"PN\":\"{EscapeJsonString(pact.PactName ?? "")}\",\"CI\":{pact.ColorIndex},");
                    sb.Append($"\"LG\":{pact.LeaderGangId},\"CR\":{pact.ColorR},\"CG\":{pact.ColorG},\"CB\":{pact.ColorB},");
                    sb.Append($"\"FD\":{pact.FormedDays},\"IP\":{pact.IsPending.ToString().ToLower()},\"PV\":{pact.PlayerInvited.ToString().ToLower()},");
                    sb.Append("\"MI\":[");
                    for (int mi = 0; mi < pact.MemberIds.Count; mi++)
                    {
                        if (mi > 0) sb.Append(",");
                        sb.Append(pact.MemberIds[mi]);
                    }
                    sb.Append("]}");
                }
                sb.Append("],\"GV\":[");
                for (int gi = 0; gi < SaveData.GrapevineEvents.Count; gi++)
                {
                    if (gi > 0) sb.Append(",");
                    sb.Append($"\"{EscapeJsonString(SaveData.GrapevineEvents[gi])}\"");
                }
                sb.AppendLine("]}");
                File.WriteAllText(_saveFilePath, sb.ToString());
            }
            catch (Exception e) { Debug.LogError($"[GameplayTweaks] Save failed: {e}"); }
        }

        private static string EscapeJsonString(string s)
        {
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        internal static void LoadModData(string saveName)
        {
            try
            {
                string saveDir = Path.Combine(Application.persistentDataPath, "Saves");
                _saveFilePath = Path.Combine(saveDir, $"{saveName}_tweaks.json");
                SaveData = new ModSaveData();

                if (!File.Exists(_saveFilePath))
                {
                    Debug.Log($"[GameplayTweaks] No save file found, starting fresh");
                    return;
                }

                string json = File.ReadAllText(_saveFilePath);
                SaveData.NextPactId = JInt(json, "NextPactId", 0);
                SaveData.PlayerPactId = JInt(json, "PlayerPactId", -1);
                SaveData.PlayerJoinedPactIndex = JInt(json, "PJI", -1);
                SaveData.LastPactJoinDay = JInt(json, "LPJD", -1);
                SaveData.NeverAcceptPacts = JBool(json, "NAP", false);
                CrewRelationshipHandlerPatch._lastOutingDay = JInt(json, "LOD", -1);
                CrewRelationshipHandlerPatch._globalMayorBribeActive = JBool(json, "GMB", false);
                CrewRelationshipHandlerPatch._globalMayorBribeExpireDay = JInt(json, "GMBD", -1);

                // Parse CrewStates
                int csIdx = json.IndexOf("\"CrewStates\":{");
                if (csIdx >= 0)
                {
                    int braceOpen = json.IndexOf('{', csIdx + 13);
                    int braceClose = MatchBrace(json, braceOpen);
                    if (braceClose > braceOpen)
                    {
                        string csBlock = json.Substring(braceOpen + 1, braceClose - braceOpen - 1);
                        int pos = 0;
                        while (pos < csBlock.Length)
                        {
                            int ks = csBlock.IndexOf('"', pos);
                            if (ks < 0) break;
                            int ke = csBlock.IndexOf('"', ks + 1);
                            if (ke < 0) break;
                            string crewKey = csBlock.Substring(ks + 1, ke - ks - 1);
                            int os = csBlock.IndexOf('{', ke);
                            if (os < 0) break;
                            int oe = MatchBrace(csBlock, os);
                            if (oe < 0) break;
                            string obj = csBlock.Substring(os, oe - os + 1);

                            var st = new CrewModState();
                            st.StreetCreditProgress = JFloat(obj, "SCP", 0f);
                            st.StreetCreditLevel = JInt(obj, "SCL", 0);
                            st.WantedLevel = (WantedLevel)JInt(obj, "WL", 0);
                            st.WantedProgress = JFloat(obj, "WP", 0f);
                            st.MayorBribeActive = JBool(obj, "MBA", false);
                            st.JudgeBribeActive = JBool(obj, "JBA", false);
                            st.BribeExpiresRaw = JInt(obj, "BER", 0);
                            st.HappinessValue = JFloat(obj, "HV", 1f);
                            st.TurnsUnhappy = JInt(obj, "TU", 0);
                            st.OnVacation = JBool(obj, "OV", false);
                            st.VacationReturnsRaw = JInt(obj, "VRR", 0);
                            st.IsUnderboss = JBool(obj, "IU", false);
                            st.AwaitingChildBirth = JBool(obj, "ACB", false);
                            st.LastFutureKidsCount = JInt(obj, "LFKC", 0);
                            st.LastBoozeSoldCount = JInt(obj, "LBSC", 0);
                            st.VacationPending = JBool(obj, "VP", false);
                            st.VacationDuration = JInt(obj, "VD", 0);
                            st.FedArrivalCountdown = JInt(obj, "FAC", 0);
                            st.FedsIncoming = JBool(obj, "FI", false);
                            st.HasWitness = JBool(obj, "HW", false);
                            st.WitnessThreatenedSuccessfully = JBool(obj, "WTS", false);
                            st.WitnessThreatAttempted = JBool(obj, "WTA", false);
                            st.ExtraJailYears = JInt(obj, "EJY", 0);
                            st.LawyerRetainer = JInt(obj, "LR", 0);
                            st.CaseDismissed = JBool(obj, "CD", false);
                            st.LastBoozeSellTurn = JInt(obj, "LBST", 0);

                            if (long.TryParse(crewKey, out long ck))
                                SaveData.CrewStates[ck] = st;
                            pos = oe + 1;
                        }
                    }
                }

                // Parse Pacts
                int pIdx = json.IndexOf("\"Pacts\":[");
                if (pIdx >= 0)
                {
                    int brackOpen = json.IndexOf('[', pIdx);
                    int brackClose = MatchBracket(json, brackOpen);
                    if (brackClose > brackOpen)
                    {
                        string pBlock = json.Substring(brackOpen + 1, brackClose - brackOpen - 1);
                        int pos = 0;
                        while (pos < pBlock.Length)
                        {
                            int os = pBlock.IndexOf('{', pos);
                            if (os < 0) break;
                            int oe = MatchBrace(pBlock, os);
                            if (oe < 0) break;
                            string obj = pBlock.Substring(os, oe - os + 1);

                            var pact = new AlliancePact();
                            pact.PactId = JStr(obj, "PI", "");
                            pact.PactName = JStr(obj, "PN", "");
                            pact.ColorIndex = JInt(obj, "CI", 0);
                            pact.LeaderGangId = JInt(obj, "LG", -1);
                            pact.ColorR = JFloat(obj, "CR", 1f);
                            pact.ColorG = JFloat(obj, "CG", 1f);
                            pact.ColorB = JFloat(obj, "CB", 1f);
                            pact.FormedDays = JInt(obj, "FD", 0);
                            pact.IsPending = JBool(obj, "IP", false);
                            pact.PlayerInvited = JBool(obj, "PV", false);

                            int miIdx = obj.IndexOf("\"MI\":[");
                            if (miIdx >= 0)
                            {
                                int arrS = obj.IndexOf('[', miIdx);
                                int arrE = obj.IndexOf(']', arrS);
                                if (arrE > arrS + 1)
                                {
                                    foreach (var id in obj.Substring(arrS + 1, arrE - arrS - 1).Split(','))
                                        if (int.TryParse(id.Trim(), out int mid))
                                            pact.MemberIds.Add(mid);
                                }
                            }

                            SaveData.Pacts.Add(pact);
                            pos = oe + 1;
                        }
                    }
                }

                // Parse Grapevine events
                int gvIdx = json.IndexOf("\"GV\":[");
                if (gvIdx >= 0)
                {
                    int gvOpen = json.IndexOf('[', gvIdx);
                    int gvClose = MatchBracket(json, gvOpen);
                    if (gvClose > gvOpen + 1)
                    {
                        string gvBlock = json.Substring(gvOpen + 1, gvClose - gvOpen - 1);
                        // Parse comma-separated quoted strings
                        int gpos = 0;
                        while (gpos < gvBlock.Length)
                        {
                            int qs = gvBlock.IndexOf('"', gpos);
                            if (qs < 0) break;
                            int qe = gvBlock.IndexOf('"', qs + 1);
                            if (qe < 0) break;
                            SaveData.GrapevineEvents.Add(gvBlock.Substring(qs + 1, qe - qs - 1));
                            gpos = qe + 1;
                        }
                    }
                }

                RefreshPactCache();
                Debug.Log($"[GameplayTweaks] Loaded {SaveData.CrewStates.Count} crew states, {SaveData.Pacts.Count} pacts, {SaveData.GrapevineEvents.Count} grapevine events");
            }
            catch (Exception e)
            {
                Debug.LogError($"[GameplayTweaks] Load failed: {e}");
                SaveData = new ModSaveData();
            }
        }

        // Minimal JSON parse helpers
        private static int MatchBrace(string s, int open)
        {
            int d = 0;
            for (int i = open; i < s.Length; i++)
            {
                if (s[i] == '{') d++; else if (s[i] == '}') { d--; if (d == 0) return i; }
            }
            return -1;
        }
        private static int MatchBracket(string s, int open)
        {
            int d = 0;
            for (int i = open; i < s.Length; i++)
            {
                if (s[i] == '[') d++; else if (s[i] == ']') { d--; if (d == 0) return i; }
            }
            return -1;
        }
        private static string JRaw(string json, string key)
        {
            string search = $"\"{key}\":";
            int idx = json.IndexOf(search);
            if (idx < 0) return null;
            int start = idx + search.Length;
            int end = start;
            while (end < json.Length && json[end] != ',' && json[end] != '}' && json[end] != ']')
                end++;
            return json.Substring(start, end - start).Trim();
        }
        private static int JInt(string j, string k, int d)
        {
            string r = JRaw(j, k); return r != null && int.TryParse(r, out int v) ? v : d;
        }
        private static float JFloat(string j, string k, float d)
        {
            string r = JRaw(j, k); return r != null && float.TryParse(r, out float v) ? v : d;
        }
        private static bool JBool(string j, string k, bool d)
        {
            string r = JRaw(j, k); return r != null ? r == "true" : d;
        }
        private static string JStr(string j, string k, string d)
        {
            string search = $"\"{k}\":\"";
            int idx = j.IndexOf(search);
            if (idx < 0) return d;
            int start = idx + search.Length;
            int end = j.IndexOf('"', start);
            return end > start ? j.Substring(start, end - start) : d;
        }

        internal static int CalculateGangPower(PlayerInfo player)
        {
            if (player == null) return 0;
            return (player.crew?.LivingCrewCount ?? 0) * 10 + 25;
        }

        // =====================================================================
        // 1. Spouse Ethnicity
        // =====================================================================
        [HarmonyPatch(typeof(PeopleTracker), "FindMatchAndLinkCouple")]
        private static class SpouseEthnicityLinkPatch
        {
            [HarmonyPrefix]
            static void Prefix()
            {
                if (!EnableSpouseEthnicity.Value) { ForceSameEthnicity = false; return; }
                ForceSameEthnicity = SharedRng.NextDouble() < SpouseEthnicityChance.Value;
            }
        }

        [HarmonyPatch(typeof(PeopleTracker), "ScoreCandidate")]
        private static class SpouseEthnicityCandidatePatch
        {
            [HarmonyPrefix]
            static bool Prefix(Entity current, Entity other, ref float __result)
            {
                if (!EnableSpouseEthnicity.Value) return true;
                PersonData p1 = current.data.person, p2 = other.data.person;
                float ageDiff = Math.Abs(p1.born.Subtract(p2.born).YearsFloat);
                float ageScore = Mathf.Clamp(10f - ageDiff, 0f, 10f);
                if (ForceSameEthnicity)
                    __result = (p1.eth == p2.eth) ? 3f * ageScore : 0f;
                else
                    __result = ((p1.eth == p2.eth) ? 3f : 1f) * ageScore;
                return false;
            }
        }

        // =====================================================================
        // 2. Marriage Age Fix
        // =====================================================================
        private static class FindRandoToMarryPatch
        {
            public static void ApplyPatch(Harmony harmony)
            {
                try
                {
                    var original = AccessTools.Method(typeof(PeopleTracker), "FindRandoToMarry");
                    if (original == null) return;
                    harmony.Patch(original, prefix: new HarmonyMethod(typeof(FindRandoToMarryPatch), nameof(Prefix)));
                }
                catch (Exception e) { Debug.LogError($"[GameplayTweaks] FindRandoToMarryPatch failed: {e}"); }
            }

            static bool Prefix(PeopleTracker __instance, Entity peep, ref Entity __result)
            {
                try
                {
                    RelationshipTracker rels = G.GetRels();
                    PersonData pdata = peep.data.person;
                    Gender goalGender = (pdata.g == Gender.F) ? Gender.M : Gender.F;
                    SimTime now = G.GetNow();
                    int minAge = MarriageMinAge.Value;
                    int maxAgeDiff = MarriageMaxAgeDiff.Value;
                    var candidates = new List<(Entity entity, float score)>();

                    foreach (Entity other in __instance.GetAllTrackedPeople())
                    {
                        PersonData od = other.data.person;
                        if (od.g != goalGender || !od.IsAlive) continue;
                        float otherAge = od.GetAge(now).YearsFloat;
                        if (otherAge < minAge) continue;
                        float ageDiff = Math.Abs(od.born.YearsFloat - pdata.born.YearsFloat);
                        if (ageDiff > maxAgeDiff) continue;
                        if (od.business.IsValid) continue;
                        var otherRels = rels?.GetListOrNull(other.Id);
                        if (otherRels != null && otherRels.HasSpouse()) continue;
                        var peepRels = rels?.GetListOrNull(peep.Id);
                        if (peepRels != null && peepRels.HasAny(other.Id)) continue;
                        candidates.Add((other, maxAgeDiff - ageDiff + 1f));
                    }

                    if (candidates.Count == 0) { __result = null; return false; }
                    float totalScore = candidates.Sum(c => c.score);
                    float roll = (float)SharedRng.NextDouble() * totalScore;
                    float cumulative = 0f;
                    foreach (var (entity, score) in candidates)
                    {
                        cumulative += score;
                        if (roll <= cumulative) { __result = entity; return false; }
                    }
                    __result = candidates[0].entity;
                    return false;
                }
                catch { return true; }
            }
        }

        // =====================================================================
        // 3. Hireable Age
        // =====================================================================
        private static class HireableAgePatch
        {
            private delegate bool IsEligibleDelegate(SimTime now, Entity person);
            private static IsEligibleDelegate _originalTrampoline;
            private static Detour _detour;

            public static void ApplyManualDetour()
            {
                try
                {
                    var original = AccessTools.Method(typeof(PlayerSocial), "IsEligibleCrewMember");
                    var replacement = AccessTools.Method(typeof(HireableAgePatch), nameof(Replacement));
                    _detour = new Detour(original, replacement);
                    _originalTrampoline = _detour.GenerateTrampoline<IsEligibleDelegate>();
                }
                catch (Exception e) { Debug.LogError($"[GameplayTweaks] HireableAgePatch failed: {e}"); }
            }

            static bool Replacement(SimTime now, Entity person)
            {
                if (!EnableHireableAge.Value && _originalTrampoline != null)
                    return _originalTrampoline(now, person);
                try
                {
                    PersonData pdata = person.data.person;
                    if (pdata.IsAlive && pdata.GetAge(now).YearsFloat >= HireableMinAge.Value
                        && pdata.business.IsNotValid && pdata.resassigned.IsNotValid
                        && G.GetPoliticianData(person.Id) == null)
                        return person.data.agent.pid.id == 0;
                    return false;
                }
                catch { return false; }
            }
        }

        // Shared ScreenSpaceOverlay canvas for all mod popups (avoids world-space rendering bug)
        private static Canvas _modOverlayCanvas;
        internal static Canvas GetOrCreateOverlayCanvas()
        {
            if (_modOverlayCanvas != null && _modOverlayCanvas.gameObject != null)
                return _modOverlayCanvas;
            var go = new GameObject("ModOverlayCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            UnityEngine.Object.DontDestroyOnLoad(go);
            _modOverlayCanvas = go.GetComponent<Canvas>();
            _modOverlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _modOverlayCanvas.sortingOrder = 990;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            return _modOverlayCanvas;
        }

        // =====================================================================
        // 4. Crew Relationship Handler - New Tab/Popup
        // =====================================================================
        private static class CrewRelationshipHandlerPatch
        {
            private static FieldInfo _selectedField;
            private static FieldInfo _goField;
            private static FieldInfo _entryCrewField;
            private static FieldInfo _entryBuildingField;

            // Main popup
            private static GameObject _handlerPopup;
            private static bool _popupVisible;
            private static Entity _selectedPeep;

            // UI Elements
            private static Text _titleText;
            private static Image _streetCreditFill, _wantedFill, _happinessFill;
            private static Text _streetCreditLevelText, _wantedLevelText, _happinessMoodText;
            private static Text _dirtyCashText;
            private static Button _btnUnderboss, _btnMarryChild, _btnHireFamily, _btnVacation, _btnGift;
            private static Button _btnBribeMayor, _btnBribeJudge;
            private static Text _txtUnderboss, _txtMarryChild, _txtHireFamily, _txtVacation, _txtGift;
            private static Text _txtBribeMayor, _txtBribeJudge;

            // Lawyer UI (jail system)
            private static Text _txtLawyerStatus, _txtPayLawyer;
            private static Button _btnPayLawyer;
            private static GameObject _lawyerSection, _lawyerRow;

            // Threaten Witness UI
            private static Button _btnThreatenWitness;
            private static Text _txtThreatenWitness;

            // Sell Booze UI (jail feature)
            private static Button _btnSellBooze;
            private static Text _txtSellBooze;
            private static GameObject _boozeSellRow;

            // Rename / Age / Nickname UI
            private static GameObject _renameRow, _nicknameRow;
            private static InputField _inputFirstName, _inputLastName, _inputNickname;
            private static Button _btnRename, _btnAge, _btnNickname;
            private static Text _txtRename, _txtAge, _txtNickname;

            // Pep Talk
            private static Button _btnPepTalk;
            private static Text _txtPepTalk;
            private static int _pepTalkUsesThisTurn = 0;
            private static int _pepTalkLastTurnDay = -1;

            // Booze Sell Popup
            private static GameObject _boozeSellPopup;
            private static bool _boozeSellVisible;
            private static Transform _boozeSellContent;
            private static Text _boozeSellStatusText;

            // Known booze labels and base sell prices
            private static readonly Dictionary<string, int> _boozePrices = new Dictionary<string, int>
            {
                {"beer-bottled", 5}, {"wine-bottled", 8}, {"whiskey-bottled", 12},
                {"brandy-bottled", 12}, {"vodka-bottled", 10}, {"rum-bottled", 10},
                {"beer-keg", 20}, {"whiskey-barrel", 40}, {"brandy-barrel", 40},
                {"vodka-barrel", 35}, {"rum-barrel", 35}, {"champagne", 20},
                {"scotch-whiskey", 18}, {"bourbon", 15}, {"canadian-whiskey", 14},
                {"gin", 10}, {"english-gin", 15}, {"bathtub-gin", 4},
                {"moonshine", 3}, {"home-brew", 3}, {"fake-beer", 2},
                {"fake-wine", 3}, {"cider", 4}, {"absinthe", 18},
                {"ethnic-alcohol", 6}
            };

            // Boss-only Gang Pacts button reference
            private static GameObject _gangPactsBtnGo;

            // Boss-only Safebox button + popup
            private static GameObject _safeboxBtnGo;
            private static GameObject _safeboxPopup;
            private static bool _safeboxVisible;
            private static Text _safeboxCleanText, _safeboxDirtyText, _safeboxTotalText;
            private static Transform _safeboxContent;

            // Grapevine news popup
            private static GameObject _grapevineBtnGo;
            private static GameObject _grapevinePopup;
            private static bool _grapevineVisible;
            private static Transform _grapevineContent;

            // Gang Pacts Popup (boss-only, separate from crew relations)
            private static GameObject _gangPactsPopup;
            private static bool _gangPactsVisible;
            private static Transform _gangPactsContent;
            private static ScrollRect _gangPactsScroll;
            private static Text _gangPactsStatusText;

            // Pact Editor Section
            private static int _selectedPactSlot = -1;
            private static bool _pactEditorMode = false; // false = main (%), true = editor (free add)
            private static Text _pactTabLabel;
            private static GameObject[] _pactSlotRows = new GameObject[7]; // 7th = player slot
            private static Text[] _pactSlotNameTexts = new Text[7];
            private static Text[] _pactSlotMemberTexts = new Text[7];
            private static Image[] _pactSlotColorSwatches = new Image[7];
            private static Button[] _pactSlotSelectBtns = new Button[7];
            private static Button[] _pactSlotDeleteBtns = new Button[7];
            private static Text _selectedPactLabel;

            // Gang Rename inline
            private static GameObject _gangRenameRow;
            private static InputField _gangRenameInput;
            private static PlayerInfo _gangRenameTarget;
            private static Text _gangRenameNameText;

            // My Pact popup
            private static GameObject _myPactPopup;
            private static bool _myPactVisible;
            private static Transform _myPactContent;
            private static GameObject _myPactBtnGo;

            // Crew Outing Event Popup
            internal static GameObject _outingPopup;
            internal static Text _outingText;
            internal static int _lastOutingDay = -1;
            internal static int _outingIntervalDays = 56; // Every 8 weeks

            // Global Mayor Bribe (applies to all crew)
            internal static bool _globalMayorBribeActive;
            internal static int _globalMayorBribeExpireDay = -1;

            // Family Hire Popup
            private static GameObject _familyHirePopup;
            private static bool _familyHireVisible;
            private static Transform _familyHireContent;
            private static List<Entity> _filteredRelatives = new List<Entity>();
            private static string _filterEthnicity = "All";
            private static string _filterTrait = "All";
            private static int _filterMinAge = 0;
            private static int _filterMaxAge = 100;
            private static Text _familyHireStatusText;
            private static ScrollRect _familyHireScroll;

            // Navigation
            private static Button _btnPrevCrew, _btnNextCrew;
            private static int _currentCrewIndex = 0;
            private static List<CrewAssignment> _crewList = new List<CrewAssignment>();

            private static object _popupInstance;

            public static void ApplyPatch(Harmony harmony)
            {
                try
                {
                    var asm = typeof(GameClock).Assembly;
                    var popupType = asm.GetType("Game.UI.Session.Crew.CrewManagementPopup");
                    if (popupType == null) return;

                    _selectedField = popupType.GetField("_selected", BindingFlags.NonPublic | BindingFlags.Instance);
                    _goField = popupType.BaseType.GetField("_go", BindingFlags.NonPublic | BindingFlags.Instance);

                    var entryType = popupType.GetNestedType("Entry", BindingFlags.NonPublic);
                    if (entryType != null)
                    {
                        _entryCrewField = entryType.GetField("crew");
                        _entryBuildingField = entryType.GetField("building");
                    }

                    var refreshInfoPanel = popupType.GetMethod("RefreshInfoPanel", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (refreshInfoPanel != null)
                    {
                        harmony.Patch(refreshInfoPanel, postfix: new HarmonyMethod(typeof(CrewRelationshipHandlerPatch), nameof(RefreshInfoPanelPostfix)));
                        Debug.Log("[GameplayTweaks] Crew Relationship Handler initialized");
                    }
                }
                catch (Exception e) { Debug.LogError($"[GameplayTweaks] CrewRelationshipHandlerPatch failed: {e}"); }
            }

            private static Entity GetSelectedPeep()
            {
                if (_popupInstance == null || _selectedField == null || _entryCrewField == null) return null;
                var entry = _selectedField.GetValue(_popupInstance);
                if (entry == null) return null;
                var crewObj = _entryCrewField.GetValue(entry);
                if (crewObj == null) return null;
                return ((CrewAssignment)crewObj).GetPeep();
            }

            static void RefreshInfoPanelPostfix(object __instance)
            {
                try
                {
                    _popupInstance = __instance;
                    _selectedPeep = GetSelectedPeep();
                    EnsureHandlerButtonCreated();
                    if (_popupVisible) RefreshHandlerUI();

                    // Show Gang Pacts / My Pact buttons only when boss is selected
                    if (_gangPactsBtnGo != null)
                    {
                        bool isBoss = false;
                        if (_selectedPeep != null)
                        {
                            PlayerCrew crew = G.GetHumanCrew();
                            CrewAssignment bossAssign = crew?.GetCrewForIndex(0) ?? default;
                            Entity boss = bossAssign.IsValid ? bossAssign.GetPeep() : null;
                            isBoss = boss != null && boss.Id == _selectedPeep.Id;
                        }
                        _gangPactsBtnGo.SetActive(isBoss);
                        if (_myPactBtnGo != null)
                        {
                            bool hasPlayerPact = HasPlayerPact;
                            _myPactBtnGo.SetActive(isBoss && hasPlayerPact);
                        }
                        if (_grapevineBtnGo != null)
                            _grapevineBtnGo.SetActive(isBoss);
                        if (_safeboxBtnGo != null)
                            _safeboxBtnGo.SetActive(isBoss);
                    }
                }
                catch (Exception e) { Debug.LogError($"[GameplayTweaks] RefreshInfoPanelPostfix: {e}"); }
            }

            private static void EnsureHandlerButtonCreated()
            {
                if (_handlerPopup != null) return;

                var go = _goField?.GetValue(_popupInstance) as GameObject;
                if (go == null) return;

                Transform infoContent = go.transform.Find("Panel/Info/Viewport/Content")
                                     ?? go.transform.Find("Info/Viewport/Content");
                if (infoContent == null) return;

                // Create "Crew Relations" button at the bottom
                var openBtnGo = new GameObject("BtnCrewRelations", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
                openBtnGo.transform.SetParent(infoContent, false);
                openBtnGo.transform.SetAsLastSibling();

                var le = openBtnGo.GetComponent<LayoutElement>();
                le.minHeight = 35; le.preferredHeight = 35;

                var img = openBtnGo.GetComponent<Image>();
                img.color = new Color(0.4f, 0.25f, 0.15f, 0.95f);

                var btn = openBtnGo.GetComponent<Button>();
                btn.targetGraphic = img;
                btn.onClick.AddListener(ToggleHandlerPopup);

                var textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
                textGo.transform.SetParent(openBtnGo.transform, false);
                var textRt = textGo.GetComponent<RectTransform>();
                textRt.anchorMin = Vector2.zero; textRt.anchorMax = Vector2.one;
                textRt.offsetMin = Vector2.zero; textRt.offsetMax = Vector2.zero;

                var text = textGo.GetComponent<Text>();
                text.text = "Crew Relations";
                text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                text.fontSize = 14; text.color = Color.white;
                text.alignment = TextAnchor.MiddleCenter;
                text.fontStyle = FontStyle.Bold;

                // "Gang Pacts" button underneath Crew Relations (boss-only, hidden by default)
                var pactBtnGo = new GameObject("BtnGangPacts", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
                _gangPactsBtnGo = pactBtnGo;
                pactBtnGo.transform.SetParent(infoContent, false);
                pactBtnGo.transform.SetAsLastSibling();

                var pactLe = pactBtnGo.GetComponent<LayoutElement>();
                pactLe.minHeight = 35; pactLe.preferredHeight = 35;

                var pactImg = pactBtnGo.GetComponent<Image>();
                pactImg.color = new Color(0.35f, 0.2f, 0.15f, 0.95f);

                var pactBtn = pactBtnGo.GetComponent<Button>();
                pactBtn.targetGraphic = pactImg;
                pactBtn.onClick.AddListener(ToggleGangPactsPopup);

                var pactTextGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
                pactTextGo.transform.SetParent(pactBtnGo.transform, false);
                var pactTextRt = pactTextGo.GetComponent<RectTransform>();
                pactTextRt.anchorMin = Vector2.zero; pactTextRt.anchorMax = Vector2.one;
                pactTextRt.offsetMin = Vector2.zero; pactTextRt.offsetMax = Vector2.zero;
                var pactText = pactTextGo.GetComponent<Text>();
                pactText.text = "Gang Pacts (Boss)";
                pactText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                pactText.fontSize = 14; pactText.color = Color.white;
                pactText.alignment = TextAnchor.MiddleCenter;
                pactText.fontStyle = FontStyle.Bold;

                // "My Pact" button - visible when player has a pact (slot 4)
                var myPactBtnGo = new GameObject("BtnMyPact", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
                _myPactBtnGo = myPactBtnGo;
                myPactBtnGo.transform.SetParent(infoContent, false);
                myPactBtnGo.transform.SetAsLastSibling();
                var myPactLe = myPactBtnGo.GetComponent<LayoutElement>();
                myPactLe.minHeight = 35; myPactLe.preferredHeight = 35;
                var myPactImg = myPactBtnGo.GetComponent<Image>();
                myPactImg.color = new Color(0.15f, 0.35f, 0.15f, 0.95f);
                var myPactBtnComp = myPactBtnGo.GetComponent<Button>();
                myPactBtnComp.targetGraphic = myPactImg;
                myPactBtnComp.onClick.AddListener(ToggleMyPactPopup);
                var myPactTextGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
                myPactTextGo.transform.SetParent(myPactBtnGo.transform, false);
                var myPactTextRt = myPactTextGo.GetComponent<RectTransform>();
                myPactTextRt.anchorMin = Vector2.zero; myPactTextRt.anchorMax = Vector2.one;
                myPactTextRt.offsetMin = Vector2.zero; myPactTextRt.offsetMax = Vector2.zero;
                var myPactText = myPactTextGo.GetComponent<Text>();
                myPactText.text = "My Pact";
                myPactText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                myPactText.fontSize = 14; myPactText.color = new Color(0.6f, 1f, 0.6f);
                myPactText.alignment = TextAnchor.MiddleCenter;
                myPactText.fontStyle = FontStyle.Bold;
                // Only visible if player has a pact (slot 4)
                bool hasPlayerPact = HasPlayerPact;
                myPactBtnGo.SetActive(hasPlayerPact);

                // "The Grapevine" button (boss-only)
                var gvBtnGo = new GameObject("BtnGrapevine", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
                _grapevineBtnGo = gvBtnGo;
                gvBtnGo.transform.SetParent(infoContent, false);
                gvBtnGo.transform.SetAsLastSibling();
                var gvLe = gvBtnGo.GetComponent<LayoutElement>();
                gvLe.minHeight = 35; gvLe.preferredHeight = 35;
                var gvImg = gvBtnGo.GetComponent<Image>();
                gvImg.color = new Color(0.25f, 0.2f, 0.3f, 0.95f);
                var gvBtnComp = gvBtnGo.GetComponent<Button>();
                gvBtnComp.targetGraphic = gvImg;
                gvBtnComp.onClick.AddListener(ToggleGrapevinePopup);
                var gvTextGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
                gvTextGo.transform.SetParent(gvBtnGo.transform, false);
                var gvTextRt = gvTextGo.GetComponent<RectTransform>();
                gvTextRt.anchorMin = Vector2.zero; gvTextRt.anchorMax = Vector2.one;
                gvTextRt.offsetMin = Vector2.zero; gvTextRt.offsetMax = Vector2.zero;
                var gvText = gvTextGo.GetComponent<Text>();
                gvText.text = "The Grapevine";
                gvText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                gvText.fontSize = 14; gvText.color = new Color(0.85f, 0.75f, 0.95f);
                gvText.alignment = TextAnchor.MiddleCenter;
                gvText.fontStyle = FontStyle.Bold;
                gvBtnGo.SetActive(false); // Hidden until boss check

                // "Safebox" button (boss-only, dirty cash feature)
                if (EnableDirtyCash.Value)
                {
                    var sbBtnGo = new GameObject("BtnSafebox", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
                    _safeboxBtnGo = sbBtnGo;
                    sbBtnGo.transform.SetParent(infoContent, false);
                    sbBtnGo.transform.SetAsLastSibling();
                    var sbLe = sbBtnGo.GetComponent<LayoutElement>();
                    sbLe.minHeight = 35; sbLe.preferredHeight = 35;
                    var sbImg = sbBtnGo.GetComponent<Image>();
                    sbImg.color = new Color(0.2f, 0.3f, 0.15f, 0.95f);
                    var sbBtnComp = sbBtnGo.GetComponent<Button>();
                    sbBtnComp.targetGraphic = sbImg;
                    sbBtnComp.onClick.AddListener(ToggleSafeboxPopup);
                    var sbTextGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
                    sbTextGo.transform.SetParent(sbBtnGo.transform, false);
                    var sbTextRt = sbTextGo.GetComponent<RectTransform>();
                    sbTextRt.anchorMin = Vector2.zero; sbTextRt.anchorMax = Vector2.one;
                    sbTextRt.offsetMin = Vector2.zero; sbTextRt.offsetMax = Vector2.zero;
                    var sbText = sbTextGo.GetComponent<Text>();
                    sbText.text = "Safebox (Boss)";
                    sbText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                    sbText.fontSize = 14; sbText.color = new Color(0.7f, 0.95f, 0.7f);
                    sbText.alignment = TextAnchor.MiddleCenter;
                    sbText.fontStyle = FontStyle.Bold;
                    sbBtnGo.SetActive(false); // Hidden until boss check
                }

                CreateHandlerPopup(go);
            }

            private static void CreateHandlerPopup(GameObject parent)
            {
                _handlerPopup = new GameObject("CrewRelationsPopup", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
                _handlerPopup.transform.SetParent(parent.transform, false);

                var rt = _handlerPopup.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(380, 520);

                var bgImg = _handlerPopup.GetComponent<Image>();
                bgImg.color = new Color(0.12f, 0.1f, 0.08f, 0.98f);

                var vlg = _handlerPopup.GetComponent<VerticalLayoutGroup>();
                vlg.padding = new RectOffset(10, 10, 10, 10);
                vlg.spacing = 6;
                vlg.childForceExpandWidth = true;
                vlg.childForceExpandHeight = false;

                // Header row with navigation and close - compact, sized to person name
                var headerRow = CreateHorizontalRow(_handlerPopup.transform, "HeaderRow");
                var headerLE = headerRow.GetComponent<LayoutElement>();
                headerLE.minHeight = 20; headerLE.preferredHeight = 20;
                _btnPrevCrew = CreateButton(headerRow.transform, "PrevCrew", "<", OnPrevCrew);
                var prevLE = _btnPrevCrew.GetComponent<LayoutElement>();
                prevLE.preferredWidth = 20; prevLE.minHeight = 18; prevLE.preferredHeight = 18;
                _titleText = CreateLabel(headerRow.transform, "Title", "Crew Relations", 11, FontStyle.Bold);
                _titleText.alignment = TextAnchor.MiddleCenter;
                _titleText.GetComponent<LayoutElement>().minHeight = 16; _titleText.GetComponent<LayoutElement>().preferredHeight = 16;
                _btnNextCrew = CreateButton(headerRow.transform, "NextCrew", ">", OnNextCrew);
                var nextLE = _btnNextCrew.GetComponent<LayoutElement>();
                nextLE.preferredWidth = 20; nextLE.minHeight = 18; nextLE.preferredHeight = 18;
                var closeBtn = CreateButton(headerRow.transform, "Close", "X", ClosePopup);
                var closeLE = closeBtn.GetComponent<LayoutElement>();
                closeLE.preferredWidth = 18; closeLE.minHeight = 18; closeLE.preferredHeight = 18;

                // --- SCROLLABLE CONTENT AREA ---
                var hScrollArea = new GameObject("HandlerScroll", typeof(RectTransform), typeof(ScrollRect), typeof(LayoutElement));
                hScrollArea.transform.SetParent(_handlerPopup.transform, false);
                hScrollArea.GetComponent<LayoutElement>().flexibleHeight = 1;

                var hViewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
                hViewport.transform.SetParent(hScrollArea.transform, false);
                var hVpRt = hViewport.GetComponent<RectTransform>();
                hVpRt.anchorMin = Vector2.zero; hVpRt.anchorMax = Vector2.one;
                hVpRt.offsetMin = Vector2.zero; hVpRt.offsetMax = Vector2.zero;
                hViewport.GetComponent<Image>().color = new Color(1, 1, 1, 0.01f);
                hViewport.GetComponent<Mask>().showMaskGraphic = false;

                var hContent = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
                hContent.transform.SetParent(hViewport.transform, false);
                var hCRt = hContent.GetComponent<RectTransform>();
                hCRt.anchorMin = new Vector2(0, 1); hCRt.anchorMax = new Vector2(1, 1);
                hCRt.pivot = new Vector2(0.5f, 1); hCRt.sizeDelta = new Vector2(0, 0);
                hContent.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                var hCVlg = hContent.GetComponent<VerticalLayoutGroup>();
                hCVlg.spacing = 6; hCVlg.childForceExpandWidth = true; hCVlg.childForceExpandHeight = false;
                hCVlg.padding = new RectOffset(2, 2, 2, 2);

                var hScroll = hScrollArea.GetComponent<ScrollRect>();
                hScroll.viewport = hVpRt; hScroll.content = hCRt;
                hScroll.horizontal = false; hScroll.vertical = true;
                hScroll.scrollSensitivity = 30f;
                hScroll.movementType = ScrollRect.MovementType.Clamped;

                var contentRoot = hContent.transform;

                // Dirty Cash Display (safehouse)
                if (EnableDirtyCash.Value)
                {
                    _dirtyCashText = CreateLabel(contentRoot, "DirtyCash", "Dirty Cash: $0", 11, FontStyle.Bold);
                    _dirtyCashText.color = new Color(0.85f, 0.6f, 0.2f);
                    _dirtyCashText.GetComponent<LayoutElement>().minHeight = 18;
                }

                // Street Credit Section
                CreateLabel(contentRoot, "SCLabel", "Street Credit", 12, FontStyle.Bold);
                CreateStatBar(contentRoot, "StreetCreditBar", new Color(0.85f, 0.7f, 0.2f), out _streetCreditFill);
                _streetCreditLevelText = CreateLabel(contentRoot, "SCLevel", "Level: 0", 11, FontStyle.Normal);

                // Wanted Section
                CreateLabel(contentRoot, "WLabel", "Wanted Level", 12, FontStyle.Bold);
                CreateStatBar(contentRoot, "WantedBar", new Color(0.8f, 0.2f, 0.2f), out _wantedFill);
                _wantedLevelText = CreateLabel(contentRoot, "WLevel", "Status: None", 11, FontStyle.Normal);

                // Bribe buttons row
                var bribeRow = CreateHorizontalRow(contentRoot, "BribeRow");
                _btnBribeMayor = CreateButton(bribeRow.transform, "BribeMayor", "Bribe Mayor", OnBribeMayor);
                _btnBribeJudge = CreateButton(bribeRow.transform, "BribeJudge", "Bribe Judge", OnBribeJudge);
                _txtBribeMayor = _btnBribeMayor.GetComponentInChildren<Text>();
                _txtBribeJudge = _btnBribeJudge.GetComponentInChildren<Text>();

                // Threaten Witness button
                var witnessRow = CreateHorizontalRow(contentRoot, "WitnessRow");
                _btnThreatenWitness = CreateButton(witnessRow.transform, "ThreatenWitness", "Threaten Witness", OnThreatenWitness);
                _txtThreatenWitness = _btnThreatenWitness.GetComponentInChildren<Text>();

                // Lawyer/Legal Section (only shows when in jail)
                _lawyerSection = CreateLabel(contentRoot, "LawyerStatus", "", 11, FontStyle.Normal).gameObject;
                _txtLawyerStatus = _lawyerSection.GetComponent<Text>();
                _lawyerRow = CreateHorizontalRow(contentRoot, "LawyerRow");
                _btnPayLawyer = CreateButton(_lawyerRow.transform, "PayLawyer", "Hire Lawyer ($1000)", OnPayLawyer);
                _txtPayLawyer = _btnPayLawyer.GetComponentInChildren<Text>();
                _lawyerSection.SetActive(false);
                _lawyerRow.SetActive(false);

                // Sell Booze button (only visible when in jail)
                _boozeSellRow = CreateHorizontalRow(contentRoot, "BoozeSellRow");
                _btnSellBooze = CreateButton(_boozeSellRow.transform, "SellBooze", "Sell Booze (3x)", OnOpenBoozeSell);
                _txtSellBooze = _btnSellBooze.GetComponentInChildren<Text>();
                _boozeSellRow.SetActive(false);

                // Happiness Section
                CreateLabel(contentRoot, "HLabel", "Happiness", 12, FontStyle.Bold);
                CreateStatBar(contentRoot, "HappinessBar", new Color(0.2f, 0.75f, 0.3f), out _happinessFill);
                _happinessMoodText = CreateLabel(contentRoot, "HMoodText", "Content", 11, FontStyle.Normal);
                _happinessMoodText.alignment = TextAnchor.MiddleCenter;

                // Happiness buttons row
                var happyRow = CreateHorizontalRow(contentRoot, "HappyRow");
                _btnVacation = CreateButton(happyRow.transform, "Vacation", "Vacation", OnVacation);
                _btnGift = CreateButton(happyRow.transform, "Gift", "Gift", OnGift);
                _btnPepTalk = CreateButton(happyRow.transform, "PepTalk", "Pep Talk", OnPepTalk);
                _txtVacation = _btnVacation.GetComponentInChildren<Text>();
                _txtGift = _btnGift.GetComponentInChildren<Text>();
                _txtPepTalk = _btnPepTalk.GetComponentInChildren<Text>();

                // Crew Actions Section
                CreateLabel(contentRoot, "ActionsLabel", "Crew Actions", 12, FontStyle.Bold);
                var actionsRow = CreateHorizontalRow(contentRoot, "ActionsRow");
                _btnUnderboss = CreateButton(actionsRow.transform, "Underboss", "Underboss", OnUnderboss);
                _txtUnderboss = _btnUnderboss.GetComponentInChildren<Text>();
                _btnMarryChild = CreateButton(actionsRow.transform, "MarryChild", "Spouse", OnMarryChild);
                _txtMarryChild = _btnMarryChild.GetComponentInChildren<Text>();
                _btnHireFamily = CreateButton(actionsRow.transform, "HireFamily", "Hire Family", OnHireFamily);
                _txtHireFamily = _btnHireFamily.GetComponentInChildren<Text>();

                // Rename + Age + Nickname row
                var renameAgeRow = CreateHorizontalRow(contentRoot, "RenameAgeRow");
                _btnRename = CreateButton(renameAgeRow.transform, "Rename", "Rename", OnToggleRename);
                _txtRename = _btnRename.GetComponentInChildren<Text>();
                _btnAge = CreateButton(renameAgeRow.transform, "AgeUp", "Age +1yr", OnAgeUp);
                _txtAge = _btnAge.GetComponentInChildren<Text>();
                _btnNickname = CreateButton(renameAgeRow.transform, "Nickname", "Nickname", OnToggleNickname);
                _txtNickname = _btnNickname.GetComponentInChildren<Text>();

                // Rename input row (hidden by default)
                _renameRow = new GameObject("RenameRow", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
                _renameRow.transform.SetParent(contentRoot, false);
                _renameRow.GetComponent<LayoutElement>().minHeight = 28;
                _renameRow.GetComponent<LayoutElement>().preferredHeight = 28;
                var rnHlg = _renameRow.GetComponent<HorizontalLayoutGroup>();
                rnHlg.spacing = 4; rnHlg.childForceExpandWidth = true; rnHlg.childForceExpandHeight = true;

                // First name input
                var firstGo = new GameObject("FirstInput", typeof(RectTransform), typeof(Image), typeof(InputField), typeof(LayoutElement));
                firstGo.transform.SetParent(_renameRow.transform, false);
                firstGo.GetComponent<Image>().color = new Color(0.2f, 0.2f, 0.2f, 0.95f);
                firstGo.GetComponent<LayoutElement>().flexibleWidth = 1;
                var firstTxtGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
                firstTxtGo.transform.SetParent(firstGo.transform, false);
                var firstTxtRt = firstTxtGo.GetComponent<RectTransform>();
                firstTxtRt.anchorMin = Vector2.zero; firstTxtRt.anchorMax = Vector2.one;
                firstTxtRt.offsetMin = new Vector2(4, 0); firstTxtRt.offsetMax = new Vector2(-4, 0);
                var firstTxt = firstTxtGo.GetComponent<Text>();
                firstTxt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                firstTxt.fontSize = 11; firstTxt.color = Color.white;
                firstTxt.alignment = TextAnchor.MiddleLeft;
                firstTxt.supportRichText = false;
                _inputFirstName = firstGo.GetComponent<InputField>();
                _inputFirstName.textComponent = firstTxt;
                _inputFirstName.characterLimit = 20;

                // Last name input
                var lastGo = new GameObject("LastInput", typeof(RectTransform), typeof(Image), typeof(InputField), typeof(LayoutElement));
                lastGo.transform.SetParent(_renameRow.transform, false);
                lastGo.GetComponent<Image>().color = new Color(0.2f, 0.2f, 0.2f, 0.95f);
                lastGo.GetComponent<LayoutElement>().flexibleWidth = 1;
                var lastTxtGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
                lastTxtGo.transform.SetParent(lastGo.transform, false);
                var lastTxtRt = lastTxtGo.GetComponent<RectTransform>();
                lastTxtRt.anchorMin = Vector2.zero; lastTxtRt.anchorMax = Vector2.one;
                lastTxtRt.offsetMin = new Vector2(4, 0); lastTxtRt.offsetMax = new Vector2(-4, 0);
                var lastTxt = lastTxtGo.GetComponent<Text>();
                lastTxt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                lastTxt.fontSize = 11; lastTxt.color = Color.white;
                lastTxt.alignment = TextAnchor.MiddleLeft;
                lastTxt.supportRichText = false;
                _inputLastName = lastGo.GetComponent<InputField>();
                _inputLastName.textComponent = lastTxt;
                _inputLastName.characterLimit = 20;

                // Save button
                var saveBtn = CreateButton(_renameRow.transform, "SaveName", "Save", OnSaveRename);
                saveBtn.GetComponent<Image>().color = new Color(0.2f, 0.4f, 0.2f, 0.95f);
                saveBtn.GetComponent<LayoutElement>().preferredWidth = 50;
                _renameRow.SetActive(false);

                // Nickname input row (hidden by default)
                _nicknameRow = new GameObject("NicknameRow", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
                _nicknameRow.transform.SetParent(contentRoot, false);
                _nicknameRow.GetComponent<LayoutElement>().minHeight = 28;
                _nicknameRow.GetComponent<LayoutElement>().preferredHeight = 28;
                var nnHlg = _nicknameRow.GetComponent<HorizontalLayoutGroup>();
                nnHlg.spacing = 4;
                nnHlg.childForceExpandWidth = true;
                nnHlg.childForceExpandHeight = true;

                var nnGo = new GameObject("NicknameInput", typeof(RectTransform), typeof(Image), typeof(InputField), typeof(LayoutElement));
                nnGo.transform.SetParent(_nicknameRow.transform, false);
                nnGo.GetComponent<Image>().color = new Color(0.12f, 0.12f, 0.12f, 0.95f);
                nnGo.GetComponent<LayoutElement>().flexibleWidth = 1;
                var nnTxtGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
                nnTxtGo.transform.SetParent(nnGo.transform, false);
                var nnTxt = nnTxtGo.GetComponent<Text>();
                nnTxt.font = Font.CreateDynamicFontFromOSFont("Arial", 11);
                nnTxt.fontSize = 11;
                nnTxt.color = Color.white;
                nnTxt.alignment = TextAnchor.MiddleLeft;
                nnTxt.supportRichText = false;
                var nnTxtRt = nnTxtGo.GetComponent<RectTransform>();
                nnTxtRt.anchorMin = Vector2.zero;
                nnTxtRt.anchorMax = Vector2.one;
                nnTxtRt.offsetMin = new Vector2(4, 0);
                nnTxtRt.offsetMax = new Vector2(-4, 0);
                _inputNickname = nnGo.GetComponent<InputField>();
                _inputNickname.textComponent = nnTxt;
                _inputNickname.characterLimit = 20;

                var nnSaveBtn = CreateButton(_nicknameRow.transform, "SaveNickname", "Save", OnSaveNickname);
                nnSaveBtn.GetComponent<Image>().color = new Color(0.2f, 0.4f, 0.2f, 0.95f);
                nnSaveBtn.GetComponent<LayoutElement>().preferredWidth = 50;
                var nnClearBtn = CreateButton(_nicknameRow.transform, "ClearNickname", "Clear", OnClearNickname);
                nnClearBtn.GetComponent<Image>().color = new Color(0.4f, 0.2f, 0.2f, 0.95f);
                nnClearBtn.GetComponent<LayoutElement>().preferredWidth = 50;
                _nicknameRow.SetActive(false);

                // Add drag handler to header
                AddDragHandler(_handlerPopup, rt);

                _handlerPopup.SetActive(false);

                // Create the Family Hire popup (hidden by default)
                CreateFamilyHirePopup(parent);
            }

            private static void CreateFamilyHirePopup(GameObject parent)
            {
                _familyHirePopup = new GameObject("FamilyHirePopup", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
                _familyHirePopup.transform.SetParent(parent.transform, false);

                var rt = _familyHirePopup.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(420, 480);
                rt.anchoredPosition = new Vector2(200, 0); // Offset from crew relations popup

                var bgImg = _familyHirePopup.GetComponent<Image>();
                bgImg.color = new Color(0.1f, 0.12f, 0.08f, 0.98f);

                var vlg = _familyHirePopup.GetComponent<VerticalLayoutGroup>();
                vlg.padding = new RectOffset(8, 8, 8, 8);
                vlg.spacing = 4;
                vlg.childForceExpandWidth = true;
                vlg.childForceExpandHeight = false;

                // Header
                var headerRow = CreateHorizontalRow(_familyHirePopup.transform, "FH_Header");
                headerRow.GetComponent<LayoutElement>().minHeight = 30;
                CreateLabel(headerRow.transform, "FH_Title", "Hire Family Member", 14, FontStyle.Bold);
                var closeBtn = CreateButton(headerRow.transform, "FH_Close", "X", CloseFamilyHirePopup);
                closeBtn.GetComponent<LayoutElement>().preferredWidth = 30;

                // Filter row 1: Ethnicity + Trait
                var filterRow1 = CreateHorizontalRow(_familyHirePopup.transform, "FH_FilterRow1");
                filterRow1.GetComponent<LayoutElement>().minHeight = 28;
                var btnEthFilter = CreateButton(filterRow1.transform, "FH_EthFilter", "Ethnicity: All", CycleEthnicityFilter);
                btnEthFilter.GetComponentInChildren<Text>().fontSize = 10;
                var btnTraitFilter = CreateButton(filterRow1.transform, "FH_TraitFilter", "Trait: All", CycleTraitFilter);
                btnTraitFilter.GetComponentInChildren<Text>().fontSize = 10;

                // Filter row 2: Age range
                var filterRow2 = CreateHorizontalRow(_familyHirePopup.transform, "FH_FilterRow2");
                filterRow2.GetComponent<LayoutElement>().minHeight = 28;
                var btnAgeDown = CreateButton(filterRow2.transform, "FH_AgeDown", "Min Age -", () => { _filterMinAge = Math.Max(0, _filterMinAge - 5); RefreshFamilyHireList(); });
                btnAgeDown.GetComponentInChildren<Text>().fontSize = 10;
                var btnAgeUp = CreateButton(filterRow2.transform, "FH_AgeUp", "Min Age +", () => { _filterMinAge = Math.Min(90, _filterMinAge + 5); RefreshFamilyHireList(); });
                btnAgeUp.GetComponentInChildren<Text>().fontSize = 10;
                var btnMaxDown = CreateButton(filterRow2.transform, "FH_MaxDown", "Max Age -", () => { _filterMaxAge = Math.Max(10, _filterMaxAge - 5); RefreshFamilyHireList(); });
                btnMaxDown.GetComponentInChildren<Text>().fontSize = 10;
                var btnMaxUp = CreateButton(filterRow2.transform, "FH_MaxUp", "Max Age +", () => { _filterMaxAge = Math.Min(100, _filterMaxAge + 5); RefreshFamilyHireList(); });
                btnMaxUp.GetComponentInChildren<Text>().fontSize = 10;

                // Status text showing current filters
                _familyHireStatusText = CreateLabel(_familyHirePopup.transform, "FH_Status", "Showing all relatives", 10, FontStyle.Italic);

                // Scrollable list area
                var scrollArea = new GameObject("FH_ScrollArea", typeof(RectTransform), typeof(Image), typeof(ScrollRect), typeof(LayoutElement));
                scrollArea.transform.SetParent(_familyHirePopup.transform, false);
                var scrollLE = scrollArea.GetComponent<LayoutElement>();
                scrollLE.minHeight = 300; scrollLE.flexibleHeight = 1;
                scrollArea.GetComponent<Image>().color = new Color(0.08f, 0.08f, 0.06f, 0.9f);

                // Viewport
                var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
                viewport.transform.SetParent(scrollArea.transform, false);
                var viewportRt = viewport.GetComponent<RectTransform>();
                viewportRt.anchorMin = Vector2.zero; viewportRt.anchorMax = Vector2.one;
                viewportRt.offsetMin = new Vector2(2, 2); viewportRt.offsetMax = new Vector2(-2, -2);
                viewport.GetComponent<Image>().color = new Color(1, 1, 1, 0.01f); // Mask needs an image
                viewport.GetComponent<Mask>().showMaskGraphic = false;

                // Content container
                var content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
                content.transform.SetParent(viewport.transform, false);
                var contentRt = content.GetComponent<RectTransform>();
                contentRt.anchorMin = new Vector2(0, 1); contentRt.anchorMax = new Vector2(1, 1);
                contentRt.pivot = new Vector2(0.5f, 1);
                contentRt.sizeDelta = new Vector2(0, 0);
                var csf = content.GetComponent<ContentSizeFitter>();
                csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                var contentVlg = content.GetComponent<VerticalLayoutGroup>();
                contentVlg.spacing = 2;
                contentVlg.childForceExpandWidth = true;
                contentVlg.childForceExpandHeight = false;
                contentVlg.padding = new RectOffset(4, 4, 4, 4);

                _familyHireContent = content.transform;

                // Setup ScrollRect
                _familyHireScroll = scrollArea.GetComponent<ScrollRect>();
                _familyHireScroll.viewport = viewportRt;
                _familyHireScroll.content = contentRt;
                _familyHireScroll.horizontal = false;
                _familyHireScroll.vertical = true;
                _familyHireScroll.scrollSensitivity = 30f; // Good mouse scroll speed
                _familyHireScroll.movementType = ScrollRect.MovementType.Clamped;

                // No drag handler on family hire popup - it conflicts with scroll
                _familyHirePopup.SetActive(false);
            }

            private static void ClosePopup()
            {
                _popupVisible = false;
                _handlerPopup.SetActive(false);
                if (_boozeSellPopup != null) { _boozeSellVisible = false; _boozeSellPopup.SetActive(false); }
            }

            private static void OnPrevCrew()
            {
                RefreshCrewList();
                if (_crewList.Count == 0) return;
                _currentCrewIndex = (_currentCrewIndex - 1 + _crewList.Count) % _crewList.Count;
                _selectedPeep = _crewList[_currentCrewIndex].GetPeep();
                RefreshHandlerUI();
            }

            private static void OnNextCrew()
            {
                RefreshCrewList();
                if (_crewList.Count == 0) return;
                _currentCrewIndex = (_currentCrewIndex + 1) % _crewList.Count;
                _selectedPeep = _crewList[_currentCrewIndex].GetPeep();
                RefreshHandlerUI();
            }

            private static void RefreshCrewList()
            {
                _crewList.Clear();
                var crew = G.GetHumanCrew();
                if (crew == null) return;
                foreach (var ca in crew.GetLiving())
                    _crewList.Add(ca);

                // Find current index
                if (_selectedPeep != null)
                {
                    for (int i = 0; i < _crewList.Count; i++)
                    {
                        if (_crewList[i].GetPeep()?.Id == _selectedPeep.Id)
                        {
                            _currentCrewIndex = i;
                            break;
                        }
                    }
                }
            }

            private static void AddDragHandler(GameObject popup, RectTransform rt)
            {
                // Simple drag by tracking mouse position changes
                var dragHandler = popup.AddComponent<PopupDragHandler>();
                dragHandler.rectTransform = rt;
            }

            private static void ToggleHandlerPopup()
            {
                _popupVisible = !_popupVisible;
                _handlerPopup.SetActive(_popupVisible);
                if (_popupVisible) RefreshHandlerUI();
            }

            private static void RefreshHandlerUI()
            {
                if (_selectedPeep == null) return;

                var state = GetOrCreateCrewState(_selectedPeep.Id);
                if (state == null) return;

                // Check jail status from game system
                string jailStatus = JailSystem.GetJailStatusString(_selectedPeep.Id);
                if (!string.IsNullOrEmpty(jailStatus))
                {
                    _titleText.text = $"{_selectedPeep.data.person.ShortName}\n<size=10>{jailStatus}</size>";
                }
                else
                {
                    _titleText.text = $"Relations: {_selectedPeep.data.person.ShortName}";
                }

                // Dirty Cash display
                if (_dirtyCashText != null && EnableDirtyCash.Value)
                {
                    int dirtyCash = GetTotalDirtyCash();
                    int cleanCash = GetPlayerCleanCash();
                    _dirtyCashText.text = $"Clean: ${cleanCash} | Dirty: ${dirtyCash}";
                }

                // Street Credit
                _streetCreditFill.fillAmount = state.StreetCreditProgress;
                _streetCreditLevelText.text = $"Level: {state.StreetCreditLevel}";

                // Wanted - show jail info if in jail
                _wantedFill.fillAmount = state.WantedProgress;
                string wantedStatus = $"Status: {state.WantedLevel}";
                bool isInJail = JailSystem.IsInJail(_selectedPeep.Id);
                if (isInJail)
                {
                    var (daysToTrial, trialBribeCost, isPaidOff) = JailSystem.GetArrestInfo(_selectedPeep.Id);
                    if (daysToTrial > 0)
                        wantedStatus = isPaidOff ? $"IN JAIL - Trial: {daysToTrial}d (Bribed)" : $"IN JAIL - Trial: {daysToTrial}d";
                    else
                    {
                        var (daysRemaining, years) = JailSystem.GetImprisonmentInfo(_selectedPeep.Id);
                        if (daysRemaining > 0)
                            wantedStatus = $"PRISON - {daysRemaining} days left";
                    }
                }
                else if (state.FedsIncoming)
                {
                    wantedStatus += $" (Feds in {state.FedArrivalCountdown} days!)";
                }
                _wantedLevelText.text = wantedStatus;
                // Mayor bribe is global (all crew benefit)
                _txtBribeMayor.text = _globalMayorBribeActive ? "Mayor (Active - All Crew)" : $"Mayor (${ModConstants.MAYOR_BRIBE_COST})";
                int judgeBribeCost = GetBribeCost(state.WantedLevel) * 2;
                _txtBribeJudge.text = state.JudgeBribeActive ? "Judge (Active)" : $"Judge (${judgeBribeCost})";
                _btnBribeMayor.interactable = !_globalMayorBribeActive;
                _btnBribeJudge.interactable = !state.JudgeBribeActive && state.WantedLevel != WantedLevel.None;

                // Threaten Witness button - can only threaten once
                if (_btnThreatenWitness != null)
                {
                    bool hasWitness = state.HasWitness || state.WitnessThreatAttempted;
                    _btnThreatenWitness.gameObject.SetActive(hasWitness);
                    _btnThreatenWitness.interactable = state.HasWitness && !state.WitnessThreatAttempted;
                    if (state.WitnessThreatenedSuccessfully)
                        _txtThreatenWitness.text = "Witness Silenced";
                    else if (state.WitnessThreatAttempted)
                        _txtThreatenWitness.text = "Witness Scared Off";
                    else if (state.HasWitness)
                        _txtThreatenWitness.text = "Threaten Witness";
                    else
                        _txtThreatenWitness.text = "No Witness";
                }

                // Lawyer Section - only visible when in jail
                if (_lawyerSection != null && _lawyerRow != null)
                {
                    _lawyerSection.SetActive(isInJail);
                    _lawyerRow.SetActive(isInJail);
                    if (isInJail)
                    {
                        string lawyerInfo = state.LawyerRetainer > 0
                            ? $"Lawyer Retainer: ${state.LawyerRetainer}"
                            : "No lawyer hired";
                        _txtLawyerStatus.text = lawyerInfo;
                        _txtPayLawyer.text = $"Add Retainer ($1000)";
                    }
                }

                // Sell Booze - only visible when in jail
                if (_boozeSellRow != null)
                {
                    _boozeSellRow.SetActive(isInJail);
                    if (isInJail)
                    {
                        int currentDay = (int)G.GetNow().days;
                        bool canSell = state.LastBoozeSellTurn == 0 || (currentDay - state.LastBoozeSellTurn) >= 28;
                        _btnSellBooze.interactable = canSell;
                        if (!canSell)
                        {
                            int daysLeft = 28 - (currentDay - state.LastBoozeSellTurn);
                            int turnsLeft = Math.Max(1, (int)Math.Ceiling(daysLeft / 7.0));
                            _txtSellBooze.text = $"Sell Booze ({turnsLeft}w cd)";
                        }
                        else
                        {
                            _txtSellBooze.text = "Sell Booze (3x)";
                        }
                    }
                }

                // Happiness
                _happinessFill.fillAmount = state.HappinessValue;
                // Update mood text and bar color based on happiness tier
                if (_happinessMoodText != null)
                {
                    int moodIdx = ModConstants.MOOD_THRESHOLDS.Length - 1;
                    for (int i = 0; i < ModConstants.MOOD_THRESHOLDS.Length; i++)
                    {
                        if (state.HappinessValue >= ModConstants.MOOD_THRESHOLDS[i]) { moodIdx = i; break; }
                    }
                    _happinessMoodText.text = ModConstants.MOOD_LABELS[moodIdx];
                    _happinessMoodText.color = ModConstants.MOOD_COLORS[moodIdx];
                    _happinessFill.color = ModConstants.MOOD_COLORS[moodIdx];
                }
                if (state.OnVacation)
                    _txtVacation.text = "On Vacation";
                else if (state.VacationPending)
                    _txtVacation.text = "Vacation Pending";
                else
                    _txtVacation.text = "Vacation ($1500)";
                _btnVacation.interactable = !state.OnVacation && !state.VacationPending;
                int giftCost = ModConstants.GIFT_BASE_COST;
                _txtGift.text = $"Gift (${giftCost})";

                // Underboss - no captain requirement
                PlayerCrew humanCrew = G.GetHumanCrew();
                CrewAssignment bossAssignment = humanCrew?.GetCrewForIndex(0) ?? default;
                Entity boss = bossAssignment.IsValid ? bossAssignment.GetPeep() : null;
                bool isBoss = boss != null && boss.Id == _selectedPeep.Id;
                _btnUnderboss.gameObject.SetActive(!isBoss);
                _txtUnderboss.text = state.IsUnderboss ? "Is Underboss" : "Promote to Underboss";
                _btnUnderboss.interactable = !state.IsUnderboss;

                // Marry/Child
                RelationshipTracker rels = G.GetRels();
                RelationshipList relList = rels?.GetListOrNull(_selectedPeep.Id);
                bool hasSpouse = relList != null && relList.HasSpouse();
                if (hasSpouse)
                {
                    _txtMarryChild.text = state.AwaitingChildBirth ? "Awaiting Birth..." : "Have Child";
                    _btnMarryChild.interactable = !state.AwaitingChildBirth;
                }
                else
                {
                    _txtMarryChild.text = "Find Spouse";
                    _btnMarryChild.interactable = true;
                }

                // Rename / Age buttons - always available
                if (_btnRename != null)
                {
                    _txtRename.text = (_renameRow != null && _renameRow.activeSelf) ? "Cancel" : "Rename";
                }
                if (_btnAge != null)
                {
                    float curAge = _selectedPeep.data.person.GetAge(G.GetNow()).YearsFloat;
                    _txtAge.text = $"Age +1yr ({(int)curAge})";
                }

                // Nickname button
                if (_btnNickname != null)
                {
                    string nick = _selectedPeep.data.person.nickname;
                    _txtNickname.text = string.IsNullOrEmpty(nick) ? "Nickname" : $"Nick: {nick}";
                }

                // Pep Talk button - 3 uses per turn
                if (_btnPepTalk != null)
                {
                    int currentDay = (int)G.GetNow().days;
                    if (currentDay != _pepTalkLastTurnDay)
                    {
                        _pepTalkUsesThisTurn = 0;
                        _pepTalkLastTurnDay = currentDay;
                    }
                    int remaining = 3 - _pepTalkUsesThisTurn;
                    _txtPepTalk.text = $"Pep Talk ({remaining})";
                    _btnPepTalk.interactable = remaining > 0;
                }

            }

            // Button Handlers
            private static void OnUnderboss()
            {
                if (_selectedPeep == null) return;
                var state = GetOrCreateCrewState(_selectedPeep.Id);
                if (state == null || state.IsUnderboss) return;

                try
                {
                    var xp = _selectedPeep.data.agent.xp;
                    if (xp == null) { Debug.LogWarning("[GameplayTweaks] xp is null"); return; }

                    var xpType = xp.GetType();
                    var roleLabel = new Label("underboss");

                    // Method 1: SetCrewRole (public or nonpublic)
                    var setRoleMethod = xpType.GetMethod("SetCrewRole",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (setRoleMethod != null)
                    {
                        setRoleMethod.Invoke(xp, new object[] { roleLabel });
                        state.IsUnderboss = true;
                        Debug.Log($"[GameplayTweaks] {_selectedPeep.data.person.FullName} promoted to Underboss via SetCrewRole");
                        RefreshHandlerUI();
                        return;
                    }

                    // Method 2: Property setter for CrewRole
                    var prop = xpType.GetProperty("CrewRole",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (prop != null && prop.CanWrite)
                    {
                        prop.SetValue(xp, roleLabel, null);
                        state.IsUnderboss = true;
                        Debug.Log($"[GameplayTweaks] {_selectedPeep.data.person.FullName} promoted to Underboss via CrewRole property");
                        RefreshHandlerUI();
                        return;
                    }

                    // Method 3: Try common field names
                    foreach (string fname in new[] { "_crewRole", "crewRole", "m_crewRole", "crewrole", "_role" })
                    {
                        var field = xpType.GetField(fname,
                            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        if (field != null)
                        {
                            field.SetValue(xp, roleLabel);
                            state.IsUnderboss = true;
                            Debug.Log($"[GameplayTweaks] {_selectedPeep.data.person.FullName} promoted to Underboss via field '{fname}'");
                            RefreshHandlerUI();
                            return;
                        }
                    }

                    // Method 4: Try alternate method names
                    foreach (string mname in new[] { "AssignCrewRole", "SetRole", "AssignRole", "PromoteToRole" })
                    {
                        var m = xpType.GetMethod(mname,
                            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        if (m != null)
                        {
                            m.Invoke(xp, new object[] { roleLabel });
                            state.IsUnderboss = true;
                            Debug.Log($"[GameplayTweaks] {_selectedPeep.data.person.FullName} promoted to Underboss via {mname}");
                            RefreshHandlerUI();
                            return;
                        }
                    }

                    // Debug: log all role-related members on the xp type
                    Debug.LogWarning($"[GameplayTweaks] Could not find role setter on type {xpType.FullName}. Dumping role-related members:");
                    foreach (var m in xpType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                    {
                        if (m.Name.IndexOf("role", StringComparison.OrdinalIgnoreCase) >= 0
                            || m.Name.IndexOf("Role", StringComparison.Ordinal) >= 0)
                            Debug.Log($"  Method: {m.Name}({string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name))}) -> {m.ReturnType.Name}");
                    }
                    foreach (var f in xpType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                    {
                        if (f.Name.IndexOf("role", StringComparison.OrdinalIgnoreCase) >= 0)
                            Debug.Log($"  Field: {f.FieldType.Name} {f.Name}");
                    }
                    foreach (var p in xpType.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                    {
                        if (p.Name.IndexOf("role", StringComparison.OrdinalIgnoreCase) >= 0)
                            Debug.Log($"  Property: {p.PropertyType.Name} {p.Name} (get={p.CanRead}, set={p.CanWrite})");
                    }

                    // Fallback: just set mod state so it at least tracks in our system
                    state.IsUnderboss = true;
                    Debug.LogWarning("[GameplayTweaks] Set IsUnderboss in mod state only (game role not set)");
                }
                catch (Exception e) { Debug.LogError($"[GameplayTweaks] OnUnderboss failed: {e}"); }

                RefreshHandlerUI();
            }

            private static void OnMarryChild()
            {
                if (_selectedPeep == null) return;
                RelationshipTracker rels = G.GetRels();
                RelationshipList relList = rels?.GetListOrNull(_selectedPeep.Id);
                bool hasSpouse = relList != null && relList.HasSpouse();

                if (hasSpouse)
                {
                    Entity spouse = relList.GetSpouse();
                    Entity mother = (_selectedPeep.data.person.g == Gender.F) ? _selectedPeep : spouse;
                    SimTime now = G.GetNow();
                    mother.data.person.futurekids.Add(now.IncrementDays(-1));
                    mother.data.person.futurekids.Sort(SimTime.CompareDescending);

                    var state = GetOrCreateCrewState(_selectedPeep.Id);
                    if (state != null)
                    {
                        state.AwaitingChildBirth = true;
                        state.LastFutureKidsCount = mother.data.person.futurekids.Count;
                    }
                }
                else
                {
                    PeopleTracker pt = G.GetPeopleGen();
                    Entity match = pt?.FindRandoToMarry(_selectedPeep);
                    if (match != null) pt.ForceMarry(_selectedPeep, match);
                }
                RefreshHandlerUI();
            }

            private static void OnThreatenWitness()
            {
                if (_selectedPeep == null) return;
                var state = GetOrCreateCrewState(_selectedPeep.Id);
                if (state == null || !state.HasWitness || state.WitnessThreatAttempted) return;

                // Mark as attempted - can only try once
                state.WitnessThreatAttempted = true;

                // Check if player's gang is one of the top 4 largest
                bool isTopGang = false;
                try
                {
                    var allGangs = G.GetAllPlayers()
                        .Where(p => p.IsJustGang && !p.crew.IsCrewDefeated)
                        .OrderByDescending(p => CalculateGangPower(p))
                        .Take(4)
                        .ToList();
                    PlayerInfo human = G.GetHumanPlayer();
                    isTopGang = allGangs.Any(g => g.PID == human.PID);
                }
                catch { }

                // 50% fail chance normally, 25% if top-4 gang
                float failChance = isTopGang ? 0.25f : 0.50f;
                bool success = SharedRng.NextDouble() >= failChance;

                if (success)
                {
                    state.WitnessThreatenedSuccessfully = true;
                    state.HasWitness = false;
                    // If witness is silenced, reduce wanted level
                    if (state.WantedLevel > WantedLevel.None)
                    {
                        state.WantedLevel = (WantedLevel)Math.Max(0, (int)state.WantedLevel - 1);
                        state.WantedProgress = Mathf.Clamp01(state.WantedProgress - 0.25f);
                    }
                    // Cancel fed tracking if witness silenced
                    state.FedsIncoming = false;
                    state.FedArrivalCountdown = 0;
                    Debug.Log($"[GameplayTweaks] Successfully threatened witness for {_selectedPeep.data.person.FullName}");
                }
                else
                {
                    // Failed - witness scared off, person gets more jail time (up to 5 years)
                    state.ExtraJailYears = SharedRng.Next(1, 6); // 1-5 years extra
                    state.WantedProgress = Mathf.Clamp01(state.WantedProgress + 0.15f);
                    if (state.WantedProgress >= 0.75f) state.WantedLevel = WantedLevel.High;
                    else if (state.WantedProgress >= 0.5f) state.WantedLevel = WantedLevel.Medium;
                    Debug.Log($"[GameplayTweaks] Failed! Witness scared off. {_selectedPeep.data.person.FullName} faces {state.ExtraJailYears} extra years!");
                }
                RefreshHandlerUI();
            }

            // ===================== The Grapevine Popup =====================
            private static void ToggleGrapevinePopup()
            {
                if (_grapevinePopup == null)
                    CreateGrapevinePopup();

                _grapevineVisible = !_grapevineVisible;
                _grapevinePopup.SetActive(_grapevineVisible);
                if (_grapevineVisible)
                    RefreshGrapevine();
            }

            private static void CreateGrapevinePopup()
            {
                var overlayCanvas = GetOrCreateOverlayCanvas();
                if (overlayCanvas == null) return;

                _grapevinePopup = new GameObject("GrapevinePopup", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(Canvas), typeof(GraphicRaycaster));
                _grapevinePopup.transform.SetParent(overlayCanvas.transform, false);

                var popCanvas = _grapevinePopup.GetComponent<Canvas>();
                popCanvas.overrideSorting = true;
                popCanvas.sortingOrder = 997;

                var rt = _grapevinePopup.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(360, 450);
                rt.anchoredPosition = new Vector2(0, 0);

                _grapevinePopup.GetComponent<Image>().color = new Color(0.12f, 0.1f, 0.15f, 0.98f);

                var vlg = _grapevinePopup.GetComponent<VerticalLayoutGroup>();
                vlg.padding = new RectOffset(10, 10, 8, 8);
                vlg.spacing = 3;
                vlg.childForceExpandWidth = true;
                vlg.childForceExpandHeight = false;

                // Header
                var headerRow = CreateHorizontalRow(_grapevinePopup.transform, "GV_Header");
                headerRow.GetComponent<LayoutElement>().minHeight = 28;
                var title = CreateLabel(headerRow.transform, "GV_Title", "The Grapevine", 14, FontStyle.Bold);
                title.color = new Color(0.85f, 0.75f, 0.95f);
                var closeBtn = CreateButton(headerRow.transform, "GV_Close", "X", () => { _grapevineVisible = false; _grapevinePopup.SetActive(false); });
                closeBtn.GetComponent<LayoutElement>().preferredWidth = 28;

                var subtitle = CreateLabel(_grapevinePopup.transform, "GV_Sub", "Word on the street...", 10, FontStyle.Italic);
                subtitle.color = new Color(0.6f, 0.55f, 0.7f);
                subtitle.alignment = TextAnchor.MiddleCenter;

                // Scrollable news list
                var scrollArea = new GameObject("GV_ScrollArea", typeof(RectTransform), typeof(Image), typeof(ScrollRect), typeof(LayoutElement));
                scrollArea.transform.SetParent(_grapevinePopup.transform, false);
                var scrollLE = scrollArea.GetComponent<LayoutElement>();
                scrollLE.flexibleHeight = 1; scrollLE.minHeight = 340;
                scrollArea.GetComponent<Image>().color = new Color(0.08f, 0.07f, 0.1f, 0.9f);

                var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
                viewport.transform.SetParent(scrollArea.transform, false);
                var viewportRt = viewport.GetComponent<RectTransform>();
                viewportRt.anchorMin = Vector2.zero; viewportRt.anchorMax = Vector2.one;
                viewportRt.offsetMin = new Vector2(2, 2); viewportRt.offsetMax = new Vector2(-2, -2);
                viewport.GetComponent<Image>().color = new Color(1, 1, 1, 0.01f);
                viewport.GetComponent<Mask>().showMaskGraphic = false;

                var content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
                content.transform.SetParent(viewport.transform, false);
                var contentRt = content.GetComponent<RectTransform>();
                contentRt.anchorMin = new Vector2(0, 1); contentRt.anchorMax = new Vector2(1, 1);
                contentRt.pivot = new Vector2(0.5f, 1);
                contentRt.sizeDelta = new Vector2(0, 0);
                content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                var contentVlg = content.GetComponent<VerticalLayoutGroup>();
                contentVlg.spacing = 2;
                contentVlg.childForceExpandWidth = true;
                contentVlg.childForceExpandHeight = false;
                contentVlg.padding = new RectOffset(4, 4, 4, 4);
                _grapevineContent = content.transform;

                var scroll = scrollArea.GetComponent<ScrollRect>();
                scroll.viewport = viewportRt;
                scroll.content = contentRt;
                scroll.horizontal = false;
                scroll.vertical = true;
                scroll.scrollSensitivity = 30f;
                scroll.movementType = ScrollRect.MovementType.Clamped;

                _grapevinePopup.SetActive(false);
            }

            private static void RefreshGrapevine()
            {
                if (_grapevineContent == null) return;

                for (int i = _grapevineContent.childCount - 1; i >= 0; i--)
                    GameObject.Destroy(_grapevineContent.GetChild(i).gameObject);

                if (SaveData.GrapevineEvents.Count == 0)
                {
                    var empty = CreateLabel(_grapevineContent, "Empty", "No news yet. Check back later.", 11, FontStyle.Italic);
                    empty.color = new Color(0.5f, 0.5f, 0.55f);
                    return;
                }

                foreach (string evt in SaveData.GrapevineEvents)
                {
                    var entryGo = new GameObject("GV_Entry", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
                    entryGo.transform.SetParent(_grapevineContent, false);
                    entryGo.GetComponent<LayoutElement>().minHeight = 28;

                    Color bgColor;
                    Color textColor;
                    if (evt.StartsWith("KILL:") || evt.StartsWith("DEATH:"))
                    {
                        bgColor = new Color(0.18f, 0.1f, 0.1f, 0.9f);
                        textColor = new Color(0.9f, 0.6f, 0.6f);
                    }
                    else if (evt.StartsWith("WAR:"))
                    {
                        bgColor = new Color(0.2f, 0.12f, 0.08f, 0.9f);
                        textColor = new Color(0.95f, 0.7f, 0.4f);
                    }
                    else if (evt.StartsWith("PACT:"))
                    {
                        bgColor = new Color(0.1f, 0.15f, 0.1f, 0.9f);
                        textColor = new Color(0.6f, 0.9f, 0.6f);
                    }
                    else if (evt.StartsWith("FRONT:"))
                    {
                        bgColor = new Color(0.1f, 0.12f, 0.18f, 0.9f);
                        textColor = new Color(0.5f, 0.7f, 0.95f);
                    }
                    else
                    {
                        bgColor = new Color(0.12f, 0.12f, 0.14f, 0.9f);
                        textColor = new Color(0.75f, 0.75f, 0.7f);
                    }

                    entryGo.GetComponent<Image>().color = bgColor;
                    var label = CreateLabel(entryGo.transform, "Text", evt, 13, FontStyle.Normal);
                    label.color = textColor;
                    var labelRt = label.GetComponent<RectTransform>();
                    labelRt.anchorMin = Vector2.zero; labelRt.anchorMax = Vector2.one;
                    labelRt.offsetMin = new Vector2(6, 2); labelRt.offsetMax = new Vector2(-6, -2);
                }
            }

            // ===================== Safebox Popup =====================
            private static void ToggleSafeboxPopup()
            {
                if (_safeboxPopup == null)
                    CreateSafeboxPopup();

                _safeboxVisible = !_safeboxVisible;
                _safeboxPopup.SetActive(_safeboxVisible);
                if (_safeboxVisible)
                    RefreshSafeboxInfo();
            }

            private static void CreateSafeboxPopup()
            {
                var overlayCanvas = GetOrCreateOverlayCanvas();
                if (overlayCanvas == null) return;

                _safeboxPopup = new GameObject("SafeboxPopup", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(Canvas), typeof(GraphicRaycaster));
                _safeboxPopup.transform.SetParent(overlayCanvas.transform, false);

                var popCanvas = _safeboxPopup.GetComponent<Canvas>();
                popCanvas.overrideSorting = true;
                popCanvas.sortingOrder = 998;

                var rt = _safeboxPopup.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(400, 500);
                rt.anchoredPosition = new Vector2(0, 0);

                _safeboxPopup.GetComponent<Image>().color = new Color(0.1f, 0.12f, 0.08f, 0.98f);

                var vlg = _safeboxPopup.GetComponent<VerticalLayoutGroup>();
                vlg.padding = new RectOffset(10, 10, 8, 8);
                vlg.spacing = 4;
                vlg.childForceExpandWidth = true;
                vlg.childForceExpandHeight = false;

                var outline = _safeboxPopup.AddComponent<Outline>();
                outline.effectColor = new Color(0.4f, 0.7f, 0.3f, 0.8f);
                outline.effectDistance = new Vector2(2, -2);

                // Header
                var headerRow = CreateHorizontalRow(_safeboxPopup.transform, "SB_Header");
                headerRow.GetComponent<LayoutElement>().minHeight = 28;
                var title = CreateLabel(headerRow.transform, "SB_Title", "Safebox", 14, FontStyle.Bold);
                title.color = new Color(0.7f, 0.95f, 0.7f);
                var closeBtn = CreateButton(headerRow.transform, "SB_Close", "X", () => { _safeboxVisible = false; _safeboxPopup.SetActive(false); });
                closeBtn.GetComponent<LayoutElement>().preferredWidth = 28;

                // Cash summary labels
                _safeboxCleanText = CreateLabel(_safeboxPopup.transform, "SB_Clean", "Clean Cash: $0", 12, FontStyle.Bold);
                _safeboxCleanText.color = new Color(0.3f, 0.9f, 0.3f);
                _safeboxDirtyText = CreateLabel(_safeboxPopup.transform, "SB_Dirty", "Dirty Cash: $0", 12, FontStyle.Bold);
                _safeboxDirtyText.color = new Color(0.9f, 0.65f, 0.2f);
                _safeboxTotalText = CreateLabel(_safeboxPopup.transform, "SB_Total", "Total: $0", 12, FontStyle.Bold);
                _safeboxTotalText.color = new Color(0.95f, 0.95f, 0.8f);

                // Divider
                CreateLabel(_safeboxPopup.transform, "SB_Div", "--- Building Inventories ---", 10, FontStyle.Italic).color = new Color(0.5f, 0.6f, 0.5f);

                // Scrollable building list
                var scrollArea = new GameObject("SB_ScrollArea", typeof(RectTransform), typeof(Image), typeof(ScrollRect), typeof(LayoutElement));
                scrollArea.transform.SetParent(_safeboxPopup.transform, false);
                var scrollLE = scrollArea.GetComponent<LayoutElement>();
                scrollLE.flexibleHeight = 1; scrollLE.minHeight = 300;
                scrollArea.GetComponent<Image>().color = new Color(0.08f, 0.09f, 0.06f, 0.9f);

                var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
                viewport.transform.SetParent(scrollArea.transform, false);
                var viewportRt = viewport.GetComponent<RectTransform>();
                viewportRt.anchorMin = Vector2.zero; viewportRt.anchorMax = Vector2.one;
                viewportRt.offsetMin = new Vector2(2, 2); viewportRt.offsetMax = new Vector2(-2, -2);
                viewport.GetComponent<Image>().color = new Color(1, 1, 1, 0.01f);
                viewport.GetComponent<Mask>().showMaskGraphic = false;

                var content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
                content.transform.SetParent(viewport.transform, false);
                var contentRt = content.GetComponent<RectTransform>();
                contentRt.anchorMin = new Vector2(0, 1); contentRt.anchorMax = new Vector2(1, 1);
                contentRt.pivot = new Vector2(0.5f, 1);
                contentRt.sizeDelta = new Vector2(0, 0);
                content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                var contentVlg = content.GetComponent<VerticalLayoutGroup>();
                contentVlg.spacing = 3;
                contentVlg.childForceExpandWidth = true;
                contentVlg.childForceExpandHeight = false;
                contentVlg.padding = new RectOffset(4, 4, 4, 4);
                _safeboxContent = content.transform;

                var scroll = scrollArea.GetComponent<ScrollRect>();
                scroll.viewport = viewportRt;
                scroll.content = contentRt;
                scroll.horizontal = false;
                scroll.vertical = true;
                scroll.scrollSensitivity = 30f;
                scroll.movementType = ScrollRect.MovementType.Clamped;

                AddDragHandler(_safeboxPopup, rt);
                _safeboxPopup.SetActive(false);
            }

            private static void RefreshSafeboxInfo()
            {
                try
                {
                    PlayerInfo human = G.GetHumanPlayer();
                    if (human == null) return;

                    int cleanCash = GetPlayerCleanCash();
                    int dirtyCash = GetTotalDirtyCash();
                    _safeboxCleanText.text = $"Clean Cash: ${cleanCash}";
                    _safeboxDirtyText.text = $"Dirty Cash: ${dirtyCash}";
                    _safeboxTotalText.text = $"Total: ${cleanCash + dirtyCash}";

                    // Clear old content
                    if (_safeboxContent != null)
                    {
                        for (int i = _safeboxContent.childCount - 1; i >= 0; i--)
                            GameObject.Destroy(_safeboxContent.GetChild(i).gameObject);
                    }

                    // List safehouse
                    var safehouse = human.territory.Safehouse;
                    if (!safehouse.IsNotValid)
                    {
                        var safeEntity = safehouse.FindEntity();
                        if (safeEntity != null)
                        {
                            int safeDirty = ReadInventoryAmount(safeEntity, ModConstants.DIRTY_CASH_LABEL);
                            CreateSafeboxEntry("Safehouse", safeDirty, safeEntity);
                        }
                    }

                    // List player buildings
                    try
                    {
                        var territory = human.territory;
                        var buildingsField = territory.GetType().GetField("_buildings", BindingFlags.NonPublic | BindingFlags.Instance)
                            ?? territory.GetType().GetField("buildings", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        if (buildingsField != null)
                        {
                            var buildings = buildingsField.GetValue(territory) as System.Collections.IEnumerable;
                            if (buildings != null)
                            {
                                foreach (var b in buildings)
                                {
                                    try
                                    {
                                        EntityID bid;
                                        if (b is EntityID) bid = (EntityID)b;
                                        else
                                        {
                                            var idProp = b.GetType().GetProperty("Id") ?? b.GetType().GetProperty("id");
                                            if (idProp != null) bid = (EntityID)idProp.GetValue(b);
                                            else continue;
                                        }
                                        if (bid == safehouse) continue; // Skip safehouse, already listed
                                        var bEntity = bid.FindEntity();
                                        if (bEntity == null) continue;
                                        string bName = bEntity.ToString() ?? "Building";
                                        int bDirty = ReadInventoryAmount(bEntity, ModConstants.DIRTY_CASH_LABEL);
                                        CreateSafeboxEntry(bName, bDirty, bEntity);
                                    }
                                    catch { }
                                }
                            }
                        }
                        else
                        {
                            // Fallback: try OwnedBuildings property
                            var ownedProp = territory.GetType().GetProperty("OwnedBuildings")
                                ?? territory.GetType().GetProperty("ownedBuildings");
                            if (ownedProp != null)
                            {
                                var owned = ownedProp.GetValue(territory) as System.Collections.IEnumerable;
                                if (owned != null)
                                {
                                    foreach (var b in owned)
                                    {
                                        try
                                        {
                                            Entity bEntity = b as Entity;
                                            if (bEntity == null)
                                            {
                                                if (b is EntityID)
                                                    bEntity = ((EntityID)b).FindEntity();
                                                else continue;
                                            }
                                            string bName = bEntity.ToString() ?? "Building";
                                            int bDirty = ReadInventoryAmount(bEntity, ModConstants.DIRTY_CASH_LABEL);
                                            CreateSafeboxEntry(bName, bDirty, bEntity);
                                        }
                                        catch { }
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception e) { Debug.LogWarning($"[GameplayTweaks] Safebox building list: {e.Message}"); }

                    if (_safeboxContent.childCount == 0)
                    {
                        var empty = CreateLabel(_safeboxContent, "Empty", "No buildings found.", 11, FontStyle.Italic);
                        empty.color = new Color(0.5f, 0.5f, 0.5f);
                    }
                }
                catch (Exception e) { Debug.LogError($"[GameplayTweaks] RefreshSafeboxInfo: {e}"); }
            }

            private static void CreateSafeboxEntry(string name, int dirtyCash, Entity entity)
            {
                var entryGo = new GameObject("SB_Entry", typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
                entryGo.transform.SetParent(_safeboxContent, false);
                entryGo.GetComponent<LayoutElement>().minHeight = 24;
                entryGo.GetComponent<Image>().color = new Color(0.12f, 0.14f, 0.1f, 0.9f);
                var hlg = entryGo.GetComponent<HorizontalLayoutGroup>();
                hlg.spacing = 4; hlg.childForceExpandWidth = true; hlg.childForceExpandHeight = true;
                hlg.padding = new RectOffset(6, 6, 2, 2);

                var nameLabel = CreateLabel(entryGo.transform, "Name", name, 10, FontStyle.Normal);
                nameLabel.color = new Color(0.85f, 0.85f, 0.8f);
                nameLabel.GetComponent<LayoutElement>().flexibleWidth = 3;

                string cashStr = dirtyCash > 0 ? $"${dirtyCash} dirty" : "No dirty cash";
                var cashLabel = CreateLabel(entryGo.transform, "Cash", cashStr, 10, FontStyle.Normal);
                cashLabel.color = dirtyCash > 0 ? new Color(0.9f, 0.65f, 0.2f) : new Color(0.5f, 0.5f, 0.5f);
                cashLabel.alignment = TextAnchor.MiddleRight;
                cashLabel.GetComponent<LayoutElement>().flexibleWidth = 2;
            }

            // ===================== My Pact Popup =====================
            private static void ToggleMyPactPopup()
            {
                if (_myPactPopup == null)
                    CreateMyPactPopup();

                _myPactVisible = !_myPactVisible;
                _myPactPopup.SetActive(_myPactVisible);
                if (_myPactVisible)
                    RefreshMyPactInfo();
            }

            private static void CreateMyPactPopup()
            {
                var go = _goField?.GetValue(_popupInstance) as GameObject;
                if (go == null) return;

                _myPactPopup = new GameObject("MyPactPopup", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
                _myPactPopup.transform.SetParent(go.transform, false);

                var rt = _myPactPopup.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(380, 420);
                rt.anchoredPosition = new Vector2(200, 0);

                _myPactPopup.GetComponent<Image>().color = new Color(0.1f, 0.14f, 0.1f, 0.98f);

                var vlg = _myPactPopup.GetComponent<VerticalLayoutGroup>();
                vlg.padding = new RectOffset(10, 10, 8, 8);
                vlg.spacing = 4;
                vlg.childForceExpandWidth = true;
                vlg.childForceExpandHeight = false;

                // Header
                var headerRow = CreateHorizontalRow(_myPactPopup.transform, "MP_Header");
                headerRow.GetComponent<LayoutElement>().minHeight = 30;
                var title = CreateLabel(headerRow.transform, "MP_Title", "My Pact", 14, FontStyle.Bold);
                title.color = new Color(0.5f, 1f, 0.5f);
                var closeBtn = CreateButton(headerRow.transform, "MP_Close", "X", () => { _myPactVisible = false; _myPactPopup.SetActive(false); });
                closeBtn.GetComponent<LayoutElement>().preferredWidth = 30;

                // Scrollable content
                var scrollArea = new GameObject("MP_ScrollArea", typeof(RectTransform), typeof(Image), typeof(ScrollRect), typeof(LayoutElement));
                scrollArea.transform.SetParent(_myPactPopup.transform, false);
                var scrollLE = scrollArea.GetComponent<LayoutElement>();
                scrollLE.flexibleHeight = 1; scrollLE.minHeight = 300;
                scrollArea.GetComponent<Image>().color = new Color(0.08f, 0.1f, 0.08f, 0.9f);

                var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
                viewport.transform.SetParent(scrollArea.transform, false);
                var viewportRt = viewport.GetComponent<RectTransform>();
                viewportRt.anchorMin = Vector2.zero; viewportRt.anchorMax = Vector2.one;
                viewportRt.offsetMin = new Vector2(2, 2); viewportRt.offsetMax = new Vector2(-2, -2);
                viewport.GetComponent<Image>().color = new Color(1, 1, 1, 0.01f);
                viewport.GetComponent<Mask>().showMaskGraphic = false;

                var content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
                content.transform.SetParent(viewport.transform, false);
                var contentRt = content.GetComponent<RectTransform>();
                contentRt.anchorMin = new Vector2(0, 1); contentRt.anchorMax = new Vector2(1, 1);
                contentRt.pivot = new Vector2(0.5f, 1);
                contentRt.sizeDelta = new Vector2(0, 0);
                content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                var contentVlg = content.GetComponent<VerticalLayoutGroup>();
                contentVlg.spacing = 3;
                contentVlg.childForceExpandWidth = true;
                contentVlg.childForceExpandHeight = false;
                contentVlg.padding = new RectOffset(4, 4, 4, 4);
                _myPactContent = content.transform;

                var scroll = scrollArea.GetComponent<ScrollRect>();
                scroll.viewport = viewportRt;
                scroll.content = contentRt;
                scroll.horizontal = false;
                scroll.vertical = true;
                scroll.scrollSensitivity = 30f;
                scroll.movementType = ScrollRect.MovementType.Clamped;

                _myPactPopup.SetActive(false);
            }

            private static void RefreshMyPactInfo()
            {
                if (_myPactContent == null) return;

                // Clear existing entries
                for (int i = _myPactContent.childCount - 1; i >= 0; i--)
                    GameObject.Destroy(_myPactContent.GetChild(i).gameObject);

                // Check player-created pact or joined AI pact
                AlliancePact playerPact = SaveData.Pacts.FirstOrDefault(p => p.ColorIndex == ModConstants.PLAYER_PACT_SLOT);
                if (playerPact == null && SaveData.PlayerJoinedPactIndex >= 0)
                    playerPact = SaveData.Pacts.FirstOrDefault(p => p.ColorIndex == SaveData.PlayerJoinedPactIndex);
                if (playerPact == null)
                {
                    var noInfo = CreateLabel(_myPactContent, "NoInfo", "No pact yet. Join or create one.", 12, FontStyle.Italic);
                    noInfo.color = new Color(0.6f, 0.6f, 0.55f);
                    return;
                }

                // Pact header info
                var pactHeader = CreateLabel(_myPactContent, "PactName", playerPact.DisplayName, 13, FontStyle.Bold);
                pactHeader.color = playerPact.SharedColor;
                pactHeader.GetComponent<LayoutElement>().minHeight = 22;

                int totalMembers = playerPact.MemberIds.Count + (playerPact.LeaderGangId >= 0 ? 1 : 0);
                var memberCount = CreateLabel(_myPactContent, "MemberCount", $"Members: {totalMembers}", 11, FontStyle.Normal);
                memberCount.color = new Color(0.8f, 0.8f, 0.75f);
                memberCount.GetComponent<LayoutElement>().minHeight = 18;

                // Divider
                var divider = new GameObject("Divider", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
                divider.transform.SetParent(_myPactContent, false);
                divider.GetComponent<LayoutElement>().minHeight = 2;
                divider.GetComponent<Image>().color = new Color(0.3f, 0.5f, 0.3f, 0.5f);

                // List each member gang with info
                int totalEarnings = 0;
                var allMemberIds = new List<int>();
                if (playerPact.LeaderGangId >= 0) allMemberIds.Add(playerPact.LeaderGangId);
                allMemberIds.AddRange(playerPact.MemberIds);

                foreach (int gangId in allMemberIds)
                {
                    PlayerInfo gangPlayer = null;
                    foreach (var p in TrackedGangs)
                    {
                        if (p.PID.id == gangId) { gangPlayer = p; break; }
                    }

                    string name = gangPlayer?.social?.PlayerGroupName ?? $"Gang #{gangId}";
                    int crewCount = gangPlayer?.crew?.LivingCrewCount ?? 0;
                    int power = gangPlayer != null ? CalculateGangPower(gangPlayer) : 0;
                    int gangCash = 0;
                    try { if (gangPlayer != null) gangCash = (int)gangPlayer.finances.GetMoneyTotal(); } catch { }

                    // Estimate earnings from this gang (income share)
                    int gangEarnings = (int)(power * ModConstants.INCOME_SHARE_PERCENT * 10);
                    totalEarnings += gangEarnings;

                    var entryGo = new GameObject("MP_Entry", typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
                    entryGo.transform.SetParent(_myPactContent, false);
                    entryGo.GetComponent<LayoutElement>().minHeight = 48;
                    entryGo.GetComponent<Image>().color = new Color(0.12f, 0.16f, 0.12f, 0.9f);
                    var hlg = entryGo.GetComponent<HorizontalLayoutGroup>();
                    hlg.padding = new RectOffset(6, 6, 3, 3);
                    hlg.spacing = 4;
                    hlg.childForceExpandWidth = false;
                    hlg.childForceExpandHeight = true;

                    // Color swatch
                    var swatchGo = new GameObject("Swatch", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
                    swatchGo.transform.SetParent(entryGo.transform, false);
                    swatchGo.GetComponent<LayoutElement>().preferredWidth = 6;
                    swatchGo.GetComponent<Image>().color = playerPact.SharedColor;

                    // Info column
                    var infoGo = new GameObject("Info", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
                    infoGo.transform.SetParent(entryGo.transform, false);
                    infoGo.GetComponent<LayoutElement>().flexibleWidth = 1;
                    var infoVlg = infoGo.GetComponent<VerticalLayoutGroup>();
                    infoVlg.spacing = 1;
                    infoVlg.childForceExpandWidth = true;
                    infoVlg.childForceExpandHeight = false;

                    var nameLabel = CreateLabel(infoGo.transform, "Name", name, 11, FontStyle.Bold);
                    nameLabel.color = new Color(0.9f, 0.95f, 0.85f);
                    nameLabel.GetComponent<LayoutElement>().minHeight = 16;

                    var detailLabel = CreateLabel(infoGo.transform, "Details", $"Crew: {crewCount} | Power: {power} | Cash: ${gangCash}", 9, FontStyle.Normal);
                    detailLabel.color = new Color(0.65f, 0.7f, 0.6f);
                    detailLabel.GetComponent<LayoutElement>().minHeight = 14;

                    // Right column: earnings + give button
                    var rightCol = new GameObject("RightCol", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
                    rightCol.transform.SetParent(entryGo.transform, false);
                    rightCol.GetComponent<LayoutElement>().preferredWidth = 90;
                    var rcVlg = rightCol.GetComponent<VerticalLayoutGroup>();
                    rcVlg.spacing = 2; rcVlg.childForceExpandWidth = true; rcVlg.childForceExpandHeight = false;

                    // Earnings
                    var earningsLabel = CreateLabel(rightCol.transform, "Earnings", $"${gangEarnings}/turn", 11, FontStyle.Bold);
                    earningsLabel.color = new Color(0.4f, 0.85f, 0.4f);
                    earningsLabel.alignment = TextAnchor.MiddleRight;

                    // Give $500 button (only for AI gangs, not player)
                    if (gangPlayer != null && !gangPlayer.PID.IsHumanPlayer)
                    {
                        var giveBtn = CreateButton(rightCol.transform, "Give", "Give $500", null);
                        giveBtn.GetComponent<Image>().color = new Color(0.2f, 0.35f, 0.2f, 0.9f);
                        giveBtn.GetComponent<LayoutElement>().minHeight = 18;
                        giveBtn.GetComponent<LayoutElement>().preferredHeight = 18;
                        giveBtn.GetComponentInChildren<Text>().fontSize = 9;
                        var capturedGang = gangPlayer;
                        var capturedName = name;
                        giveBtn.onClick.AddListener(() => OnGiveCashToGang(capturedGang, capturedName));
                    }
                }

                // Total earnings divider
                var div2 = new GameObject("Divider2", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
                div2.transform.SetParent(_myPactContent, false);
                div2.GetComponent<LayoutElement>().minHeight = 2;
                div2.GetComponent<Image>().color = new Color(0.3f, 0.5f, 0.3f, 0.5f);

                // Total earnings row
                var totalRow = CreateHorizontalRow(_myPactContent, "MP_Total");
                totalRow.GetComponent<LayoutElement>().minHeight = 28;
                var totalLabel = CreateLabel(totalRow.transform, "TotalLabel", "Total Pact Earnings:", 12, FontStyle.Bold);
                totalLabel.color = new Color(0.9f, 0.9f, 0.8f);
                var totalValue = CreateLabel(totalRow.transform, "TotalValue", $"${totalEarnings}/turn", 13, FontStyle.Bold);
                totalValue.color = new Color(0.3f, 0.9f, 0.3f);
                totalValue.alignment = TextAnchor.MiddleRight;

                // --- Pact News Section (from Grapevine) ---
                // Collect member gang names for filtering
                var memberNames = new List<string>();
                foreach (int gid in allMemberIds)
                {
                    PlayerInfo gp = null;
                    foreach (var p in TrackedGangs)
                    {
                        if (p.PID.id == gid) { gp = p; break; }
                    }
                    if (gp?.social?.PlayerGroupName != null)
                        memberNames.Add(gp.social.PlayerGroupName);
                }

                // Filter grapevine events mentioning any pact member
                var pactNews = new List<string>();
                foreach (string evt in SaveData.GrapevineEvents)
                {
                    foreach (string name in memberNames)
                    {
                        if (evt.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            pactNews.Add(evt);
                            break;
                        }
                    }
                    if (pactNews.Count >= 5) break;
                }

                if (pactNews.Count > 0)
                {
                    var newsDiv = new GameObject("NewsDiv", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
                    newsDiv.transform.SetParent(_myPactContent, false);
                    newsDiv.GetComponent<LayoutElement>().minHeight = 2;
                    newsDiv.GetComponent<Image>().color = new Color(0.4f, 0.4f, 0.6f, 0.5f);

                    var newsHeader = CreateLabel(_myPactContent, "NewsHeader", "Pact News", 12, FontStyle.Bold);
                    newsHeader.color = new Color(0.75f, 0.7f, 0.9f);
                    newsHeader.GetComponent<LayoutElement>().minHeight = 20;

                    foreach (string news in pactNews)
                    {
                        // Determine color by type prefix
                        Color newsColor = new Color(0.7f, 0.7f, 0.65f);
                        if (news.StartsWith("KILL:") || news.StartsWith("DEATH:")) newsColor = new Color(0.9f, 0.4f, 0.3f);
                        else if (news.StartsWith("WAR:")) newsColor = new Color(0.9f, 0.65f, 0.3f);
                        else if (news.StartsWith("PACT:")) newsColor = new Color(0.4f, 0.85f, 0.4f);
                        else if (news.StartsWith("FRONT:")) newsColor = new Color(0.5f, 0.7f, 0.95f);

                        // Remove prefix for display
                        string display = news;
                        int colonIdx = news.IndexOf(':');
                        if (colonIdx > 0 && colonIdx < 7) display = news.Substring(colonIdx + 2);

                        var newsLabel = CreateLabel(_myPactContent, "News", display, 13, FontStyle.Normal);
                        newsLabel.color = newsColor;
                        newsLabel.GetComponent<LayoutElement>().minHeight = 22;
                    }
                }
            }

            private static void OnGiveCashToGang(PlayerInfo gangPlayer, string gangName)
            {
                try
                {
                    PlayerInfo human = G.GetHumanPlayer();
                    if (human == null || gangPlayer == null) return;
                    int amount = 500;
                    if (!human.finances.CanChangeMoneyOnSafehouse(new Price(-amount)))
                    {
                        Debug.Log("[GameplayTweaks] Can't afford to give $500");
                        return;
                    }
                    human.finances.DoChangeMoneyOnSafehouse(new Price(-amount), MoneyReason.Other);
                    gangPlayer.finances.DoChangeMoneyOnSafehouse(new Price(amount), MoneyReason.Other);
                    LogGrapevine($"PACT: Your outfit donated ${amount} to {gangName}");
                    RefreshMyPactInfo();
                }
                catch (Exception e) { Debug.LogError($"[GameplayTweaks] Give cash failed: {e}"); }
            }

            // ===================== Gang Pacts Popup (Boss-only) =====================
            private static void ToggleGangPactsPopup()
            {
                // Only boss can use this
                PlayerInfo human = G.GetHumanPlayer();
                if (human == null) return;

                if (_gangPactsPopup == null)
                    CreateGangPactsPopup();

                _gangPactsVisible = !_gangPactsVisible;
                _gangPactsPopup.SetActive(_gangPactsVisible);
                if (_gangPactsVisible)
                {
                    RefreshGangTracker(); // Always refresh when opening
                    RefreshGangPactsList();
                }
            }

            private static void CreateGangPactsPopup()
            {
                var go = _goField?.GetValue(_popupInstance) as GameObject;
                if (go == null) return;

                _gangPactsPopup = new GameObject("GangPactsPopup", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
                _gangPactsPopup.transform.SetParent(go.transform, false);

                var rt = _gangPactsPopup.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(460, 680);
                rt.anchoredPosition = new Vector2(-200, 0);

                var bgImg = _gangPactsPopup.GetComponent<Image>();
                bgImg.color = new Color(0.12f, 0.08f, 0.06f, 0.98f);

                var vlg = _gangPactsPopup.GetComponent<VerticalLayoutGroup>();
                vlg.padding = new RectOffset(8, 8, 8, 8);
                vlg.spacing = 4;
                vlg.childForceExpandWidth = true;
                vlg.childForceExpandHeight = false;

                // --- HEADER ---
                var headerRow = CreateHorizontalRow(_gangPactsPopup.transform, "GP_Header");
                headerRow.GetComponent<LayoutElement>().minHeight = 30;
                CreateLabel(headerRow.transform, "GP_Title", "Gang Pacts - Diplomacy", 14, FontStyle.Bold);
                var closeBtn = CreateButton(headerRow.transform, "GP_Close", "X", () => { _gangPactsVisible = false; _gangPactsPopup.SetActive(false); });
                closeBtn.GetComponent<LayoutElement>().preferredWidth = 30;

                // --- TAB BUTTONS ---
                var tabRow = CreateHorizontalRow(_gangPactsPopup.transform, "GP_TabRow");
                tabRow.GetComponent<LayoutElement>().minHeight = 28;
                var mainTabBtn = CreateButton(tabRow.transform, "GP_MainTab", "Main", () => { _pactEditorMode = false; RefreshGangPactsList(); });
                mainTabBtn.GetComponent<Image>().color = new Color(0.25f, 0.2f, 0.15f, 0.95f);
                var editorTabBtn = CreateButton(tabRow.transform, "GP_EditorTab", "Editor", () => { _pactEditorMode = true; RefreshGangPactsList(); });
                editorTabBtn.GetComponent<Image>().color = new Color(0.15f, 0.2f, 0.25f, 0.95f);
                _pactTabLabel = CreateLabel(tabRow.transform, "GP_TabLabel", "Main", 10, FontStyle.Italic);
                _pactTabLabel.alignment = TextAnchor.MiddleCenter;
                _pactTabLabel.GetComponent<LayoutElement>().preferredWidth = 80;

                // --- PACT EDITOR SECTION ---
                CreateLabel(_gangPactsPopup.transform, "GP_EditorLabel", "Alliance Slots", 12, FontStyle.Bold);

                // 7 slots: 0-5 = AI pacts, 6 = player pact
                for (int i = 0; i < 7; i++)
                    CreatePactSlotRow(i);

                // Never Accept Pacts toggle
                var toggleRow = CreateHorizontalRow(_gangPactsPopup.transform, "GP_ToggleRow");
                toggleRow.GetComponent<LayoutElement>().minHeight = 22;
                var toggleLabel = CreateLabel(toggleRow.transform, "GP_ToggleLabel", "Never Accept Pact Invites:", 10, FontStyle.Normal);
                toggleLabel.color = new Color(0.7f, 0.7f, 0.65f);
                var toggleBtn = CreateButton(toggleRow.transform, "GP_ToggleBtn",
                    SaveData.NeverAcceptPacts ? "ON" : "OFF", () =>
                    {
                        SaveData.NeverAcceptPacts = !SaveData.NeverAcceptPacts;
                        var txt = toggleRow.transform.Find("GP_ToggleBtn")?.GetComponentInChildren<Text>();
                        if (txt != null) txt.text = SaveData.NeverAcceptPacts ? "ON" : "OFF";
                    });
                toggleBtn.GetComponent<LayoutElement>().preferredWidth = 45;
                toggleBtn.GetComponent<Image>().color = new Color(0.3f, 0.25f, 0.2f, 0.95f);

                // --- SELECTION INDICATOR ---
                _selectedPactLabel = CreateLabel(_gangPactsPopup.transform, "GP_SelectedPact",
                    "Select a pact slot above, then invite gangs below", 10, FontStyle.Italic);
                _selectedPactLabel.color = new Color(0.7f, 0.7f, 0.6f);
                _selectedPactLabel.alignment = TextAnchor.MiddleCenter;

                // --- STATUS ---
                _gangPactsStatusText = CreateLabel(_gangPactsPopup.transform, "GP_Status", "", 10, FontStyle.Normal);
                _gangPactsStatusText.alignment = TextAnchor.MiddleCenter;

                // --- GANG BROWSER (Scrollable) ---
                var scrollArea = new GameObject("GP_ScrollArea", typeof(RectTransform), typeof(Image), typeof(ScrollRect), typeof(LayoutElement));
                scrollArea.transform.SetParent(_gangPactsPopup.transform, false);
                var scrollLE = scrollArea.GetComponent<LayoutElement>();
                scrollLE.minHeight = 240; scrollLE.flexibleHeight = 1;
                scrollArea.GetComponent<Image>().color = new Color(0.08f, 0.06f, 0.04f, 0.9f);

                var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
                viewport.transform.SetParent(scrollArea.transform, false);
                var viewportRt = viewport.GetComponent<RectTransform>();
                viewportRt.anchorMin = Vector2.zero; viewportRt.anchorMax = Vector2.one;
                viewportRt.offsetMin = new Vector2(2, 2); viewportRt.offsetMax = new Vector2(-2, -2);
                viewport.GetComponent<Image>().color = new Color(1, 1, 1, 0.01f);
                viewport.GetComponent<Mask>().showMaskGraphic = false;

                var content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
                content.transform.SetParent(viewport.transform, false);
                var contentRt = content.GetComponent<RectTransform>();
                contentRt.anchorMin = new Vector2(0, 1); contentRt.anchorMax = new Vector2(1, 1);
                contentRt.pivot = new Vector2(0.5f, 1);
                contentRt.sizeDelta = new Vector2(0, 0);
                content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                var contentVlg = content.GetComponent<VerticalLayoutGroup>();
                contentVlg.spacing = 3;
                contentVlg.childForceExpandWidth = true;
                contentVlg.childForceExpandHeight = false;
                contentVlg.padding = new RectOffset(4, 4, 4, 4);
                _gangPactsContent = content.transform;

                _gangPactsScroll = scrollArea.GetComponent<ScrollRect>();
                _gangPactsScroll.viewport = viewportRt;
                _gangPactsScroll.content = contentRt;
                _gangPactsScroll.horizontal = false;
                _gangPactsScroll.vertical = true;
                _gangPactsScroll.scrollSensitivity = 30f;
                _gangPactsScroll.movementType = ScrollRect.MovementType.Clamped;

                _gangPactsPopup.SetActive(false);
            }

            private static void CreatePactSlotRow(int slotIndex)
            {
                var row = new GameObject($"GP_PactSlot_{slotIndex}", typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
                row.transform.SetParent(_gangPactsPopup.transform, false);
                row.GetComponent<LayoutElement>().minHeight = 34;
                row.GetComponent<LayoutElement>().preferredHeight = 34;
                row.GetComponent<Image>().color = new Color(0.16f, 0.14f, 0.1f, 0.9f);
                var hlg = row.GetComponent<HorizontalLayoutGroup>();
                hlg.padding = new RectOffset(4, 4, 3, 3);
                hlg.spacing = 4;
                hlg.childForceExpandWidth = false;
                hlg.childForceExpandHeight = true;
                _pactSlotRows[slotIndex] = row;

                // Color swatch
                var swatchGo = new GameObject("Swatch", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
                swatchGo.transform.SetParent(row.transform, false);
                swatchGo.GetComponent<LayoutElement>().preferredWidth = 22;
                swatchGo.GetComponent<LayoutElement>().minWidth = 22;
                _pactSlotColorSwatches[slotIndex] = swatchGo.GetComponent<Image>();
                _pactSlotColorSwatches[slotIndex].color = ModConstants.PACT_COLORS[slotIndex];

                // Name text (flexible)
                var nameText = CreateLabel(row.transform, "Name", $"{ModConstants.PACT_COLOR_NAMES[slotIndex]} - Empty", 11, FontStyle.Bold);
                nameText.GetComponent<LayoutElement>().flexibleWidth = 1;
                _pactSlotNameTexts[slotIndex] = nameText;

                // Member count text
                var memberText = CreateLabel(row.transform, "Members", "", 9, FontStyle.Normal);
                memberText.GetComponent<LayoutElement>().preferredWidth = 65;
                memberText.alignment = TextAnchor.MiddleCenter;
                memberText.color = new Color(0.7f, 0.7f, 0.65f);
                _pactSlotMemberTexts[slotIndex] = memberText;

                // Select/Create button
                int capturedIdx = slotIndex;
                var selectBtn = CreateButton(row.transform, "Select", "Create", () => OnSelectPactSlot(capturedIdx));
                selectBtn.GetComponent<LayoutElement>().preferredWidth = 55;
                selectBtn.GetComponent<LayoutElement>().minWidth = 55;
                _pactSlotSelectBtns[slotIndex] = selectBtn;

                // Delete button
                var deleteBtn = CreateButton(row.transform, "Delete", "X", () => OnDeletePactSlot(capturedIdx));
                deleteBtn.GetComponent<LayoutElement>().preferredWidth = 26;
                deleteBtn.GetComponent<LayoutElement>().minWidth = 26;
                deleteBtn.GetComponent<Image>().color = new Color(0.5f, 0.15f, 0.15f, 0.95f);
                _pactSlotDeleteBtns[slotIndex] = deleteBtn;
                deleteBtn.gameObject.SetActive(false);
            }

            private static void RefreshPactSlots()
            {
                int playerSlot = ModConstants.PLAYER_PACT_SLOT;
                bool playerInAIPact = SaveData.PlayerJoinedPactIndex >= 0;
                SimTime now = G.GetNow();

                for (int i = 0; i < 7; i++)
                {
                    if (_pactSlotRows[i] == null) continue;
                    var pact = SaveData.Pacts.FirstOrDefault(p => p.ColorIndex == i);
                    bool isSelected = (_selectedPactSlot == i);

                    if (i == playerSlot)
                    {
                        // Player pact slot
                        if (pact != null)
                        {
                            bool established = pact.MemberIds.Count > 0 || pact.LeaderGangId >= 0;
                            _pactSlotNameTexts[i].text = established ? pact.DisplayName : $"{pact.DisplayName} (Need 1 gang)";
                            int totalMembers = pact.MemberIds.Count + (pact.LeaderGangId >= 0 ? 1 : 0);
                            _pactSlotMemberTexts[i].text = $"{totalMembers} gang(s)";
                            _pactSlotSelectBtns[i].GetComponentInChildren<Text>().text = isSelected ? "Active" : "Select";
                            _pactSlotDeleteBtns[i].gameObject.SetActive(true);
                        }
                        else
                        {
                            _pactSlotNameTexts[i].text = "Your Pact - Not Created";
                            _pactSlotMemberTexts[i].text = $"{ModConstants.PLAYER_PACT_TERRITORY_COST}T+{ModConstants.PLAYER_PACT_SC_COST}SC";
                            _pactSlotSelectBtns[i].GetComponentInChildren<Text>().text = "Create";
                            _pactSlotDeleteBtns[i].gameObject.SetActive(false);
                        }
                    }
                    else if (pact != null)
                    {
                        _pactSlotNameTexts[i].text = pact.DisplayName;
                        int totalMembers = pact.MemberIds.Count + (pact.LeaderGangId >= 0 ? 1 : 0);
                        _pactSlotMemberTexts[i].text = $"{totalMembers} gang(s)";

                        if (_pactEditorMode)
                        {
                            _pactSlotSelectBtns[i].GetComponentInChildren<Text>().text = isSelected ? "Active" : "Select";
                        }
                        else
                        {
                            // Main mode: Join button for AI pacts
                            bool isPlayerMember = (SaveData.PlayerJoinedPactIndex == i);
                            if (isPlayerMember)
                            {
                                _pactSlotSelectBtns[i].GetComponentInChildren<Text>().text = "Joined";
                            }
                            else
                            {
                                // Per-pact cooldown check
                                int lastAttemptDay;
                                SaveData.PactJoinCooldowns.TryGetValue(i, out lastAttemptDay);
                                int daysSince = (lastAttemptDay > 0) ? (now.days - lastAttemptDay) : 9999;
                                bool canAskThisPact = daysSince >= ModConstants.PACT_JOIN_COOLDOWN_DAYS;
                                _pactSlotSelectBtns[i].GetComponentInChildren<Text>().text = canAskThisPact ? "Join" : "Wait";
                            }
                        }
                        _pactSlotDeleteBtns[i].gameObject.SetActive(_pactEditorMode);
                    }
                    else
                    {
                        _pactSlotNameTexts[i].text = $"{ModConstants.PACT_COLOR_NAMES[i]} - Empty";
                        _pactSlotMemberTexts[i].text = "";
                        _pactSlotSelectBtns[i].GetComponentInChildren<Text>().text = _pactEditorMode ? "Create" : "--";
                        _pactSlotDeleteBtns[i].gameObject.SetActive(false);
                    }

                    // Highlight selected/joined slot
                    bool highlight = isSelected || (SaveData.PlayerJoinedPactIndex == i && !_pactEditorMode);
                    _pactSlotRows[i].GetComponent<Image>().color = highlight
                        ? new Color(0.25f, 0.2f, 0.15f, 0.95f)
                        : new Color(0.16f, 0.14f, 0.1f, 0.9f);

                    _pactSlotColorSwatches[i].color = ModConstants.PACT_COLORS[i];
                }

                // Update selection indicator
                if (_pactEditorMode && _selectedPactSlot >= 0)
                {
                    var activePact = SaveData.Pacts.FirstOrDefault(p => p.ColorIndex == _selectedPactSlot);
                    string name = activePact?.DisplayName ?? $"{ModConstants.PACT_COLOR_NAMES[_selectedPactSlot]} Alliance";
                    _selectedPactLabel.text = $"Editing: {name}";
                    _selectedPactLabel.color = ModConstants.PACT_COLORS[_selectedPactSlot];
                }
                else if (playerInAIPact)
                {
                    var joinedPact = SaveData.Pacts.FirstOrDefault(p => p.ColorIndex == SaveData.PlayerJoinedPactIndex);
                    _selectedPactLabel.text = $"Member of: {joinedPact?.DisplayName ?? "Unknown Pact"}";
                    _selectedPactLabel.color = ModConstants.PACT_COLORS[SaveData.PlayerJoinedPactIndex];
                }
                else
                {
                    _selectedPactLabel.text = _pactEditorMode
                        ? "Select a pact slot to edit"
                        : "Join an AI pact or create your own";
                    _selectedPactLabel.color = new Color(0.7f, 0.7f, 0.6f);
                }
            }

            private static void OnSelectPactSlot(int slotIndex)
            {
                int playerSlot = ModConstants.PLAYER_PACT_SLOT;
                var existingPact = SaveData.Pacts.FirstOrDefault(p => p.ColorIndex == slotIndex);

                if (_pactEditorMode)
                {
                    // Editor mode: create/select pact for editing
                    if (existingPact == null && slotIndex != playerSlot)
                    {
                        int pactIdNum = SaveData.NextPactId++;
                        var newPact = new AlliancePact
                        {
                            PactId = $"pact_{pactIdNum}",
                            PactName = $"{ModConstants.PACT_COLOR_NAMES[slotIndex]} Alliance",
                            ColorIndex = slotIndex,
                            LeaderGangId = -1,
                            MemberIds = new List<int>(),
                            SharedColor = ModConstants.PACT_COLORS[slotIndex],
                            Formed = G.GetNow(),
                            IsPending = true
                        };
                        SaveData.Pacts.Add(newPact);
                        Debug.Log($"[GameplayTweaks] Created pact slot: {newPact.PactName}");
                    }
                    else if (existingPact == null && slotIndex == playerSlot)
                    {
                        // Player pact creation - requires 24 territory + 2 SC
                        if (!TryCreatePlayerPact()) return;
                    }
                    _selectedPactSlot = (_selectedPactSlot == slotIndex) ? -1 : slotIndex;
                }
                else
                {
                    // Main mode: AI slots = Join, Player slot = Create
                    if (slotIndex == playerSlot)
                    {
                        if (existingPact == null)
                        {
                            if (!TryCreatePlayerPact()) return;
                        }
                        _selectedPactSlot = (_selectedPactSlot == slotIndex) ? -1 : slotIndex;
                    }
                    else if (existingPact != null)
                    {
                        // Join an AI pact
                        if (SaveData.PlayerJoinedPactIndex == slotIndex)
                        {
                            // Already a member - do nothing
                            return;
                        }

                        SimTime now = G.GetNow();
                        int lastAttemptDay;
                        SaveData.PactJoinCooldowns.TryGetValue(slotIndex, out lastAttemptDay);
                        int daysSince = (lastAttemptDay > 0) ? (now.days - lastAttemptDay) : 9999;
                        if (daysSince < ModConstants.PACT_JOIN_COOLDOWN_DAYS)
                        {
                            int daysLeft = ModConstants.PACT_JOIN_COOLDOWN_DAYS - daysSince;
                            if (_selectedPactLabel != null)
                            {
                                _selectedPactLabel.text = $"Must wait {daysLeft} more days before asking {existingPact.DisplayName}!";
                                _selectedPactLabel.color = new Color(0.9f, 0.3f, 0.3f);
                            }
                            return;
                        }

                        SaveData.PactJoinCooldowns[slotIndex] = now.days;
                        float acceptance = CalculateJoinAcceptance(existingPact);
                        bool accepted = SharedRng.NextDouble() < acceptance;

                        if (accepted)
                        {
                            // If player was in another AI pact, leave it
                            if (SaveData.PlayerJoinedPactIndex >= 0)
                                LeaveCurrentAIPact();

                            // If player had own pact, leave it and go to war
                            var playerPact = SaveData.Pacts.FirstOrDefault(p => p.ColorIndex == playerSlot);
                            if (playerPact != null)
                            {
                                LeavePlayerPactForWar(playerPact);
                            }

                            SaveData.PlayerJoinedPactIndex = slotIndex;
                            RefreshPactCache();
                            TerritoryColorPatch.RefreshAllTerritoryColors();
                            Debug.Log($"[GameplayTweaks] Player joined {existingPact.DisplayName}!");
                            if (_selectedPactLabel != null)
                            {
                                _selectedPactLabel.text = $"Joined {existingPact.DisplayName}!";
                                _selectedPactLabel.color = new Color(0.3f, 0.9f, 0.3f);
                            }
                            if (_myPactBtnGo != null) _myPactBtnGo.SetActive(true);
                        }
                        else
                        {
                            Debug.Log($"[GameplayTweaks] {existingPact.DisplayName} rejected player!");
                            if (_selectedPactLabel != null)
                            {
                                _selectedPactLabel.text = $"{existingPact.DisplayName} rejected your request!";
                                _selectedPactLabel.color = new Color(0.9f, 0.3f, 0.3f);
                            }
                        }
                    }
                }
                RefreshGangPactsList();
            }

            private static bool TryCreatePlayerPact()
            {
                int playerSlot = ModConstants.PLAYER_PACT_SLOT;
                PlayerInfo human = G.GetHumanPlayer();
                if (human == null) return false;

                int territories = 0;
                int scLevel = 0;
                try
                {
                    var terr = human.territory;
                    if (terr != null)
                    {
                        var countProp = terr.GetType().GetProperty("Count") ?? terr.GetType().GetProperty("count");
                        if (countProp != null) territories = Convert.ToInt32(countProp.GetValue(terr));
                        else
                        {
                            var ownedProp = terr.GetType().GetProperty("OwnedCount");
                            if (ownedProp != null) territories = Convert.ToInt32(ownedProp.GetValue(terr));
                        }
                    }
                    CrewAssignment bossAssign = human.crew.GetCrewForIndex(0);
                    if (bossAssign.IsValid)
                    {
                        Entity boss = bossAssign.GetPeep();
                        if (boss != null)
                        {
                            var bossState = GetCrewStateOrNull(boss.Id);
                            if (bossState != null) scLevel = bossState.StreetCreditLevel;
                        }
                    }
                }
                catch { }

                if (territories < ModConstants.PLAYER_PACT_TERRITORY_COST || scLevel < ModConstants.PLAYER_PACT_SC_COST)
                {
                    if (_selectedPactLabel != null)
                    {
                        _selectedPactLabel.text = $"Need {ModConstants.PLAYER_PACT_TERRITORY_COST} territories (have {territories}) and {ModConstants.PLAYER_PACT_SC_COST} SC levels (have {scLevel})!";
                        _selectedPactLabel.color = new Color(0.9f, 0.3f, 0.3f);
                    }
                    return false;
                }

                // If player was in an AI pact, leave and go to war
                if (SaveData.PlayerJoinedPactIndex >= 0)
                {
                    var oldPact = SaveData.Pacts.FirstOrDefault(p => p.ColorIndex == SaveData.PlayerJoinedPactIndex);
                    if (oldPact != null) LeavePlayerPactForWar(oldPact);
                    SaveData.PlayerJoinedPactIndex = -1;
                }

                int pactIdNum = SaveData.NextPactId++;
                var newPact = new AlliancePact
                {
                    PactId = $"pact_{pactIdNum}",
                    PactName = "Your Pact",
                    ColorIndex = playerSlot,
                    LeaderGangId = -1,
                    MemberIds = new List<int>(),
                    SharedColor = ModConstants.PACT_COLORS[playerSlot],
                    Formed = G.GetNow(),
                    IsPending = true
                };
                SaveData.Pacts.Add(newPact);
                SaveData.PlayerPactId = pactIdNum;

                // Auto-join: player automatically becomes part of their own pact
                SaveData.PlayerJoinedPactIndex = playerSlot;
                newPact.IsPending = false;
                RefreshPactCache();
                TerritoryColorPatch.RefreshAllTerritoryColors();
                LogGrapevine("PACT: Your outfit established a new alliance!");
                Debug.Log($"[GameplayTweaks] Created player pact - auto-joined!");

                if (_selectedPactLabel != null)
                {
                    _selectedPactLabel.text = "Your pact has been established!";
                    _selectedPactLabel.color = new Color(0.3f, 0.9f, 0.3f);
                }
                if (_myPactBtnGo != null) _myPactBtnGo.SetActive(true);
                return true;
            }

            private static float CalculateJoinAcceptance(AlliancePact pact)
            {
                // Base 40% + power bonus
                float acceptance = 0.40f;
                PlayerInfo human = G.GetHumanPlayer();
                if (human != null)
                {
                    int humanPower = CalculateGangPower(human);
                    // More powerful = higher chance
                    if (humanPower >= 100) acceptance += 0.20f;
                    else if (humanPower >= 50) acceptance += 0.10f;
                    // More members = harder to join
                    int members = pact.MemberIds.Count + (pact.LeaderGangId >= 0 ? 1 : 0);
                    if (members >= 3) acceptance -= 0.15f;
                }
                return Mathf.Clamp01(acceptance);
            }

            internal static void LeaveCurrentAIPact()
            {
                SaveData.PlayerJoinedPactIndex = -1;
                Debug.Log("[GameplayTweaks] Player left AI pact.");
            }

            internal static void LeavePlayerPactForWar(AlliancePact playerPact)
            {
                // All gangs in the old AI pact go to war with player
                Debug.Log($"[GameplayTweaks] Player left {playerPact.DisplayName} - gangs now hostile!");
                // Remove pact but don't clear war state (gangs are now enemies)
                SaveData.PlayerJoinedPactIndex = -1;
            }

            private static void OnDeletePactSlot(int slotIndex)
            {
                var pact = SaveData.Pacts.FirstOrDefault(p => p.ColorIndex == slotIndex);
                if (pact == null) return;

                SaveData.Pacts.Remove(pact);

                if (SaveData.PlayerPactId >= 0 && pact.PactId == $"pact_{SaveData.PlayerPactId}")
                    SaveData.PlayerPactId = -1;

                if (_selectedPactSlot == slotIndex)
                    _selectedPactSlot = -1;

                // Hide My Pact button if player pact deleted
                if (slotIndex == ModConstants.PLAYER_PACT_SLOT && _myPactBtnGo != null)
                    _myPactBtnGo.SetActive(false);

                Debug.Log($"[GameplayTweaks] Deleted pact: {pact.DisplayName}");
                TerritoryColorPatch.RefreshAllTerritoryColors();
                RefreshGangPactsList();
            }

            private static void RemoveGangFromPact(PlayerInfo gang, int slotIndex)
            {
                var pact = SaveData.Pacts.FirstOrDefault(p => p.ColorIndex == slotIndex);
                if (pact == null) return;

                int gangId = gang.PID.id;
                if (pact.LeaderGangId == gangId)
                {
                    // Transfer leadership to first member, or clear
                    if (pact.MemberIds.Count > 0)
                    {
                        pact.LeaderGangId = pact.MemberIds[0];
                        pact.MemberIds.RemoveAt(0);
                    }
                    else
                    {
                        pact.LeaderGangId = -1;
                        pact.IsPending = true;
                    }
                }
                else
                {
                    pact.MemberIds.Remove(gangId);
                }

                Debug.Log($"[GameplayTweaks] Removed {gang.social?.PlayerGroupName} from {pact.DisplayName}");
                TerritoryColorPatch.RefreshAllTerritoryColors();
                RefreshGangPactsList();
            }

            private static float CalculatePactAcceptance(PlayerInfo aiGang, PlayerInfo human)
            {
                // Base acceptance: 30%
                float acceptance = 0.30f;

                int humanPower = CalculateGangPower(human);
                int aiPower = CalculateGangPower(aiGang);

                // Stronger player gang = more likely to accept (up to +30%)
                if (humanPower > aiPower)
                    acceptance += Math.Min(0.30f, (humanPower - aiPower) / 200f);
                else
                    acceptance -= Math.Min(0.20f, (aiPower - humanPower) / 200f);

                // Larger AI gangs are more selective (-10% for each 10 crew over 10)
                int aiCrew = aiGang.crew?.LivingCrewCount ?? 0;
                if (aiCrew > 10)
                    acceptance -= (aiCrew - 10) * 0.01f;

                // Small gangs are more willing to join (+20% if crew < 5)
                if (aiCrew < 5)
                    acceptance += 0.20f;

                // Already in a pact = 0%
                if (GetPactForPlayer(aiGang.PID) != null)
                    acceptance = 0f;

                return Mathf.Clamp01(acceptance);
            }

            private static void RefreshGangPactsList()
            {
                if (_gangPactsContent == null) return;

                // Refresh the pact slot editor section
                RefreshPactSlots();

                // Clear existing gang browser entries
                _gangRenameRow = null;
                _gangRenameTarget = null;
                _gangRenameNameText = null;
                for (int i = _gangPactsContent.childCount - 1; i >= 0; i--)
                    GameObject.Destroy(_gangPactsContent.GetChild(i).gameObject);

                PlayerInfo human = G.GetHumanPlayer();
                if (human == null) return;

                // Update status text
                int totalActive = SaveData.Pacts.Count(p => p.LeaderGangId >= 0 || p.MemberIds.Count > 0);
                _gangPactsStatusText.text = $"{totalActive} active alliance(s) on the map";

                // Refresh tracker if stale, then list all tracked outfits (exclude cops)
                if (TrackedGangs.Count == 0) RefreshGangTracker();
                var aiGangs = TrackedGangs
                    .Where(p => p != null && p.crew != null && !p.crew.IsCrewDefeated && p.crew.LivingCrewCount > 0 && p.IsJustGang)
                    .OrderByDescending(p => CalculateGangPower(p))
                    .ToList();

                // Update tab label
                if (_pactTabLabel != null)
                    _pactTabLabel.text = _pactEditorMode ? "Editor" : "Main";

                // In editor mode, include the player gang in the browser
                if (_pactEditorMode)
                {
                    CreateGangPactEntry(human, human);
                }

                foreach (var gang in aiGangs)
                {
                    CreateGangPactEntry(gang, human);
                }
            }

            private static void CreateGangPactEntry(PlayerInfo gang, PlayerInfo human)
            {
                string gangName = gang.social?.PlayerGroupName ?? $"Gang #{gang.PID.id}";
                int power = CalculateGangPower(gang);
                int crewCount = gang.crew?.LivingCrewCount ?? 0;
                var gangPact = GetPactForPlayer(gang.PID);
                bool inAnyPact = gangPact != null;
                bool inSelectedPact = _selectedPactSlot >= 0 && gangPact != null && gangPact.ColorIndex == _selectedPactSlot;

                var entryGo = new GameObject("GP_Entry", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
                entryGo.transform.SetParent(_gangPactsContent, false);
                entryGo.GetComponent<LayoutElement>().minHeight = 45;

                if (inSelectedPact)
                    entryGo.GetComponent<Image>().color = new Color(0.15f, 0.22f, 0.15f, 0.9f);
                else if (inAnyPact)
                    entryGo.GetComponent<Image>().color = new Color(0.15f, 0.12f, 0.18f, 0.9f);
                else
                    entryGo.GetComponent<Image>().color = new Color(0.16f, 0.14f, 0.1f, 0.9f);

                var entryHlg = entryGo.AddComponent<HorizontalLayoutGroup>();
                entryHlg.padding = new RectOffset(6, 6, 4, 4);
                entryHlg.spacing = 4;
                entryHlg.childForceExpandWidth = false;
                entryHlg.childForceExpandHeight = true;

                // Color swatch if in a pact
                if (inAnyPact)
                {
                    var swatchGo = new GameObject("Swatch", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
                    swatchGo.transform.SetParent(entryGo.transform, false);
                    swatchGo.GetComponent<LayoutElement>().preferredWidth = 8;
                    swatchGo.GetComponent<Image>().color = gangPact.SharedColor;
                }

                // Gang info (name + details)
                var infoGo = new GameObject("Info", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
                infoGo.transform.SetParent(entryGo.transform, false);
                infoGo.GetComponent<LayoutElement>().flexibleWidth = 1;
                var infoVlg = infoGo.GetComponent<VerticalLayoutGroup>();
                infoVlg.spacing = 1;
                infoVlg.childForceExpandWidth = true;
                infoVlg.childForceExpandHeight = false;

                var nameText = CreateLabel(infoGo.transform, "Name", gangName, 12, FontStyle.Bold);
                nameText.color = inSelectedPact ? new Color(0.4f, 0.9f, 0.4f) : new Color(0.95f, 0.9f, 0.75f);
                nameText.GetComponent<LayoutElement>().minHeight = 16;

                int gangCash = 0;
                try { gangCash = (int)gang.finances.GetMoneyTotal(); } catch { }
                string status = inSelectedPact ? "(Member)" : inAnyPact ? $"({gangPact.DisplayName})" : "";
                string details = $"Crew: {crewCount} | Power: {power} | ${gangCash} {status}";
                var detailText = CreateLabel(infoGo.transform, "Details", details, 9, FontStyle.Normal);
                detailText.color = new Color(0.65f, 0.65f, 0.6f);
                detailText.GetComponent<LayoutElement>().minHeight = 14;

                // Right side action area
                if (_pactEditorMode)
                {
                    // EDITOR MODE: free add / remove
                    if (_selectedPactSlot < 0)
                    {
                        var hintLabel = CreateLabel(entryGo.transform, "Hint", "---", 10, FontStyle.Italic);
                        hintLabel.color = new Color(0.5f, 0.5f, 0.45f);
                        hintLabel.alignment = TextAnchor.MiddleCenter;
                        hintLabel.GetComponent<LayoutElement>().preferredWidth = 55;
                    }
                    else if (inSelectedPact)
                    {
                        // In selected pact - show Remove button
                        var rmBtnGo = new GameObject("RmBtn", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
                        rmBtnGo.transform.SetParent(entryGo.transform, false);
                        rmBtnGo.GetComponent<LayoutElement>().preferredWidth = 65;
                        rmBtnGo.GetComponent<LayoutElement>().minHeight = 28;
                        rmBtnGo.GetComponent<Image>().color = new Color(0.5f, 0.15f, 0.15f, 0.95f);
                        var rmBtn = rmBtnGo.GetComponent<Button>();
                        rmBtn.targetGraphic = rmBtnGo.GetComponent<Image>();
                        PlayerInfo capturedGang = gang;
                        int capturedSlot = _selectedPactSlot;
                        rmBtn.onClick.AddListener(() => RemoveGangFromPact(capturedGang, capturedSlot));
                        var rmTxtGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
                        rmTxtGo.transform.SetParent(rmBtnGo.transform, false);
                        var rmTxtRt = rmTxtGo.GetComponent<RectTransform>();
                        rmTxtRt.anchorMin = Vector2.zero; rmTxtRt.anchorMax = Vector2.one;
                        rmTxtRt.offsetMin = Vector2.zero; rmTxtRt.offsetMax = Vector2.zero;
                        var rmTxt = rmTxtGo.GetComponent<Text>();
                        rmTxt.text = "Remove";
                        rmTxt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                        rmTxt.fontSize = 10; rmTxt.color = Color.white;
                        rmTxt.alignment = TextAnchor.MiddleCenter;
                        rmTxt.fontStyle = FontStyle.Bold;
                    }
                    else if (inAnyPact)
                    {
                        // In a different pact
                        var pactLabel = CreateLabel(entryGo.transform, "OtherPact", gangPact.DisplayName, 10, FontStyle.Italic);
                        pactLabel.color = gangPact.SharedColor;
                        pactLabel.alignment = TextAnchor.MiddleCenter;
                        pactLabel.GetComponent<LayoutElement>().preferredWidth = 65;
                    }
                    else
                    {
                        // Not in any pact - show free Add button (no % roll)
                        var addBtnGo = new GameObject("AddBtn", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
                        addBtnGo.transform.SetParent(entryGo.transform, false);
                        addBtnGo.GetComponent<LayoutElement>().preferredWidth = 55;
                        addBtnGo.GetComponent<LayoutElement>().minHeight = 28;
                        addBtnGo.GetComponent<Image>().color = new Color(0.2f, 0.35f, 0.5f, 0.95f);
                        var addBtn = addBtnGo.GetComponent<Button>();
                        addBtn.targetGraphic = addBtnGo.GetComponent<Image>();
                        PlayerInfo capturedGang = gang;
                        int capturedSlot = _selectedPactSlot;
                        addBtn.onClick.AddListener(() => RequestPactWithGang(capturedGang, 1f, capturedSlot));
                        var addTxtGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
                        addTxtGo.transform.SetParent(addBtnGo.transform, false);
                        var addTxtRt = addTxtGo.GetComponent<RectTransform>();
                        addTxtRt.anchorMin = Vector2.zero; addTxtRt.anchorMax = Vector2.one;
                        addTxtRt.offsetMin = Vector2.zero; addTxtRt.offsetMax = Vector2.zero;
                        var addTxt = addTxtGo.GetComponent<Text>();
                        addTxt.text = "Add";
                        addTxt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                        addTxt.fontSize = 11; addTxt.color = Color.white;
                        addTxt.alignment = TextAnchor.MiddleCenter;
                        addTxt.fontStyle = FontStyle.Bold;
                    }

                    // Rename button - always shown in editor mode for all gangs
                    var renameBtnGo = new GameObject("RenameBtn", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
                    renameBtnGo.transform.SetParent(entryGo.transform, false);
                    renameBtnGo.GetComponent<LayoutElement>().preferredWidth = 55;
                    renameBtnGo.GetComponent<LayoutElement>().minHeight = 28;
                    renameBtnGo.GetComponent<Image>().color = new Color(0.35f, 0.3f, 0.2f, 0.95f);
                    var renameBtn = renameBtnGo.GetComponent<Button>();
                    renameBtn.targetGraphic = renameBtnGo.GetComponent<Image>();
                    PlayerInfo capturedGangForRename = gang;
                    Text capturedNameText = nameText;
                    renameBtn.onClick.AddListener(() => ShowGangRenameInput(capturedGangForRename, capturedNameText));
                    var renameTxtGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
                    renameTxtGo.transform.SetParent(renameBtnGo.transform, false);
                    var renameTxtRt = renameTxtGo.GetComponent<RectTransform>();
                    renameTxtRt.anchorMin = Vector2.zero; renameTxtRt.anchorMax = Vector2.one;
                    renameTxtRt.offsetMin = Vector2.zero; renameTxtRt.offsetMax = Vector2.zero;
                    var renameTxt = renameTxtGo.GetComponent<Text>();
                    renameTxt.text = "Rename";
                    renameTxt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                    renameTxt.fontSize = 10; renameTxt.color = Color.white;
                    renameTxt.alignment = TextAnchor.MiddleCenter;
                    renameTxt.fontStyle = FontStyle.Bold;
                }
                else if (_selectedPactSlot < 0)
                {
                    // MAIN MODE: No pact selected - show dash hint
                    var hintLabel = CreateLabel(entryGo.transform, "Hint", "---", 10, FontStyle.Italic);
                    hintLabel.color = new Color(0.5f, 0.5f, 0.45f);
                    hintLabel.alignment = TextAnchor.MiddleCenter;
                    hintLabel.GetComponent<LayoutElement>().preferredWidth = 55;
                }
                else if (inSelectedPact)
                {
                    // Gang is in the selected pact
                    var memberLabel = CreateLabel(entryGo.transform, "Member", "MEMBER", 11, FontStyle.Bold);
                    memberLabel.color = new Color(0.3f, 0.8f, 0.3f);
                    memberLabel.alignment = TextAnchor.MiddleCenter;
                    memberLabel.GetComponent<LayoutElement>().preferredWidth = 65;
                }
                else if (inAnyPact)
                {
                    // Gang is in a different pact
                    var pactLabel = CreateLabel(entryGo.transform, "OtherPact", gangPact.DisplayName, 10, FontStyle.Italic);
                    pactLabel.color = gangPact.SharedColor;
                    pactLabel.alignment = TextAnchor.MiddleCenter;
                    pactLabel.GetComponent<LayoutElement>().preferredWidth = 65;
                }
                else
                {
                    // MAIN MODE: Gang not in any pact, pact selected - show Invite with %
                    float acceptance = CalculatePactAcceptance(gang, human);
                    int pctInt = Mathf.RoundToInt(acceptance * 100f);
                    Color pctColor = pctInt >= 50 ? new Color(0.3f, 0.8f, 0.3f) : pctInt >= 25 ? new Color(0.9f, 0.7f, 0.2f) : new Color(0.8f, 0.3f, 0.3f);

                    var pctText = CreateLabel(entryGo.transform, "Pct", $"{pctInt}%", 12, FontStyle.Bold);
                    pctText.color = pctColor;
                    pctText.alignment = TextAnchor.MiddleCenter;
                    pctText.GetComponent<LayoutElement>().preferredWidth = 40;

                    var invBtnGo = new GameObject("InvBtn", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
                    invBtnGo.transform.SetParent(entryGo.transform, false);
                    invBtnGo.GetComponent<LayoutElement>().preferredWidth = 55;
                    invBtnGo.GetComponent<LayoutElement>().minHeight = 28;
                    invBtnGo.GetComponent<Image>().color = new Color(0.25f, 0.4f, 0.2f, 0.95f);
                    var invBtn = invBtnGo.GetComponent<Button>();
                    invBtn.targetGraphic = invBtnGo.GetComponent<Image>();

                    PlayerInfo capturedGang = gang;
                    float capturedAcceptance = acceptance;
                    int capturedSlot = _selectedPactSlot;
                    invBtn.onClick.AddListener(() => RequestPactWithGang(capturedGang, capturedAcceptance, capturedSlot));

                    var invTxtGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
                    invTxtGo.transform.SetParent(invBtnGo.transform, false);
                    var invTxtRt = invTxtGo.GetComponent<RectTransform>();
                    invTxtRt.anchorMin = Vector2.zero; invTxtRt.anchorMax = Vector2.one;
                    invTxtRt.offsetMin = Vector2.zero; invTxtRt.offsetMax = Vector2.zero;
                    var invTxt = invTxtGo.GetComponent<Text>();
                    invTxt.text = "Invite";
                    invTxt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                    invTxt.fontSize = 11; invTxt.color = Color.white;
                    invTxt.alignment = TextAnchor.MiddleCenter;
                    invTxt.fontStyle = FontStyle.Bold;
                }
            }

            private static void RequestPactWithGang(PlayerInfo aiGang, float acceptance, int slotIndex)
            {
                PlayerInfo human = G.GetHumanPlayer();
                if (human == null) return;

                // Find pact by color slot
                var pact = SaveData.Pacts.FirstOrDefault(p => p.ColorIndex == slotIndex);
                if (pact == null) return;

                // Roll for acceptance
                bool accepted = SharedRng.NextDouble() < acceptance;

                if (accepted)
                {
                    if (pact.LeaderGangId < 0)
                    {
                        // First member becomes leader
                        pact.LeaderGangId = aiGang.PID.id;
                    }
                    else
                    {
                        pact.MemberIds.Add(aiGang.PID.id);
                    }
                    pact.IsPending = false;
                    Debug.Log($"[GameplayTweaks] {aiGang.social?.PlayerGroupName} joined {pact.DisplayName}!");
                    TerritoryColorPatch.RefreshAllTerritoryColors();
                }
                else
                {
                    Debug.Log($"[GameplayTweaks] {aiGang.social?.PlayerGroupName} declined invite to {pact.DisplayName}.");
                }

                RefreshGangPactsList();
            }


            private static void OnHireFamily()
            {
                // Open the family hire popup instead of auto-hiring
                ToggleFamilyHirePopup();
            }

            private static void ToggleFamilyHirePopup()
            {
                if (_familyHirePopup == null) return;
                _familyHireVisible = !_familyHireVisible;
                _familyHirePopup.SetActive(_familyHireVisible);
                if (_familyHireVisible)
                {
                    _filterEthnicity = "All";
                    _filterTrait = "All";
                    _filterMinAge = 0;
                    _filterMaxAge = 100;
                    RefreshFamilyHireList();
                }
            }

            private static void CloseFamilyHirePopup()
            {
                _familyHireVisible = false;
                if (_familyHirePopup != null)
                    _familyHirePopup.SetActive(false);
            }

            private static readonly string[] ETHNICITY_FILTERS = { "All", "Irish", "Italian", "Jewish", "Chinese", "African", "German", "Polish" };
            private static readonly string[] TRAIT_FILTERS = { "All", "Strong", "Fast", "Smart", "Tough", "Lucky", "Charming" };
            private static int _ethFilterIndex = 0;
            private static int _traitFilterIndex = 0;

            private static void CycleEthnicityFilter()
            {
                _ethFilterIndex = (_ethFilterIndex + 1) % ETHNICITY_FILTERS.Length;
                _filterEthnicity = ETHNICITY_FILTERS[_ethFilterIndex];
                RefreshFamilyHireList();
            }

            private static void CycleTraitFilter()
            {
                _traitFilterIndex = (_traitFilterIndex + 1) % TRAIT_FILTERS.Length;
                _filterTrait = TRAIT_FILTERS[_traitFilterIndex];
                RefreshFamilyHireList();
            }

            private static void RefreshFamilyHireList()
            {
                if (_familyHireContent == null) return;

                // Clear existing entries
                for (int i = _familyHireContent.childCount - 1; i >= 0; i--)
                    GameObject.Destroy(_familyHireContent.GetChild(i).gameObject);

                if (_selectedPeep == null)
                {
                    _familyHireStatusText.text = "No crew member selected";
                    return;
                }

                // Get all relatives from ALL crew members, not just selected
                var allRelatives = new List<Entity>();
                PlayerCrew crew = G.GetHumanCrew();
                if (crew != null)
                {
                    foreach (var ca in crew.GetLiving())
                    {
                        Entity peep = ca.GetPeep();
                        if (peep != null)
                        {
                            foreach (var rel in FindAllRelatives(peep))
                            {
                                if (!allRelatives.Any(r => r.Id == rel.Id))
                                    allRelatives.Add(rel);
                            }
                        }
                    }
                }

                // Also add relatives of selected peep directly
                foreach (var rel in FindAllRelatives(_selectedPeep))
                {
                    if (!allRelatives.Any(r => r.Id == rel.Id))
                        allRelatives.Add(rel);
                }

                SimTime now = G.GetNow();

                // Apply filters
                _filteredRelatives = allRelatives.Where(r =>
                {
                    var pdata = r.data.person;
                    float age = pdata.GetAge(now).YearsFloat;

                    // Age filter
                    if (age < _filterMinAge || age > _filterMaxAge) return false;

                    // Ethnicity filter
                    if (_filterEthnicity != "All")
                    {
                        string ethName = pdata.eth.ToString();
                        if (!ethName.Equals(_filterEthnicity, StringComparison.OrdinalIgnoreCase))
                            return false;
                    }

                    // Trait filter - check if entity has trait (via crew stats or labels)
                    if (_filterTrait != "All")
                    {
                        string traitLower = _filterTrait.ToLower();
                        bool hasTrait = false;
                        try
                        {
                            var xp = r.data.agent.xp;
                            if (xp != null)
                            {
                                var roles = xp.GetType().GetField("roles", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                                if (roles != null)
                                {
                                    var roleList = roles.GetValue(xp) as System.Collections.IEnumerable;
                                    if (roleList != null)
                                    {
                                        foreach (var role in roleList)
                                        {
                                            if (role.ToString().ToLower().Contains(traitLower))
                                            { hasTrait = true; break; }
                                        }
                                    }
                                }
                                // Also check labels/traits
                                var traitsField = xp.GetType().GetField("traits", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                                if (traitsField != null)
                                {
                                    var traitsList = traitsField.GetValue(xp) as System.Collections.IEnumerable;
                                    if (traitsList != null)
                                    {
                                        foreach (var t in traitsList)
                                        {
                                            if (t.ToString().ToLower().Contains(traitLower))
                                            { hasTrait = true; break; }
                                        }
                                    }
                                }
                            }
                            // For the "All" filter we already returned early, so for specific traits
                            // if we can't find any trait info, still show them
                            if (!hasTrait)
                            {
                                // Check person data for ethnicity-related traits
                                string personStr = pdata.ToString().ToLower();
                                if (personStr.Contains(traitLower)) hasTrait = true;
                            }
                        }
                        catch { /* If trait detection fails, include them anyway */ hasTrait = true; }
                        if (!hasTrait) return false;
                    }

                    return true;
                }).OrderBy(r => r.data.person.FullName).ToList();

                // Update status text
                string filterInfo = $"Eth: {_filterEthnicity} | Trait: {_filterTrait} | Age: {_filterMinAge}-{_filterMaxAge}";
                _familyHireStatusText.text = $"{_filteredRelatives.Count} found | {filterInfo}";

                // Update filter button texts
                var filterRow1 = _familyHirePopup.transform.Find("FH_FilterRow1");
                if (filterRow1 != null)
                {
                    var ethBtn = filterRow1.Find("FH_EthFilter");
                    if (ethBtn != null) ethBtn.GetComponentInChildren<Text>().text = $"Eth: {_filterEthnicity}";
                    var traitBtn = filterRow1.Find("FH_TraitFilter");
                    if (traitBtn != null) traitBtn.GetComponentInChildren<Text>().text = $"Trait: {_filterTrait}";
                }
                var filterRow2 = _familyHirePopup.transform.Find("FH_FilterRow2");
                if (filterRow2 != null)
                {
                    var minDownBtn = filterRow2.Find("FH_AgeDown");
                    if (minDownBtn != null) minDownBtn.GetComponentInChildren<Text>().text = $"Min:{_filterMinAge}";
                    var maxUpBtn = filterRow2.Find("FH_MaxUp");
                    if (maxUpBtn != null) maxUpBtn.GetComponentInChildren<Text>().text = $"Max:{_filterMaxAge}";
                }

                // Create entry for each relative
                foreach (var relative in _filteredRelatives)
                {
                    CreateFamilyHireEntry(relative, now);
                }
            }

            private static void CreateFamilyHireEntry(Entity relative, SimTime now)
            {
                var pdata = relative.data.person;
                float age = pdata.GetAge(now).YearsFloat;
                string ethName = pdata.eth.ToString();
                string gender = pdata.g == Gender.F ? "F" : "M";

                // Try to get traits/role info
                string traitInfo = "";
                try
                {
                    var xp = relative.data.agent.xp;
                    if (xp != null)
                    {
                        var crewRole = xp.GetCrewRole();
                        string roleStr = crewRole.ToString();
                        if (!string.IsNullOrEmpty(roleStr) && roleStr != "None" && roleStr != "0")
                            traitInfo = roleStr;
                    }
                }
                catch { }

                // Entry: HorizontalLayoutGroup with [Info | Hire Button]
                var entryGo = new GameObject("FH_Entry", typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
                entryGo.transform.SetParent(_familyHireContent, false);
                entryGo.GetComponent<LayoutElement>().minHeight = 52;
                entryGo.GetComponent<Image>().color = new Color(0.18f, 0.16f, 0.12f, 0.9f);
                var hlg = entryGo.GetComponent<HorizontalLayoutGroup>();
                hlg.padding = new RectOffset(6, 4, 3, 3);
                hlg.spacing = 4;
                hlg.childForceExpandWidth = false;
                hlg.childForceExpandHeight = true;

                // Left side: info (name + details)
                var infoGo = new GameObject("Info", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
                infoGo.transform.SetParent(entryGo.transform, false);
                infoGo.GetComponent<LayoutElement>().flexibleWidth = 1;
                var infoVlg = infoGo.GetComponent<VerticalLayoutGroup>();
                infoVlg.spacing = 1;
                infoVlg.childForceExpandWidth = true;
                infoVlg.childForceExpandHeight = false;

                // Name
                var nameText = CreateLabel(infoGo.transform, "Name", pdata.FullName, 12, FontStyle.Bold);
                nameText.color = new Color(0.95f, 0.9f, 0.75f);
                nameText.GetComponent<LayoutElement>().minHeight = 16;

                // Details: age, gender, ethnicity
                string details = $"Age: {(int)age} | {gender} | {ethName}";
                var detailText = CreateLabel(infoGo.transform, "Details", details, 10, FontStyle.Normal);
                detailText.color = new Color(0.7f, 0.7f, 0.65f);
                detailText.GetComponent<LayoutElement>().minHeight = 13;

                // Traits line (always shown, even if empty)
                string traitsDisplay = !string.IsNullOrEmpty(traitInfo) ? $"Role: {traitInfo}" : "No role";
                var traitText = CreateLabel(infoGo.transform, "Traits", traitsDisplay, 9, FontStyle.Italic);
                traitText.color = !string.IsNullOrEmpty(traitInfo)
                    ? new Color(0.85f, 0.75f, 0.4f)
                    : new Color(0.5f, 0.5f, 0.45f);
                traitText.GetComponent<LayoutElement>().minHeight = 12;

                // Right side: Hire button
                var hireBtnGo = new GameObject("HireBtn", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
                hireBtnGo.transform.SetParent(entryGo.transform, false);
                hireBtnGo.GetComponent<LayoutElement>().preferredWidth = 50;
                hireBtnGo.GetComponent<LayoutElement>().minHeight = 40;
                hireBtnGo.GetComponent<Image>().color = new Color(0.2f, 0.45f, 0.2f, 0.95f);

                var hireBtn = hireBtnGo.GetComponent<Button>();
                hireBtn.targetGraphic = hireBtnGo.GetComponent<Image>();

                Entity capturedRelative = relative;
                hireBtn.onClick.AddListener(() => HireSpecificRelative(capturedRelative));

                var hireTxtGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
                hireTxtGo.transform.SetParent(hireBtnGo.transform, false);
                var hireTxtRt = hireTxtGo.GetComponent<RectTransform>();
                hireTxtRt.anchorMin = Vector2.zero; hireTxtRt.anchorMax = Vector2.one;
                hireTxtRt.offsetMin = Vector2.zero; hireTxtRt.offsetMax = Vector2.zero;
                var hireTxt = hireTxtGo.GetComponent<Text>();
                hireTxt.text = "Hire";
                hireTxt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                hireTxt.fontSize = 12; hireTxt.color = Color.white;
                hireTxt.alignment = TextAnchor.MiddleCenter;
                hireTxt.fontStyle = FontStyle.Bold;
            }

            private static void HireSpecificRelative(Entity relative)
            {
                if (relative == null) return;

                PlayerCrew crew = G.GetHumanCrew();
                if (crew == null)
                {
                    Debug.LogWarning("[GameplayTweaks] HireSpecificRelative: Could not get player crew");
                    return;
                }

                if (!crew.CanAddCrew(1))
                {
                    Debug.Log("[GameplayTweaks] Crew is full, cannot hire more");
                    return;
                }

                try
                {
                    Entity sponsor = _selectedPeep;
                    // If no selected peep, find any crew member related to this person
                    if (sponsor == null)
                    {
                        foreach (var ca in crew.GetLiving())
                        {
                            Entity peep = ca.GetPeep();
                            if (peep != null)
                            {
                                var rels = G.GetRels()?.GetListOrNull(peep.Id);
                                if (rels != null)
                                {
                                    foreach (Relationship rel in rels.data)
                                    {
                                        if (rel.to == relative.Id)
                                        {
                                            sponsor = peep;
                                            break;
                                        }
                                    }
                                }
                                if (sponsor != null) break;
                            }
                        }
                    }

                    crew.HireNewCrewMemberUnassigned(relative, sponsor ?? _selectedPeep);
                    Debug.Log($"[GameplayTweaks] Hired {relative.data.person.FullName} as new crew member");

                    // Refresh the list to remove hired relative
                    RefreshFamilyHireList();
                    RefreshHandlerUI();
                }
                catch (Exception e)
                {
                    Debug.LogError($"[GameplayTweaks] Failed to hire relative: {e}");
                }
            }

            private static void OnVacation()
            {
                if (_selectedPeep == null) return;
                var state = GetOrCreateCrewState(_selectedPeep.Id);
                if (state == null || state.OnVacation || state.VacationPending) return;

                // Vacation costs $1500
                const int VACATION_COST = 1500;
                PlayerInfo human = G.GetHumanPlayer();
                if (human == null) return;

                try
                {
                    human.finances.DoChangeMoneyOnSafehouse(new Price(-VACATION_COST), MoneyReason.Other);
                }
                catch { return; }

                // Mark as pending - will leave on next turn start
                state.VacationPending = true;
                state.VacationDuration = ModConstants.VACATION_DAYS;
                state.HappinessValue = 1f; // Max happiness
                state.TurnsUnhappy = 0;

                Debug.Log($"[GameplayTweaks] {_selectedPeep.data.person.FullName} will go on vacation ($1500)");
                RefreshHandlerUI();
            }

            private static void OnGift()
            {
                if (_selectedPeep == null) return;
                var state = GetOrCreateCrewState(_selectedPeep.Id);
                if (state == null) return;

                int cost = ModConstants.GIFT_BASE_COST;
                PlayerInfo human = G.GetHumanPlayer();
                if (human == null) return;

                try
                {
                    human.finances.DoChangeMoneyOnSafehouse(new Price(-cost), MoneyReason.Other);
                    state.HappinessValue = Mathf.Clamp01(state.HappinessValue + 0.2f);
                    state.TurnsUnhappy = 0;
                }
                catch { }
                RefreshHandlerUI();
            }

            private static void OnToggleRename()
            {
                if (_renameRow == null || _selectedPeep == null) return;
                bool show = !_renameRow.activeSelf;
                _renameRow.SetActive(show);
                if (show)
                {
                    try
                    {
                        _inputFirstName.text = _selectedPeep.data.person.first ?? "";
                        _inputLastName.text = _selectedPeep.data.person.last ?? "";
                    }
                    catch { }
                }
            }

            private static void OnSaveRename()
            {
                if (_selectedPeep == null || _inputFirstName == null || _inputLastName == null) return;
                string newFirst = _inputFirstName.text?.Trim();
                string newLast = _inputLastName.text?.Trim();
                if (string.IsNullOrEmpty(newFirst) || string.IsNullOrEmpty(newLast)) return;

                try
                {
                    var person = _selectedPeep.data.person;
                    person.first = newFirst;
                    person.last = newLast;

                    // Clear cached name fields so FullName/ShortName regenerate
                    var personType = person.GetType();
                    foreach (string cacheName in new[] { "_fullname", "_shortname", "_fullName", "_shortName" })
                    {
                        var field = personType.GetField(cacheName, BindingFlags.NonPublic | BindingFlags.Instance);
                        if (field != null && field.FieldType == typeof(string))
                        {
                            field.SetValue(person, null);
                        }
                    }

                    _renameRow.SetActive(false);
                    RefreshHandlerUI();
                }
                catch (Exception e) { Debug.LogError($"[GameplayTweaks] Rename failed: {e}"); }
            }

            private static void OnAgeUp()
            {
                if (_selectedPeep == null) return;

                try
                {
                    var person = _selectedPeep.data.person;
                    string name = person.FullName;

                    // Subtract 365 days from born to make them 1 year older
                    int origDays = (int)person.born.days;
                    person.born = new SimTime(origDays - 365);

                    // Find family members via RelationshipTracker and age them too
                    int familyAged = 0;
                    RelationshipTracker rels = G.GetRels();
                    if (rels != null)
                    {
                        RelationshipList relList = rels.GetListOrNull(_selectedPeep.Id);
                        if (relList != null)
                        {
                            foreach (Relationship rel in relList.data)
                            {
                                if (rel.type != RelationshipType.Child && rel.type != RelationshipType.Sibling &&
                                    rel.type != RelationshipType.Spouse && rel.type != RelationshipType.Mother &&
                                    rel.type != RelationshipType.Father && rel.type != RelationshipType.Cousin)
                                    continue;

                                Entity relative = rel.to.FindEntity();
                                if (relative == null) continue;

                                try
                                {
                                    var relPerson = relative.data.person;
                                    int relDays = (int)relPerson.born.days;
                                    relPerson.born = new SimTime(relDays - 365);
                                    familyAged++;
                                }
                                catch { }
                            }
                        }
                    }

                    float newAge = person.GetAge(G.GetNow()).YearsFloat;
                    string familyMsg = familyAged > 0 ? $" ({familyAged} family members also aged)" : "";
                    LogGrapevine($"CREW: {name}'s family aged by 1 year (now {(int)newAge}){familyMsg}");
                    RefreshHandlerUI();
                }
                catch (Exception e) { Debug.LogError($"[GameplayTweaks] Age up failed: {e}"); }
            }

            private static void OnPepTalk()
            {
                if (_selectedPeep == null) return;
                var state = GetOrCreateCrewState(_selectedPeep.Id);
                if (state == null) return;

                int currentDay = (int)G.GetNow().days;
                if (currentDay != _pepTalkLastTurnDay)
                {
                    _pepTalkUsesThisTurn = 0;
                    _pepTalkLastTurnDay = currentDay;
                }
                if (_pepTalkUsesThisTurn >= 3) return;

                _pepTalkUsesThisTurn++;
                state.HappinessValue = Mathf.Clamp01(state.HappinessValue + 0.05f);
                RefreshHandlerUI();
            }

            private static void OnToggleNickname()
            {
                if (_nicknameRow == null || _selectedPeep == null) return;
                bool show = !_nicknameRow.activeSelf;
                _nicknameRow.SetActive(show);
                if (show)
                {
                    try { _inputNickname.text = _selectedPeep.data.person.nickname ?? ""; }
                    catch { _inputNickname.text = ""; }
                }
                // Hide rename row if showing nickname
                if (show && _renameRow != null && _renameRow.activeSelf)
                    _renameRow.SetActive(false);
                RefreshHandlerUI();
            }

            private static void OnSaveNickname()
            {
                if (_selectedPeep == null || _inputNickname == null) return;
                string nick = _inputNickname.text?.Trim();
                if (string.IsNullOrEmpty(nick)) return;

                try
                {
                    var setNickMethod = _selectedPeep.data.person.GetType().GetMethod("SetNickname",
                        BindingFlags.Public | BindingFlags.Instance);
                    if (setNickMethod != null)
                        setNickMethod.Invoke(_selectedPeep.data.person, new object[] { nick });
                    else
                        _selectedPeep.data.person.nickname = nick;

                    _nicknameRow.SetActive(false);
                    RefreshHandlerUI();
                }
                catch (Exception e) { Debug.LogError($"[GameplayTweaks] Save nickname failed: {e}"); }
            }

            private static void OnClearNickname()
            {
                if (_selectedPeep == null) return;

                try
                {
                    var setNickMethod = _selectedPeep.data.person.GetType().GetMethod("SetNickname",
                        BindingFlags.Public | BindingFlags.Instance);
                    if (setNickMethod != null)
                        setNickMethod.Invoke(_selectedPeep.data.person, new object[] { "" });
                    else
                        _selectedPeep.data.person.nickname = "";

                    // Clear cached names
                    var personType = _selectedPeep.data.person.GetType();
                    foreach (string cacheName in new[] { "_fullname", "_shortname", "_fullName", "_shortName" })
                    {
                        var field = personType.GetField(cacheName, BindingFlags.NonPublic | BindingFlags.Instance);
                        if (field != null && field.FieldType == typeof(string))
                            field.SetValue(_selectedPeep.data.person, null);
                    }

                    _nicknameRow.SetActive(false);
                    RefreshHandlerUI();
                }
                catch (Exception e) { Debug.LogError($"[GameplayTweaks] Clear nickname failed: {e}"); }
            }

            private static void OnBribeMayor()
            {
                // Global mayor bribe - helps ALL crew on ALL fed cases
                if (_globalMayorBribeActive) return;

                PlayerInfo human = G.GetHumanPlayer();
                try
                {
                    human.finances.DoChangeMoneyOnSafehouse(new Price(-ModConstants.MAYOR_BRIBE_COST), MoneyReason.Other);
                    _globalMayorBribeActive = true;
                    _globalMayorBribeExpireDay = G.GetNow().days + ModConstants.MAYOR_BRIBE_DURATION_DAYS;
                    Debug.Log("[GameplayTweaks] Mayor bribed! All crew benefit for 6 months.");
                }
                catch { }
                RefreshHandlerUI();
            }

            private static void OnBribeJudge()
            {
                if (_selectedPeep == null) return;
                var state = GetOrCreateCrewState(_selectedPeep.Id);
                if (state == null || state.JudgeBribeActive) return;

                int cost = GetBribeCost(state.WantedLevel) * 2;
                PlayerInfo human = G.GetHumanPlayer();
                try
                {
                    human.finances.DoChangeMoneyOnSafehouse(new Price(-cost), MoneyReason.Other);
                    state.JudgeBribeActive = true; // Lasts until trial ends (no expiry)
                }
                catch { }
                RefreshHandlerUI();
            }

            private static void OnPayLawyer()
            {
                if (_selectedPeep == null) return;
                if (!JailSystem.IsInJail(_selectedPeep.Id)) return;

                var state = GetOrCreateCrewState(_selectedPeep.Id);
                if (state == null) return;

                const int LAWYER_COST = 1000;
                PlayerInfo human = G.GetHumanPlayer();
                if (human == null) return;

                try
                {
                    human.finances.DoChangeMoneyOnSafehouse(new Price(-LAWYER_COST), MoneyReason.Other);
                    JailSystem.PayLawyerRetainer(_selectedPeep.Id, LAWYER_COST);
                    Debug.Log($"[GameplayTweaks] Paid ${LAWYER_COST} for lawyer for {_selectedPeep.data.person.FullName}");
                }
                catch (Exception e) { Debug.LogError($"[GameplayTweaks] OnPayLawyer failed: {e}"); }
                RefreshHandlerUI();
            }

            private static void OnOpenBoozeSell()
            {
                if (_selectedPeep == null) return;
                if (!JailSystem.IsInJail(_selectedPeep.Id)) return;

                var state = GetOrCreateCrewState(_selectedPeep.Id);
                if (state == null) return;

                int currentDay = (int)G.GetNow().days;
                bool canSell = state.LastBoozeSellTurn == 0 || (currentDay - state.LastBoozeSellTurn) >= 28;
                if (!canSell) return;

                // Create popup if needed (lazy init)
                if (_boozeSellPopup == null)
                {
                    var go = _goField?.GetValue(_popupInstance) as GameObject;
                    if (go == null) return;
                    CreateBoozeSellPopup(go);
                }

                _boozeSellVisible = !_boozeSellVisible;
                _boozeSellPopup.SetActive(_boozeSellVisible);
                if (_boozeSellVisible)
                    PopulateBoozeSellPopup();
            }

            private static void CreateBoozeSellPopup(GameObject parent)
            {
                _boozeSellPopup = new GameObject("BoozeSellPopup", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
                _boozeSellPopup.transform.SetParent(parent.transform, false);

                var rt = _boozeSellPopup.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(380, 400);
                rt.anchoredPosition = new Vector2(200, 0);

                var bgImg = _boozeSellPopup.GetComponent<Image>();
                bgImg.color = new Color(0.1f, 0.08f, 0.12f, 0.98f);

                var vlg = _boozeSellPopup.GetComponent<VerticalLayoutGroup>();
                vlg.padding = new RectOffset(8, 8, 8, 8);
                vlg.spacing = 4;
                vlg.childForceExpandWidth = true;
                vlg.childForceExpandHeight = false;

                // Header
                var headerRow = CreateHorizontalRow(_boozeSellPopup.transform, "BS_Header");
                headerRow.GetComponent<LayoutElement>().minHeight = 30;
                CreateLabel(headerRow.transform, "BS_Title", "Sell Booze (3x Price)", 13, FontStyle.Bold);
                var closeBtn = CreateButton(headerRow.transform, "BS_Close", "X", () => { _boozeSellVisible = false; _boozeSellPopup.SetActive(false); });
                closeBtn.GetComponent<LayoutElement>().preferredWidth = 30;

                // Status text
                _boozeSellStatusText = CreateLabel(_boozeSellPopup.transform, "BS_Status", "Scanning safehouse...", 10, FontStyle.Italic);

                // Sell All button
                var sellAllRow = CreateHorizontalRow(_boozeSellPopup.transform, "BS_SellAllRow");
                CreateButton(sellAllRow.transform, "BS_SellAll", "Sell All Booze", OnSellAllBooze);

                // Scrollable item list
                var scrollArea = new GameObject("BS_ScrollArea", typeof(RectTransform), typeof(Image), typeof(ScrollRect), typeof(LayoutElement));
                scrollArea.transform.SetParent(_boozeSellPopup.transform, false);
                var scrollLE = scrollArea.GetComponent<LayoutElement>();
                scrollLE.minHeight = 260; scrollLE.flexibleHeight = 1;
                scrollArea.GetComponent<Image>().color = new Color(0.06f, 0.05f, 0.08f, 0.9f);

                var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
                viewport.transform.SetParent(scrollArea.transform, false);
                var viewportRt = viewport.GetComponent<RectTransform>();
                viewportRt.anchorMin = Vector2.zero; viewportRt.anchorMax = Vector2.one;
                viewportRt.offsetMin = new Vector2(2, 2); viewportRt.offsetMax = new Vector2(-2, -2);
                viewport.GetComponent<Image>().color = new Color(1, 1, 1, 0.01f);
                viewport.GetComponent<Mask>().showMaskGraphic = false;

                var content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
                content.transform.SetParent(viewport.transform, false);
                var contentRt = content.GetComponent<RectTransform>();
                contentRt.anchorMin = new Vector2(0, 1); contentRt.anchorMax = new Vector2(1, 1);
                contentRt.pivot = new Vector2(0.5f, 1);
                contentRt.sizeDelta = new Vector2(0, 0);
                content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                var contentVlg = content.GetComponent<VerticalLayoutGroup>();
                contentVlg.spacing = 3;
                contentVlg.childForceExpandWidth = true;
                contentVlg.childForceExpandHeight = false;
                contentVlg.padding = new RectOffset(4, 4, 4, 4);
                _boozeSellContent = content.transform;

                var scroll = scrollArea.GetComponent<ScrollRect>();
                scroll.viewport = viewportRt;
                scroll.content = contentRt;
                scroll.horizontal = false;
                scroll.vertical = true;
                scroll.scrollSensitivity = 30f;
                scroll.movementType = ScrollRect.MovementType.Clamped;

                _boozeSellPopup.SetActive(false);
            }

            private static void PopulateBoozeSellPopup()
            {
                // Clear previous entries
                if (_boozeSellContent != null)
                {
                    for (int i = _boozeSellContent.childCount - 1; i >= 0; i--)
                        GameObject.Destroy(_boozeSellContent.GetChild(i).gameObject);
                }

                try
                {
                    PlayerInfo human = G.GetHumanPlayer();
                    if (human == null) { _boozeSellStatusText.text = "No player found"; return; }

                    var safehouse = human.territory.Safehouse;
                    if (safehouse.IsNotValid) { _boozeSellStatusText.text = "No safehouse found"; return; }

                    var safeEntity = safehouse.FindEntity();
                    if (safeEntity == null) { _boozeSellStatusText.text = "Safehouse entity not found"; return; }

                    var inv = ModulesUtil.GetInventory(safeEntity);
                    if (inv == null) { _boozeSellStatusText.text = "No inventory found"; return; }

                    int totalBoozeValue = 0;
                    int boozeCount = 0;

                    // Check each known booze type against safehouse inventory
                    foreach (var kvp in _boozePrices)
                    {
                        try
                        {
                            var label = new Label(kvp.Key);
                            var rq = inv.data.Get(label);
                            // Get quantity - try to extract int from Fixnum
                            int qty = 0;
                            try { qty = Convert.ToInt32(rq.qty); }
                            catch
                            {
                                try
                                {
                                    var qtyField = rq.qty.GetType().GetField("v") ?? rq.qty.GetType().GetField("_value") ?? rq.qty.GetType().GetField("raw");
                                    if (qtyField != null) qty = Convert.ToInt32(qtyField.GetValue(rq.qty));
                                }
                                catch { }
                            }
                            if (qty <= 0) continue;

                            int basePrice = kvp.Value;
                            int triplePrice = basePrice * 3;
                            int totalForItem = triplePrice * qty;
                            totalBoozeValue += totalForItem;
                            boozeCount++;

                            // Create entry row
                            CreateBoozeSellEntry(kvp.Key, qty, triplePrice, totalForItem, label);
                        }
                        catch { }
                    }

                    if (boozeCount == 0)
                        _boozeSellStatusText.text = "No booze in safehouse";
                    else
                        _boozeSellStatusText.text = $"{boozeCount} types found - Total value: ${totalBoozeValue}";
                }
                catch (Exception e)
                {
                    _boozeSellStatusText.text = "Error reading inventory";
                    Debug.LogError($"[GameplayTweaks] PopulateBoozeSellPopup: {e}");
                }
            }

            private static void CreateBoozeSellEntry(string itemName, int qty, int unitPrice, int totalPrice, Label label)
            {
                // Entry with HorizontalLayoutGroup: [Info | Sell Button]
                var entryGo = new GameObject("BS_Entry_" + itemName, typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
                entryGo.transform.SetParent(_boozeSellContent, false);
                entryGo.GetComponent<Image>().color = new Color(0.15f, 0.12f, 0.18f, 0.8f);
                var entryLE = entryGo.GetComponent<LayoutElement>();
                entryLE.minHeight = 40; entryLE.preferredHeight = 40;
                var hlg = entryGo.GetComponent<HorizontalLayoutGroup>();
                hlg.spacing = 4;
                hlg.childForceExpandWidth = false;
                hlg.childForceExpandHeight = true;
                hlg.padding = new RectOffset(4, 4, 2, 2);

                // Left side: item info (flex width)
                var infoGo = new GameObject("Info", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
                infoGo.transform.SetParent(entryGo.transform, false);
                infoGo.GetComponent<LayoutElement>().flexibleWidth = 1;
                var infoVlg = infoGo.GetComponent<VerticalLayoutGroup>();
                infoVlg.childForceExpandWidth = true;
                infoVlg.childForceExpandHeight = false;
                infoVlg.spacing = 1;

                // Item name - capitalize and clean up label
                string displayName = itemName.Replace("-", " ");
                displayName = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(displayName);
                var nameText = CreateLabel(infoGo.transform, "Name", displayName, 11, FontStyle.Bold);
                nameText.GetComponent<LayoutElement>().minHeight = 14;
                nameText.GetComponent<LayoutElement>().preferredHeight = 14;

                // Qty and price info
                var detailText = CreateLabel(infoGo.transform, "Detail", $"x{qty} @ ${unitPrice}/ea = ${totalPrice}", 9, FontStyle.Normal);
                detailText.color = new Color(0.8f, 0.9f, 0.7f);
                detailText.GetComponent<LayoutElement>().minHeight = 12;
                detailText.GetComponent<LayoutElement>().preferredHeight = 12;

                // Right side: Sell button (fixed width)
                var sellBtnGo = new GameObject("SellBtn", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
                sellBtnGo.transform.SetParent(entryGo.transform, false);
                sellBtnGo.GetComponent<LayoutElement>().preferredWidth = 65;
                sellBtnGo.GetComponent<LayoutElement>().minWidth = 65;
                sellBtnGo.GetComponent<Image>().color = new Color(0.2f, 0.5f, 0.2f, 0.95f);
                var sellBtn = sellBtnGo.GetComponent<Button>();
                sellBtn.targetGraphic = sellBtnGo.GetComponent<Image>();

                string capturedKey = itemName;
                int capturedBasePrice = unitPrice / 3; // store base price
                sellBtn.onClick.AddListener(() => SellBoozeItem(capturedKey, capturedBasePrice));

                var sellTextGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
                sellTextGo.transform.SetParent(sellBtnGo.transform, false);
                var strt = sellTextGo.GetComponent<RectTransform>();
                strt.anchorMin = Vector2.zero; strt.anchorMax = Vector2.one;
                strt.offsetMin = Vector2.zero; strt.offsetMax = Vector2.zero;
                var sellText = sellTextGo.GetComponent<Text>();
                sellText.text = $"Sell ${totalPrice}";
                sellText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                sellText.fontSize = 10; sellText.color = Color.white;
                sellText.alignment = TextAnchor.MiddleCenter;
            }

            private static void SellBoozeItem(string itemKey, int basePrice)
            {
                if (_selectedPeep == null) return;
                var state = GetOrCreateCrewState(_selectedPeep.Id);
                if (state == null) return;

                try
                {
                    PlayerInfo human = G.GetHumanPlayer();
                    if (human == null) return;

                    var safehouse = human.territory.Safehouse;
                    if (safehouse.IsNotValid) return;
                    var safeEntity = safehouse.FindEntity();
                    if (safeEntity == null) return;
                    var inv = ModulesUtil.GetInventory(safeEntity);
                    if (inv == null) return;

                    var label = new Label(itemKey);
                    // Remove all of this booze from inventory
                    int removed = 0;
                    try
                    {
                        var result = inv.TryRemoveResourcesUpToAll(label, 9999);
                        removed = Convert.ToInt32(result);
                    }
                    catch
                    {
                        // Fallback: try direct Increment via reflection
                        try
                        {
                            var rq = inv.data.Get(label);
                            int qty = Convert.ToInt32(rq.qty);
                            if (qty > 0)
                            {
                                // Use TryRemoveResourcesUpToAll with exact qty
                                inv.TryRemoveResourcesUpToAll(label, qty);
                                removed = qty;
                            }
                        }
                        catch (Exception e2) { Debug.LogError($"[GameplayTweaks] Booze remove fallback failed: {e2}"); }
                    }

                    if (removed <= 0)
                    {
                        Debug.Log($"[GameplayTweaks] No {itemKey} to sell");
                        PopulateBoozeSellPopup();
                        return;
                    }

                    int triplePrice = basePrice * 3;
                    int totalMoney = triplePrice * removed;
                    human.finances.DoChangeMoneyOnSafehouse(new Price(totalMoney), MoneyReason.Other);

                    // Set cooldown
                    state.LastBoozeSellTurn = (int)G.GetNow().days;

                    // Add street credit
                    state.StreetCreditProgress += 0.15f;
                    if (state.StreetCreditProgress >= 1f)
                    {
                        state.StreetCreditProgress -= 1f;
                        state.StreetCreditLevel++;
                    }

                    Debug.Log($"[GameplayTweaks] Sold {removed}x {itemKey} for ${totalMoney} (3x). Street credit +0.15");

                    // Refresh the popup to show updated inventory
                    PopulateBoozeSellPopup();
                    RefreshHandlerUI();
                }
                catch (Exception e) { Debug.LogError($"[GameplayTweaks] SellBoozeItem failed: {e}"); }
            }

            private static void OnSellAllBooze()
            {
                if (_selectedPeep == null) return;
                var state = GetOrCreateCrewState(_selectedPeep.Id);
                if (state == null) return;

                int currentDay = (int)G.GetNow().days;
                bool canSell = state.LastBoozeSellTurn == 0 || (currentDay - state.LastBoozeSellTurn) >= 28;
                if (!canSell) return;

                try
                {
                    PlayerInfo human = G.GetHumanPlayer();
                    if (human == null) return;

                    var safehouse = human.territory.Safehouse;
                    if (safehouse.IsNotValid) return;
                    var safeEntity = safehouse.FindEntity();
                    if (safeEntity == null) return;
                    var inv = ModulesUtil.GetInventory(safeEntity);
                    if (inv == null) return;

                    int totalMoney = 0;
                    int totalItems = 0;

                    foreach (var kvp in _boozePrices)
                    {
                        try
                        {
                            var label = new Label(kvp.Key);
                            int removed = 0;
                            try
                            {
                                var result = inv.TryRemoveResourcesUpToAll(label, 9999);
                                removed = Convert.ToInt32(result);
                            }
                            catch
                            {
                                try
                                {
                                    var rq = inv.data.Get(label);
                                    int qty = Convert.ToInt32(rq.qty);
                                    if (qty > 0)
                                    {
                                        inv.TryRemoveResourcesUpToAll(label, qty);
                                        removed = qty;
                                    }
                                }
                                catch { }
                            }

                            if (removed > 0)
                            {
                                totalMoney += kvp.Value * 3 * removed;
                                totalItems += removed;
                            }
                        }
                        catch { }
                    }

                    if (totalItems > 0)
                    {
                        human.finances.DoChangeMoneyOnSafehouse(new Price(totalMoney), MoneyReason.Other);
                        state.LastBoozeSellTurn = currentDay;
                        state.StreetCreditProgress += 0.15f;
                        if (state.StreetCreditProgress >= 1f)
                        {
                            state.StreetCreditProgress -= 1f;
                            state.StreetCreditLevel++;
                        }
                        Debug.Log($"[GameplayTweaks] Sold all booze: {totalItems} items for ${totalMoney}. Street credit +0.15");
                    }
                    else
                    {
                        Debug.Log("[GameplayTweaks] No booze found in safehouse to sell");
                    }

                    PopulateBoozeSellPopup();
                    RefreshHandlerUI();
                }
                catch (Exception e) { Debug.LogError($"[GameplayTweaks] OnSellAllBooze failed: {e}"); }
            }

            // UI Helpers
            internal static Text CreateLabel(Transform parent, string name, string text, int size, FontStyle style)
            {
                var go = new GameObject(name, typeof(RectTransform), typeof(Text), typeof(LayoutElement));
                go.transform.SetParent(parent, false);
                var le = go.GetComponent<LayoutElement>();
                le.minHeight = size + 8; le.preferredHeight = size + 8;
                var t = go.GetComponent<Text>();
                t.text = text;
                t.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                t.fontSize = size; t.color = Color.white; t.fontStyle = style;
                t.alignment = TextAnchor.MiddleLeft;
                return t;
            }

            internal static Button CreateButton(Transform parent, string name, string label, Action onClick)
            {
                var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
                go.transform.SetParent(parent, false);
                var le = go.GetComponent<LayoutElement>();
                le.minHeight = 28; le.preferredHeight = 28; le.flexibleWidth = 1;
                var img = go.GetComponent<Image>();
                img.color = new Color(0.3f, 0.25f, 0.2f, 0.95f);
                var btn = go.GetComponent<Button>();
                btn.targetGraphic = img;
                btn.onClick.AddListener(() => onClick());

                var textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
                textGo.transform.SetParent(go.transform, false);
                var trt = textGo.GetComponent<RectTransform>();
                trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
                trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;
                var t = textGo.GetComponent<Text>();
                t.text = label;
                t.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                t.fontSize = 11; t.color = Color.white;
                t.alignment = TextAnchor.MiddleCenter;
                return btn;
            }

            private static GameObject CreateStatBar(Transform parent, string name, Color fillColor, out Image fillImage)
            {
                var container = new GameObject(name, typeof(RectTransform), typeof(LayoutElement));
                container.transform.SetParent(parent, false);
                var le = container.GetComponent<LayoutElement>();
                le.minHeight = 20; le.preferredHeight = 20;

                var bg = new GameObject("Bg", typeof(RectTransform), typeof(Image));
                bg.transform.SetParent(container.transform, false);
                var bgRt = bg.GetComponent<RectTransform>();
                bgRt.anchorMin = Vector2.zero; bgRt.anchorMax = Vector2.one;
                bgRt.offsetMin = Vector2.zero; bgRt.offsetMax = Vector2.zero;
                bg.GetComponent<Image>().color = new Color(0.15f, 0.15f, 0.15f, 0.9f);

                var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
                fill.transform.SetParent(bg.transform, false);
                var fillRt = fill.GetComponent<RectTransform>();
                fillRt.anchorMin = Vector2.zero; fillRt.anchorMax = Vector2.one;
                fillRt.offsetMin = new Vector2(2, 2); fillRt.offsetMax = new Vector2(-2, -2);
                fillImage = fill.GetComponent<Image>();
                fillImage.color = fillColor;
                fillImage.type = Image.Type.Filled;
                fillImage.fillMethod = Image.FillMethod.Horizontal;
                fillImage.fillAmount = 0.5f;

                return container;
            }

            internal static GameObject CreateHorizontalRow(Transform parent, string name)
            {
                var go = new GameObject(name, typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
                go.transform.SetParent(parent, false);
                var le = go.GetComponent<LayoutElement>();
                le.minHeight = 30; le.preferredHeight = 30;
                var hlg = go.GetComponent<HorizontalLayoutGroup>();
                hlg.spacing = 5;
                hlg.childForceExpandWidth = true;
                hlg.childForceExpandHeight = true;
                return go;
            }

            private static List<Entity> FindAllRelatives(Entity peep)
            {
                var result = new List<Entity>();
                RelationshipTracker rels = G.GetRels();
                SimTime now = G.GetNow();
                if (rels == null) return result;

                RelationshipList relList = rels.GetListOrNull(peep.Id);
                if (relList == null) return result;

                foreach (Relationship rel in relList.data)
                {
                    if (rel.type != RelationshipType.Child && rel.type != RelationshipType.Sibling &&
                        rel.type != RelationshipType.Spouse && rel.type != RelationshipType.Mother &&
                        rel.type != RelationshipType.Father && rel.type != RelationshipType.Cousin)
                        continue;

                    Entity relative = rel.to.FindEntity();
                    if (relative == null) continue;
                    if (PlayerSocial.IsEligibleCrewMember(now, relative))
                        result.Add(relative);
                }
                return result;
            }

            private static void ShowGangRenameInput(PlayerInfo gang, Text nameLabel)
            {
                // If there's already a rename row, destroy it
                if (_gangRenameRow != null)
                {
                    UnityEngine.Object.Destroy(_gangRenameRow);
                    _gangRenameRow = null;
                }

                _gangRenameTarget = gang;
                _gangRenameNameText = nameLabel;
                string currentName = gang.social?.PlayerGroupName ?? "";

                // Create inline rename row below the gang entry
                var parent = nameLabel.transform.parent; // the Info VLG
                _gangRenameRow = new GameObject("RenameRow", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
                _gangRenameRow.transform.SetParent(parent, false);
                _gangRenameRow.GetComponent<LayoutElement>().minHeight = 24;
                var hlg = _gangRenameRow.GetComponent<HorizontalLayoutGroup>();
                hlg.spacing = 4;
                hlg.childForceExpandWidth = false;
                hlg.childForceExpandHeight = true;

                // InputField
                var inputGo = new GameObject("Input", typeof(RectTransform), typeof(Image), typeof(InputField), typeof(LayoutElement));
                inputGo.transform.SetParent(_gangRenameRow.transform, false);
                inputGo.GetComponent<LayoutElement>().flexibleWidth = 1;
                inputGo.GetComponent<LayoutElement>().minHeight = 22;
                inputGo.GetComponent<Image>().color = new Color(0.12f, 0.1f, 0.08f, 1f);
                _gangRenameInput = inputGo.GetComponent<InputField>();
                _gangRenameInput.characterLimit = 30;

                var inputTextGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
                inputTextGo.transform.SetParent(inputGo.transform, false);
                var inputTextRt = inputTextGo.GetComponent<RectTransform>();
                inputTextRt.anchorMin = Vector2.zero; inputTextRt.anchorMax = Vector2.one;
                inputTextRt.offsetMin = new Vector2(4, 0); inputTextRt.offsetMax = new Vector2(-4, 0);
                var inputText = inputTextGo.GetComponent<Text>();
                inputText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                inputText.fontSize = 11;
                inputText.color = Color.white;
                inputText.supportRichText = false;
                _gangRenameInput.textComponent = inputText;
                _gangRenameInput.text = currentName;

                // Save button
                var saveBtnGo = CreateButton(_gangRenameRow.transform, "SaveRename", "Save", OnSaveGangRename);
                saveBtnGo.GetComponent<LayoutElement>().preferredWidth = 45;
                saveBtnGo.GetComponent<Image>().color = new Color(0.2f, 0.4f, 0.2f, 0.95f);
            }

            private static void OnSaveGangRename()
            {
                if (_gangRenameTarget == null || _gangRenameInput == null) return;
                string newName = _gangRenameInput.text?.Trim();
                if (string.IsNullOrEmpty(newName)) return;

                try
                {
                    // Use ForceGroupNameIfValid to rename the gang
                    var social = _gangRenameTarget.social;
                    if (social != null)
                    {
                        var method = social.GetType().GetMethod("ForceGroupNameIfValid",
                            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        if (method != null)
                        {
                            method.Invoke(social, new object[] { newName });
                            Debug.Log($"[GameplayTweaks] Renamed gang to: {newName}");
                        }
                        else
                        {
                            // Fallback: set _data.social.groupname directly
                            var dataField = social.GetType().GetField("_data", BindingFlags.NonPublic | BindingFlags.Instance);
                            if (dataField != null)
                            {
                                var data = dataField.GetValue(social);
                                var socialField = data.GetType().GetField("social", BindingFlags.Public | BindingFlags.Instance);
                                if (socialField != null)
                                {
                                    var socialData = socialField.GetValue(data);
                                    var gnField = socialData.GetType().GetField("groupname", BindingFlags.Public | BindingFlags.Instance);
                                    if (gnField != null)
                                    {
                                        gnField.SetValue(socialData, newName);
                                        socialField.SetValue(data, socialData);
                                        dataField.SetValue(social, data);
                                        Debug.Log($"[GameplayTweaks] Renamed gang via fallback to: {newName}");
                                    }
                                }
                            }
                        }
                    }

                    // Update the name label in the UI
                    if (_gangRenameNameText != null)
                        _gangRenameNameText.text = newName;
                }
                catch (Exception e)
                {
                    Debug.LogError($"[GameplayTweaks] Gang rename failed: {e}");
                }

                // Clean up rename row
                if (_gangRenameRow != null)
                {
                    UnityEngine.Object.Destroy(_gangRenameRow);
                    _gangRenameRow = null;
                }
                _gangRenameTarget = null;
                _gangRenameNameText = null;
            }
        }

        // =====================================================================
        // 4b. Crew Outing Event
        // =====================================================================
        private static class CrewOutingEvent
        {
            public static void ShowOutingPrompt(GameObject ignored = null)
            {
                if (CrewRelationshipHandlerPatch._outingPopup != null)
                {
                    CrewRelationshipHandlerPatch._outingPopup.SetActive(true);
                    CrewRelationshipHandlerPatch._outingPopup.transform.SetAsLastSibling();
                    RefreshOutingText();
                    return;
                }

                var overlayCanvas = GetOrCreateOverlayCanvas();
                if (overlayCanvas == null)
                {
                    Debug.LogWarning("[GameplayTweaks] Could not create overlay canvas for outing popup");
                    return;
                }

                var popup = new GameObject("OutingEventPopup", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(Canvas), typeof(UnityEngine.UI.GraphicRaycaster));
                popup.transform.SetParent(overlayCanvas.transform, false);

                // Override sorting so it appears above everything
                var popCanvas = popup.GetComponent<Canvas>();
                popCanvas.overrideSorting = true;
                popCanvas.sortingOrder = 999;

                var rt = popup.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 0.7f);
                rt.anchorMax = new Vector2(0.5f, 0.7f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(380, 220);
                popup.GetComponent<Image>().color = new Color(0.1f, 0.08f, 0.06f, 0.98f);

                var vlg = popup.GetComponent<VerticalLayoutGroup>();
                vlg.padding = new RectOffset(14, 14, 12, 12);
                vlg.spacing = 8;
                vlg.childForceExpandWidth = true;
                vlg.childForceExpandHeight = false;

                // Border effect
                var outline = popup.AddComponent<Outline>();
                outline.effectColor = new Color(0.85f, 0.7f, 0.3f, 0.8f);
                outline.effectDistance = new Vector2(2, -2);

                var title = CrewRelationshipHandlerPatch.CreateLabel(popup.transform, "OE_Title", "-- Gang Meeting Proposal --", 15, FontStyle.Bold);
                title.alignment = TextAnchor.MiddleCenter;
                title.color = new Color(0.95f, 0.85f, 0.4f);

                CrewRelationshipHandlerPatch._outingText = CrewRelationshipHandlerPatch.CreateLabel(popup.transform, "OE_Desc", "", 12, FontStyle.Normal);
                CrewRelationshipHandlerPatch._outingText.alignment = TextAnchor.MiddleCenter;
                CrewRelationshipHandlerPatch._outingText.color = new Color(0.9f, 0.85f, 0.7f);
                RefreshOutingText();

                var btnRow = CrewRelationshipHandlerPatch.CreateHorizontalRow(popup.transform, "OE_Buttons");
                btnRow.GetComponent<LayoutElement>().minHeight = 38;

                var acceptBtn = CrewRelationshipHandlerPatch.CreateButton(btnRow.transform, "OE_Accept", "Accept", OnAcceptOuting);
                acceptBtn.GetComponent<Image>().color = new Color(0.2f, 0.45f, 0.2f, 0.95f);

                var denyBtn = CrewRelationshipHandlerPatch.CreateButton(btnRow.transform, "OE_Deny", "Deny", OnDenyOuting);
                denyBtn.GetComponent<Image>().color = new Color(0.45f, 0.2f, 0.2f, 0.95f);

                CrewRelationshipHandlerPatch._outingPopup = popup;
                popup.transform.SetAsLastSibling();
                Debug.Log("[GameplayTweaks] Outing event popup created and shown");
            }

            private static void RefreshOutingText()
            {
                if (CrewRelationshipHandlerPatch._outingText == null) return;
                PlayerCrew crew = G.GetHumanCrew();
                int crewCount = crew?.LivingCrewCount ?? 0;
                int cost = crewCount * 25;
                // Show available cash (clean from finances, dirty from inventory)
                int cleanCash = GetPlayerCleanCash();
                int dirtyCash = GetTotalDirtyCash();
                int total = cleanCash + dirtyCash;
                string cashInfo = dirtyCash > 0
                    ? $"Available: ${cleanCash} clean + ${dirtyCash} dirty = ${total}"
                    : $"Available: ${cleanCash}";
                CrewRelationshipHandlerPatch._outingText.text =
                    $"Your crew is calling a gang meeting!\n\nCost: ${cost} ($25 x {crewCount} members)\n{cashInfo}\nEffect: All crew happiness restored to full";
            }

            private static void OnAcceptOuting()
            {
                try
                {
                    PlayerInfo human = G.GetHumanPlayer();
                    if (human == null) return;
                    PlayerCrew crew = human.crew;
                    if (crew == null) return;

                    int crewCount = crew.LivingCrewCount;
                    int cost = crewCount * 25;

                    // Read clean cash from financial system, dirty from inventory
                    int cleanCash = GetPlayerCleanCash();
                    int dirtyCash = GetTotalDirtyCash();
                    int totalAvailable = cleanCash + dirtyCash;

                    if (totalAvailable < cost)
                    {
                        Debug.Log($"[GameplayTweaks] Can't afford gang meeting: need ${cost}, have ${totalAvailable} (clean=${cleanCash}, dirty=${dirtyCash})");
                        if (CrewRelationshipHandlerPatch._outingText != null)
                            CrewRelationshipHandlerPatch._outingText.text = $"Not enough money! Need ${cost}, have ${totalAvailable}";
                        return;
                    }

                    // Deduct: clean first (via finances), then dirty for remainder (via inventory)
                    int fromClean = Math.Min(cleanCash, cost);
                    if (fromClean > 0)
                        human.finances.DoChangeMoneyOnSafehouse(new Price(-fromClean), MoneyReason.Other);
                    int remaining = cost - fromClean;
                    if (remaining > 0)
                    {
                        var safehouse = human.territory.Safehouse;
                        if (!safehouse.IsNotValid)
                        {
                            var safeEntity = safehouse.FindEntity();
                            if (safeEntity != null)
                                RemoveDirtyCash(safeEntity, remaining);
                        }
                    }

                    // Set all crew happiness to full
                    foreach (CrewAssignment ca in crew.GetLiving())
                    {
                        var peep = ca.GetPeep();
                        if (peep == null) continue;
                        var state = GetOrCreateCrewState(peep.Id);
                        if (state != null)
                        {
                            state.HappinessValue = 1f;
                            state.TurnsUnhappy = 0;
                        }
                    }

                    Debug.Log($"[GameplayTweaks] Gang meeting accepted! Spent ${cost} for {crewCount} crew members");
                    CrewRelationshipHandlerPatch._outingPopup.SetActive(false);
                }
                catch (Exception e) { Debug.LogError($"[GameplayTweaks] Outing accept failed: {e}"); }
            }

            private static void OnDenyOuting()
            {
                if (CrewRelationshipHandlerPatch._outingPopup != null)
                    CrewRelationshipHandlerPatch._outingPopup.SetActive(false);
                Debug.Log("[GameplayTweaks] Gang meeting denied");
            }
        }

        // =====================================================================
        // 4b. Pact Invitation Event (AI invites player to join pact)
        // =====================================================================
        private static class PactInvitationEvent
        {
            private static GameObject _invitePopup;
            private static AlliancePact _invitingPact;

            internal static void ShowInvitation(AlliancePact pact)
            {
                _invitingPact = pact;

                if (_invitePopup != null)
                {
                    _invitePopup.SetActive(true);
                    RefreshText();
                    return;
                }

                var overlayCanvas = GetOrCreateOverlayCanvas();
                if (overlayCanvas == null) return;

                var popup = new GameObject("PactInvitePopup", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(Canvas), typeof(UnityEngine.UI.GraphicRaycaster));
                popup.transform.SetParent(overlayCanvas.transform, false);

                var popCanvas = popup.GetComponent<Canvas>();
                popCanvas.overrideSorting = true;
                popCanvas.sortingOrder = 998;

                var rt = popup.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 0.65f);
                rt.anchorMax = new Vector2(0.5f, 0.65f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(380, 200);
                popup.GetComponent<Image>().color = new Color(0.08f, 0.1f, 0.15f, 0.98f);

                var vlg = popup.GetComponent<VerticalLayoutGroup>();
                vlg.padding = new RectOffset(14, 14, 12, 12);
                vlg.spacing = 8;
                vlg.childForceExpandWidth = true;
                vlg.childForceExpandHeight = false;

                var outline = popup.AddComponent<Outline>();
                outline.effectColor = pact.SharedColor;
                outline.effectDistance = new Vector2(2, -2);

                var title = CrewRelationshipHandlerPatch.CreateLabel(popup.transform, "PI_Title", "-- Pact Invitation --", 15, FontStyle.Bold);
                title.alignment = TextAnchor.MiddleCenter;
                title.color = pact.SharedColor;

                int members = pact.MemberIds.Count + (pact.LeaderGangId >= 0 ? 1 : 0);
                var body = CrewRelationshipHandlerPatch.CreateLabel(popup.transform, "PI_Body",
                    $"The {pact.DisplayName} ({members} gangs) is inviting\nyour outfit to join their alliance!", 12, FontStyle.Normal);
                body.alignment = TextAnchor.MiddleCenter;
                body.color = new Color(0.85f, 0.85f, 0.8f);

                var btnRow = CrewRelationshipHandlerPatch.CreateHorizontalRow(popup.transform, "PI_Buttons");
                btnRow.GetComponent<LayoutElement>().minHeight = 35;

                var acceptBtn = CrewRelationshipHandlerPatch.CreateButton(btnRow.transform, "PI_Accept", "Join Pact", OnAcceptInvite);
                acceptBtn.GetComponent<Image>().color = new Color(0.2f, 0.4f, 0.2f, 0.95f);
                acceptBtn.GetComponent<LayoutElement>().flexibleWidth = 1;

                var denyBtn = CrewRelationshipHandlerPatch.CreateButton(btnRow.transform, "PI_Deny", "Decline", OnDeclineInvite);
                denyBtn.GetComponent<Image>().color = new Color(0.4f, 0.2f, 0.2f, 0.95f);
                denyBtn.GetComponent<LayoutElement>().flexibleWidth = 1;

                popup.transform.SetAsLastSibling();
                _invitePopup = popup;
            }

            private static void RefreshText() { }

            private static void OnAcceptInvite()
            {
                if (_invitingPact == null) return;
                try
                {
                    // If player has own pact, leave it and go to war
                    var playerPact = SaveData.Pacts.FirstOrDefault(p => p.ColorIndex == ModConstants.PLAYER_PACT_SLOT);
                    if (playerPact != null)
                    {
                        CrewRelationshipHandlerPatch.LeavePlayerPactForWar(playerPact);
                    }

                    // If player was in another AI pact, leave it
                    if (SaveData.PlayerJoinedPactIndex >= 0)
                        CrewRelationshipHandlerPatch.LeaveCurrentAIPact();

                    SaveData.PlayerJoinedPactIndex = _invitingPact.ColorIndex;
                    RefreshPactCache();
                    TerritoryColorPatch.RefreshAllTerritoryColors();
                    Debug.Log($"[GameplayTweaks] Player accepted invitation to {_invitingPact.DisplayName}!");
                }
                catch (Exception e) { Debug.LogError($"[GameplayTweaks] Accept invite failed: {e}"); }

                if (_invitePopup != null) _invitePopup.SetActive(false);
            }

            private static void OnDeclineInvite()
            {
                Debug.Log("[GameplayTweaks] Player declined pact invitation.");
                if (_invitePopup != null) _invitePopup.SetActive(false);
            }
        }

        // =====================================================================
        // 5. Stat Tracking
        // =====================================================================
        private static class StatTrackingPatch
        {
            public static void ApplyPatch(Harmony harmony)
            {
                try
                {
                    var agentType = typeof(GameClock).Assembly.GetType("Game.Session.Entities.AgentComponent");
                    if (agentType == null) return;
                    var method = agentType.GetMethod("IncrementStat", BindingFlags.Public | BindingFlags.Instance);
                    if (method != null)
                    {
                        harmony.Patch(method, postfix: new HarmonyMethod(typeof(StatTrackingPatch), nameof(IncrementStatPostfix)));
                        Debug.Log("[GameplayTweaks] Stat tracking enabled");
                    }
                }
                catch (Exception e) { Debug.LogError($"[GameplayTweaks] StatTrackingPatch failed: {e}"); }
            }

            private static FieldInfo _entityField;

            static void IncrementStatPostfix(object __instance, CrewStats key, int delta)
            {
                if (!EnableCrewStats.Value) return;
                try
                {
                    if (_entityField == null)
                        _entityField = typeof(AgentComponent).BaseType?.GetField("_entity", BindingFlags.NonPublic | BindingFlags.Instance);
                    Entity entity = _entityField?.GetValue(__instance) as Entity;
                    if (entity == null) return;

                    PlayerInfo human = G.GetHumanPlayer();
                    if (human == null || entity.data.agent.pid != human.PID) return;

                    CrewModState state = GetOrCreateCrewState(entity.Id);
                    if (state == null) return;

                    // Track stats for street credit
                    if (key == CrewStats.TimesFought)
                    {
                        state.StreetCreditProgress += delta * ModConstants.STREET_CREDIT_PER_FIGHT;
                        // Fights lower happiness significantly
                        state.HappinessValue = Mathf.Clamp01(state.HappinessValue - 0.15f * delta);
                    }
                    else if (key == CrewStats.PeepsKilled)
                    {
                        state.StreetCreditProgress += delta * ModConstants.STREET_CREDIT_PER_KILL;
                        string peepName = entity.data.person.FullName;
                        LogGrapevine($"DEATH: {delta} rival(s) fell to {peepName}");

                        // Kills lower happiness the most
                        state.HappinessValue = Mathf.Clamp01(state.HappinessValue - 0.25f * delta);

                        // 30% chance a witness sees the kill
                        for (int i = 0; i < delta; i++)
                        {
                            if (SharedRng.NextDouble() < 0.30)
                            {
                                state.HasWitness = true;
                                state.WitnessThreatenedSuccessfully = false;
                                state.WantedProgress = Mathf.Clamp01(state.WantedProgress + 0.25f);

                                if (state.WantedProgress >= 0.75f) state.WantedLevel = WantedLevel.High;
                                else if (state.WantedProgress >= 0.5f) state.WantedLevel = WantedLevel.Medium;
                                else if (state.WantedProgress >= 0.25f) state.WantedLevel = WantedLevel.Low;

                                Debug.Log($"[GameplayTweaks] Witness saw {entity.data.person.FullName} commit a kill!");
                            }
                        }
                    }
                    else if (key == CrewStats.BoozeSold)
                    {
                        int newTotal = 0;
                        entity.data.agent.crewHistoryStats.TryGetValue(CrewStats.BoozeSold, out newTotal);
                        int newChunks = newTotal / 25;
                        int oldChunks = state.LastBoozeSoldCount / 25;
                        if (newChunks > oldChunks)
                            state.StreetCreditProgress += (newChunks - oldChunks) * ModConstants.STREET_CREDIT_PER_25_BOOZE;
                        state.LastBoozeSoldCount = newTotal;
                    }

                    state.StreetCreditProgress = Mathf.Clamp01(state.StreetCreditProgress);
                    state.WantedProgress = Mathf.Clamp01(state.WantedProgress);
                }
                catch { }
            }
        }

        // =====================================================================
        // 6. Turn Update Processing
        // =====================================================================
        private static class TurnUpdatePatch
        {
            // Cached reflection for vacation return
            private static MethodInfo _returnCrewMethod;
            private static MethodInfo _addCrewMethod;
            // Cached reflection for crew transfer
            private static MethodInfo _removeCrewCompletelyMethod;
            private static MethodInfo _addToCrewMethod;

            public static void ApplyPatch(Harmony harmony)
            {
                try
                {
                    var method = typeof(PlayerCrew).GetMethod("OnPlayerTurnStarted", BindingFlags.Public | BindingFlags.Instance);
                    if (method != null)
                    {
                        harmony.Patch(method, postfix: new HarmonyMethod(typeof(TurnUpdatePatch), nameof(OnTurnPostfix)));
                        Debug.Log("[GameplayTweaks] Turn update enabled");
                    }
                }
                catch (Exception e) { Debug.LogError($"[GameplayTweaks] TurnUpdatePatch failed: {e}"); }
            }

            static void OnTurnPostfix(PlayerCrew __instance)
            {
                try
                {
                    PlayerInfo human = G.GetHumanPlayer();
                    if (human == null || __instance != human.crew) return;

                    SimTime now = G.GetNow();

                    // Use ToList() to avoid collection modified exception
                    var livingCrew = __instance.GetLiving().ToList();
                    foreach (CrewAssignment ca in livingCrew)
                    {
                        Entity peep = ca.GetPeep();
                        if (peep == null) continue;
                        ProcessCrewMemberTurn(peep, now, human);
                    }

                    // Refresh gang tracker each turn
                    int today = now.days;
                    if (today != _lastGangTrackDay)
                    {
                        _lastGangTrackDay = today;
                        RefreshGangTracker();
                    }

                    if (EnableAIAlliances.Value)
                    {
                        ProcessAIAlliances(now);
                        // Distribute pact pot earnings each turn (7 days)
                        ProcessPactEarnings();
                    }

                    // Crew outing event - every 56 days (8 weeks)
                    int dayNum = now.days;
                    if (CrewRelationshipHandlerPatch._lastOutingDay < 0)
                        CrewRelationshipHandlerPatch._lastOutingDay = dayNum;
                    if (dayNum - CrewRelationshipHandlerPatch._lastOutingDay >= CrewRelationshipHandlerPatch._outingIntervalDays)
                    {
                        CrewRelationshipHandlerPatch._lastOutingDay = dayNum;
                        try { CrewOutingEvent.ShowOutingPrompt(); }
                        catch (Exception e) { Debug.LogError($"[GameplayTweaks] Outing prompt failed: {e}"); }
                    }

                    // Dirty cash laundering each turn
                    if (EnableDirtyCash.Value)
                    {
                        try { DirtyCashPatches.ProcessLaundering(); }
                        catch (Exception e) { Debug.LogError($"[GameplayTweaks] Laundering failed: {e}"); }
                    }

                    // AI pact invitation - rare chance every 90 days
                    if (EnableAIAlliances.Value && dayNum % 90 == 0 && !SaveData.NeverAcceptPacts
                        && SaveData.PlayerJoinedPactIndex < 0
                        && !SaveData.Pacts.Any(p => p.ColorIndex == ModConstants.PLAYER_PACT_SLOT))
                    {
                        // Only if player has high power or not in any pact
                        int humanPower = CalculateGangPower(human);
                        if (humanPower >= 30 && SharedRng.NextDouble() < 0.35)
                        {
                            var aiPacts = SaveData.Pacts.Where(p => p.ColorIndex < ModConstants.PLAYER_PACT_SLOT && !p.IsPending).ToList();
                            if (aiPacts.Count > 0)
                            {
                                var invitingPact = aiPacts[SharedRng.Next(aiPacts.Count)];
                                try { PactInvitationEvent.ShowInvitation(invitingPact); }
                                catch (Exception e) { Debug.LogError($"[GameplayTweaks] Pact invitation failed: {e}"); }
                            }
                        }
                    }

                    SaveModData();
                }
                catch (Exception e) { Debug.LogError($"[GameplayTweaks] OnTurnPostfix: {e}"); }
            }

            private static void ProcessCrewMemberTurn(Entity peep, SimTime now, PlayerInfo player)
            {
                if (!EnableCrewStats.Value) return;
                CrewModState state = GetOrCreateCrewState(peep.Id);
                if (state == null) return;

                // Check child birth
                if (state.AwaitingChildBirth)
                {
                    int currentKids = peep.data.person.futurekids.Count;
                    if (currentKids < state.LastFutureKidsCount)
                    {
                        state.AwaitingChildBirth = false;
                    }
                    state.LastFutureKidsCount = currentKids;
                }

                // Street Credit level up - GRANT ACTUAL RESOURCES
                if (state.StreetCreditProgress >= 1f)
                {
                    state.StreetCreditLevel++;
                    state.StreetCreditProgress = 0f;
                    GrantStreetCredit(player, ModConstants.STREET_CREDIT_REWARD_COUNT);
                }

                // Happiness: slight weekly decrease if street credit is low
                if (state.StreetCreditLevel < 2)
                    state.HappinessValue = Mathf.Clamp01(state.HappinessValue - 0.03f);

                // Happiness tiers: 1.0-0.75 = Happy, 0.75-0.5 = Content, 0.5-0.25 = Unhappy, 0.25-0 = Miserable
                // Track turns at lowest tier (miserable)
                if (state.HappinessValue <= 0.25f)
                    state.TurnsUnhappy++;
                else
                    state.TurnsUnhappy = Math.Max(0, state.TurnsUnhappy - 1);

                // If miserable for 6 turns (6 weeks), crew member leaves and joins a random gang
                if (state.TurnsUnhappy >= 6)
                {
                    try
                    {
                        // Skip the boss (index 0)
                        CrewAssignment bossAssign = player.crew.GetCrewForIndex(0);
                        if (bossAssign.IsValid && bossAssign.peepId == peep.Id)
                        {
                            // Boss can't defect, just reset unhappy counter
                            state.TurnsUnhappy = 4;
                        }
                        else if (SharedRng.NextDouble() < 0.50)
                        {
                            // Cache reflection methods
                            if (_removeCrewCompletelyMethod == null)
                            {
                                _removeCrewCompletelyMethod = typeof(PlayerCrew).GetMethod("RemoveFromCrewCompletely",
                                    BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
                                _addToCrewMethod = typeof(PlayerCrew).GetMethod("AddToCrew",
                                    BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
                            }

                            // Pick a random AI gang from tracker
                            var candidates = TrackedGangs.Where(g => g.crew != null && !g.crew.IsCrewDefeated && g.crew.LivingCrewCount > 0).ToList();
                            if (candidates.Count > 0 && _removeCrewCompletelyMethod != null)
                            {
                                var targetGang = candidates[SharedRng.Next(candidates.Count)];
                                string peepName = peep.data.person.FullName;
                                string gangName = targetGang.social?.PlayerGroupName ?? "unknown";

                                // Remove from player crew completely
                                _removeCrewCompletelyMethod.Invoke(player.crew, new object[] { peep });

                                // Add to target gang crew
                                if (_addToCrewMethod != null)
                                {
                                    _addToCrewMethod.Invoke(targetGang.crew, new object[] { peep, null });
                                    Debug.Log($"[GameplayTweaks] {peepName} defected to {gangName} - too unhappy!");
                                }
                                else
                                {
                                    Debug.Log($"[GameplayTweaks] {peepName} left the crew (couldn't join another gang)");
                                }
                            }
                        }
                    }
                    catch (Exception e) { Debug.LogError($"[GameplayTweaks] Crew defection failed: {e}"); }
                }

                // Process pending vacation - crew leaves on turn start
                if (state.VacationPending && !state.OnVacation)
                {
                    try
                    {
                        var crewAssign = player.crew.GetCrewForPeep(peep.Id);
                        if (crewAssign.IsValid && player.crew.IsOnBoard(peep.Id))
                        {
                            // OffBoardReason.LayingLow = 3
                            player.crew.RemoveCrewFromBoard(crewAssign, (PlayerCrewData.OffBoardReason)3, false, state.VacationDuration);
                            state.OnVacation = true;
                            state.VacationPending = false;
                            state.VacationReturns = now.IncrementDays(state.VacationDuration);
                            Debug.Log($"[GameplayTweaks] {peep.data.person.FullName} left for vacation, returns in {state.VacationDuration} days");
                        }
                    }
                    catch (Exception e) { Debug.LogError($"[GameplayTweaks] Vacation departure failed: {e}"); }
                }

                // Vacation return - explicitly re-board crew member
                if (state.OnVacation && now >= state.VacationReturns)
                {
                    state.OnVacation = false;
                    state.VacationPending = false;
                    try
                    {
                        var crewAssign = player.crew.GetCrewForPeep(peep.Id);
                        if (crewAssign.IsValid && !player.crew.IsOnBoard(peep.Id))
                        {
                            // Cache these method lookups
                            if (_returnCrewMethod == null)
                            {
                                _returnCrewMethod = typeof(PlayerCrew).GetMethod("ReturnCrewToBoard",
                                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                                _addCrewMethod = typeof(PlayerCrew).GetMethod("AddCrewToBoard",
                                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                            }

                            if (_returnCrewMethod != null)
                            {
                                _returnCrewMethod.Invoke(player.crew, new object[] { crewAssign });
                                Debug.Log($"[GameplayTweaks] {peep.data.person.FullName} returned from vacation");
                            }
                            else if (_addCrewMethod != null)
                            {
                                _addCrewMethod.Invoke(player.crew, new object[] { crewAssign });
                                Debug.Log($"[GameplayTweaks] {peep.data.person.FullName} returned from vacation (fallback)");
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"[GameplayTweaks] Vacation return failed for {peep.data.person.FullName}: {e}");
                    }
                }

                // Global mayor bribe expiration
                if (CrewRelationshipHandlerPatch._globalMayorBribeActive
                    && now.days >= CrewRelationshipHandlerPatch._globalMayorBribeExpireDay)
                {
                    CrewRelationshipHandlerPatch._globalMayorBribeActive = false;
                    Debug.Log("[GameplayTweaks] Mayor bribe expired.");
                }
                // Judge bribe lasts until trial ends (cleared on case dismiss)
                // No time-based expiration for judge bribe

                // Wanted level based on witness system (set during kills via StatTrackingPatch)
                if (state.WantedProgress >= 0.75f) state.WantedLevel = WantedLevel.High;
                else if (state.WantedProgress >= 0.5f) state.WantedLevel = WantedLevel.Medium;
                else if (state.WantedProgress >= 0.25f) state.WantedLevel = WantedLevel.Low;
                else state.WantedLevel = WantedLevel.None;

                // If witness was successfully threatened, clear wanted
                if (state.WitnessThreatenedSuccessfully && !state.HasWitness)
                {
                    state.WantedProgress = Mathf.Max(0f, state.WantedProgress - 0.05f);
                    if (state.WantedProgress < 0.25f)
                    {
                        state.WantedLevel = WantedLevel.None;
                        state.WitnessThreatenedSuccessfully = false;
                    }
                }

                // Bribes and lawyer increase chance of case dismissal each turn
                // Witness threatened (success) gives bonus to bribe effectiveness
                bool mayorActive = CrewRelationshipHandlerPatch._globalMayorBribeActive;
                if (state.WantedLevel != WantedLevel.None)
                {
                    float dismissChance = 0f;
                    float witnessBonus = state.WitnessThreatenedSuccessfully ? 0.10f : 0f;
                    if (mayorActive) dismissChance += 0.15f + witnessBonus;
                    if (state.JudgeBribeActive) dismissChance += 0.25f + witnessBonus;
                    if (state.LawyerRetainer >= 3000) dismissChance += 0.30f;
                    else if (state.LawyerRetainer >= 2000) dismissChance += 0.20f;
                    else if (state.LawyerRetainer >= 1000) dismissChance += 0.10f;

                    if (dismissChance > 0 && SharedRng.NextDouble() < dismissChance)
                    {
                        state.CaseDismissed = true;
                        state.WantedLevel = WantedLevel.None;
                        state.WantedProgress = 0f;
                        state.HasWitness = false;
                        state.FedsIncoming = false;
                        state.FedArrivalCountdown = 0;
                        state.LawyerRetainer = 0;
                        state.JudgeBribeActive = false; // Judge bribe ends with trial
                        Debug.Log($"[GameplayTweaks] Case dismissed for {peep.data.person.FullName}!");
                    }
                }

                // Fed arrival system - start countdown when wanted, auto-arrest when countdown hits 0
                if (state.WantedLevel != WantedLevel.None && !state.FedsIncoming && !CrewRelationshipHandlerPatch._globalMayorBribeActive && !state.JudgeBribeActive)
                {
                    // Start tracking this crew member
                    state.FedsIncoming = true;
                    switch (state.WantedLevel)
                    {
                        case WantedLevel.High:
                            state.FedArrivalCountdown = ModConstants.FED_ARRIVAL_HIGH;
                            break;
                        case WantedLevel.Medium:
                            state.FedArrivalCountdown = ModConstants.FED_ARRIVAL_MEDIUM;
                            break;
                        case WantedLevel.Low:
                            state.FedArrivalCountdown = ModConstants.FED_ARRIVAL_LOW;
                            break;
                    }
                    Debug.Log($"[GameplayTweaks] Feds tracking {peep.data.person.FullName}, arrival in {state.FedArrivalCountdown} days");
                }

                // Decrement countdown and auto-arrest when it hits 0
                if (state.FedsIncoming && !CrewRelationshipHandlerPatch._globalMayorBribeActive && !state.JudgeBribeActive)
                {
                    state.FedArrivalCountdown--;
                    if (state.FedArrivalCountdown <= 0)
                    {
                        try
                        {
                            if (player.crew.IsOnBoard(peep.Id))
                            {
                                // Use game's arrest system for proper jail overlay and trial
                                if (JailSystem.ArrestCrew(player, peep))
                                {
                                    state.FedsIncoming = false;
                                    state.WantedLevel = WantedLevel.None;
                                    state.WantedProgress = 0f;
                                    Debug.Log($"[GameplayTweaks] {peep.data.person.FullName} arrested by feds (game system)!");
                                }
                                else
                                {
                                    // Fallback to manual arrest if game system fails
                                    var crewAssign = player.crew.GetCrewForPeep(peep.Id);
                                    if (crewAssign.IsValid)
                                    {
                                        player.crew.RemoveCrewFromBoard(crewAssign, PlayerCrewData.OffBoardReason.Arrested, false, 7);
                                        state.FedsIncoming = false;
                                        state.WantedLevel = WantedLevel.None;
                                        state.WantedProgress = 0f;
                                        Debug.Log($"[GameplayTweaks] {peep.data.person.FullName} arrested by feds (fallback)!");
                                    }
                                }
                            }
                        }
                        catch (Exception e) { Debug.LogError($"[GameplayTweaks] Fed arrest failed: {e}"); }
                    }
                }

                // If bribed, pause the countdown
                if ((state.MayorBribeActive || state.JudgeBribeActive) && state.FedsIncoming)
                {
                    // Bribes suppress the feds temporarily - countdown paused
                }

                // If wanted level drops to none, cancel fed tracking
                if (state.WantedLevel == WantedLevel.None && state.FedsIncoming)
                {
                    state.FedsIncoming = false;
                    state.FedArrivalCountdown = 0;
                }

                // Mortality system: crew members over 50 have increasing chance of death
                try
                {
                    float age = peep.data.person.GetAge(now).YearsFloat;
                    if (age >= 50f)
                    {
                        // Skip boss (index 0)
                        CrewAssignment bossCheck = player.crew.GetCrewForIndex(0);
                        bool isBoss = bossCheck.IsValid && bossCheck.peepId == peep.Id;
                        if (!isBoss)
                        {
                            // Death chance: 2% per year over 50, capped at 60%
                            float deathChance = Math.Min(0.60f, (age - 50f) * 0.02f);
                            if (SharedRng.NextDouble() < deathChance)
                            {
                                string deadName = peep.data.person.FullName;
                                int deadAge = (int)age;
                                // ProcessCrewMemberDeath may be internal - use reflection
                                var deathMethod = player.crew.GetType().GetMethod("ProcessCrewMemberDeath",
                                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                                if (deathMethod != null)
                                    deathMethod.Invoke(player.crew, new object[] { peep });
                                LogGrapevine($"DEATH: {deadName} passed away at age {deadAge} (natural causes)");
                            }
                        }
                    }
                }
                catch { }
            }

            private static void ProcessAIAlliances(SimTime now)
            {
                try
                {
                    var allPlayers = G.GetAllPlayers().ToList();
                    var aiGangs = allPlayers
                        .Where(p => p.IsJustGang && !p.crew.IsCrewDefeated && !p.PID.IsHumanPlayer)
                        .ToList();

                    // Check for pact leader deaths - dissolve pacts when leader dies
                    var pactsToRemove = new List<AlliancePact>();
                    foreach (var pact in SaveData.Pacts)
                    {
                        var leaderGang = allPlayers.FirstOrDefault(p => p.PID.id == pact.LeaderGangId);
                        if (leaderGang == null || leaderGang.crew.IsCrewDefeated)
                        {
                            pactsToRemove.Add(pact);
                            Debug.Log($"[GameplayTweaks] Pact {pact.PactId} dissolved - leader defeated");
                            LogGrapevine($"WAR: {pact.DisplayName} collapsed - leader gang defeated!");
                        }
                    }
                    foreach (var pact in pactsToRemove)
                    {
                        // Reset player references if they were in this pact
                        if (SaveData.PlayerPactId >= 0 && pact.PactId == $"pact_{SaveData.PlayerPactId}")
                            SaveData.PlayerPactId = -1;
                        if (SaveData.PlayerJoinedPactIndex == pact.ColorIndex)
                            SaveData.PlayerJoinedPactIndex = -1;
                        SaveData.Pacts.Remove(pact);
                    }

                    if (aiGangs.Count < 2) return;

                    var gangPowers = aiGangs
                        .Select(g => new { Gang = g, Power = CalculateGangPower(g) })
                        .OrderByDescending(x => x.Power)
                        .ToList();

                    // Form pacts periodically - max 6 AI pacts (slots 0-5)
                    int aiPactCount = SaveData.Pacts.Count(p => p.ColorIndex < ModConstants.PLAYER_PACT_SLOT);
                    if (now.days % 30 == 0 && aiPactCount < ModConstants.PLAYER_PACT_SLOT)
                    {
                        // Find first unused color slot (0-5, not player slot)
                        int freeSlot = -1;
                        var usedSlots = new HashSet<int>(SaveData.Pacts.Select(p => p.ColorIndex));
                        for (int s = 0; s < ModConstants.PLAYER_PACT_SLOT; s++)
                        {
                            if (!usedSlots.Contains(s)) { freeSlot = s; break; }
                        }
                        if (freeSlot < 0) return; // All AI slots taken

                        var unallied = gangPowers.Where(x => GetPactForPlayer(x.Gang.PID) == null).ToList();
                        if (unallied.Count >= 2)
                        {
                            var leader = unallied[0];
                            var member = unallied.Skip(1).FirstOrDefault(x => x.Power > leader.Power / 3);
                            if (member != null)
                            {
                                var pact = new AlliancePact
                                {
                                    PactId = $"pact_{SaveData.NextPactId++}",
                                    PactName = $"{ModConstants.PACT_COLOR_NAMES[freeSlot]} Alliance",
                                    ColorIndex = freeSlot,
                                    LeaderGangId = leader.Gang.PID.id,
                                    MemberIds = new List<int> { member.Gang.PID.id },
                                    SharedColor = ModConstants.PACT_COLORS[freeSlot],
                                    Formed = now
                                };
                                SaveData.Pacts.Add(pact);
                                Debug.Log($"[GameplayTweaks] AI Alliance: {leader.Gang.social.PlayerGroupName} + {member.Gang.social.PlayerGroupName} ({pact.PactName})");
                                LogGrapevine($"PACT: {leader.Gang.social.PlayerGroupName} and {member.Gang.social.PlayerGroupName} formed {pact.PactName}");
                            }
                        }
                    }
                    // Generate random AI gang interaction events for the grapevine
                    if (now.days % 14 == 0 && aiGangs.Count >= 2)
                    {
                        // Random kill event
                        if (SharedRng.NextDouble() < 0.4)
                        {
                            var killer = gangPowers[SharedRng.Next(Math.Min(4, gangPowers.Count))];
                            var target = gangPowers[SharedRng.Next(gangPowers.Count)];
                            if (killer.Gang.PID.id != target.Gang.PID.id)
                            {
                                string killerName = killer.Gang.social?.PlayerGroupName ?? "Unknown";
                                string targetName = target.Gang.social?.PlayerGroupName ?? "Unknown";
                                LogGrapevine($"DEATH: A member of {targetName} was killed by {killerName}");
                            }
                        }
                        // Random war event between gangs
                        if (SharedRng.NextDouble() < 0.15)
                        {
                            var g1 = gangPowers[SharedRng.Next(gangPowers.Count)];
                            var g2 = gangPowers[SharedRng.Next(gangPowers.Count)];
                            if (g1.Gang.PID.id != g2.Gang.PID.id)
                            {
                                string n1 = g1.Gang.social?.PlayerGroupName ?? "Unknown";
                                string n2 = g2.Gang.social?.PlayerGroupName ?? "Unknown";
                                LogGrapevine($"WAR: {n1} started a turf war with {n2}!");
                            }
                        }
                    }
                }
                catch (Exception e) { Debug.LogError($"[GameplayTweaks] ProcessAIAlliances: {e}"); }
            }

            internal static void ProcessPactEarnings()
            {
                try
                {
                    var allPlayers = G.GetAllPlayers().ToList();
                    PlayerInfo human = G.GetHumanPlayer();
                    int humanId = human?.PID.id ?? -1;

                    foreach (var pact in SaveData.Pacts)
                    {
                        // Gather all gang IDs in this pact
                        var gangIds = new List<int>();
                        if (pact.LeaderGangId >= 0) gangIds.Add(pact.LeaderGangId);
                        gangIds.AddRange(pact.MemberIds);

                        // Include player if they joined this pact
                        bool playerInThisPact = (SaveData.PlayerJoinedPactIndex == pact.ColorIndex);
                        if (playerInThisPact && human != null && !gangIds.Contains(humanId))
                            gangIds.Add(humanId);

                        if (gangIds.Count < 1) continue;

                        // Calculate pot and find strongest
                        int totalPot = 0;
                        int strongestId = -1;
                        int strongestPower = -1;
                        string strongestName = "Unknown";

                        foreach (int gid in gangIds)
                        {
                            PlayerInfo gang = allPlayers.FirstOrDefault(p => p.PID.id == gid);
                            if (gang == null || gang.crew.IsCrewDefeated) continue;

                            int power = CalculateGangPower(gang);
                            int earnings = (int)(power * ModConstants.INCOME_SHARE_PERCENT * 10);
                            totalPot += earnings;

                            if (power > strongestPower)
                            {
                                strongestPower = power;
                                strongestId = gid;
                                strongestName = gang.social?.PlayerGroupName ?? "Unknown";
                            }
                        }

                        if (totalPot <= 0 || strongestId < 0) continue;

                        // Pay the strongest gang
                        PlayerInfo winner = allPlayers.FirstOrDefault(p => p.PID.id == strongestId);
                        if (winner != null)
                        {
                            winner.finances.DoChangeMoneyOnSafehouse(new Price(totalPot), MoneyReason.Other);
                            LogGrapevine($"PACT: {strongestName} earned ${totalPot} as the strongest in {pact.DisplayName}");
                            Debug.Log($"[GameplayTweaks] Pact {pact.DisplayName}: ${totalPot} -> {strongestName} (power={strongestPower})");
                        }
                    }
                }
                catch (Exception e) { Debug.LogError($"[GameplayTweaks] ProcessPactEarnings: {e}"); }
            }
        }

        // =====================================================================
        // 7. AI Crew Cap
        // =====================================================================
        private static class AICrewCapPatch
        {
            private static FieldInfo _playerField;
            private static HashSet<int> _topGangIds;
            private static int _topGangCacheDay = -1;

            public static void ApplyPatch(Harmony harmony)
            {
                try
                {
                    var advisorType = typeof(GameClock).Assembly.GetType("Game.Session.Sim.Advisors.UnitsAdvisor");
                    if (advisorType == null) return;
                    var method = advisorType.GetMethod("ShouldGrow", BindingFlags.Public | BindingFlags.Instance);
                    if (method != null)
                    {
                        harmony.Patch(method, prefix: new HarmonyMethod(typeof(AICrewCapPatch), nameof(ShouldGrowPrefix)));
                        Debug.Log("[GameplayTweaks] AI crew cap enabled");
                    }
                }
                catch { }
            }

            static void ShouldGrowPrefix(object __instance, ref int ___maxCrew)
            {
                if (!EnableAIAlliances.Value) return;
                try
                {
                    if (_playerField == null)
                        _playerField = __instance.GetType().GetField("player", BindingFlags.NonPublic | BindingFlags.Instance);
                    var player = _playerField?.GetValue(__instance) as PlayerInfo;
                    if (player == null || player.PID.IsHumanPlayer) return;

                    // Cache top gang IDs once per game day instead of per call
                    int currentDay = (int)G.GetNow().days;
                    if (_topGangIds == null || _topGangCacheDay != currentDay)
                    {
                        _topGangCacheDay = currentDay;
                        _topGangIds = new HashSet<int>(
                            G.GetAllPlayers()
                                .Where(p => p.IsJustGang && !p.crew.IsCrewDefeated)
                                .Select(p => new { PID = p.PID, Power = CalculateGangPower(p) })
                                .OrderByDescending(x => x.Power)
                                .Take(ModConstants.TOP_GANG_COUNT)
                                .Select(x => (int)x.PID.id)
                        );
                    }

                    if (_topGangIds.Contains((int)player.PID.id))
                        ___maxCrew = ModConstants.TOP_GANG_CREW_CAP;
                }
                catch { }
            }
        }

        // =====================================================================
        // 8. Territory Color for Pacts
        // =====================================================================
        private static class TerritoryColorPatch
        {
            private static FieldInfo _colorsField;
            private static object _mapDisplayInstance;

            public static void ApplyPatch(Harmony harmony)
            {
                try
                {
                    // Patch MapDisplayManager.GetColorForPlayer
                    var mapDisplayType = typeof(GameClock).Assembly.GetType("Game.Session.Board.MapDisplayManager");
                    if (mapDisplayType != null)
                    {
                        _colorsField = mapDisplayType.GetField("_colors", BindingFlags.NonPublic | BindingFlags.Instance);

                        var colorMethod = mapDisplayType.GetMethod("GetColorForPlayer",
                            BindingFlags.Public | BindingFlags.Instance);
                        if (colorMethod != null)
                        {
                            harmony.Patch(colorMethod, postfix: new HarmonyMethod(typeof(TerritoryColorPatch), nameof(GetColorPostfix)));
                            Debug.Log("[GameplayTweaks] Territory color patch applied to MapDisplayManager");
                        }
                    }

                    // Patch PlayerTerritoryDisplay.RefreshBorder for outline/border colors
                    var territoryDisplayType = typeof(GameClock).Assembly.GetType("Game.Session.Player.PlayerTerritoryDisplay");
                    if (territoryDisplayType != null)
                    {
                        var refreshMethod = territoryDisplayType.GetMethod("RefreshBorder",
                            BindingFlags.Public | BindingFlags.Instance);
                        if (refreshMethod != null)
                        {
                            harmony.Patch(refreshMethod, prefix: new HarmonyMethod(typeof(TerritoryColorPatch), nameof(RefreshBorderPrefix)));
                            Debug.Log("[GameplayTweaks] Territory border refresh patch applied");
                        }
                    }
                }
                catch (Exception e) { Debug.LogError($"[GameplayTweaks] TerritoryColorPatch failed: {e}"); }
            }

            static void GetColorPostfix(ref Color __result, PlayerInfo player, object __instance)
            {
                if (!EnableAIAlliances.Value || player == null) return;
                try
                {
                    _mapDisplayInstance = __instance;
                    var pact = GetPactForPlayer(player.PID);
                    if (pact != null && pact.IsActive)
                    {
                        __result = pact.SharedColor;
                        // Force-update the cached color dictionary
                        if (_colorsField != null)
                        {
                            var colors = _colorsField.GetValue(__instance) as System.Collections.IDictionary;
                            if (colors != null)
                            {
                                colors[player.PID] = pact.SharedColor;
                            }
                        }
                    }
                }
                catch { }
            }

            // Cached reflection for RefreshBorderPrefix
            private static FieldInfo _borderManagerField;
            private static FieldInfo _borderPlayerField;
            private static PropertyInfo _borderPlayerProp;
            private static FieldInfo _borderColorInfoField;
            private static FieldInfo _borderColorField;
            private static FieldInfo _borderBorderColorField;
            private static PropertyInfo _displayColorProp;
            private static FieldInfo _displayColorField;
            private static bool _borderReflectionCached;

            static void RefreshBorderPrefix(object __instance)
            {
                if (!EnableAIAlliances.Value) return;
                try
                {
                    // Cache reflection lookups once
                    if (!_borderReflectionCached)
                    {
                        _borderReflectionCached = true;
                        _borderManagerField = __instance.GetType().GetField("_manager", BindingFlags.NonPublic | BindingFlags.Instance);
                        _displayColorProp = __instance.GetType().GetProperty("color", BindingFlags.Public | BindingFlags.Instance);
                        _displayColorField = __instance.GetType().GetField("_color", BindingFlags.NonPublic | BindingFlags.Instance);
                    }

                    if (_borderManagerField == null) return;
                    var manager = _borderManagerField.GetValue(__instance);
                    if (manager == null) return;

                    // Cache manager-level reflection on first use
                    if (_borderPlayerField == null && _borderPlayerProp == null)
                    {
                        _borderPlayerField = manager.GetType().GetField("_player", BindingFlags.NonPublic | BindingFlags.Instance);
                        if (_borderPlayerField == null)
                            _borderPlayerProp = manager.GetType().GetProperty("player", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
                        _borderColorInfoField = manager.GetType().GetField("colorInfo", BindingFlags.Public | BindingFlags.Instance);
                    }

                    PlayerInfo player = (_borderPlayerField != null)
                        ? _borderPlayerField.GetValue(manager) as PlayerInfo
                        : _borderPlayerProp?.GetValue(manager) as PlayerInfo;
                    if (player == null) return;

                    var pact = GetPactForPlayer(player.PID);
                    if (pact == null || !pact.IsActive) return;

                    if (_borderColorInfoField != null)
                    {
                        object colorInfo = _borderColorInfoField.GetValue(manager);
                        if (colorInfo != null)
                        {
                            // Cache colorInfo sub-fields on first use
                            if (_borderColorField == null)
                            {
                                _borderColorField = colorInfo.GetType().GetField("color", BindingFlags.Public | BindingFlags.Instance)
                                                  ?? colorInfo.GetType().GetField("Color", BindingFlags.Public | BindingFlags.Instance);
                                _borderBorderColorField = colorInfo.GetType().GetField("borderColor", BindingFlags.Public | BindingFlags.Instance)
                                                        ?? colorInfo.GetType().GetField("BorderColor", BindingFlags.Public | BindingFlags.Instance);
                            }

                            if (_borderColorField != null)
                            {
                                _borderColorField.SetValue(colorInfo, pact.SharedColor);
                                _borderColorInfoField.SetValue(manager, colorInfo);
                            }
                            if (_borderBorderColorField != null)
                            {
                                _borderBorderColorField.SetValue(colorInfo, pact.SharedColor);
                                _borderColorInfoField.SetValue(manager, colorInfo);
                            }
                        }
                    }

                    if (_displayColorProp != null && _displayColorProp.CanWrite)
                        _displayColorProp.SetValue(__instance, pact.SharedColor);
                    if (_displayColorField != null)
                        _displayColorField.SetValue(__instance, pact.SharedColor);
                }
                catch { }
            }

            // Public method to force refresh all territory colors
            public static void RefreshAllTerritoryColors()
            {
                try
                {
                    // Update pact member colors directly (don't Clear - causes race with async RefreshRegionAsync)
                    if (_mapDisplayInstance != null && _colorsField != null)
                    {
                        var colors = _colorsField.GetValue(_mapDisplayInstance) as System.Collections.IDictionary;
                        if (colors != null)
                        {
                            foreach (var pact in SaveData.Pacts)
                            {
                                if (!pact.IsActive) continue;
                                foreach (var player in G.GetAllPlayers())
                                {
                                    if (player == null) continue;
                                    if (pact.IsMember(player.PID))
                                    {
                                        try { colors[player.PID] = pact.SharedColor; } catch { }
                                    }
                                }
                            }
                        }
                    }

                    // Now trigger RefreshBorder on all territory displays
                    var displayType = typeof(GameClock).Assembly.GetType("Game.Session.Player.PlayerTerritoryDisplay");
                    var refreshMethod = displayType?.GetMethod("RefreshBorder", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
                    var displayField = typeof(PlayerTerritory).GetField("_display", BindingFlags.NonPublic | BindingFlags.Instance);

                    // Also try to call RefreshDisplay/Refresh on the territory itself
                    var territoryRefresh = typeof(PlayerTerritory).GetMethod("RefreshDisplay",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    var territoryRefresh2 = typeof(PlayerTerritory).GetMethod("Refresh",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                    foreach (var player in G.GetAllPlayers())
                    {
                        if (player?.territory == null) continue;

                        // Try RefreshDisplay on PlayerTerritory
                        try
                        {
                            if (territoryRefresh != null)
                                territoryRefresh.Invoke(player.territory, null);
                            else if (territoryRefresh2 != null)
                                territoryRefresh2.Invoke(player.territory, null);
                        }
                        catch { }

                        // Get the display object and call RefreshBorder
                        var display = displayField?.GetValue(player.territory);
                        if (display == null) continue;

                        try
                        {
                            if (refreshMethod != null)
                                refreshMethod.Invoke(display, null);
                        }
                        catch { }
                    }

                    // Also try to trigger a full map refresh via MapDisplayManager
                    if (_mapDisplayInstance != null)
                    {
                        try
                        {
                            var refreshMapMethod = _mapDisplayInstance.GetType().GetMethod("RefreshColors",
                                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                            if (refreshMapMethod != null)
                                refreshMapMethod.Invoke(_mapDisplayInstance, null);
                            else
                            {
                                var refreshAllMethod = _mapDisplayInstance.GetType().GetMethod("Refresh",
                                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                                if (refreshAllMethod != null)
                                    refreshAllMethod.Invoke(_mapDisplayInstance, null);
                            }
                        }
                        catch { }
                    }

                    Debug.Log("[GameplayTweaks] Territory colors refreshed for all players");
                }
                catch (Exception e) { Debug.LogError($"[GameplayTweaks] RefreshAllTerritoryColors failed: {e}"); }
            }
        }

        // =====================================================================
        // 9. Boss Arrest Patch - Allow boss to go to jail
        // =====================================================================
        private static class BossArrestPatch
        {
            public static void ApplyPatch(Harmony harmony)
            {
                try
                {
                    var copTrackerType = typeof(GameClock).Assembly.GetType("Game.Session.Sim.CopTracker");
                    if (copTrackerType == null)
                    {
                        Debug.LogWarning("[GameplayTweaks] CopTracker type not found");
                        return;
                    }

                    var canBeArrested = copTrackerType.GetMethod("CanBeArrested",
                        BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
                    if (canBeArrested != null)
                    {
                        harmony.Patch(canBeArrested, postfix: new HarmonyMethod(typeof(BossArrestPatch), nameof(CanBeArrestedPostfix)));
                        Debug.Log("[GameplayTweaks] Boss arrest patch enabled");
                    }
                    else
                    {
                        Debug.LogWarning("[GameplayTweaks] CanBeArrested method not found");
                    }
                }
                catch (Exception e) { Debug.LogError($"[GameplayTweaks] BossArrestPatch failed: {e}"); }
            }

            static void CanBeArrestedPostfix(ref bool __result, CrewAssignment crew, PlayerInfo player)
            {
                try
                {
                    // Check if this is the boss (player peep)
                    if (player == null || crew.peepId != player.social.PlayerPeepId) return;

                    // Allow boss to be arrested if wanted level is High
                    var state = GetCrewStateOrNull(crew.peepId);
                    if (state != null && state.WantedLevel == WantedLevel.High)
                    {
                        __result = true;
                        Debug.Log($"[GameplayTweaks] Boss can be arrested - wanted level is High");
                    }
                }
                catch { }
            }
        }

        // =====================================================================
        // 10. Jail System - Enhanced arrest and trial mechanics
        // =====================================================================
        private static class JailSystem
        {
            private static Type _copTrackerType;
            private static object _copTrackerInstance;
            private static MethodInfo _startHumanArrestMethod;
            private static PropertyInfo _dataProperty;

            public static void Initialize()
            {
                try
                {
                    _copTrackerType = typeof(GameClock).Assembly.GetType("Game.Session.Sim.CopTracker");
                    if (_copTrackerType == null) return;

                    _startHumanArrestMethod = _copTrackerType.GetMethod("StartHumanArrest",
                        BindingFlags.Public | BindingFlags.Instance);
                    _dataProperty = _copTrackerType.GetProperty("data", BindingFlags.Public | BindingFlags.Instance);

                    Debug.Log("[GameplayTweaks] JailSystem initialized");
                }
                catch (Exception e) { Debug.LogError($"[GameplayTweaks] JailSystem init failed: {e}"); }
            }

            private static object GetCopTracker()
            {
                if (_copTrackerInstance != null) return _copTrackerInstance;
                try
                {
                    var ctxObj = G.GetCtx();
                    if (ctxObj == null) return null;
                    var simmanField = ctxObj.GetType().GetField("simman");
                    var simman = simmanField?.GetValue(ctxObj);
                    if (simman == null) return null;
                    var copTrackerField = simman.GetType().GetField("copTracker")
                        ?? simman.GetType().GetField("cops");
                    _copTrackerInstance = copTrackerField?.GetValue(simman);
                    return _copTrackerInstance;
                }
                catch { return null; }
            }

            // Trigger arrest using game's system
            public static bool ArrestCrew(PlayerInfo player, Entity peep)
            {
                try
                {
                    var copTracker = GetCopTracker();
                    if (copTracker == null || _startHumanArrestMethod == null) return false;

                    var crewAssign = player.crew.GetCrewForPeep(peep.Id);
                    if (!crewAssign.IsValid) return false;

                    _startHumanArrestMethod.Invoke(copTracker, new object[] { player, crewAssign });

                    // Update our mod state
                    var state = GetOrCreateCrewState(peep.Id);
                    if (state != null)
                    {
                        state.InJail = true;
                        state.DaysInJail = 0;
                        state.TrialDaysRemaining = 7; // Default 7 days to trial
                        state.FedsIncoming = false;
                    }

                    Debug.Log($"[GameplayTweaks] {peep.data.person.FullName} arrested via game system");
                    return true;
                }
                catch (Exception e)
                {
                    Debug.LogError($"[GameplayTweaks] ArrestCrew failed: {e}");
                    return false;
                }
            }

            // Check if crew is arrested or imprisoned using game's system
            public static bool IsInJail(EntityID peepId)
            {
                try
                {
                    var copTracker = GetCopTracker();
                    if (copTracker == null) return false;

                    var isArrestedMethod = _copTrackerType.GetMethod("IsArrestedOrImprisoned",
                        BindingFlags.Public | BindingFlags.Instance);
                    if (isArrestedMethod != null)
                    {
                        return (bool)isArrestedMethod.Invoke(copTracker, new object[] { peepId });
                    }
                }
                catch { }
                return false;
            }

            // Get arrest info for a crew member
            public static (int daysToTrial, int bribeCost, bool isPaidOff) GetArrestInfo(EntityID peepId)
            {
                try
                {
                    var copTracker = GetCopTracker();
                    if (copTracker == null) return (0, 0, false);

                    var dataField = _copTrackerType.GetField("data", BindingFlags.Public | BindingFlags.Instance);
                    var data = dataField?.GetValue(copTracker);
                    if (data == null) return (0, 0, false);

                    var arrestsField = data.GetType().GetField("fedArrests", BindingFlags.Public | BindingFlags.Instance);
                    var arrests = arrestsField?.GetValue(data) as System.Collections.IList;
                    if (arrests == null) return (0, 0, false);

                    foreach (var arrest in arrests)
                    {
                        var peepIdField = arrest.GetType().GetField("peepId", BindingFlags.Public | BindingFlags.Instance);
                        var arrestPeepId = (EntityID)peepIdField.GetValue(arrest);
                        if (arrestPeepId == peepId)
                        {
                            var trialDateField = arrest.GetType().GetField("trialDate", BindingFlags.Public | BindingFlags.Instance);
                            var payOffCostField = arrest.GetType().GetField("payOffCost", BindingFlags.Public | BindingFlags.Instance);
                            var paidOffField = arrest.GetType().GetField("paidOff", BindingFlags.Public | BindingFlags.Instance);

                            var trialDate = (SimTime)trialDateField.GetValue(arrest);
                            var payOffCost = payOffCostField.GetValue(arrest);
                            var paidOff = (bool)paidOffField.GetValue(arrest);

                            int daysToTrial = trialDate.days - G.GetNow().days;
                            int bribeCost = 0;
                            try
                            {
                                var cashField = payOffCost.GetType().GetField("cash", BindingFlags.Public | BindingFlags.Instance);
                                bribeCost = (int)cashField.GetValue(payOffCost);
                            }
                            catch { }

                            return (daysToTrial, bribeCost, paidOff);
                        }
                    }
                }
                catch { }
                return (0, 0, false);
            }

            // Get imprisonment info
            public static (int daysRemaining, int totalYears) GetImprisonmentInfo(EntityID peepId)
            {
                try
                {
                    var copTracker = GetCopTracker();
                    if (copTracker == null) return (0, 0);

                    var dataField = _copTrackerType.GetField("data", BindingFlags.Public | BindingFlags.Instance);
                    var data = dataField?.GetValue(copTracker);
                    if (data == null) return (0, 0);

                    var imprisonedField = data.GetType().GetField("fedImprisoned", BindingFlags.Public | BindingFlags.Instance);
                    var imprisoned = imprisonedField?.GetValue(data) as System.Collections.IList;
                    if (imprisoned == null) return (0, 0);

                    foreach (var entry in imprisoned)
                    {
                        var peepIdField = entry.GetType().GetField("peepId", BindingFlags.Public | BindingFlags.Instance);
                        var entryPeepId = (EntityID)peepIdField.GetValue(entry);
                        if (entryPeepId == peepId)
                        {
                            var endDateField = entry.GetType().GetField("endDate", BindingFlags.Public | BindingFlags.Instance);
                            var yearsField = entry.GetType().GetField("years", BindingFlags.Public | BindingFlags.Instance);

                            var endDate = (SimTime)endDateField.GetValue(entry);
                            var years = (int)yearsField.GetValue(entry);

                            int daysRemaining = endDate.days - G.GetNow().days;
                            return (daysRemaining, years);
                        }
                    }
                }
                catch { }
                return (0, 0);
            }

            // Pay lawyer retainer to affect trial outcome
            public static void PayLawyerRetainer(EntityID peepId, int amount)
            {
                var state = GetOrCreateCrewState(peepId);
                if (state != null)
                {
                    state.LawyerRetainer += amount;
                    Debug.Log($"[GameplayTweaks] Lawyer retainer paid: ${amount}, total: ${state.LawyerRetainer}");
                }
            }

            // Get jail status string for UI
            public static string GetJailStatusString(EntityID peepId)
            {
                if (!IsInJail(peepId)) return null;

                var (daysToTrial, bribeCost, isPaidOff) = GetArrestInfo(peepId);
                if (daysToTrial > 0)
                {
                    if (isPaidOff)
                        return $"Arrested - Trial in {daysToTrial} days (Bribed)";
                    else
                        return $"Arrested - Trial in {daysToTrial} days (Bribe: ${bribeCost})";
                }

                var (daysRemaining, years) = GetImprisonmentInfo(peepId);
                if (daysRemaining > 0)
                {
                    return $"In Prison - {daysRemaining} days remaining ({years} year sentence)";
                }

                return "In Jail";
            }
        }

        // =====================================================================
        // 11. Save/Load
        // =====================================================================
        private static class SaveLoadPatch
        {
            public static void ApplyPatch(Harmony harmony)
            {
                try
                {
                    var saveType = typeof(GameClock).Assembly.GetType("Game.Services.SaveManager");
                    if (saveType == null) return;

                    var loadMethod = saveType.GetMethod("LoadGame", BindingFlags.Public | BindingFlags.Instance);
                    if (loadMethod != null)
                        harmony.Patch(loadMethod, postfix: new HarmonyMethod(typeof(SaveLoadPatch), nameof(LoadPostfix)));

                    var saveMethod = saveType.GetMethod("SaveGame", BindingFlags.Public | BindingFlags.Instance);
                    if (saveMethod != null)
                        harmony.Patch(saveMethod, postfix: new HarmonyMethod(typeof(SaveLoadPatch), nameof(SavePostfix)));

                    Debug.Log("[GameplayTweaks] Save/Load enabled");
                }
                catch { }
            }

            static void LoadPostfix(string saveName)
            {
                LoadModData(saveName);
                // Delay gang tracker refresh slightly - game may not have spawned gangs yet
                _lastGangTrackDay = -1;
            }
            static void SavePostfix(string saveName) => SaveModData();
        }
    }

    // =========================================================================
    // AttackAdvisor Crash Fix - prevents NullRef in TrySellOutToFeds
    // =========================================================================
    internal static class AttackAdvisorPatch
    {
        public static void ApplyPatch(Harmony harmony)
        {
            try
            {
                var asm = typeof(GameClock).Assembly;
                var advType = asm.GetType("Game.Session.Player.AI.AttackAdvisor");
                if (advType == null) { Debug.LogWarning("[GameplayTweaks] AttackAdvisor type not found"); return; }
                var method = advType.GetMethod("TrySellOutToFeds", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (method == null) { Debug.LogWarning("[GameplayTweaks] TrySellOutToFeds method not found"); return; }
                harmony.Patch(method, prefix: new HarmonyMethod(typeof(AttackAdvisorPatch), nameof(TrySellOutToFedsPrefix)));
                Debug.Log("[GameplayTweaks] AttackAdvisor.TrySellOutToFeds crash fix applied");
            }
            catch (Exception e) { Debug.LogError($"[GameplayTweaks] AttackAdvisorPatch failed: {e}"); }
        }

        static bool TrySellOutToFedsPrefix(object __instance)
        {
            try
            {
                // Check if the instance has valid player reference
                var playerField = __instance.GetType().GetField("_player", BindingFlags.NonPublic | BindingFlags.Instance)
                    ?? __instance.GetType().GetField("player", BindingFlags.NonPublic | BindingFlags.Instance);
                if (playerField != null)
                {
                    var player = playerField.GetValue(__instance);
                    if (player == null) return false; // Skip - no valid player
                }
                return true; // Let original run
            }
            catch
            {
                return false; // Skip on any error
            }
        }
    }

    // =========================================================================
    // Dirty Cash Economy Patches
    // =========================================================================
    internal static class DirtyCashPatches
    {
        public static void ApplyPatches(Harmony harmony)
        {
            if (!GameplayTweaksPlugin.EnableDirtyCash.Value) return;

            try
            {
                var asm = typeof(GameClock).Assembly;

                // Patch 1: Volume fix - ensure dirty-cash has volume of 1
                var resourceType = asm.GetType("Game.Services.Resource");
                if (resourceType != null)
                {
                    var volumeMethod = resourceType.GetMethod("FindTotalVolume", BindingFlags.Public | BindingFlags.Instance);
                    if (volumeMethod != null)
                    {
                        harmony.Patch(volumeMethod, postfix: new HarmonyMethod(typeof(DirtyCashPatches), nameof(VolumePostfix)));
                        Debug.Log("[GameplayTweaks] Dirty cash volume fix applied");
                    }
                }

                // Patch 2: Income conversion - intercept DoChangeMoney to convert illegal income to dirty cash
                var finType = typeof(PlayerFinances);
                // Find the entity-based DoChangeMoney overload
                var methods = finType.GetMethods(BindingFlags.Public | BindingFlags.Instance);
                foreach (var m in methods)
                {
                    if (m.Name == "DoChangeMoney")
                    {
                        var parms = m.GetParameters();
                        // Look for overload with Entity/InventoryModuleData parameter
                        if (parms.Length >= 3)
                        {
                            harmony.Patch(m, prefix: new HarmonyMethod(typeof(DirtyCashPatches), nameof(DoChangeMoneyPrefix)));
                            Debug.Log($"[GameplayTweaks] Dirty cash income conversion patched (params: {parms.Length})");
                            break;
                        }
                    }
                }

                Debug.Log("[GameplayTweaks] Dirty cash economy initialized");
            }
            catch (Exception e) { Debug.LogError($"[GameplayTweaks] DirtyCashPatches failed: {e}"); }
        }

        // Volume fix: dirty-cash should have volume 1 (not 0)
        static void VolumePostfix(object __instance, ref object __result)
        {
            try
            {
                string resName = __instance.ToString();
                if (resName != null && resName.Contains("dirty-cash"))
                {
                    // Override volume to 1
                    var volumeType = __result.GetType();
                    var ctor = volumeType.GetConstructor(new Type[] { typeof(int) });
                    if (ctor != null)
                        __result = ctor.Invoke(new object[] { 1 });
                }
            }
            catch { }
        }

        // Income conversion: intercept illegal income and redirect to dirty cash
        // Returns false to skip original (money goes to dirty cash instead), true to run original normally
        static bool DoChangeMoneyPrefix(object __instance, object[] __args)
        {
            try
            {
                if (__args == null || __args.Length < 2) return true;

                // Parse arguments to find MoneyReason and Price
                object reasonObj = null;
                object priceObj = null;
                foreach (var arg in __args)
                {
                    if (arg == null) continue;
                    string typeName = arg.GetType().Name;
                    if (typeName == "MoneyReason" || typeName.Contains("MoneyReason"))
                        reasonObj = arg;
                    else if (typeName == "Price")
                        priceObj = arg;
                }

                if (reasonObj == null || priceObj == null) return true;

                int reasonVal = Convert.ToInt32(reasonObj);
                // Check if this is illegal income
                bool isDirty = (reasonVal == 40 || reasonVal == 41) // Protection
                    || (reasonVal == 50 || reasonVal == 51) // Raid
                    || (reasonVal >= 71 && reasonVal <= 75) // Gambling
                    || reasonVal == 80 // Combat victory
                    || reasonVal == 60 || reasonVal == 61; // Backroom/illegal sale

                if (!isDirty) return true; // Not illegal, run original normally

                // Read the cash amount from Price.cash (public Fixnum field)
                int amount = 0;
                try
                {
                    var cashField = priceObj.GetType().GetField("cash");
                    if (cashField != null)
                    {
                        var cashVal = cashField.GetValue(priceObj);
                        amount = GameplayTweaksPlugin.ReadFixnum(cashVal);
                    }
                }
                catch { }

                if (amount <= 0) return true; // Only intercept positive income (not expenses)

                // Check this is for the human player
                PlayerInfo human = G.GetHumanPlayer();
                if (human == null) return true;

                var playerField = __instance.GetType().GetField("_player", BindingFlags.NonPublic | BindingFlags.Instance);
                if (playerField != null)
                {
                    var finPlayer = playerField.GetValue(__instance) as PlayerInfo;
                    if (finPlayer != null && finPlayer.PID.id != human.PID.id) return true; // Not human player
                }

                // Redirect: skip the clean money addition, add dirty cash to safehouse instead
                var safehouse = human.territory.Safehouse;
                if (safehouse.IsNotValid) return true;
                var safeEntity = safehouse.FindEntity();
                if (safeEntity == null) return true;

                GameplayTweaksPlugin.AddDirtyCash(safeEntity, amount);
                return false; // Skip original - money goes to dirty cash, not clean
            }
            catch
            {
                return true; // On error, let original run normally
            }
        }

        // Laundering: called during turn update to auto-launder some dirty cash at legal businesses
        internal static void ProcessLaundering()
        {
            try
            {
                PlayerInfo human = G.GetHumanPlayer();
                if (human == null) return;

                var safehouse = human.territory.Safehouse;
                if (safehouse.IsNotValid) return;
                var safeEntity = safehouse.FindEntity();
                if (safeEntity == null) return;

                int dirtyCash = GameplayTweaksPlugin.ReadInventoryAmount(safeEntity, ModConstants.DIRTY_CASH_LABEL);
                if (dirtyCash <= 0) return;

                // Launder 5% of dirty cash per turn (simulates funneling through businesses)
                int laundered = Math.Max(1, (int)(dirtyCash * 0.05f));
                int removed = GameplayTweaksPlugin.RemoveDirtyCash(safeEntity, laundered);
                if (removed > 0)
                {
                    // Add laundered money as clean cash via the financial system
                    human.finances.DoChangeMoneyOnSafehouse(new Price(removed), MoneyReason.Other);
                    if (removed >= 10) Debug.Log($"[GameplayTweaks] Laundered ${removed} dirty cash");
                }
            }
            catch (Exception e) { Debug.LogError($"[GameplayTweaks] Laundering failed: {e}"); }
        }
    }

    // =========================================================================
    // Front Tracking Patch - tracks when gangs take over buildings
    // =========================================================================
    internal static class FrontTrackingPatch
    {
        public static void ApplyPatch(Harmony harmony)
        {
            try
            {
                var territoryType = typeof(PlayerTerritory);
                var takeoverMethod = territoryType.GetMethod("PerformTakeover",
                    BindingFlags.Public | BindingFlags.Instance);
                if (takeoverMethod != null)
                {
                    harmony.Patch(takeoverMethod,
                        postfix: new HarmonyMethod(typeof(FrontTrackingPatch), nameof(TakeoverPostfix)));
                    Debug.Log("[GameplayTweaks] Front tracking patch applied");
                }
            }
            catch (Exception e) { Debug.LogError($"[GameplayTweaks] FrontTrackingPatch failed: {e}"); }
        }

        static void TakeoverPostfix(object __instance, Entity __result)
        {
            try
            {
                if (__result == null) return;
                var territory = __instance as PlayerTerritory;
                if (territory == null) return;
                var player = territory.PlayerInfo;
                if (player == null) return;
                string gangName = player.social?.PlayerGroupName ?? $"Gang #{player.PID.id}";
                string buildingName = __result.ToString() ?? "a building";
                GameplayTweaksPlugin.LogGrapevine($"FRONT: {gangName} opened a front at {buildingName}");
            }
            catch { }
        }
    }

    // =========================================================================
    // Popup Drag Handler Component
    // =========================================================================
    public class PopupDragHandler : MonoBehaviour
    {
        public RectTransform rectTransform;
        private Vector2 _dragOffset;
        private bool _isDragging;

        void Update()
        {
            if (rectTransform == null) return;

            // Start drag on left mouse button down
            if (UnityEngine.Input.GetMouseButtonDown(0))
            {
                Vector2 localPoint;
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    rectTransform, UnityEngine.Input.mousePosition, null, out localPoint))
                {
                    // Check if clicking in header area (top 40 pixels)
                    if (localPoint.y > rectTransform.rect.height / 2 - 40)
                    {
                        _isDragging = true;
                        _dragOffset = (Vector2)rectTransform.position - (Vector2)UnityEngine.Input.mousePosition;
                    }
                }
            }

            // End drag
            if (UnityEngine.Input.GetMouseButtonUp(0))
            {
                _isDragging = false;
            }

            // Drag
            if (_isDragging)
            {
                rectTransform.position = (Vector2)UnityEngine.Input.mousePosition + _dragOffset;
            }

            // Close on right-click outside
            if (UnityEngine.Input.GetMouseButtonDown(1))
            {
                gameObject.SetActive(false);
            }
        }
    }
}
