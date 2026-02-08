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
            var allProp = players.GetType().GetProperty("all");
            if (allProp == null) yield break;
            var all = allProp.GetValue(players) as IEnumerable<PlayerInfo>;
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
        public int LeaderGangId;
        public List<int> MemberIds = new List<int>();
        public float ColorR, ColorG, ColorB;
        public int FormedDays;
        public bool PlayerInvited;

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
        public int PlayerPactId = -1; // -1 means not in a pact
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
        public const int VACATION_DAYS = 3;
        public const int GIFT_BASE_COST = 500;
        public const int TURNS_FOR_DEFECTION = 5;
        public const float UNHAPPY_THRESHOLD = 0.20f;
        public const int FAMILY_REHIRE_COST = 3500;
        public const int TOP_GANG_COUNT = 6;
        public const int TOP_GANG_CREW_CAP = 40;
        public const float INCOME_SHARE_PERCENT = 0.05f;
        public const int BASE_BRIBE_COST = 520; // Increased by 20
        public const int BRIBE_DURATION_DAYS = 7;
        public const int KILLS_FOR_MAX_WANTED = 5;
        public const int MAX_PACTS = 4;

        // Fed arrival countdown based on wanted level
        public const int FED_ARRIVAL_HIGH = 2;
        public const int FED_ARRIVAL_MEDIUM = 5;
        public const int FED_ARRIVAL_LOW = 10;

        // Pact colors - only 4, no purple or green (outline colors)
        public static readonly Color[] PACT_COLORS = new Color[]
        {
            new Color(0.9f, 0.2f, 0.2f),   // Red
            new Color(0.2f, 0.5f, 0.9f),   // Blue
            new Color(0.9f, 0.7f, 0.1f),   // Yellow/Gold
            new Color(0.9f, 0.4f, 0.1f),   // Orange
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

        internal static bool ForceSameEthnicity;
        internal static System.Random SharedRng = new System.Random();
        internal static ModSaveData SaveData = new ModSaveData();
        private static string _saveFilePath;
        internal static GameplayTweaksPlugin Instance;

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

            Logger.LogInfo("Gameplay Tweaks Extended loaded");
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
                sb.AppendLine("{\"NextPactId\":" + SaveData.NextPactId + ",\"PlayerPactId\":" + SaveData.PlayerPactId + ",\"CrewStates\":{");
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
                    sb.Append($"\"FAC\":{s.FedArrivalCountdown},\"FI\":{s.FedsIncoming.ToString().ToLower()}}}");
                }
                sb.AppendLine("},\"Pacts\":[]}");
                File.WriteAllText(_saveFilePath, sb.ToString());
            }
            catch (Exception e) { Debug.LogError($"[GameplayTweaks] Save failed: {e}"); }
        }

        internal static void LoadModData(string saveName)
        {
            try
            {
                string saveDir = Path.Combine(Application.persistentDataPath, "Saves");
                _saveFilePath = Path.Combine(saveDir, $"{saveName}_tweaks.json");
                SaveData = new ModSaveData();
                Debug.Log($"[GameplayTweaks] Initialized mod data");
            }
            catch (Exception e)
            {
                Debug.LogError($"[GameplayTweaks] Load failed: {e}");
                SaveData = new ModSaveData();
            }
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
            private static Text _streetCreditLevelText, _wantedLevelText;
            private static Button _btnUnderboss, _btnMarryChild, _btnHireFamily, _btnVacation, _btnGift;
            private static Button _btnBribeMayor, _btnBribeJudge;
            private static Text _txtUnderboss, _txtMarryChild, _txtHireFamily, _txtVacation, _txtGift;
            private static Text _txtBribeMayor, _txtBribeJudge;

            // Lawyer UI (jail system)
            private static Text _txtLawyerStatus, _txtPayLawyer;
            private static Button _btnPayLawyer;
            private static GameObject _lawyerSection, _lawyerRow;

            // Pact UI
            private static Button _btnViewPacts, _btnJoinPact, _btnCreatePact, _btnLeavePact;
            private static Text _txtPactStatus;

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

                // Header row with navigation and close
                var headerRow = CreateHorizontalRow(_handlerPopup.transform, "HeaderRow");
                headerRow.GetComponent<LayoutElement>().minHeight = 32;
                _btnPrevCrew = CreateButton(headerRow.transform, "PrevCrew", "<", OnPrevCrew);
                _btnPrevCrew.GetComponent<LayoutElement>().preferredWidth = 40;
                _titleText = CreateLabel(headerRow.transform, "Title", "Crew Relations", 16, FontStyle.Bold);
                _titleText.alignment = TextAnchor.MiddleCenter;
                _btnNextCrew = CreateButton(headerRow.transform, "NextCrew", ">", OnNextCrew);
                _btnNextCrew.GetComponent<LayoutElement>().preferredWidth = 40;
                var closeBtn = CreateButton(headerRow.transform, "Close", "X", ClosePopup);
                closeBtn.GetComponent<LayoutElement>().preferredWidth = 35;

                // Street Credit Section
                CreateLabel(_handlerPopup.transform, "SCLabel", "Street Credit", 12, FontStyle.Bold);
                CreateStatBar(_handlerPopup.transform, "StreetCreditBar", new Color(0.85f, 0.7f, 0.2f), out _streetCreditFill);
                _streetCreditLevelText = CreateLabel(_handlerPopup.transform, "SCLevel", "Level: 0", 11, FontStyle.Normal);

                // Wanted Section
                CreateLabel(_handlerPopup.transform, "WLabel", "Wanted Level", 12, FontStyle.Bold);
                CreateStatBar(_handlerPopup.transform, "WantedBar", new Color(0.8f, 0.2f, 0.2f), out _wantedFill);
                _wantedLevelText = CreateLabel(_handlerPopup.transform, "WLevel", "Status: None", 11, FontStyle.Normal);

                // Bribe buttons row
                var bribeRow = CreateHorizontalRow(_handlerPopup.transform, "BribeRow");
                _btnBribeMayor = CreateButton(bribeRow.transform, "BribeMayor", "Bribe Mayor", OnBribeMayor);
                _btnBribeJudge = CreateButton(bribeRow.transform, "BribeJudge", "Bribe Judge", OnBribeJudge);
                _txtBribeMayor = _btnBribeMayor.GetComponentInChildren<Text>();
                _txtBribeJudge = _btnBribeJudge.GetComponentInChildren<Text>();

                // Lawyer/Legal Section (only shows when in jail)
                _lawyerSection = CreateLabel(_handlerPopup.transform, "LawyerStatus", "", 11, FontStyle.Normal).gameObject;
                _txtLawyerStatus = _lawyerSection.GetComponent<Text>();
                _lawyerRow = CreateHorizontalRow(_handlerPopup.transform, "LawyerRow");
                _btnPayLawyer = CreateButton(_lawyerRow.transform, "PayLawyer", "Hire Lawyer ($1000)", OnPayLawyer);
                _txtPayLawyer = _btnPayLawyer.GetComponentInChildren<Text>();
                // Initially hide lawyer section
                _lawyerSection.SetActive(false);
                _lawyerRow.SetActive(false);

                // Happiness Section
                CreateLabel(_handlerPopup.transform, "HLabel", "Happiness", 12, FontStyle.Bold);
                CreateStatBar(_handlerPopup.transform, "HappinessBar", new Color(0.2f, 0.75f, 0.3f), out _happinessFill);

                // Happiness buttons row
                var happyRow = CreateHorizontalRow(_handlerPopup.transform, "HappyRow");
                _btnVacation = CreateButton(happyRow.transform, "Vacation", "Vacation", OnVacation);
                _btnGift = CreateButton(happyRow.transform, "Gift", "Gift", OnGift);
                _txtVacation = _btnVacation.GetComponentInChildren<Text>();
                _txtGift = _btnGift.GetComponentInChildren<Text>();

                // Crew Actions Section
                CreateLabel(_handlerPopup.transform, "ActionsLabel", "Crew Actions", 12, FontStyle.Bold);
                var actionsRow = CreateHorizontalRow(_handlerPopup.transform, "ActionsRow");
                _btnUnderboss = CreateButton(actionsRow.transform, "Underboss", "Underboss", OnUnderboss);
                _txtUnderboss = _btnUnderboss.GetComponentInChildren<Text>();
                _btnMarryChild = CreateButton(actionsRow.transform, "MarryChild", "Spouse", OnMarryChild);
                _txtMarryChild = _btnMarryChild.GetComponentInChildren<Text>();
                _btnHireFamily = CreateButton(actionsRow.transform, "HireFamily", "Hire Family", OnHireFamily);
                _txtHireFamily = _btnHireFamily.GetComponentInChildren<Text>();

                // Pact Section
                CreateLabel(_handlerPopup.transform, "PactLabel", "Gang Pacts", 12, FontStyle.Bold);
                _txtPactStatus = CreateLabel(_handlerPopup.transform, "PactStatus", "Not in a pact", 11, FontStyle.Normal);
                var pactRow = CreateHorizontalRow(_handlerPopup.transform, "PactRow");
                _btnViewPacts = CreateButton(pactRow.transform, "ViewPacts", "View", OnViewPacts);
                _btnJoinPact = CreateButton(pactRow.transform, "JoinPact", "Join", OnJoinPact);
                _btnCreatePact = CreateButton(pactRow.transform, "CreatePact", "Create", OnCreatePact);
                _btnLeavePact = CreateButton(pactRow.transform, "LeavePact", "Leave", OnLeavePact);

                // Add drag handler to header
                AddDragHandler(_handlerPopup, rt);

                _handlerPopup.SetActive(false);
            }

            private static void ClosePopup()
            {
                _popupVisible = false;
                _handlerPopup.SetActive(false);
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
                int mayorBribeCost = GetBribeCost(state.WantedLevel);
                _txtBribeMayor.text = state.MayorBribeActive ? "Mayor (Active)" : $"Mayor (${mayorBribeCost})";
                _txtBribeJudge.text = state.JudgeBribeActive ? "Judge (Active)" : $"Judge (${mayorBribeCost * 2})";
                _btnBribeMayor.interactable = !state.MayorBribeActive && state.WantedLevel != WantedLevel.None;
                _btnBribeJudge.interactable = !state.JudgeBribeActive && state.WantedLevel != WantedLevel.None;

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

                // Happiness
                _happinessFill.fillAmount = state.HappinessValue;
                if (state.OnVacation)
                    _txtVacation.text = "On Vacation";
                else if (state.VacationPending)
                    _txtVacation.text = "Vacation Pending";
                else
                    _txtVacation.text = "Vacation";
                _btnVacation.interactable = !state.OnVacation && !state.VacationPending;
                int giftCost = ModConstants.GIFT_BASE_COST * Math.Max(1, state.StreetCreditLevel);
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

                // Pacts - show gang names
                var playerPact = SaveData.PlayerPactId >= 0 ? SaveData.Pacts.FirstOrDefault(p => p.PactId == $"pact_{SaveData.PlayerPactId}") : null;
                if (playerPact != null)
                {
                    var allPlayers = G.GetAllPlayers().ToList();
                    var leader = allPlayers.FirstOrDefault(p => p.PID.id == playerPact.LeaderGangId);
                    string leaderName = leader?.social?.PlayerGroupName ?? $"Gang #{playerPact.LeaderGangId}";
                    var memberNames = playerPact.MemberIds
                        .Select(id => allPlayers.FirstOrDefault(p => p.PID.id == id)?.social?.PlayerGroupName ?? $"#{id}")
                        .ToList();
                    string pactInfo = memberNames.Count > 0
                        ? $"Pact: {leaderName} + {string.Join(", ", memberNames)}"
                        : $"Pact: {leaderName} (leader)";
                    _txtPactStatus.text = pactInfo;
                    _btnJoinPact.gameObject.SetActive(false);
                    _btnCreatePact.gameObject.SetActive(false);
                    _btnLeavePact.gameObject.SetActive(true);
                }
                else
                {
                    _txtPactStatus.text = $"Not in a pact ({SaveData.Pacts.Count} active)";
                    _btnJoinPact.gameObject.SetActive(SaveData.Pacts.Count > 0 && SaveData.Pacts.Count < ModConstants.MAX_PACTS);
                    _btnCreatePact.gameObject.SetActive(SaveData.Pacts.Count < ModConstants.MAX_PACTS);
                    _btnLeavePact.gameObject.SetActive(false);
                }
            }

            // Button Handlers
            private static void OnUnderboss()
            {
                if (_selectedPeep == null) return;
                var state = GetOrCreateCrewState(_selectedPeep.Id);
                if (state == null || state.IsUnderboss) return;

                var xp = _selectedPeep.data.agent.xp;
                if (xp == null) return;

                // No captain requirement - directly promote to underboss
                xp.SetCrewRole(new Label("underboss"));
                state.IsUnderboss = true;

                // Trigger proper game event
                try
                {
                    var eventsField = G.ctx?.GetType().GetField("events");
                    var events = eventsField?.GetValue(G.ctx);
                    var enqueueMethod = events?.GetType().GetMethod("EnqueueOnce");
                    var sessionEventType = typeof(GameClock).Assembly.GetType("Game.Session.SessionEventType");
                    var crewRoleGiven = sessionEventType?.GetField("CrewRoleGiven")?.GetValue(null);
                    if (enqueueMethod != null && crewRoleGiven != null)
                        enqueueMethod.Invoke(events, new[] { crewRoleGiven });
                }
                catch (Exception e) { Debug.LogWarning($"[GameplayTweaks] Could not trigger CrewRoleGiven event: {e.Message}"); }

                Debug.Log($"[GameplayTweaks] {_selectedPeep.data.person.FullName} promoted to Underboss");
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

            private static void OnHireFamily()
            {
                if (_selectedPeep == null)
                {
                    Debug.LogWarning("[GameplayTweaks] OnHireFamily: No selected peep");
                    return;
                }

                var relatives = FindAllRelatives(_selectedPeep);
                if (relatives.Count == 0)
                {
                    Debug.Log($"[GameplayTweaks] {_selectedPeep.data.person.FullName} has no eligible relatives to hire");
                    return;
                }

                PlayerCrew crew = G.GetHumanCrew();
                if (crew == null)
                {
                    Debug.LogWarning("[GameplayTweaks] OnHireFamily: Could not get player crew");
                    return;
                }

                if (!crew.CanAddCrew(1))
                {
                    Debug.Log("[GameplayTweaks] Crew is full, cannot hire more");
                    return;
                }

                // Hire first available relative
                Entity relative = relatives[0];
                try
                {
                    crew.HireNewCrewMemberUnassigned(relative, _selectedPeep);
                    Debug.Log($"[GameplayTweaks] Hired {relative.data.person.FullName} as new crew member");
                }
                catch (Exception e)
                {
                    Debug.LogError($"[GameplayTweaks] Failed to hire relative: {e}");
                }
                RefreshHandlerUI();
            }

            private static void OnVacation()
            {
                if (_selectedPeep == null) return;
                var state = GetOrCreateCrewState(_selectedPeep.Id);
                if (state == null || state.OnVacation || state.VacationPending) return;

                // Mark as pending - will leave on next turn start
                state.VacationPending = true;
                state.VacationDuration = ModConstants.VACATION_DAYS;
                state.HappinessValue = Mathf.Clamp01(state.HappinessValue + 0.3f);
                state.TurnsUnhappy = 0;

                Debug.Log($"[GameplayTweaks] {_selectedPeep.data.person.FullName} will go on vacation next turn for {state.VacationDuration} days");
                RefreshHandlerUI();
            }

            private static void OnGift()
            {
                if (_selectedPeep == null) return;
                var state = GetOrCreateCrewState(_selectedPeep.Id);
                if (state == null) return;

                int cost = ModConstants.GIFT_BASE_COST * Math.Max(1, state.StreetCreditLevel);
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

            private static void OnBribeMayor()
            {
                if (_selectedPeep == null) return;
                var state = GetOrCreateCrewState(_selectedPeep.Id);
                if (state == null || state.MayorBribeActive) return;

                int cost = GetBribeCost(state.WantedLevel);
                PlayerInfo human = G.GetHumanPlayer();
                try
                {
                    human.finances.DoChangeMoneyOnSafehouse(new Price(-cost), MoneyReason.Other);
                    state.MayorBribeActive = true;
                    state.BribeExpires = G.GetNow().IncrementDays(ModConstants.BRIBE_DURATION_DAYS);
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
                    state.JudgeBribeActive = true;
                    state.BribeExpires = G.GetNow().IncrementDays(ModConstants.BRIBE_DURATION_DAYS);
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

            private static void OnViewPacts()
            {
                var allPlayers = G.GetAllPlayers().ToList();
                var sb = new System.Text.StringBuilder("Active Pacts:\n");
                foreach (var pact in SaveData.Pacts)
                {
                    var leader = allPlayers.FirstOrDefault(p => p.PID.id == pact.LeaderGangId);
                    string leaderName = leader?.social?.PlayerGroupName ?? $"Gang #{pact.LeaderGangId}";
                    sb.Append($"- {pact.PactId}: Led by {leaderName}");

                    // List member gangs
                    var memberNames = new List<string>();
                    foreach (int memberId in pact.MemberIds)
                    {
                        var member = allPlayers.FirstOrDefault(p => p.PID.id == memberId);
                        memberNames.Add(member?.social?.PlayerGroupName ?? $"Gang #{memberId}");
                    }
                    if (memberNames.Count > 0)
                        sb.Append($" + {string.Join(", ", memberNames)}");
                    sb.AppendLine();
                }
                Debug.Log($"[GameplayTweaks] {sb}");
            }

            private static void OnJoinPact()
            {
                if (SaveData.Pacts.Count == 0) return;
                var pact = SaveData.Pacts[0];
                PlayerInfo human = G.GetHumanPlayer();
                if (human != null && !pact.MemberIds.Contains(human.PID.id))
                {
                    pact.MemberIds.Add(human.PID.id);
                    SaveData.PlayerPactId = SaveData.Pacts.IndexOf(pact);
                    Debug.Log($"[GameplayTweaks] Joined pact {pact.PactId}");
                    TerritoryColorPatch.RefreshAllTerritoryColors();
                }
                RefreshHandlerUI();
            }

            private static void OnCreatePact()
            {
                PlayerInfo human = G.GetHumanPlayer();
                if (human == null) return;

                var pact = new AlliancePact
                {
                    PactId = $"pact_{SaveData.NextPactId++}",
                    LeaderGangId = human.PID.id,
                    SharedColor = ModConstants.PACT_COLORS[SaveData.Pacts.Count % ModConstants.PACT_COLORS.Length],
                    Formed = G.GetNow()
                };
                SaveData.Pacts.Add(pact);
                SaveData.PlayerPactId = SaveData.Pacts.Count - 1;
                Debug.Log($"[GameplayTweaks] Created pact {pact.PactId}");
                TerritoryColorPatch.RefreshAllTerritoryColors();
                RefreshHandlerUI();
            }

            private static void OnLeavePact()
            {
                if (SaveData.PlayerPactId < 0) return;
                PlayerInfo human = G.GetHumanPlayer();
                var pact = SaveData.Pacts.FirstOrDefault(p => p.PactId == $"pact_{SaveData.PlayerPactId}");
                if (pact != null && human != null)
                {
                    pact.MemberIds.Remove(human.PID.id);
                    if (pact.LeaderGangId == human.PID.id)
                        SaveData.Pacts.Remove(pact);
                }
                SaveData.PlayerPactId = -1;
                TerritoryColorPatch.RefreshAllTerritoryColors();
                RefreshHandlerUI();
            }

            // UI Helpers
            private static Text CreateLabel(Transform parent, string name, string text, int size, FontStyle style)
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

            private static Button CreateButton(Transform parent, string name, string label, Action onClick)
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

            private static GameObject CreateHorizontalRow(Transform parent, string name)
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

            static void IncrementStatPostfix(object __instance, CrewStats key, int delta)
            {
                if (!EnableCrewStats.Value) return;
                try
                {
                    var entityField = typeof(AgentComponent).BaseType?.GetField("_entity", BindingFlags.NonPublic | BindingFlags.Instance);
                    Entity entity = entityField?.GetValue(__instance) as Entity;
                    if (entity == null) return;

                    PlayerInfo human = G.GetHumanPlayer();
                    if (human == null || entity.data.agent.pid != human.PID) return;

                    CrewModState state = GetOrCreateCrewState(entity.Id);
                    if (state == null) return;

                    // Track stats for street credit
                    if (key == CrewStats.TimesFought)
                        state.StreetCreditProgress += delta * ModConstants.STREET_CREDIT_PER_FIGHT;
                    else if (key == CrewStats.PeepsKilled)
                    {
                        state.StreetCreditProgress += delta * ModConstants.STREET_CREDIT_PER_KILL;

                        // Update wanted level based on kills
                        int kills = 0;
                        entity.data.agent.crewHistoryStats.TryGetValue(CrewStats.PeepsKilled, out kills);

                        if (kills >= ModConstants.KILLS_FOR_MAX_WANTED)
                            state.WantedLevel = WantedLevel.High;
                        else if (kills >= 3)
                            state.WantedLevel = WantedLevel.Medium;
                        else if (kills >= 1)
                            state.WantedLevel = WantedLevel.Low;

                        // Higher street credit = faster wanted gain
                        state.WantedProgress += (0.1f + state.StreetCreditLevel * 0.02f) * delta;
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

                    if (EnableAIAlliances.Value)
                        ProcessAIAlliances(now);

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
                        Debug.Log($"[GameplayTweaks] Child born for {peep.data.person.FullName}");
                    }
                    state.LastFutureKidsCount = currentKids;
                }

                // Street Credit level up - GRANT ACTUAL RESOURCES
                if (state.StreetCreditProgress >= 1f)
                {
                    state.StreetCreditLevel++;
                    state.StreetCreditProgress = 0f;
                    GrantStreetCredit(player, ModConstants.STREET_CREDIT_REWARD_COUNT);
                    Debug.Log($"[GameplayTweaks] {peep.data.person.FullName} street credit level {state.StreetCreditLevel}!");
                }

                // Happiness based on street credit
                float baseHappiness = 1f - (state.StreetCreditLevel * ModConstants.HAPPINESS_LOSS_PER_LEVEL);
                state.HappinessValue = Mathf.Clamp01(baseHappiness);

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

                // Vacation return
                if (state.OnVacation && now >= state.VacationReturns)
                {
                    state.OnVacation = false;
                    state.VacationPending = false;
                    Debug.Log($"[GameplayTweaks] {peep.data.person.FullName} returned from vacation");
                }

                // Bribe expiration
                if (state.MayorBribeActive && now >= state.BribeExpires)
                    state.MayorBribeActive = false;
                if (state.JudgeBribeActive && now >= state.BribeExpires)
                    state.JudgeBribeActive = false;

                // Wanted level update based on stats
                var crewStats = peep.data.agent.crewHistoryStats;
                int kills = 0;
                crewStats.TryGetValue(CrewStats.PeepsKilled, out kills);

                // 5+ kills = max wanted
                if (kills >= ModConstants.KILLS_FOR_MAX_WANTED)
                {
                    state.WantedLevel = WantedLevel.High;
                    state.WantedProgress = 1f;
                }
                else
                {
                    int fights = 0, booze = 0;
                    crewStats.TryGetValue(CrewStats.TimesFought, out fights);
                    crewStats.TryGetValue(CrewStats.BoozeSold, out booze);

                    // Calculate wanted with street credit multiplier
                    float multiplier = 1f + state.StreetCreditLevel * 0.15f;
                    state.WantedProgress = Mathf.Clamp01((kills * 0.2f + fights * 0.03f + booze * 0.002f) * multiplier);

                    if (state.WantedProgress >= 0.75f) state.WantedLevel = WantedLevel.High;
                    else if (state.WantedProgress >= 0.5f) state.WantedLevel = WantedLevel.Medium;
                    else if (state.WantedProgress >= 0.25f) state.WantedLevel = WantedLevel.Low;
                    else state.WantedLevel = WantedLevel.None;
                }

                // Fed arrival system - start countdown when wanted, auto-arrest when countdown hits 0
                if (state.WantedLevel != WantedLevel.None && !state.FedsIncoming && !state.MayorBribeActive && !state.JudgeBribeActive)
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
                if (state.FedsIncoming && !state.MayorBribeActive && !state.JudgeBribeActive)
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
                        }
                    }
                    foreach (var pact in pactsToRemove)
                    {
                        SaveData.Pacts.Remove(pact);
                        // Reset player pact if they were in this pact
                        if (SaveData.PlayerPactId >= 0 && pact.PactId == $"pact_{SaveData.PlayerPactId}")
                            SaveData.PlayerPactId = -1;
                    }

                    if (aiGangs.Count < 2) return;

                    var gangPowers = aiGangs
                        .Select(g => new { Gang = g, Power = CalculateGangPower(g) })
                        .OrderByDescending(x => x.Power)
                        .ToList();

                    // Form pacts periodically - max 4 pacts
                    if (now.days % 30 == 0 && SaveData.Pacts.Count < ModConstants.MAX_PACTS)
                    {
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
                                    LeaderGangId = leader.Gang.PID.id,
                                    MemberIds = new List<int> { member.Gang.PID.id },
                                    SharedColor = ModConstants.PACT_COLORS[SaveData.Pacts.Count % ModConstants.PACT_COLORS.Length],
                                    Formed = now
                                };
                                SaveData.Pacts.Add(pact);
                                Debug.Log($"[GameplayTweaks] AI Alliance: {leader.Gang.social.PlayerGroupName} + {member.Gang.social.PlayerGroupName}");
                            }
                        }
                    }
                }
                catch (Exception e) { Debug.LogError($"[GameplayTweaks] ProcessAIAlliances: {e}"); }
            }
        }

        // =====================================================================
        // 7. AI Crew Cap
        // =====================================================================
        private static class AICrewCapPatch
        {
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
                    var playerField = __instance.GetType().GetField("player", BindingFlags.NonPublic | BindingFlags.Instance);
                    var player = playerField?.GetValue(__instance) as PlayerInfo;
                    if (player == null || player.PID.IsHumanPlayer) return;

                    var topGangs = G.GetAllPlayers()
                        .Where(p => p.IsJustGang && !p.crew.IsCrewDefeated)
                        .Select(p => new { Player = p, Power = CalculateGangPower(p) })
                        .OrderByDescending(x => x.Power)
                        .Take(ModConstants.TOP_GANG_COUNT)
                        .ToList();

                    if (topGangs.Any(x => x.Player.PID == player.PID))
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

                    // Also patch PlayerTerritoryDisplay.SetBorderMeshes for outline colors
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
                    if (pact != null)
                    {
                        __result = pact.SharedColor;
                        // Clear the cached color so it updates
                        if (_colorsField != null)
                        {
                            var colors = _colorsField.GetValue(__instance) as System.Collections.IDictionary;
                            if (colors != null && colors.Contains(player.PID))
                            {
                                colors[player.PID] = pact.SharedColor;
                            }
                        }
                    }
                }
                catch { }
            }

            static void RefreshBorderPrefix(object __instance)
            {
                // Force territory display to use pact colors when refreshing
                if (!EnableAIAlliances.Value) return;
                try
                {
                    var managerField = __instance.GetType().GetField("_manager", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (managerField == null) return;

                    var manager = managerField.GetValue(__instance);
                    if (manager == null) return;

                    PlayerInfo player = null;
                    var playerField = manager.GetType().GetField("_player", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (playerField != null)
                    {
                        player = playerField.GetValue(manager) as PlayerInfo;
                    }
                    else
                    {
                        var playerProp = manager.GetType().GetProperty("player", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
                        player = playerProp?.GetValue(manager) as PlayerInfo;
                    }

                    if (player == null) return;

                    var pact = GetPactForPlayer(player.PID);
                    if (pact != null)
                    {
                        // Update the color info to use pact color
                        var colorInfoField = manager.GetType().GetField("colorInfo", BindingFlags.Public | BindingFlags.Instance);
                        if (colorInfoField != null)
                        {
                            // Can't directly modify struct, so we just log for now
                            Debug.Log($"[GameplayTweaks] Pact color applied for {player.social?.PlayerGroupName}");
                        }
                    }
                }
                catch { }
            }

            // Public method to force refresh all territory colors
            public static void RefreshAllTerritoryColors()
            {
                try
                {
                    var displayType = typeof(GameClock).Assembly.GetType("Game.Session.Player.PlayerTerritoryDisplay");
                    var refreshMethod = displayType?.GetMethod("RefreshBorder", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
                    var displayField = typeof(PlayerTerritory).GetField("_display", BindingFlags.NonPublic | BindingFlags.Instance);

                    foreach (var player in G.GetAllPlayers())
                    {
                        if (player?.territory == null) continue;

                        // Get the display object using reflection
                        var display = displayField?.GetValue(player.territory);
                        if (display == null) continue;

                        // Try to call RefreshBorder
                        try
                        {
                            if (refreshMethod != null)
                            {
                                refreshMethod.Invoke(display, null);
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

            static void LoadPostfix(string saveName) => LoadModData(saveName);
            static void SavePostfix(string saveName) => SaveModData();
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
