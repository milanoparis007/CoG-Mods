using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using Game.Core;
using Game.Services;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Player;
using Game.Session.Sim;
using Game.UI.Session.OwnedBiz;
using Game.UI.Session.OwnedGambling;
using HarmonyLib;
using MonoMod.RuntimeDetour;
using UnityEngine;
using UnityEngine.UI;

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
            try
            {
                return BuildingUtil.FindOwnerOrManagerForAnyBuilding(building);
            }
            catch { return null; }
        }
    }

    [BepInPlugin("com.mods.gameplaytweaks", "Gameplay Tweaks", "1.0.0")]
    public class GameplayTweaksPlugin : BaseUnityPlugin
    {
        internal static ConfigEntry<bool> EnableSpouseEthnicity;
        internal static ConfigEntry<float> SpouseEthnicityChance;
        internal static ConfigEntry<bool> EnableHireableAge;
        internal static ConfigEntry<float> HireableMinAge;

        internal static bool ForceSameEthnicity;
        internal static System.Random SharedRng = new System.Random();

        private void Awake()
        {
            EnableSpouseEthnicity = Config.Bind("SpouseEthnicity", "Enabled", true,
                "Spouses have a configurable chance to share the same ethnicity.");
            SpouseEthnicityChance = Config.Bind("SpouseEthnicity", "SameEthnicityChance", 0.80f,
                "Probability (0.0-1.0) that a spouse will be the same ethnicity.");

            EnableHireableAge = Config.Bind("HireableAge", "Enabled", true,
                "Change the minimum age for characters to become hireable crew members.");
            HireableMinAge = Config.Bind("HireableAge", "MinAge", 16f,
                "Minimum age in years before a character can be hired. Game default is 20.");

            var harmony = new Harmony("com.mods.gameplaytweaks");
            harmony.PatchAll(typeof(SpouseEthnicityLinkPatch));
            harmony.PatchAll(typeof(SpouseEthnicityCandidatePatch));
            HireableAgePatch.ApplyManualDetour();
            harmony.PatchAll(typeof(GamblingDialogRefreshPatch));

            Logger.LogInfo("Gameplay Tweaks loaded:");
            Logger.LogInfo($"  SpouseEthnicity: {EnableSpouseEthnicity.Value} (chance={SpouseEthnicityChance.Value})");
            Logger.LogInfo($"  HireableAge: {EnableHireableAge.Value} (min={HireableMinAge.Value})");
            Logger.LogInfo($"  Gambling UI buttons: active");
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

                PersonData p1 = current.data.person;
                PersonData p2 = other.data.person;
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
        // 2. Hireable Age — MonoMod detour
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
                    Debug.Log("[GameplayTweaks] HireableAgePatch applied via MonoMod detour.");
                }
                catch (Exception e)
                {
                    Debug.LogError($"[GameplayTweaks] HireableAgePatch detour failed: {e}");
                }
            }

            static bool Replacement(SimTime now, Entity person)
            {
                if (!EnableHireableAge.Value && _originalTrampoline != null)
                    return _originalTrampoline(now, person);

                try
                {
                    PersonData pdata = person.data.person;
                    if (pdata.IsAlive
                        && pdata.GetAge(now).YearsFloat >= HireableMinAge.Value
                        && pdata.business.IsNotValid
                        && pdata.resassigned.IsNotValid
                        && G.GetPoliticianData(person.Id) == null)
                    {
                        return person.data.agent.pid.id == 0;
                    }
                    return false;
                }
                catch (Exception e)
                {
                    Debug.LogError($"[GameplayTweaks] HireableAgePatch error: {e}");
                    return false;
                }
            }
        }

        // =====================================================================
        // 3. Gambling Dialog UI — Marry/Child & Hire Family buttons
        //    Adds two toggle buttons to the left side of the gambling dialog.
        // =====================================================================

        private static GameObject _btnMarryChild;
        private static GameObject _btnHireFamily;
        private static FieldInfo _goField;
        private static FieldInfo _modelField;
        private static PropertyInfo _visitProp;
        private static MethodInfo _getTmplMethod;
        private static Type _moduleToggleContextType;
        private static FieldInfo _mouseoverField;

        static GameplayTweaksPlugin()
        {
            _goField = AccessTools.Field(typeof(OwnedGamblingDialog), "_go");
            _modelField = AccessTools.Field(typeof(OwnedGamblingDialog).BaseType, "Model");
            _getTmplMethod = AccessTools.Method(typeof(OwnedGamblingDialog), "GetTmpl");
            _moduleToggleContextType = typeof(OwnedGamblingDialog).Assembly.GetType("Game.UI.Session.OwnedBiz.ModuleToggleContext");
            if (_moduleToggleContextType != null)
                _mouseoverField = AccessTools.Field(_moduleToggleContextType, "mouseover");
        }

        private static Entity GetManagerFromDialog(OwnedGamblingDialog dialog)
        {
            try
            {
                var model = _modelField?.GetValue(dialog);
                if (model == null) return null;
                if (_visitProp == null) _visitProp = model.GetType().GetProperty("visit");
                var visit = _visitProp?.GetValue(model);
                if (visit == null) return null;
                var buildingField = visit.GetType().GetField("building");
                var building = buildingField?.GetValue(visit) as Entity;
                return G.GetManager(building);
            }
            catch { return null; }
        }

        [HarmonyPatch(typeof(OwnedGamblingDialog), "RefreshModuleButtons")]
        private static class GamblingDialogRefreshPatch
        {
            [HarmonyPostfix]
            static void Postfix(OwnedGamblingDialog __instance)
            {
                try
                {
                    var go = _goField?.GetValue(__instance) as GameObject;
                    if (go == null) return;

                    var buttonsContainer = go.transform.Find("Background/Modules/Buttons");
                    if (buttonsContainer == null) return;

                    Entity manager = GetManagerFromDialog(__instance);
                    if (manager == null) return;

                    // Create buttons if they don't exist
                    if (_btnMarryChild == null || _btnMarryChild.transform.parent != buttonsContainer)
                    {
                        CreateCustomButtons(__instance, buttonsContainer, manager);
                    }

                    // Refresh button states
                    RefreshCustomButtons(manager);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[GameplayTweaks] GamblingDialogRefreshPatch error: {e}");
                }
            }
        }

        private static void CreateCustomButtons(OwnedGamblingDialog dialog, Transform container, Entity manager)
        {
            // Find template button via reflection (GetTmpl is internal)
            var tmplButton = _getTmplMethod?.Invoke(dialog, new object[] { "Templates/Module Toggle" }) as GameObject;
            if (tmplButton == null) return;

            // Create Marry/Child button
            _btnMarryChild = CreateToggleButton(container, tmplButton, "MarryChild",
                () => OnMarryChildClick(manager));

            // Create Hire Family button
            _btnHireFamily = CreateToggleButton(container, tmplButton, "HireFamily",
                () => OnHireFamilyClick(manager));
        }

        private static GameObject CreateToggleButton(Transform container, GameObject template, string name, Action onClick)
        {
            var btn = UnityEngine.Object.Instantiate(template, container);
            btn.name = name;

            var toggle = btn.GetComponentInChildren<Toggle>();
            if (toggle != null)
            {
                toggle.interactable = true;
                toggle.group = null; // Not part of the toggle group
                toggle.onValueChanged.RemoveAllListeners();
                toggle.onValueChanged.AddListener((isOn) => { if (isOn) { toggle.isOn = false; onClick(); } });
            }

            // Set a simple icon/text indicator
            var cardImg = btn.transform.Find("Toggle/Card")?.GetComponent<Image>();
            var iconImg = btn.transform.Find("Toggle/Image")?.GetComponent<Image>();
            if (cardImg != null) cardImg.enabled = false;
            if (iconImg != null) iconImg.enabled = false;

            // Add text label
            var textGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(btn.transform, false);
            var text = textGo.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = 10;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleCenter;
            text.text = name == "MarryChild" ? "M" : "H";

            var rt = textGo.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            btn.SetActive(true);
            return btn;
        }

        private static void RefreshCustomButtons(Entity manager)
        {
            if (_btnMarryChild == null || _btnHireFamily == null) return;

            RelationshipTracker rels = G.GetRels();
            if (rels == null) return;

            RelationshipList relList = rels.GetListOrNull(manager.Id);
            bool hasSpouse = relList != null && relList.HasSpouse();

            // Update marry/child button tooltip via ModuleToggleContext (internal type, use reflection)
            if (_moduleToggleContextType != null && _mouseoverField != null)
            {
                var marryCtx = _btnMarryChild.GetComponent(_moduleToggleContextType);
                if (marryCtx != null)
                    _mouseoverField.SetValue(marryCtx, hasSpouse ? "Have Child" : "Find Spouse & Marry");
            }

            // Update hire family button
            List<Entity> eligible = FindEligibleFamily(manager);
            if (_moduleToggleContextType != null && _mouseoverField != null)
            {
                var hireCtx = _btnHireFamily.GetComponent(_moduleToggleContextType);
                if (hireCtx != null)
                    _mouseoverField.SetValue(hireCtx, eligible.Count > 0
                        ? $"Hire Family ({eligible.Count} available)"
                        : "Hire Family (none eligible)");
            }

            // Set interactable state
            var marryToggle = _btnMarryChild.GetComponentInChildren<Toggle>();
            var hireToggle = _btnHireFamily.GetComponentInChildren<Toggle>();

            if (marryToggle != null)
            {
                if (!hasSpouse)
                {
                    PeopleTracker pt = G.GetPeopleGen();
                    Entity match = pt?.FindRandoToMarry(manager);
                    marryToggle.interactable = match != null;
                }
                else
                {
                    marryToggle.interactable = true;
                }
            }

            if (hireToggle != null)
            {
                PlayerCrew crew = G.GetHumanCrew();
                hireToggle.interactable = eligible.Count > 0 && crew != null && crew.CanAddCrew(1);
            }
        }

        private static void OnMarryChildClick(Entity manager)
        {
            try
            {
                RelationshipTracker rels = G.GetRels();
                if (rels == null) return;

                RelationshipList relList = rels.GetListOrNull(manager.Id);
                bool hasSpouse = relList != null && relList.HasSpouse();

                if (hasSpouse)
                {
                    Entity spouse = relList.GetSpouse();
                    Entity mother = (manager.data.person.g == Gender.F) ? manager : spouse;
                    SimTime now = G.GetNow();
                    SimTime birthTime = now.IncrementDays(-1);
                    mother.data.person.futurekids.Add(birthTime);
                    mother.data.person.futurekids.Sort(SimTime.CompareDescending);
                    Debug.Log($"[GameplayTweaks] Scheduled child for {mother.data.person.FullName}");
                }
                else
                {
                    PeopleTracker pt = G.GetPeopleGen();
                    Entity match = pt?.FindRandoToMarry(manager);
                    if (match != null)
                    {
                        pt.ForceMarry(manager, match);
                        Debug.Log($"[GameplayTweaks] Married {manager.data.person.FullName} to {match.data.person.FullName}");
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[GameplayTweaks] OnMarryChildClick error: {e}");
            }
        }

        private static void OnHireFamilyClick(Entity manager)
        {
            try
            {
                List<Entity> eligible = FindEligibleFamily(manager);
                if (eligible.Count == 0) return;

                PlayerCrew playerCrew = G.GetHumanCrew();
                if (playerCrew == null || !playerCrew.CanAddCrew(1)) return;

                Entity toHire = eligible[0];
                playerCrew.HireNewCrewMemberUnassigned(toHire, manager);
                Debug.Log($"[GameplayTweaks] Hired {toHire.data.person.FullName} (introduced by {manager.data.person.FullName})");
            }
            catch (Exception e)
            {
                Debug.LogError($"[GameplayTweaks] OnHireFamilyClick error: {e}");
            }
        }

        private static List<Entity> FindEligibleFamily(Entity peep)
        {
            var result = new List<Entity>();
            RelationshipTracker rels = G.GetRels();
            SimTime now = G.GetNow();

            if (rels == null) return result;

            RelationshipList relList = rels.GetListOrNull(peep.Id);
            if (relList == null) return result;

            foreach (Relationship rel in relList.data)
            {
                if (rel.type != RelationshipType.Child &&
                    rel.type != RelationshipType.Sibling &&
                    rel.type != RelationshipType.Spouse &&
                    rel.type != RelationshipType.Mother &&
                    rel.type != RelationshipType.Father &&
                    rel.type != RelationshipType.Cousin &&
                    rel.type != RelationshipType.SibChild)
                    continue;

                Entity relative = rel.to.FindEntity();
                if (relative == null) continue;

                if (PlayerSocial.IsEligibleCrewMember(now, relative))
                {
                    result.Add(relative);
                }
            }

            return result;
        }
    }
}
