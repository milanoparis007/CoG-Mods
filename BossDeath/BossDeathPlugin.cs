using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

namespace BossDeath
{
    [BepInPlugin("com.mods.bossdeath", "Boss Death Continuation", "1.0.0")]
    [BepInDependency("com.mods.modlauncher")]
    public class BossDeathPlugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log;

        void Awake()
        {
            Log = Logger;
            var harmony = new Harmony("com.mods.bossdeath");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
            Log.LogInfo("Boss Death Continuation mod loaded.");
        }
    }

    internal static class GameReflection
    {
        public static readonly Assembly Asm;
        public static readonly Type GameType;
        public static readonly Type CombatManagerType;
        public static readonly Type PeepDeathPopupType;
        public static readonly Type CrewDeathEntryType;
        public static readonly Type CombatSummaryType;

        // Game.ctx, Game.serv — static fields
        public static readonly FieldInfo CtxField;
        public static readonly FieldInfo ServField;

        // SessionContext fields
        public static readonly FieldInfo PlayersField;    // ctx.players
        public static readonly FieldInfo MapdisplayField; // ctx.mapdisplay

        // AllPlayersManager.Human — property
        public static readonly PropertyInfo HumanProp;

        // PlayerInfo fields: crew, social (public fields)
        public static readonly FieldInfo CrewField;   // human.crew
        public static readonly FieldInfo SocialField; // human.social

        // PlayerInfo properties
        public static readonly PropertyInfo PIDProp;
        public static readonly PropertyInfo IsHumanProp;

        // PlayerSocial
        public static readonly PropertyInfo PlayerPeepIdProp;
        public static readonly MethodInfo SetBossInfoMethod;

        // ServiceContext.ui — field
        public static readonly FieldInfo UiField;

        // CombatManager.ProcessCrewDeathChoice
        public static readonly MethodInfo ProcessCrewDeathChoiceMethod;

        // CombatSummary.humanCrewDied field
        public static readonly FieldInfo HumanCrewDiedField;
        // CrewDeathEntry.peepId field, IsValid property
        public static readonly FieldInfo CrewDeathEntryPeepIdField;
        public static readonly PropertyInfo CrewDeathEntryIsValidProp;

        // PlayerCrew._crewdata, rawcrew
        public static readonly FieldInfo CrewdataField;
        public static readonly FieldInfo RawcrewField;

        // CrewAssignment
        public static readonly PropertyInfo IsDeadProp;
        public static readonly PropertyInfo IsNotDeadProp;
        public static readonly MethodInfo GetPeepMethod;

        // MapDisplayManager.RefreshTerritoryLabel
        public static readonly MethodInfo RefreshTerritoryLabelMethod;

        // XP.SetCrewRole(Label) and XP.crewRole field
        public static readonly MethodInfo SetCrewRoleMethod;
        public static readonly FieldInfo XpCrewRoleField;

        // Entity.data.agent.xp accessor chain (all public fields)
        public static readonly FieldInfo EntityDataField;    // Entity.data
        public static readonly FieldInfo DataAgentField;     // data.agent (AgentData)
        public static readonly FieldInfo AgentXpField;       // agent.xp (XP)

        // PlayerScheme type
        public static readonly Type PlayerSchemeType;

        // UIService.AddPopup<T>(T) — generic method, needs MakeGenericMethod
        public static readonly MethodInfo AddPopupGenericMethod;

        // PeepDeathPopup constructor
        public static readonly ConstructorInfo PeepDeathPopupCtor;

        // Label type
        public static readonly Type LabelType;

        static GameReflection()
        {
            try
            {
                Asm = AppDomain.CurrentDomain.GetAssemblies()
                    .First(a => a.GetName().Name == "Assembly-CSharp");

                GameType = Asm.GetTypes().First(t => t.Name == "Game" &&
                    t.GetField("ctx", BindingFlags.Public | BindingFlags.Static) != null);
                CombatManagerType = Asm.GetTypes().First(t => t.Name == "CombatManager");
                CombatSummaryType = Asm.GetTypes().FirstOrDefault(t => t.Name == "CombatSummary");
                CrewDeathEntryType = Asm.GetTypes().FirstOrDefault(t => t.Name == "CrewDeathEntry");
                PeepDeathPopupType = Asm.GetTypes().FirstOrDefault(t => t.Name == "PeepDeathPopup");

                // Game static fields
                CtxField = GameType.GetField("ctx", BindingFlags.Public | BindingFlags.Static);
                ServField = GameType.GetField("serv", BindingFlags.Public | BindingFlags.Static);

                // SessionContext fields
                var ctxType = CtxField.FieldType;
                PlayersField = ctxType.GetField("players", BindingFlags.Public | BindingFlags.Instance);
                MapdisplayField = ctxType.GetField("mapdisplay", BindingFlags.Public | BindingFlags.Instance);

                // AllPlayersManager
                var playersType = PlayersField.FieldType;
                HumanProp = playersType.GetProperty("Human");

                // PlayerInfo
                var humanType = HumanProp.PropertyType;
                CrewField = humanType.GetField("crew", BindingFlags.Public | BindingFlags.Instance);
                SocialField = humanType.GetField("social", BindingFlags.Public | BindingFlags.Instance);
                PIDProp = humanType.GetProperty("PID");
                IsHumanProp = humanType.GetProperty("IsHuman");

                // PlayerSocial
                var socialType = SocialField.FieldType;
                PlayerPeepIdProp = socialType.GetProperty("PlayerPeepId");
                SetBossInfoMethod = socialType.GetMethod("SetBossInfo",
                    BindingFlags.Public | BindingFlags.Instance);

                // ServiceContext.ui field
                var servType = ServField.FieldType;
                UiField = servType.GetField("ui", BindingFlags.Public | BindingFlags.Instance);

                // UIService.AddPopup<T>(T) — find the generic version with 1 param
                var uiType = UiField.FieldType;
                AddPopupGenericMethod = uiType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .FirstOrDefault(m => m.Name == "AddPopup" && m.IsGenericMethodDefinition
                        && m.GetParameters().Length == 1);

                // CombatManager.ProcessCrewDeathChoice
                ProcessCrewDeathChoiceMethod = CombatManagerType.GetMethod("ProcessCrewDeathChoice",
                    BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);

                // CombatSummary
                if (CombatSummaryType != null)
                    HumanCrewDiedField = CombatSummaryType.GetField("humanCrewDied",
                        BindingFlags.Public | BindingFlags.Instance);

                // CrewDeathEntry
                if (CrewDeathEntryType != null)
                {
                    CrewDeathEntryPeepIdField = CrewDeathEntryType.GetField("peepId",
                        BindingFlags.Public | BindingFlags.Instance);
                    CrewDeathEntryIsValidProp = CrewDeathEntryType.GetProperty("IsValid",
                        BindingFlags.Public | BindingFlags.Instance);
                }

                // PeepDeathPopup(CrewDeathEntry, Action<CrewDeathEntry>)
                if (PeepDeathPopupType != null)
                    PeepDeathPopupCtor = PeepDeathPopupType.GetConstructors()
                        .FirstOrDefault(c => c.GetParameters().Length == 2);

                // PlayerCrew internals
                var playerCrewType = Asm.GetTypes().First(t => t.Name == "PlayerCrew");
                CrewdataField = playerCrewType.GetField("_crewdata",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (CrewdataField != null)
                {
                    var crewdataType = CrewdataField.FieldType;
                    RawcrewField = crewdataType.GetField("rawcrew",
                        BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
                }

                // CrewAssignment
                var crewAssignmentType = Asm.GetTypes().FirstOrDefault(t => t.Name == "CrewAssignment");
                if (crewAssignmentType != null)
                {
                    IsDeadProp = crewAssignmentType.GetProperty("IsDead");
                    IsNotDeadProp = crewAssignmentType.GetProperty("IsNotDead");
                    GetPeepMethod = crewAssignmentType.GetMethod("GetPeep",
                        BindingFlags.Public | BindingFlags.Instance);
                }

                // MapDisplayManager.RefreshTerritoryLabel
                if (MapdisplayField != null)
                {
                    var mapdisplayType = MapdisplayField.FieldType;
                    RefreshTerritoryLabelMethod = mapdisplayType.GetMethod("RefreshTerritoryLabel",
                        BindingFlags.Public | BindingFlags.Instance);
                }

                // XP and Entity data chain
                var xpType = Asm.GetTypes().FirstOrDefault(t => t.FullName == "Game.Session.Entities.XP");
                if (xpType != null)
                {
                    SetCrewRoleMethod = xpType.GetMethod("SetCrewRole", BindingFlags.Public | BindingFlags.Instance);
                    XpCrewRoleField = xpType.GetField("crewRole", BindingFlags.Public | BindingFlags.Instance);
                }

                var entityType = Asm.GetTypes().FirstOrDefault(t => t.Name == "Entity");
                if (entityType != null)
                    EntityDataField = entityType.GetField("data", BindingFlags.Public | BindingFlags.Instance);

                if (EntityDataField != null)
                {
                    var entityDataType = EntityDataField.FieldType;
                    DataAgentField = entityDataType.GetField("agent", BindingFlags.Public | BindingFlags.Instance);
                }

                if (DataAgentField != null)
                {
                    var agentDataType = DataAgentField.FieldType;
                    AgentXpField = agentDataType.GetField("xp", BindingFlags.Public | BindingFlags.Instance);
                }

                // PlayerScheme type
                PlayerSchemeType = Asm.GetTypes().FirstOrDefault(t => t.Name == "PlayerScheme");

                // Label type
                LabelType = Asm.GetTypes().FirstOrDefault(t => t.FullName == "Game.Core.Label");

                BossDeathPlugin.Log.LogInfo("GameReflection: All reflection handles initialized.");
                if (HumanCrewDiedField == null) BossDeathPlugin.Log.LogWarning("  HumanCrewDiedField is null");
                if (CrewDeathEntryPeepIdField == null) BossDeathPlugin.Log.LogWarning("  CrewDeathEntryPeepIdField is null");
                if (AddPopupGenericMethod == null) BossDeathPlugin.Log.LogWarning("  AddPopupGenericMethod is null");
                if (ProcessCrewDeathChoiceMethod == null) BossDeathPlugin.Log.LogWarning("  ProcessCrewDeathChoiceMethod is null");
                if (PeepDeathPopupCtor == null) BossDeathPlugin.Log.LogWarning("  PeepDeathPopupCtor is null");
                if (RawcrewField == null) BossDeathPlugin.Log.LogWarning("  RawcrewField is null");
                if (SetBossInfoMethod == null) BossDeathPlugin.Log.LogWarning("  SetBossInfoMethod is null");
                if (PlayersField == null) BossDeathPlugin.Log.LogWarning("  PlayersField is null");
                if (CrewField == null) BossDeathPlugin.Log.LogWarning("  CrewField is null");
                if (SocialField == null) BossDeathPlugin.Log.LogWarning("  SocialField is null");
                if (UiField == null) BossDeathPlugin.Log.LogWarning("  UiField is null");
                if (EntityDataField == null) BossDeathPlugin.Log.LogWarning("  EntityDataField is null");
                if (DataAgentField == null) BossDeathPlugin.Log.LogWarning("  DataAgentField is null");
                if (AgentXpField == null) BossDeathPlugin.Log.LogWarning("  AgentXpField is null");
                if (SetCrewRoleMethod == null) BossDeathPlugin.Log.LogWarning("  SetCrewRoleMethod is null");
                if (PlayerSchemeType == null) BossDeathPlugin.Log.LogWarning("  PlayerSchemeType is null");
                if (LabelType == null) BossDeathPlugin.Log.LogWarning("  LabelType is null");
            }
            catch (Exception ex)
            {
                BossDeathPlugin.Log.LogError($"GameReflection: Init failed: {ex}");
            }
        }

        public static object GetCtx() => CtxField.GetValue(null);
        public static object GetServ() => ServField.GetValue(null);
        public static object GetHuman()
        {
            var ctx = GetCtx();
            var players = PlayersField.GetValue(ctx);
            return HumanProp.GetValue(players);
        }

        public static object CreateLabel(string id)
        {
            return Activator.CreateInstance(LabelType, new object[] { id });
        }

        public static object GetXpForCrewAssignment(object crewAssignment)
        {
            var peep = GetPeepMethod.Invoke(crewAssignment, null);
            if (peep == null) return null;
            var entityData = EntityDataField.GetValue(peep);
            if (entityData == null) return null;
            var agentData = DataAgentField.GetValue(entityData);
            if (agentData == null) return null;
            return AgentXpField.GetValue(agentData);
        }

        public static void CallAddPopup(object popup)
        {
            var serv = GetServ();
            var ui = UiField.GetValue(serv);
            // AddPopup<T>(T popup) — make concrete generic for PeepDeathPopup
            var concrete = AddPopupGenericMethod.MakeGenericMethod(popup.GetType());
            concrete.Invoke(ui, new object[] { popup });
        }
    }

    public static class BossPromotion
    {
        public static bool TryPromoteNextBoss()
        {
            try
            {
                var human = GameReflection.GetHuman();
                if (human == null)
                {
                    BossDeathPlugin.Log.LogWarning("BossPromotion: Could not find human player.");
                    return false;
                }

                var crew = GameReflection.CrewField.GetValue(human);
                var social = GameReflection.SocialField.GetValue(human);
                var crewdata = GameReflection.CrewdataField.GetValue(crew);
                var rawcrew = GameReflection.RawcrewField.GetValue(crewdata) as IList;

                if (rawcrew == null || rawcrew.Count == 0)
                {
                    BossDeathPlugin.Log.LogWarning("BossPromotion: rawcrew is null or empty.");
                    return false;
                }

                bool bossIsDead = (bool)GameReflection.IsDeadProp.GetValue(rawcrew[0]);
                if (!bossIsDead)
                    return false;

                // Find the underboss — only an underboss can replace the boss
                var underbossLabel = GameReflection.CreateLabel("underboss");
                int underbossIndex = -1;
                for (int i = 1; i < rawcrew.Count; i++)
                {
                    if (!(bool)GameReflection.IsNotDeadProp.GetValue(rawcrew[i]))
                        continue;
                    var xp = GameReflection.GetXpForCrewAssignment(rawcrew[i]);
                    if (xp == null) continue;
                    var role = GameReflection.XpCrewRoleField.GetValue(xp);
                    if (role != null && underbossLabel.Equals(role))
                    {
                        underbossIndex = i;
                        break;
                    }
                }

                if (underbossIndex < 0)
                {
                    BossDeathPlugin.Log.LogInfo("BossPromotion: No living underboss to promote.");
                    return false;
                }

                var oldBoss = rawcrew[0];
                var newBoss = rawcrew[underbossIndex];
                rawcrew[0] = newBoss;
                rawcrew[underbossIndex] = oldBoss;

                // Set the new boss's crewRole to "boss"
                var newBossXp = GameReflection.GetXpForCrewAssignment(newBoss);
                if (newBossXp != null)
                {
                    var bossLabel = GameReflection.CreateLabel("boss");
                    GameReflection.SetCrewRoleMethod.Invoke(newBossXp, new object[] { bossLabel });
                }

                var newBossPeep = GameReflection.GetPeepMethod.Invoke(newBoss, null);
                GameReflection.SetBossInfoMethod.Invoke(social, new object[] { newBossPeep, false });

                var ctx = GameReflection.GetCtx();
                var pid = GameReflection.PIDProp.GetValue(human);
                var mapdisplay = GameReflection.MapdisplayField.GetValue(ctx);
                if (GameReflection.RefreshTerritoryLabelMethod != null)
                    GameReflection.RefreshTerritoryLabelMethod.Invoke(mapdisplay, new object[] { pid, true });

                BossDeathPlugin.Log.LogInfo("BossPromotion: Successfully promoted new boss.");
                return true;
            }
            catch (Exception ex)
            {
                BossDeathPlugin.Log.LogError($"BossPromotion: Error: {ex}");
                return false;
            }
        }

        public static bool HasLivingUnderboss()
        {
            try
            {
                var human = GameReflection.GetHuman();
                var crew = GameReflection.CrewField.GetValue(human);
                var crewdata = GameReflection.CrewdataField.GetValue(crew);
                var rawcrew = GameReflection.RawcrewField.GetValue(crewdata) as IList;
                if (rawcrew == null) return false;

                var underbossLabel = GameReflection.CreateLabel("underboss");
                for (int i = 1; i < rawcrew.Count; i++)
                {
                    if (!(bool)GameReflection.IsNotDeadProp.GetValue(rawcrew[i]))
                        continue;
                    var xp = GameReflection.GetXpForCrewAssignment(rawcrew[i]);
                    if (xp == null) continue;
                    var role = GameReflection.XpCrewRoleField.GetValue(xp);
                    if (role != null && underbossLabel.Equals(role))
                        return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                BossDeathPlugin.Log.LogError($"BossPromotion: Error checking underboss: {ex}");
                return false;
            }
        }

        public static int CountCrewWithRole(string roleId)
        {
            try
            {
                var human = GameReflection.GetHuman();
                var crew = GameReflection.CrewField.GetValue(human);
                var crewdata = GameReflection.CrewdataField.GetValue(crew);
                var rawcrew = GameReflection.RawcrewField.GetValue(crewdata) as IList;
                if (rawcrew == null) return 0;

                var label = GameReflection.CreateLabel(roleId);
                int count = 0;
                for (int i = 0; i < rawcrew.Count; i++)
                {
                    if (!(bool)GameReflection.IsNotDeadProp.GetValue(rawcrew[i]))
                        continue;
                    var xp = GameReflection.GetXpForCrewAssignment(rawcrew[i]);
                    if (xp == null) continue;
                    var role = GameReflection.XpCrewRoleField.GetValue(xp);
                    if (role != null && label.Equals(role))
                        count++;
                }
                return count;
            }
            catch (Exception ex)
            {
                BossDeathPlugin.Log.LogError($"BossPromotion: Error counting role {roleId}: {ex}");
                return 0;
            }
        }

        public static int GetLivingCrewCount()
        {
            try
            {
                var human = GameReflection.GetHuman();
                var crew = GameReflection.CrewField.GetValue(human);
                var crewdata = GameReflection.CrewdataField.GetValue(crew);
                var rawcrew = GameReflection.RawcrewField.GetValue(crewdata) as IList;
                if (rawcrew == null) return 0;

                int count = 0;
                for (int i = 0; i < rawcrew.Count; i++)
                {
                    if ((bool)GameReflection.IsNotDeadProp.GetValue(rawcrew[i]))
                        count++;
                }
                return count;
            }
            catch (Exception ex)
            {
                BossDeathPlugin.Log.LogError($"BossPromotion: Error counting crew: {ex}");
                return 0;
            }
        }
    }

    [HarmonyPatch]
    static class ShowCrewDiedPatch
    {
        static MethodBase TargetMethod()
        {
            return AccessTools.Method(GameReflection.CombatManagerType, "ShowCrewDied");
        }

        static bool Prefix(object __0)
        {
            try
            {
                BossDeathPlugin.Log.LogInfo("ShowCrewDiedPatch: Prefix fired.");

                var summary = __0;
                var humanCrewDied = GameReflection.HumanCrewDiedField.GetValue(summary);
                if (humanCrewDied == null)
                {
                    BossDeathPlugin.Log.LogWarning("ShowCrewDiedPatch: humanCrewDied is null.");
                    return true;
                }

                bool isValid = (bool)GameReflection.CrewDeathEntryIsValidProp.GetValue(humanCrewDied);
                if (!isValid) return true;

                var deadPeepId = GameReflection.CrewDeathEntryPeepIdField.GetValue(humanCrewDied);
                var human = GameReflection.GetHuman();
                var social = GameReflection.SocialField.GetValue(human);
                var playerPeepId = GameReflection.PlayerPeepIdProp.GetValue(social);

                BossDeathPlugin.Log.LogInfo($"ShowCrewDiedPatch: deadPeepId={deadPeepId}, playerPeepId={playerPeepId}");

                bool isBoss = deadPeepId.Equals(playerPeepId);
                if (!isBoss) return true;

                bool hasUnderboss = BossPromotion.HasLivingUnderboss();
                BossDeathPlugin.Log.LogInfo($"ShowCrewDiedPatch: Boss died. Has underboss: {hasUnderboss}");

                if (!hasUnderboss) return true;

                bool promoted = BossPromotion.TryPromoteNextBoss();
                if (!promoted) return true;

                // Show PeepDeathPopup instead of game-over newspaper
                var actionType = typeof(Action<>).MakeGenericType(GameReflection.CrewDeathEntryType);
                var callback = Delegate.CreateDelegate(actionType, GameReflection.ProcessCrewDeathChoiceMethod);
                var popup = GameReflection.PeepDeathPopupCtor.Invoke(new object[] { humanCrewDied, callback });

                GameReflection.CallAddPopup(popup);

                BossDeathPlugin.Log.LogInfo("ShowCrewDiedPatch: Showing PeepDeathPopup instead of game-over.");
                return false;
            }
            catch (Exception ex)
            {
                BossDeathPlugin.Log.LogError($"ShowCrewDiedPatch: Exception: {ex}");
                return true;
            }
        }
    }

    [HarmonyPatch]
    static class HandleBossDeathPatch
    {
        static MethodBase TargetMethod()
        {
            var playerCrewType = GameReflection.Asm.GetTypes().First(t => t.Name == "PlayerCrew");
            return AccessTools.Method(playerCrewType, "HandleBossDeath");
        }

        static void Postfix(object __instance)
        {
            try
            {
                var playerField = AccessTools.Field(__instance.GetType(), "_player");
                var player = playerField.GetValue(__instance);
                if (player == null) return;

                bool isHuman = (bool)GameReflection.IsHumanProp.GetValue(player);
                if (!isHuman) return;

                var crewdata = GameReflection.CrewdataField.GetValue(__instance);
                var rawcrew = GameReflection.RawcrewField.GetValue(crewdata) as IList;
                if (rawcrew == null || rawcrew.Count == 0) return;

                bool bossIsDead = (bool)GameReflection.IsDeadProp.GetValue(rawcrew[0]);
                if (!bossIsDead) return;

                if (!BossPromotion.HasLivingUnderboss()) return;

                BossDeathPlugin.Log.LogInfo("HandleBossDeathPatch: Human boss is dead, underboss found, promoting.");
                BossPromotion.TryPromoteNextBoss();
            }
            catch (Exception ex)
            {
                BossDeathPlugin.Log.LogError($"HandleBossDeathPatch: Error: {ex}");
            }
        }
    }

    /// <summary>
    /// Shared logic: ensure the human player's boss peep has crewRole = "boss".
    /// </summary>
    static class BossRoleSetter
    {
        private static object _bossLabel;

        public static void EnsureBossRole()
        {
            try
            {
                var human = GameReflection.GetHuman();
                if (human == null) return;

                var crew = GameReflection.CrewField.GetValue(human);
                if (crew == null) { BossDeathPlugin.Log.LogWarning("BossRoleSetter: crew is null"); return; }

                var crewdata = GameReflection.CrewdataField.GetValue(crew);
                if (crewdata == null) { BossDeathPlugin.Log.LogWarning("BossRoleSetter: crewdata is null"); return; }

                var rawcrew = GameReflection.RawcrewField.GetValue(crewdata) as IList;
                if (rawcrew == null || rawcrew.Count == 0) return;

                var bossAssignment = rawcrew[0];
                if ((bool)GameReflection.IsDeadProp.GetValue(bossAssignment)) return;

                var bossPeep = GameReflection.GetPeepMethod.Invoke(bossAssignment, null);
                if (bossPeep == null) { BossDeathPlugin.Log.LogWarning("BossRoleSetter: bossPeep is null"); return; }

                var entityData = GameReflection.EntityDataField.GetValue(bossPeep);
                if (entityData == null) { BossDeathPlugin.Log.LogWarning("BossRoleSetter: entityData is null"); return; }

                var agentData = GameReflection.DataAgentField.GetValue(entityData);
                if (agentData == null) { BossDeathPlugin.Log.LogWarning("BossRoleSetter: agentData is null"); return; }

                var xp = GameReflection.AgentXpField.GetValue(agentData);
                if (xp == null) { BossDeathPlugin.Log.LogWarning("BossRoleSetter: xp is null"); return; }

                // Create the "boss" Label once and cache it
                if (_bossLabel == null)
                {
                    var labelType = GameReflection.Asm.GetTypes().First(t => t.FullName == "Game.Core.Label");
                    _bossLabel = Activator.CreateInstance(labelType, new object[] { "boss" });
                }

                var currentRole = GameReflection.XpCrewRoleField.GetValue(xp);
                if (!_bossLabel.Equals(currentRole))
                {
                    GameReflection.SetCrewRoleMethod.Invoke(xp, new object[] { _bossLabel });
                    BossDeathPlugin.Log.LogInfo("BossRoleSetter: Set boss peep crewRole to 'boss'.");
                }
            }
            catch (Exception ex)
            {
                BossDeathPlugin.Log.LogError($"BossRoleSetter: Error: {ex}");
            }
        }
    }

    /// <summary>
    /// Patch 3a: Set boss crewRole before scheme availability is recalculated each turn.
    /// </summary>
    [HarmonyPatch]
    static class BossSchemeRoleTurnPatch
    {
        static MethodBase TargetMethod()
        {
            return AccessTools.Method(GameReflection.PlayerSchemeType, "OnPlayerTurnStarted");
        }

        static void Prefix()
        {
            BossRoleSetter.EnsureBossRole();
        }
    }

    /// <summary>
    /// Patch 3b: Set boss crewRole when the session initializes (new game or load),
    /// so schemes are available from turn 1.
    /// </summary>
    [HarmonyPatch]
    static class BossSchemeRoleInitPatch
    {
        static MethodBase TargetMethod()
        {
            return AccessTools.Method(GameReflection.PlayerSchemeType, "OnPostInitialize");
        }

        static void Postfix()
        {
            BossRoleSetter.EnsureBossRole();
        }
    }

}
