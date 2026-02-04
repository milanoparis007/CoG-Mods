using System;
using System.Collections.Generic;
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
            CrewManagementButtonsPatch.ApplyPatch(harmony);

            Logger.LogInfo("Gameplay Tweaks loaded:");
            Logger.LogInfo($"  SpouseEthnicity: {EnableSpouseEthnicity.Value} (chance={SpouseEthnicityChance.Value})");
            Logger.LogInfo($"  HireableAge: {EnableHireableAge.Value} (min={HireableMinAge.Value})");
            Logger.LogInfo($"  Crew Management buttons: active");
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
        // 3. Crew Management UI — Marry/Child & Hire Family buttons
        //    Adds two buttons to the crew management popup for the selected crew member.
        // =====================================================================

        private static class CrewManagementButtonsPatch
        {
            private static Type _crewManagementPopupType;
            private static Type _buttonStatusType;
            private static FieldInfo _selectedCrewField;
            private static FieldInfo _goField;
            private static MethodInfo _getPeepMethod;

            private static GameObject _btnMarryChild;
            private static GameObject _btnHireFamily;
            private static Entity _currentCrewMember;

            public static void ApplyPatch(Harmony harmony)
            {
                try
                {
                    var asm = AppDomain.CurrentDomain.GetAssemblies()
                        .First(a => a.GetName().Name == "Assembly-CSharp");

                    _crewManagementPopupType = asm.GetTypes().First(t => t.Name == "CrewManagementPopup");
                    _buttonStatusType = _crewManagementPopupType.GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Public)
                        .FirstOrDefault(t => t.Name == "ButtonStatus");

                    // Find the field that holds the selected crew assignment
                    _selectedCrewField = _crewManagementPopupType.GetField("_selected", BindingFlags.NonPublic | BindingFlags.Instance)
                        ?? _crewManagementPopupType.GetField("selected", BindingFlags.NonPublic | BindingFlags.Instance);

                    _goField = _crewManagementPopupType.GetField("_go", BindingFlags.NonPublic | BindingFlags.Instance)
                        ?? _crewManagementPopupType.GetField("go", BindingFlags.NonPublic | BindingFlags.Instance);

                    // Get CrewAssignment.GetPeep method
                    var crewAssignmentType = asm.GetTypes().FirstOrDefault(t => t.Name == "CrewAssignment");
                    if (crewAssignmentType != null)
                        _getPeepMethod = crewAssignmentType.GetMethod("GetPeep", BindingFlags.Public | BindingFlags.Instance);

                    // Patch the Refresh method to add our buttons
                    var refreshMethod = _crewManagementPopupType.GetMethod("Refresh", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (refreshMethod != null)
                    {
                        harmony.Patch(refreshMethod, postfix: new HarmonyMethod(typeof(CrewManagementButtonsPatch), nameof(RefreshPostfix)));
                        Debug.Log("[GameplayTweaks] CrewManagementPopup.Refresh patched successfully");
                    }
                    else
                    {
                        // Try OnOpen as fallback
                        var onOpenMethod = _crewManagementPopupType.GetMethod("OnOpen", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        if (onOpenMethod != null)
                        {
                            harmony.Patch(onOpenMethod, postfix: new HarmonyMethod(typeof(CrewManagementButtonsPatch), nameof(RefreshPostfix)));
                            Debug.Log("[GameplayTweaks] CrewManagementPopup.OnOpen patched successfully");
                        }
                    }

                    // Also patch selection change if available
                    var selectMethod = _crewManagementPopupType.GetMethod("SelectCrew", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                        ?? _crewManagementPopupType.GetMethod("OnCrewSelected", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (selectMethod != null)
                    {
                        harmony.Patch(selectMethod, postfix: new HarmonyMethod(typeof(CrewManagementButtonsPatch), nameof(OnSelectionChangedPostfix)));
                        Debug.Log("[GameplayTweaks] CrewManagementPopup selection method patched");
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"[GameplayTweaks] CrewManagementButtonsPatch.ApplyPatch failed: {e}");
                }
            }

            static void RefreshPostfix(object __instance)
            {
                try
                {
                    EnsureButtonsCreated(__instance);
                    UpdateSelectedCrewMember(__instance);
                    RefreshButtonStates();
                }
                catch (Exception e)
                {
                    Debug.LogError($"[GameplayTweaks] RefreshPostfix error: {e}");
                }
            }

            static void OnSelectionChangedPostfix(object __instance)
            {
                try
                {
                    UpdateSelectedCrewMember(__instance);
                    RefreshButtonStates();
                }
                catch (Exception e)
                {
                    Debug.LogError($"[GameplayTweaks] OnSelectionChangedPostfix error: {e}");
                }
            }

            private static void UpdateSelectedCrewMember(object popup)
            {
                _currentCrewMember = null;

                if (_selectedCrewField == null || _getPeepMethod == null) return;

                var selected = _selectedCrewField.GetValue(popup);
                if (selected == null) return;

                _currentCrewMember = _getPeepMethod.Invoke(selected, null) as Entity;
            }

            private static void EnsureButtonsCreated(object popup)
            {
                if (_btnMarryChild != null && _btnMarryChild.transform != null) return;

                GameObject popupGo = null;
                if (_goField != null)
                    popupGo = _goField.GetValue(popup) as GameObject;

                if (popupGo == null && popup is MonoBehaviour mb)
                    popupGo = mb.gameObject;

                if (popupGo == null) return;

                // Find a suitable container for our buttons (look for existing button area)
                Transform buttonContainer = FindButtonContainer(popupGo.transform);
                if (buttonContainer == null)
                {
                    Debug.LogWarning("[GameplayTweaks] Could not find button container in CrewManagementPopup");
                    return;
                }

                // Create our custom buttons
                _btnMarryChild = CreateButton(buttonContainer, "BtnMarryChild", "Marry/Child", OnMarryChildClicked);
                _btnHireFamily = CreateButton(buttonContainer, "BtnHireFamily", "Hire Family", OnHireFamilyClicked);

                Debug.Log("[GameplayTweaks] Created Marry/Child and Hire Family buttons in CrewManagementPopup");
            }

            private static Transform FindButtonContainer(Transform root)
            {
                // Try common container names
                string[] containerNames = { "Buttons", "ButtonContainer", "Actions", "ButtonArea", "Bottom", "Footer" };

                foreach (var name in containerNames)
                {
                    var container = root.Find(name);
                    if (container != null) return container;
                }

                // Search recursively for a horizontal/vertical layout group that contains buttons
                foreach (Transform child in root)
                {
                    var layoutGroup = child.GetComponent<HorizontalLayoutGroup>() ?? (LayoutGroup)child.GetComponent<VerticalLayoutGroup>();
                    if (layoutGroup != null && child.GetComponentInChildren<Button>() != null)
                        return child;

                    var found = FindButtonContainer(child);
                    if (found != null) return found;
                }

                // Fallback: just use the root
                return root;
            }

            private static GameObject CreateButton(Transform parent, string name, string label, Action onClick)
            {
                // Try to find an existing button to use as template
                var existingButton = parent.GetComponentInChildren<Button>();
                GameObject btnGo;

                if (existingButton != null)
                {
                    btnGo = UnityEngine.Object.Instantiate(existingButton.gameObject, parent);
                    btnGo.name = name;

                    var btn = btnGo.GetComponent<Button>();
                    btn.onClick.RemoveAllListeners();
                    btn.onClick.AddListener(() => onClick());

                    // Update text
                    var text = btnGo.GetComponentInChildren<Text>();
                    if (text != null) text.text = label;

                    var tmpText = btnGo.GetComponentInChildren<TMPro.TextMeshProUGUI>();
                    if (tmpText != null) tmpText.text = label;
                }
                else
                {
                    // Create a simple button from scratch
                    btnGo = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
                    btnGo.transform.SetParent(parent, false);

                    var rt = btnGo.GetComponent<RectTransform>();
                    rt.sizeDelta = new Vector2(100, 30);

                    var img = btnGo.GetComponent<Image>();
                    img.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);

                    var btn = btnGo.GetComponent<Button>();
                    btn.targetGraphic = img;
                    btn.onClick.AddListener(() => onClick());

                    // Add label
                    var textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
                    textGo.transform.SetParent(btnGo.transform, false);

                    var textRt = textGo.GetComponent<RectTransform>();
                    textRt.anchorMin = Vector2.zero;
                    textRt.anchorMax = Vector2.one;
                    textRt.offsetMin = Vector2.zero;
                    textRt.offsetMax = Vector2.zero;

                    var text = textGo.GetComponent<Text>();
                    text.text = label;
                    text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                    text.fontSize = 12;
                    text.color = Color.white;
                    text.alignment = TextAnchor.MiddleCenter;
                }

                btnGo.SetActive(true);
                return btnGo;
            }

            private static void RefreshButtonStates()
            {
                if (_btnMarryChild == null || _btnHireFamily == null) return;
                if (_currentCrewMember == null)
                {
                    SetButtonInteractable(_btnMarryChild, false, "No crew member selected");
                    SetButtonInteractable(_btnHireFamily, false, "No crew member selected");
                    return;
                }

                RelationshipTracker rels = G.GetRels();
                if (rels == null) return;

                RelationshipList relList = rels.GetListOrNull(_currentCrewMember.Id);
                bool hasSpouse = relList != null && relList.HasSpouse();

                // Marry/Child button
                if (hasSpouse)
                {
                    SetButtonInteractable(_btnMarryChild, true, "Have Child");
                    SetButtonLabel(_btnMarryChild, "Have Child");
                }
                else
                {
                    PeopleTracker pt = G.GetPeopleGen();
                    Entity match = pt?.FindRandoToMarry(_currentCrewMember);
                    SetButtonInteractable(_btnMarryChild, match != null, match != null ? "Find Spouse" : "No match available");
                    SetButtonLabel(_btnMarryChild, "Find Spouse");
                }

                // Hire Family button
                List<Entity> eligible = FindEligibleFamily(_currentCrewMember);
                PlayerCrew crew = G.GetHumanCrew();
                bool canHire = eligible.Count > 0 && crew != null && crew.CanAddCrew(1);
                SetButtonInteractable(_btnHireFamily, canHire,
                    eligible.Count > 0 ? $"Hire Family ({eligible.Count})" : "No eligible family");
                SetButtonLabel(_btnHireFamily, $"Hire Family ({eligible.Count})");
            }

            private static void SetButtonInteractable(GameObject btnGo, bool interactable, string tooltip = null)
            {
                var btn = btnGo.GetComponent<Button>();
                if (btn != null) btn.interactable = interactable;
            }

            private static void SetButtonLabel(GameObject btnGo, string label)
            {
                var text = btnGo.GetComponentInChildren<Text>();
                if (text != null) text.text = label;

                var tmpText = btnGo.GetComponentInChildren<TMPro.TextMeshProUGUI>();
                if (tmpText != null) tmpText.text = label;
            }

            private static void OnMarryChildClicked()
            {
                if (_currentCrewMember == null) return;

                try
                {
                    RelationshipTracker rels = G.GetRels();
                    if (rels == null) return;

                    RelationshipList relList = rels.GetListOrNull(_currentCrewMember.Id);
                    bool hasSpouse = relList != null && relList.HasSpouse();

                    if (hasSpouse)
                    {
                        Entity spouse = relList.GetSpouse();
                        Entity mother = (_currentCrewMember.data.person.g == Gender.F) ? _currentCrewMember : spouse;
                        SimTime now = G.GetNow();
                        SimTime birthTime = now.IncrementDays(-1);
                        mother.data.person.futurekids.Add(birthTime);
                        mother.data.person.futurekids.Sort(SimTime.CompareDescending);
                        Debug.Log($"[GameplayTweaks] Scheduled child for {mother.data.person.FullName}");
                    }
                    else
                    {
                        PeopleTracker pt = G.GetPeopleGen();
                        Entity match = pt?.FindRandoToMarry(_currentCrewMember);
                        if (match != null)
                        {
                            pt.ForceMarry(_currentCrewMember, match);
                            Debug.Log($"[GameplayTweaks] Married {_currentCrewMember.data.person.FullName} to {match.data.person.FullName}");
                        }
                    }

                    RefreshButtonStates();
                }
                catch (Exception e)
                {
                    Debug.LogError($"[GameplayTweaks] OnMarryChildClicked error: {e}");
                }
            }

            private static void OnHireFamilyClicked()
            {
                if (_currentCrewMember == null) return;

                try
                {
                    List<Entity> eligible = FindEligibleFamily(_currentCrewMember);
                    if (eligible.Count == 0) return;

                    PlayerCrew playerCrew = G.GetHumanCrew();
                    if (playerCrew == null || !playerCrew.CanAddCrew(1)) return;

                    Entity toHire = eligible[0];
                    playerCrew.HireNewCrewMemberUnassigned(toHire, _currentCrewMember);
                    Debug.Log($"[GameplayTweaks] Hired {toHire.data.person.FullName} (introduced by {_currentCrewMember.data.person.FullName})");

                    RefreshButtonStates();
                }
                catch (Exception e)
                {
                    Debug.LogError($"[GameplayTweaks] OnHireFamilyClicked error: {e}");
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
}
