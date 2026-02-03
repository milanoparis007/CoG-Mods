using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using Game.Core;
using Game.Services;
using Game.Session.Player;
using HarmonyLib;
using SomaSim.Util;
using UnityEngine;
using UnityEngine.UI;

namespace OrgChartMod
{
    [BepInPlugin("com.mods.orgchart", "Org Chart Enhancements", "1.0.0")]
    [BepInDependency("com.mods.modlauncher")]
    public class OrgChartPlugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log;

        void Awake()
        {
            Log = Logger;
            var harmony = new Harmony("com.mods.orgchart");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
            Log.LogInfo("Org Chart Enhancements mod loaded.");
        }
    }

    /// <summary>
    /// Patch: OrgChartPopup.RefreshRoles — show all roles with underboss on its own row.
    /// Layout:  Boss | Underboss row | 12 Specialists row | Grunts
    /// </summary>
    [HarmonyPatch]
    static class OrgChartRefreshRolesPatch
    {
        static readonly Type OrgChartPopupType;
        static readonly FieldInfo PanelField;
        static readonly FieldInfo TmplRoleColumnField;
        static readonly FieldInfo GoField; // BasePopup._go

        static OrgChartRefreshRolesPatch()
        {
            var asm = typeof(Game.Game).Assembly;
            OrgChartPopupType = asm.GetTypes().First(t => t.FullName == "Game.UI.Session.OrgChartPopup");
            PanelField = typeof(BasePopup).GetField("_panel",
                BindingFlags.NonPublic | BindingFlags.Instance);
            GoField = typeof(BasePopup).GetField("_go",
                BindingFlags.NonPublic | BindingFlags.Instance);
            TmplRoleColumnField = OrgChartPopupType.GetField("_tmplRoleColumn",
                BindingFlags.NonPublic | BindingFlags.Instance);
        }

        static MethodBase TargetMethod()
        {
            return AccessTools.Method(OrgChartPopupType, "RefreshRoles");
        }

        static bool Prefix(object __instance)
        {
            try
            {
                var panel = (GameObject)PanelField.GetValue(__instance);
                var go = (GameObject)GoField.GetValue(__instance);
                var tmplRoleColumn = (GameObject)TmplRoleColumnField.GetValue(__instance);

                var roleDefs = Game.Game.serv.globals.settings.people.social.crew.roleSettings.roleDefs;
                var bossLabel = new Label("boss");
                var underbossLabel = new Label("underboss");

                // Split roles into underboss and the rest
                RoleDef underbossRole = null;
                var specialists = new List<RoleDef>();
                foreach (var roleDef in roleDefs)
                {
                    if (roleDef.id == bossLabel)
                        continue;
                    if (roleDef.id == underbossLabel)
                        underbossRole = roleDef;
                    else
                        specialists.Add(roleDef);
                }

                // Get the InitRoleColumn delegate from the instance
                var initMethod = OrgChartPopupType.GetMethod("InitRoleColumn",
                    BindingFlags.Public | BindingFlags.Instance);
                var initDelegate = (Action<int, GameObject, RoleDef>)Delegate.CreateDelegate(
                    typeof(Action<int, GameObject, RoleDef>), __instance, initMethod);

                // --- Underboss row ---
                SetupUnderbossRow(panel, go, tmplRoleColumn, underbossRole, initDelegate);

                // --- Specialists row (the 12 remaining roles) ---
                SetupSpecialistsRow(panel, tmplRoleColumn, specialists, initDelegate);

                return false; // skip original
            }
            catch (Exception ex)
            {
                OrgChartPlugin.Log.LogError($"RefreshRolesPatch: {ex}");
                return true;
            }
        }

        static void SetupUnderbossRow(GameObject panel, GameObject go,
            GameObject tmplRoleColumn, RoleDef underbossRole,
            Action<int, GameObject, RoleDef> initDelegate)
        {
            // Find the Content container and Boss section to know where to insert
            var content = panel.GetChild("Page/Scroll View List/Viewport/Content");
            var bossSection = panel.GetChild("Page/Scroll View List/Viewport/Content/Boss");
            var specialistSection = panel.GetChild("Page/Scroll View List/Viewport/Content/Specialists");

            // Find or create the underboss section
            var ubTransform = content.transform.Find("UnderbossSection");
            GameObject underbossSection;

            if (ubTransform == null)
            {
                // Clone the Specialists section as a template for the underboss row
                underbossSection = UnityEngine.Object.Instantiate(specialistSection, content.transform);
                underbossSection.name = "UnderbossSection";

                // Position it right after Boss
                underbossSection.transform.SetSiblingIndex(
                    bossSection.transform.GetSiblingIndex() + 1);
            }
            else
            {
                underbossSection = ubTransform.gameObject;
            }

            underbossSection.SetActive(true);

            // Hide the "Specialists" header entirely — underboss doesn't need it
            var header = underbossSection.transform.Find("Header");
            if (header != null)
                header.gameObject.SetActive(false);

            // Get the Roles sub-container inside the underboss section
            var ubRoles = underbossSection.transform.Find("Roles")?.gameObject;
            if (ubRoles == null || underbossRole == null)
                return;

            // Populate with a single underboss role column
            var ubList = new List<RoleDef> { underbossRole };
            ubRoles.EnsureChildCount(ubList, tmplRoleColumn);
            ubRoles.InitializeChildren(ubList, initDelegate);

            underbossSection.ForceRebuildLayoutImmediate();
        }

        // The default game layout fits ~9 role columns. To squeeze 12 we need to
        // figure out the natural single-column width, then scale every column so
        // that all of them fit inside the 930 px container.
        static void SetupSpecialistsRow(GameObject panel, GameObject tmplRoleColumn,
            List<RoleDef> specialists, Action<int, GameObject, RoleDef> initDelegate)
        {
            var specialistCat = panel.GetChild(
                "Page/Scroll View List/Viewport/Content/Specialists");
            var roleColumns = panel.GetChild(
                "Page/Scroll View List/Viewport/Content/Specialists/Roles");
            var headerBar = panel.GetChild(
                "Page/Scroll View List/Viewport/Content/Specialists/Header/Top Divider");

            if (specialists.Count == 0) return;

            // --- configure the HorizontalLayoutGroup BEFORE populating ---
            var hlg = roleColumns.GetComponent<HorizontalLayoutGroup>();
            float spacing = 0f;
            if (hlg != null)
            {
                spacing = hlg.spacing;          // remember original
                hlg.childForceExpandWidth = false;
                hlg.childControlWidth = true;
            }

            // Populate columns
            roleColumns.EnsureChildCount(specialists, tmplRoleColumn);
            roleColumns.InitializeChildren(specialists, initDelegate);

            // Measure natural width of one column from the template
            var templateLE = tmplRoleColumn.GetComponent<LayoutElement>();
            float naturalWidth = templateLE != null && templateLE.preferredWidth > 0
                ? templateLE.preferredWidth
                : 100f;   // fallback

            // Also try reading from the first active child
            for (int i = 0; i < roleColumns.transform.childCount; i++)
            {
                var c = roleColumns.transform.GetChild(i).gameObject;
                if (!c.activeSelf) continue;
                var cle = c.GetComponent<LayoutElement>();
                if (cle != null && cle.preferredWidth > 0)
                {
                    naturalWidth = cle.preferredWidth;
                    break;
                }
                var crt = c.GetComponent<RectTransform>();
                if (crt != null && crt.sizeDelta.x > 10)
                {
                    naturalWidth = crt.sizeDelta.x;
                    break;
                }
            }

            float totalNatural = naturalWidth * specialists.Count
                + spacing * (specialists.Count - 1);
            float scale = totalNatural > 930f ? 930f / totalNatural : 1f;

            // Apply per-column: scale the RectTransform width and LayoutElement
            float scaledWidth = naturalWidth * scale;
            float scaledSpacing = spacing * scale;
            if (hlg != null)
                hlg.spacing = scaledSpacing;

            for (int i = 0; i < roleColumns.transform.childCount; i++)
            {
                var child = roleColumns.transform.GetChild(i).gameObject;
                if (!child.activeSelf) continue;

                // Shrink via localScale so all inner elements (icon, text,
                // portraits, button) scale down proportionally
                child.transform.localScale = new Vector3(scale, scale, 1f);

                // Also tell the layout system the new width so columns
                // don't overlap
                var le = child.GetComponent<LayoutElement>();
                if (le == null)
                    le = child.AddComponent<LayoutElement>();
                le.preferredWidth = scaledWidth;
                le.minWidth = scaledWidth;
                le.flexibleWidth = 0;
            }

            // Header bar insets
            int num = (int)(930f / specialists.Count / 2f) - 1;
            RectTransform rect = headerBar.GetRect();
            rect.offsetMin = new Vector2(num, rect.offsetMin.y);
            rect.offsetMax = new Vector2(-num, rect.offsetMax.y);

            specialistCat.ForceRebuildLayoutImmediate();
            specialistCat.ForceRebuildLayoutImmediate();
            specialistCat.ForceRebuildLayoutImmediate();
        }
    }

    /// <summary>
    /// Patch: OrgChartPopup.InitRoleColumn — disable "Add To Role" button
    /// when the underboss slot is already filled (limit 1).
    /// </summary>
    [HarmonyPatch]
    static class OrgChartInitRoleColumnPatch
    {
        static readonly Type OrgChartPopupType;

        static OrgChartInitRoleColumnPatch()
        {
            var asm = typeof(Game.Game).Assembly;
            OrgChartPopupType = asm.GetTypes().First(t => t.FullName == "Game.UI.Session.OrgChartPopup");
        }

        static MethodBase TargetMethod()
        {
            return AccessTools.Method(OrgChartPopupType, "InitRoleColumn");
        }

        static void Postfix(GameObject __1, RoleDef __2)
        {
            try
            {
                var underbossLabel = new Label("underboss");
                if (__2.id != underbossLabel)
                    return;

                var humanCrew = Game.Game.ctx.players.Human.crew;
                int count = humanCrew.GetLiving()
                    .Count(x => {
                        var xp = x.GetPeep()?.data.agent?.xp;
                        return xp != null && xp.crewRole == underbossLabel;
                    });

                if (count < 1)
                    return;

                var addChild = __1.transform.Find("Add To Role");
                if (addChild != null)
                {
                    var button = addChild.GetComponent<Button>();
                    if (button != null)
                        button.interactable = false;
                }
            }
            catch (Exception ex)
            {
                OrgChartPlugin.Log.LogError($"InitRoleColumnPatch: {ex}");
            }
        }
    }
}
