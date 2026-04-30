using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Game.Core;
using Game.Session.Board;
using Game.Session.Entities;
using Game.Session.Player;
using Game.Session.Sim;
using HarmonyLib;
using SomaSim.Util;
using UnityEngine;
namespace GameplayTweaks
{
public partial class GameplayTweaksPlugin
{
	private static class TerritoryColorPatch
	{
		private static FieldInfo _colorsField;

		internal static object _mapDisplayInstance;

		private static FieldInfo _borderManagerField;

		private static FieldInfo _borderPlayerField;

		private static PropertyInfo _borderPlayerProp;

		private static FieldInfo _borderColorInfoField;

		private static FieldInfo _borderColorField;

		private static FieldInfo _borderBorderColorField;

		private static PropertyInfo _displayColorProp;

		private static FieldInfo _displayColorField;

		private static bool _borderReflectionCached;

		private static int _cornerRespectFallbackLogCount;

		private static FieldInfo _cornerPickDataField;

		public static void ApplyPatch(Harmony harmony)
		{

			try
			{
				Type type = typeof(GameClock).Assembly.GetType("Game.Session.Board.MapDisplayManager");
				if (type != null)
				{
					_colorsField = type.GetField("_colors", BindingFlags.Instance | BindingFlags.NonPublic);
					MethodInfo method = type.GetMethod("GetColorForPlayer", BindingFlags.Instance | BindingFlags.Public);
					if (method != null)
					{
						harmony.Patch((MethodBase)method, (HarmonyMethod)null, new HarmonyMethod(typeof(TerritoryColorPatch), "GetColorPostfix", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
						Debug.Log("[GameplayTweaks] Territory color patch applied to MapDisplayManager");
					}
				}
				Type type2 = typeof(GameClock).Assembly.GetType("Game.Session.Player.PlayerTerritoryDisplay");
				if (type2 != null)
				{
					MethodInfo method2 = type2.GetMethod("RefreshBorder", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
					if (method2 != null)
					{
						harmony.Patch((MethodBase)method2, new HarmonyMethod(typeof(TerritoryColorPatch), "RefreshBorderPrefix", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
						Debug.Log("[GameplayTweaks] Territory border refresh patch applied");
					}
				}

				MethodInfo method3 = typeof(global::Game.UI.Session.Picks.CornerPickData).GetMethod("GenerateCornerButtonData", BindingFlags.Static | BindingFlags.Public);
				if (method3 != null)
				{
					harmony.Patch((MethodBase)method3, (HarmonyMethod)null, new HarmonyMethod(typeof(TerritoryColorPatch), "GenerateCornerButtonDataPostfix", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
					Debug.Log("[GameplayTweaks] Corner pick respect color fallback patch applied");
				}

				MethodInfo method4 = typeof(global::Game.UI.Session.Picks.CornerPick).GetMethod("RefreshContents", BindingFlags.Instance | BindingFlags.Public);
				if (method4 != null)
				{
					harmony.Patch((MethodBase)method4, (HarmonyMethod)null, new HarmonyMethod(typeof(TerritoryColorPatch), "CornerPickRefreshContentsPostfix", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
					Debug.Log("[GameplayTweaks] Corner pick visual refresh fallback patch applied");
				}

				MethodInfo method5 = typeof(global::Game.UI.Session.Picks.BuildingPickUtil).GetMethod("GenerateCornerButtonColor", BindingFlags.Static | BindingFlags.Public);
				if (method5 != null)
				{
					harmony.Patch((MethodBase)method5, (HarmonyMethod)null, new HarmonyMethod(typeof(TerritoryColorPatch), "GenerateCornerButtonColorPostfix", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
					Debug.Log("[GameplayTweaks] Corner button color fallback patch applied");
				}

			}
			catch (Exception arg)
			{
				Debug.LogError($"[GameplayTweaks] TerritoryColorPatch failed: {arg}");
			}
		}

		private static void GetColorPostfix(ref Color __result, PlayerInfo player, object __instance)
		{

			if (player == null || (ShouldDeferTerritoryVisualOverrides() && !ShouldPactColorWinOverGangWarsVisuals()))
			{
				return;
			}
			try
			{
				_mapDisplayInstance = __instance;
				Color displayColor = ResolveDisplayedTerritoryColor(player, __result);
				__result = displayColor;
				if (_colorsField != null && _colorsField.GetValue(__instance) is IDictionary dictionary)
				{
					dictionary[player.PID] = displayColor;
				}
			}
			catch
			{
			}
		}

		private static Color ResolveBaseTerritoryColor(PlayerInfo player, Color fallback)
		{
			try
			{
				if (player?.territory != null)
				{
					return player.territory.colorInfo.GetPlayerColor();
				}
			}
			catch
			{
			}
			return fallback;
		}

		private static Color ResolveDisplayedTerritoryColor(PlayerInfo player, Color fallback)
		{
			Color displayColor = ResolveBaseTerritoryColor(player, fallback);
			if (!EnableAIAlliances.Value || player == null)
			{
				return displayColor;
			}

			AlliancePact pactForPlayer = GetPactForPlayer(player.PID);
			if (pactForPlayer != null && pactForPlayer.IsActive)
			{
				return pactForPlayer.SharedColor;
			}

			return displayColor;
		}

		private static void GenerateCornerButtonDataPostfix(Node node, ref global::Game.UI.Session.Picks.CornerPickData __result)
		{
			if (node == null)
			{
				return;
			}

			if (!TryResolveCornerDisplayOwner(node, out PlayerInfo ownerPlayer, out PlayerID displayOwner, out bool respectFallback))
			{
				return;
			}
			if (!respectFallback && !IsOwnerRespectAtOrAboveColorThreshold(node, displayOwner))
			{
				ReconcileLowRespectTerritoryOwnership("corner-data", refreshColors: false);
				__result.owner = PlayerID.INVALID;
				__result.color = new Color(0f, 0f, 0f, 0f);
				return;
			}

			__result.owner = displayOwner;
			__result.color = ResolveDisplayedTerritoryColor(ownerPlayer, __result.color);
			if (respectFallback)
			{
				LogCornerRespectFallback(node, displayOwner, node.respect?.GetOrNull(displayOwner)?.current ?? Fixnum.ZERO, "corner-data");
			}
		}

		private static void GenerateCornerButtonColorPostfix(Node node, ref Color __result)
		{
			if (node == null)
			{
				return;
			}

			if (!TryResolveCornerDisplayOwner(node, out PlayerInfo ownerPlayer, out PlayerID displayOwner, out bool respectFallback))
			{
				return;
			}
			if (!respectFallback && !IsOwnerRespectAtOrAboveColorThreshold(node, displayOwner))
			{
				ReconcileLowRespectTerritoryOwnership("corner-color", refreshColors: false);
				__result = new Color(0f, 0f, 0f, 0f);
				return;
			}

			__result = ResolveDisplayedTerritoryColor(ownerPlayer, __result);
			if (respectFallback)
			{
				LogCornerRespectFallback(node, displayOwner, node.respect?.GetOrNull(displayOwner)?.current ?? Fixnum.ZERO, "corner-color");
			}
		}

		private static void CornerPickRefreshContentsPostfix(global::Game.UI.Session.Picks.CornerPick __instance)
		{
			if (__instance == null)
			{
				return;
			}

			try
			{
				Node node = __instance.GetNode();
				if (node == null)
				{
					return;
				}
				if (!TryResolveCornerDisplayOwner(node, out PlayerInfo ownerPlayer, out PlayerID displayOwner, out bool respectFallback))
				{
					return;
				}
				if (!respectFallback && !IsOwnerRespectAtOrAboveColorThreshold(node, displayOwner))
				{
					ReconcileLowRespectTerritoryOwnership("corner-refresh", refreshColors: false);
					GameObject staleOverlay = __instance.go?.GetChild("Button/BG Overlay/");
					if (staleOverlay != null)
					{
						staleOverlay.GetImage().color = new Color(0f, 0f, 0f, 0f);
					}
					return;
				}

				GameObject overlay = __instance.go?.GetChild("Button/BG Overlay/");
				if (overlay != null)
				{
					overlay.GetImage().color = ResolveDisplayedTerritoryColor(ownerPlayer, overlay.GetImage().color);
				}

				if (_cornerPickDataField == null)
				{
					_cornerPickDataField = typeof(global::Game.UI.Session.Picks.CornerPick).GetField("_pickdata", BindingFlags.Instance | BindingFlags.NonPublic);
				}

				if (_cornerPickDataField != null)
				{
					object boxed = _cornerPickDataField.GetValue(__instance);
					if (boxed is global::Game.UI.Session.Picks.CornerPickData pickData)
					{
						pickData.owner = displayOwner;
						pickData.color = ResolveDisplayedTerritoryColor(ownerPlayer, pickData.color);
						_cornerPickDataField.SetValue(__instance, pickData);
					}
				}
				if (respectFallback)
				{
					LogCornerRespectFallback(node, displayOwner, node.respect?.GetOrNull(displayOwner)?.current ?? Fixnum.ZERO, "corner-refresh");
				}
			}
			catch
			{
			}
		}

		private static bool TryResolveCornerDisplayOwner(Node node, out PlayerInfo player, out PlayerID displayOwner, out bool respectFallback)
		{
			player = null;
			displayOwner = PlayerID.INVALID;
			respectFallback = false;
			if (node == null)
			{
				return false;
			}

			PlayerID owner = node.owner.Get();
			if (owner.IsValid)
			{
				player = owner.FindPlayer();
				if (player == null)
				{
					return false;
				}

				displayOwner = owner;
				return true;
			}

			if (!TryGetDominantRespectOwner(node, out player, out _))
			{
				return false;
			}

			displayOwner = player.PID;
			respectFallback = true;
			return displayOwner.IsValid;
		}

		private static bool TryGetDominantRespectOwner(Node node, out PlayerInfo player, out Fixnum displayedRespect)
		{
			player = null;
			displayedRespect = Fixnum.ZERO;
			if (node?.respect?.data == null || node.respect.data.Count == 0)
			{
				return false;
			}

			PlayerID highestPid = PlayerID.INVALID;
			Fixnum highestRespect = Fixnum.ZERO;
			for (int i = 0; i < node.respect.data.Count; i++)
			{
				Respect respect = node.respect.data[i];
				if (respect == null || respect.pid.IsNotValid)
				{
					continue;
				}

				Fixnum effectiveRespect = GetDisplayedCornerRespectValue(respect);
				if (effectiveRespect < (Fixnum)TerritoryColorOwnerLossThreshold)
				{
					continue;
				}

				if (highestPid.IsNotValid || effectiveRespect > highestRespect)
				{
					highestPid = respect.pid;
					highestRespect = effectiveRespect;
				}
			}

			if (highestPid.IsNotValid)
			{
				return false;
			}

			player = highestPid.FindPlayer();
			displayedRespect = highestRespect;
			return player != null;
		}

		private static Fixnum GetDisplayedCornerRespectValue(Respect respect)
		{
			if (respect == null)
			{
				return Fixnum.ZERO;
			}

			return respect.current;
		}

		private static bool IsOwnerRespectAtOrAboveColorThreshold(Node node, PlayerID owner)
		{
			if (node == null || owner.IsNotValid || owner.IsHumanPlayer)
			{
				return true;
			}
			if (IsProtectedTerritorySupportNodeForOwner(node, owner))
			{
				return true;
			}

			Respect respect = node.respect?.GetOrNull(owner);
			return (respect?.current ?? Fixnum.ZERO) >= (Fixnum)TerritoryColorOwnerLossThreshold;
		}

		private static void LogCornerRespectFallback(Node node, PlayerID pid, Fixnum value, string sourceTag)
		{
			if (_cornerRespectFallbackLogCount >= 12 || node == null || pid.IsNotValid)
			{
				return;
			}

			_cornerRespectFallbackLogCount++;
			VerificationLog("Compat", $"corner-respect-fallback source={sourceTag} node={node.id} pid={pid.id} respect={value}");
		}

		private static void RefreshBorderPrefix(object __instance)
		{

			if (ShouldDeferTerritoryVisualOverrides() && !ShouldPactColorWinOverGangWarsVisuals())
			{
				return;
			}
			try
			{
				if (!_borderReflectionCached)
				{
					_borderReflectionCached = true;
					_borderManagerField = __instance.GetType().GetField("_manager", BindingFlags.Instance | BindingFlags.NonPublic);
					_displayColorProp = __instance.GetType().GetProperty("color", BindingFlags.Instance | BindingFlags.Public);
					_displayColorField = __instance.GetType().GetField("_color", BindingFlags.Instance | BindingFlags.NonPublic);
				}
				if (_borderManagerField == null)
				{
					return;
				}
				object value = _borderManagerField.GetValue(__instance);
				if (value == null)
				{
					return;
				}
				if (_borderPlayerField == null && _borderPlayerProp == null)
				{
					_borderPlayerField = value.GetType().GetField("_player", BindingFlags.Instance | BindingFlags.NonPublic);
					if (_borderPlayerField == null)
					{
						_borderPlayerProp = value.GetType().GetProperty("player", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
					}
					_borderColorInfoField = value.GetType().GetField("colorInfo", BindingFlags.Instance | BindingFlags.Public);
				}
				PlayerInfo val = (_borderPlayerField != null)
				? _borderPlayerField.GetValue(value) as PlayerInfo
				: _borderPlayerProp?.GetValue(value) as PlayerInfo;
				if (val == null)
				{
					return;
				}
				Color displayColor = ResolveDisplayedTerritoryColor(val, default(Color));
				if (_borderColorInfoField != null)
				{
					object value2 = _borderColorInfoField.GetValue(value);
					if (value2 != null)
					{
						if (_borderColorField == null)
						{
							_borderColorField = value2.GetType().GetField("color", BindingFlags.Instance | BindingFlags.Public) ?? value2.GetType().GetField("Color", BindingFlags.Instance | BindingFlags.Public);
							_borderBorderColorField = value2.GetType().GetField("borderColor", BindingFlags.Instance | BindingFlags.Public) ?? value2.GetType().GetField("BorderColor", BindingFlags.Instance | BindingFlags.Public);
						}
						if (_borderColorField != null)
						{
							_borderColorField.SetValue(value2, displayColor);
							_borderColorInfoField.SetValue(value, value2);
						}
						if (_borderBorderColorField != null)
						{
							_borderBorderColorField.SetValue(value2, displayColor);
							_borderColorInfoField.SetValue(value, value2);
						}
					}
				}
				if (_displayColorProp != null && _displayColorProp.CanWrite)
				{
					_displayColorProp.SetValue(__instance, displayColor);
				}
				if (_displayColorField != null)
				{
					_displayColorField.SetValue(__instance, displayColor);
				}
			}
			catch
			{
			}
		}

		public static void RefreshAllTerritoryColors()
		{

			try
			{
				EnsureHumanSafehouseTerritoryColorOwner("territory-color-refresh", refreshColors: false);
				ReconcileLowRespectTerritoryOwnership("territory-color-refresh", refreshColors: false);
				if (ShouldDeferTerritoryVisualOverrides() && !ShouldPactColorWinOverGangWarsVisuals())
				{
					VerificationLog("Compat", "territory visual refresh skipped reason=external-territory-mod");
					return;
				}
				if (_mapDisplayInstance == null)
				{
					try
					{
						Type ctxType = typeof(GameClock).Assembly.GetType("Game.Core.Game");
						if (ctxType != null)
						{
							PropertyInfo ctxProp = ctxType.GetProperty("ctx", BindingFlags.Static | BindingFlags.Public);
							object ctx = ctxProp?.GetValue(null);
							if (ctx != null)
							{
								PropertyInfo mdProp = ctx.GetType().GetProperty("mapdisplay") ?? ctx.GetType().GetProperty("MapDisplay");
								FieldInfo mdField = ctx.GetType().GetField("mapdisplay") ?? ctx.GetType().GetField("MapDisplay");
								object md = mdProp?.GetValue(ctx) ?? mdField?.GetValue(ctx);
								if (md != null)
								{
									_mapDisplayInstance = md;
									if (_colorsField == null)
									{
										_colorsField = md.GetType().GetField("_colors", BindingFlags.Instance | BindingFlags.NonPublic);
									}
								}
							}
						}
					}
					catch
					{
					}
				}
				if (_mapDisplayInstance != null && _colorsField != null && _colorsField.GetValue(_mapDisplayInstance) is IDictionary dictionary)
				{
					dictionary.Clear();
					VerificationLog("Compat", "territory-color-cache-cleared");
					int baseRestoreCount = 0;
					foreach (PlayerInfo p in G.GetAllPlayers())
					{
						if (p == null)
						{
							continue;
						}
						try
						{
							dictionary[p.PID] = ResolveBaseTerritoryColor(p, default(Color));
							baseRestoreCount++;
						}
						catch
						{
						}
					}
					VerificationLog("Compat", $"territory-color-base-restored count={baseRestoreCount}");
					int pactApplyCount = 0;
					foreach (AlliancePact pact in SaveData.Pacts)
					{
						if (!pact.IsActive)
						{
							continue;
						}
						foreach (PlayerInfo allPlayer in G.GetAllPlayers())
						{
							if (allPlayer != null && pact.IsMember(allPlayer.PID))
							{
								try
								{
									dictionary[allPlayer.PID] = pact.SharedColor;
									pactApplyCount++;
								}
								catch
								{
								}
							}
						}
					}
					VerificationLog("Compat", $"territory-color-pact-applied count={pactApplyCount}");
				}
				MethodInfo methodInfo = typeof(GameClock).Assembly.GetType("Game.Session.Player.PlayerTerritoryDisplay")?.GetMethod("RefreshBorder", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				FieldInfo field = typeof(PlayerTerritory).GetField("_display", BindingFlags.Instance | BindingFlags.NonPublic);
				MethodInfo method = typeof(PlayerTerritory).GetMethod("RefreshDisplay", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				MethodInfo method2 = typeof(PlayerTerritory).GetMethod("Refresh", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				foreach (PlayerInfo allPlayer2 in G.GetAllPlayers())
				{
					if (allPlayer2?.territory == null)
					{
						continue;
					}
					try
					{
						if (method != null)
						{
							method.Invoke(allPlayer2.territory, null);
						}
						else if (method2 != null)
						{
							method2.Invoke(allPlayer2.territory, null);
						}
					}
					catch
					{
					}
					object obj3 = field?.GetValue(allPlayer2.territory);
					if (obj3 == null)
					{
						continue;
					}
					try
					{
						if (methodInfo != null)
						{
							methodInfo.Invoke(obj3, null);
						}
					}
					catch
					{
					}
				}
				if (_mapDisplayInstance != null)
				{
					try
					{
						MethodInfo method3 = _mapDisplayInstance.GetType().GetMethod("RefreshColors", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
						if (method3 != null)
						{
							method3.Invoke(_mapDisplayInstance, null);
						}
						else
						{
							MethodInfo method4 = _mapDisplayInstance.GetType().GetMethod("Refresh", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
							if (method4 != null)
							{
								method4.Invoke(_mapDisplayInstance, null);
							}
						}
					}
					catch
					{
					}
				}
				Debug.Log("[GameplayTweaks] Territory colors refreshed for all players");
			}
			catch (Exception arg)
			{
				Debug.LogError($"[GameplayTweaks] RefreshAllTerritoryColors failed: {arg}");
			}
		}
	}

	private static class GangWarsAdapterPatch
	{
		private static bool _applyAttempted;

		private static int _lastVassalBlockLogDay = -1;

		private static int _lastTributeBlockLogDay = -1;

		private static int _lastExpansionBlockLogDay = -1;

		private static int _lastAllianceProjectionDay = -1;

		private static int _lastColorBridgeLogDay = -1;

		private static int _lastPactAggroBoostDay = -1;

		private static bool _projectingAllianceState;

		private static readonly HashSet<string> _patchedSignatures = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		public static void ApplyPatch(Harmony harmony)
		{
			if (_applyAttempted)
			{
				return;
			}
			_applyAttempted = true;
			GangWarsAdapterInitialized = false;
			if (harmony == null)
			{
				return;
			}
			if (!ShouldEnableGangWarsPactAdapter() && !ExternalTerritoryExpansionDetected)
			{
				VerificationLog("Compat", "GangWars adapter inactive reason=no-gangwars-and-no-territory-expansion");
				return;
			}
			try
			{
				HarmonyMethod blockVassalPrefix = new HarmonyMethod(typeof(GangWarsAdapterPatch), "BlockVassalPrefix", (Type[])null);
				HarmonyMethod blockTributePrefix = new HarmonyMethod(typeof(GangWarsAdapterPatch), "BlockTributePrefix", (Type[])null);
				HarmonyMethod hasActiveTributePostfix = new HarmonyMethod(typeof(GangWarsAdapterPatch), "HasActiveTributeWarPostfix", (Type[])null);
				HarmonyMethod expansionGatePrefix = new HarmonyMethod(typeof(GangWarsAdapterPatch), "ExpansionGatePrefix", (Type[])null);
				HarmonyMethod canFormAlliancePrefix = new HarmonyMethod(typeof(GangWarsAdapterPatch), "CanFormAlliancePrefix", (Type[])null);
				HarmonyMethod blockBreakAlliancePrefix = new HarmonyMethod(typeof(GangWarsAdapterPatch), "BreakAlliancePrefix", (Type[])null);
				HarmonyMethod allianceRefreshPostfix = new HarmonyMethod(typeof(GangWarsAdapterPatch), "AllianceRefreshPostfix", (Type[])null);
				HarmonyMethod colorBridgePostfix = new HarmonyMethod(typeof(GangWarsAdapterPatch), "ColorBridgePostfix", (Type[])null);
				HarmonyMethod allianceSectionPostfix = new HarmonyMethod(typeof(GangWarsAdapterPatch), "AllianceSectionPostfix", (Type[])null);
				int vassalHooks = 0;
				int tributeHooks = 0;
				int expansionHooks = 0;
				int allianceHooks = 0;
				int colorHooks = 0;
				int orgHooks = 0;
				vassalHooks += PatchByName(harmony, "CreateVassalRelation", blockVassalPrefix, null, null);
				vassalHooks += PatchByName(harmony, "RestoreVassalRelations", blockVassalPrefix, null, null);
				vassalHooks += PatchByName(harmony, "OnVassalChooseAttacker", blockVassalPrefix, null, null);
				vassalHooks += PatchByName(harmony, "OnVassalChooseDefend", blockVassalPrefix, null, null);
				vassalHooks += PatchByName(harmony, "OnVassalChooseIndependence", blockVassalPrefix, null, null);
				vassalHooks += PatchByName(harmony, "OnVassalChooseNeutral", blockVassalPrefix, null, null);

				tributeHooks += PatchByName(harmony, "StartTributeWar", blockTributePrefix, null, null);
				tributeHooks += PatchByName(harmony, "UpdateTributeWars", blockTributePrefix, null, null);
				tributeHooks += PatchByName(harmony, "OnTributeWarKill", blockTributePrefix, null, null);
				tributeHooks += PatchByName(harmony, "OnTributeWarBusinessDestroyed", blockTributePrefix, null, null);
				tributeHooks += PatchByName(harmony, "OnTributeWarBuildingClosed", blockTributePrefix, null, null);
				tributeHooks += PatchByName(harmony, "OnTributeWarOutpostCaptured", blockTributePrefix, null, null);
				tributeHooks += PatchByName(harmony, "OnGangWarsTributeRefuse", blockTributePrefix, null, null);
				tributeHooks += PatchByName(harmony, "HasActiveTributeWar", null, hasActiveTributePostfix, (MethodInfo m) => m.ReturnType == typeof(bool));

				expansionHooks += PatchByName(harmony, "AutoExpandTerritory", expansionGatePrefix, null, null);
				expansionHooks += PatchByName(harmony, "UpdateAITerritoryModule", expansionGatePrefix, null, null);
				expansionHooks += PatchByName(harmony, "ScoreTerritoryExpansionOptimized", expansionGatePrefix, null, null);
				expansionHooks += PatchByName(harmony, "SetOutpostPatch", expansionGatePrefix, null, null);
				expansionHooks += PatchByName(harmony, "SetOutpost", expansionGatePrefix, null, null);
				expansionHooks += PatchByName(harmony, "OutpostAutoTerritoryExpansionPatch", expansionGatePrefix, null, null);

				allianceHooks += PatchByName(harmony, "CanFormAlliance", canFormAlliancePrefix, null, (MethodInfo m) => m.ReturnType == typeof(bool));
				allianceHooks += PatchByName(harmony, "CanFormAllianceWithReputation", canFormAlliancePrefix, null, (MethodInfo m) => m.ReturnType == typeof(bool));
				allianceHooks += PatchByName(harmony, "BreakAlliance", blockBreakAlliancePrefix, null, null);
				allianceHooks += PatchByName(harmony, "EvaluateAIAlliances", null, allianceRefreshPostfix, null);
				allianceHooks += PatchByName(harmony, "CalculateAllianceGroups", null, allianceRefreshPostfix, null);

				colorHooks += PatchByName(harmony, "GetGangFrameColor", null, colorBridgePostfix, (MethodInfo m) => m.ReturnType == typeof(Color));
				colorHooks += PatchByName(harmony, "GetColorForPlayerID", null, colorBridgePostfix, (MethodInfo m) => m.ReturnType == typeof(Color));
				colorHooks += PatchByName(harmony, "GetRelationBasedColor", null, colorBridgePostfix, (MethodInfo m) => m.ReturnType == typeof(Color));

				orgHooks += PatchByName(harmony, "DrawAIAllianceSection", null, allianceSectionPostfix, null);
				orgHooks += PatchByName(harmony, "DrawPlayerAllianceSection", null, allianceSectionPostfix, null);

				GangWarsAdapterInitialized = vassalHooks + tributeHooks + expansionHooks + allianceHooks + colorHooks + orgHooks > 0;
				VerificationLog("Compat", $"GangWars adapter active={GangWarsAdapterInitialized} vassalHooks={vassalHooks} tributeHooks={tributeHooks} expansionHooks={expansionHooks} allianceHooks={allianceHooks} colorHooks={colorHooks} orgHooks={orgHooks}");
				if (ExternalGangWarsDetected && tributeHooks == 0)
				{
					VerificationLog("GangWarsAdapter", "warning tribute hooks missing; fallback=none");
				}
				if ((ExternalGangWarsDetected || ExternalTerritoryExpansionDetected) && expansionHooks == 0)
				{
					VerificationLog("GangWarsAdapter", "warning territory expansion hooks missing; fallback=none");
				}
				if (ExternalGangWarsDetected && colorHooks == 0)
				{
					VerificationLog("GangWarsAdapter", "warning color hooks missing; fallback=TerritoryColorPatch");
				}
				if (ShouldDisableGangWarsVassals())
				{
					TryClearExistingVassals();
				}
				if (ShouldReplaceGangWarsAlliancesWithPacts())
				{
					ProjectGangWarsAllianceStateFromPacts("startup");
				}
			}
			catch (Exception ex)
			{
				GangWarsAdapterInitialized = false;
				Debug.LogWarning($"[GameplayTweaks] GangWars adapter init failed: {ex.Message}");
			}
		}

		private static IEnumerable<MethodInfo> FindGangWarsMethods(string methodName)
		{
			foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
			{
				string text = assembly.GetName().Name ?? string.Empty;
				if (text.IndexOf("GangWars", StringComparison.OrdinalIgnoreCase) < 0
					&& text.IndexOf("MafiaHierarchy", StringComparison.OrdinalIgnoreCase) < 0
					&& text.IndexOf("TerritoryExpansionPatch", StringComparison.OrdinalIgnoreCase) < 0
					&& text.IndexOf("TerritoryAutoExpand", StringComparison.OrdinalIgnoreCase) < 0
					&& text.IndexOf("OutpostTicker", StringComparison.OrdinalIgnoreCase) < 0)
				{
					continue;
				}
				Type[] array;
				try
				{
					array = assembly.GetTypes();
				}
				catch (ReflectionTypeLoadException ex)
				{
					array = ex.Types;
				}
				if (array == null)
				{
					continue;
				}
				foreach (Type type in array)
				{
					if (type == null)
					{
						continue;
					}
					MethodInfo[] methods;
					try
					{
						methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
					}
					catch
					{
						continue;
					}
					foreach (MethodInfo methodInfo in methods)
					{
						if (methodInfo != null && string.Equals(methodInfo.Name, methodName, StringComparison.OrdinalIgnoreCase))
						{
							yield return methodInfo;
						}
					}
				}
			}
		}

		private static int PatchByName(Harmony harmony, string methodName, HarmonyMethod prefix, HarmonyMethod postfix, Func<MethodInfo, bool> filter)
		{
			int num = 0;
			foreach (MethodInfo item in FindGangWarsMethods(methodName))
			{
				if (item == null || (filter != null && !filter(item)))
				{
					continue;
				}
				string text = $"{item.DeclaringType?.FullName}::{item.Name}/{item.GetParameters().Length}";
				if (!_patchedSignatures.Add(text))
				{
					continue;
				}
				try
				{
					harmony.Patch((MethodBase)item, prefix, postfix, (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
					num++;
					VerificationLog("GangWarsAdapter", $"patched method={text}");
				}
				catch (Exception ex)
				{
					Debug.LogWarning($"[GameplayTweaks] GangWars adapter patch failed method={text} err={ex.Message}");
				}
			}
			return num;
		}

		private static bool BlockVassalPrefix(MethodBase __originalMethod)
		{
			if (!ShouldDisableGangWarsVassals())
			{
				return true;
			}
			int days = G.GetNow().days;
			if (_lastVassalBlockLogDay != days)
			{
				_lastVassalBlockLogDay = days;
				VerificationLog("GangWarsAdapter", $"vassal path blocked method={__originalMethod?.Name} day={days}");
			}
			return false;
		}

		private static bool BlockTributePrefix(MethodBase __originalMethod)
		{
			if (ShouldAllowGangWarsTributeSystems())
			{
				return true;
			}
			int days = G.GetNow().days;
			if (_lastTributeBlockLogDay != days)
			{
				_lastTributeBlockLogDay = days;
				VerificationLog("GangWarsAdapter", $"tribute blocked method={__originalMethod?.Name} day={days}");
			}
			return false;
		}

		private static void HasActiveTributeWarPostfix(ref bool __result)
		{
			if (!ShouldAllowGangWarsTributeSystems())
			{
				__result = false;
			}
		}

		private static bool ExpansionGatePrefix(object[] __args, MethodBase __originalMethod)
		{
			string text = __originalMethod?.Name ?? string.Empty;
			if (text.Length == 0)
			{
				return true;
			}
			bool isOutpostPath = text.IndexOf("Outpost", StringComparison.OrdinalIgnoreCase) >= 0 || text.IndexOf("SetOutpost", StringComparison.OrdinalIgnoreCase) >= 0;
			if (isOutpostPath && !ShouldAllowOutpostAutoExpand())
			{
				LogExpansionBlocked(__originalMethod, "outpostAutoExpandDisabled");
				return false;
			}
			if (text.IndexOf("UpdateAITerritoryModule", StringComparison.OrdinalIgnoreCase) >= 0 || text.IndexOf("ScoreTerritoryExpansionOptimized", StringComparison.OrdinalIgnoreCase) >= 0)
			{
				if (!ShouldAllowGangTerritoryExpansion())
				{
					LogExpansionBlocked(__originalMethod, "gangExpandDisabled");
					return false;
				}
				return true;
			}
			if (TryResolveActorIsHuman(__args, out bool isHuman))
			{
				if (isHuman)
				{
					if (!ShouldAllowPlayerAutoExpandTerritory())
					{
						LogExpansionBlocked(__originalMethod, "playerAutoExpandDisabled");
						return false;
					}
				}
				else if (!ShouldAllowGangTerritoryExpansion())
				{
					LogExpansionBlocked(__originalMethod, "gangExpandDisabled");
					return false;
				}
				return true;
			}
			if (text.IndexOf("Player", StringComparison.OrdinalIgnoreCase) >= 0)
			{
				if (!ShouldAllowPlayerAutoExpandTerritory())
				{
					LogExpansionBlocked(__originalMethod, "playerAutoExpandDisabled");
					return false;
				}
				return true;
			}
			if (text.IndexOf("Gang", StringComparison.OrdinalIgnoreCase) >= 0 || text.IndexOf("AI", StringComparison.OrdinalIgnoreCase) >= 0)
			{
				if (!ShouldAllowGangTerritoryExpansion())
				{
					LogExpansionBlocked(__originalMethod, "gangExpandDisabled");
					return false;
				}
				return true;
			}
			if (!ShouldAllowGangTerritoryExpansion() && !ShouldAllowPlayerAutoExpandTerritory())
			{
				LogExpansionBlocked(__originalMethod, "allExpansionDisabled");
				return false;
			}
			return true;
		}

		private static void LogExpansionBlocked(MethodBase method, string reason)
		{
			int days = G.GetNow().days;
			if (_lastExpansionBlockLogDay == days)
			{
				return;
			}
			_lastExpansionBlockLogDay = days;
			VerificationLog("Territory", $"blocked method={method?.Name} reason={reason} day={days} gangExpand={ShouldAllowGangTerritoryExpansion()} playerExpand={ShouldAllowPlayerAutoExpandTerritory()} outpostExpand={ShouldAllowOutpostAutoExpand()}");
		}

		private static bool CanFormAlliancePrefix(ref bool __result, object[] __args, MethodBase __originalMethod)
		{
			if (!ShouldReplaceGangWarsAlliancesWithPacts())
			{
				return true;
			}
			if (!TryResolvePlayerPair(__args, out int firstGangId, out int secondGangId))
			{
				__result = false;
				VerificationLog("GangWarsAdapter", $"alliance replace unresolved method={__originalMethod?.Name} -> false");
				return false;
			}
			__result = AreInSameActivePact(firstGangId, secondGangId);
			VerificationLog("GangWarsAdapter", $"alliance replace method={__originalMethod?.Name} a={firstGangId} b={secondGangId} allowed={__result}");
			return false;
		}

		private static bool BreakAlliancePrefix(object[] __args, MethodBase __originalMethod)
		{
			if (!ShouldReplaceGangWarsAlliancesWithPacts())
			{
				return true;
			}
			if (!TryResolvePlayerPair(__args, out int firstGangId, out int secondGangId))
			{
				return true;
			}
			if (!AreInSameActivePact(firstGangId, secondGangId))
			{
				return true;
			}
			VerificationLog("GangWarsAdapter", $"break alliance blocked by pact method={__originalMethod?.Name} a={firstGangId} b={secondGangId}");
			return false;
		}

		private static void AllianceRefreshPostfix(MethodBase __originalMethod)
		{
			if (ShouldEnableGangWarsPactAdapter())
			{
				if (ShouldReplaceGangWarsAlliancesWithPacts())
				{
					ProjectGangWarsAllianceStateFromPacts("gangwars-refresh");
				}
				VerificationLog("GangWarsAdapter", $"alliance refresh observed method={__originalMethod?.Name}");
			}
		}

		private static void ColorBridgePostfix(ref Color __result, object[] __args, MethodBase __originalMethod)
		{
			if (!ShouldUseGangWarsColorStyleForPacts() || !ShouldReplaceGangWarsAlliancesWithPacts())
			{
				return;
			}
			if (!TryResolveSingleGangId(__args, out int gangId))
			{
				return;
			}
			PlayerInfo playerInfo = G.FindPlayerById(gangId);
			if (playerInfo == null)
			{
				return;
			}
			AlliancePact pactForPlayer = GetPactForPlayer(playerInfo.PID);
			if (pactForPlayer == null || !pactForPlayer.IsActive)
			{
				return;
			}
			__result = pactForPlayer.SharedColor;
			int days = G.GetNow().days;
			if (_lastColorBridgeLogDay != days)
			{
				_lastColorBridgeLogDay = days;
				VerificationLog("GangWarsAdapter", $"color bridge method={__originalMethod?.Name} gang={gangId} pact={pactForPlayer.ColorIndex}");
			}
		}

		private static void AllianceSectionPostfix(MethodBase __originalMethod)
		{
			if (ShouldReplaceGangWarsAlliancesWithPacts())
			{
				VerificationLog("GangWarsAdapter", $"organization section bridged method={__originalMethod?.Name} source=pacts");
			}
		}

		private static bool TryResolveSingleGangId(object[] args, out int gangId)
		{
			gangId = -1;
			if (args == null)
			{
				return false;
			}
			foreach (object arg in args)
			{
				if (TryExtractPlayerId(arg, out gangId))
				{
					return gangId >= 0;
				}
			}
			return false;
		}

		private static bool TryResolveActorIsHuman(object[] args, out bool isHuman)
		{
			isHuman = false;
			if (!TryResolveSingleGangId(args, out int gangId))
			{
				return false;
			}
			PlayerInfo playerInfo = G.FindPlayerById(gangId);
			if (playerInfo == null)
			{
				return false;
			}
			isHuman = playerInfo.IsHuman;
			return true;
		}

		private static bool TryResolvePlayerPair(object[] args, out int firstGangId, out int secondGangId)
		{
			firstGangId = -1;
			secondGangId = -1;
			if (args == null || args.Length < 2)
			{
				return false;
			}
			foreach (object arg in args)
			{
				if (TryExtractPlayerId(arg, out int gangId))
				{
					if (firstGangId < 0)
					{
						firstGangId = gangId;
					}
					else if (secondGangId < 0 && gangId != firstGangId)
					{
						secondGangId = gangId;
						break;
					}
				}
			}
			return firstGangId >= 0 && secondGangId >= 0 && firstGangId != secondGangId;
		}

		private static bool TryExtractPlayerId(object value, out int gangId)
		{
			gangId = -1;
			if (value == null)
			{
				return false;
			}
			try
			{
				if (value is PlayerInfo playerInfo)
				{
					gangId = playerInfo.PID.id;
					return gangId >= 0;
				}
				if (value is PlayerID playerID)
				{
					gangId = playerID.id;
					return gangId >= 0;
				}
				if (value is int num)
				{
					gangId = num;
					return gangId >= 0;
				}
				if (value is short num2)
				{
					gangId = num2;
					return gangId >= 0;
				}
				Type type = value.GetType();
				PropertyInfo property = type.GetProperty("PID", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
					?? type.GetProperty("pid", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				if (property != null && property.GetValue(value) is PlayerID playerID2)
				{
					gangId = playerID2.id;
					return gangId >= 0;
				}
				FieldInfo fieldInfo = type.GetField("PID", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
					?? type.GetField("pid", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				if (fieldInfo != null && fieldInfo.GetValue(value) is PlayerID playerID3)
				{
					gangId = playerID3.id;
					return gangId >= 0;
				}
				FieldInfo fieldInfo2 = type.GetField("id", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				if (fieldInfo2 != null)
				{
					object value2 = fieldInfo2.GetValue(value);
					if (value2 is short num3)
					{
						gangId = num3;
						return gangId >= 0;
					}
					if (value2 is int num4)
					{
						gangId = num4;
						return gangId >= 0;
					}
				}
			}
			catch
			{
			}
			return false;
		}

		private static bool AreInSameActivePact(int firstGangId, int secondGangId)
		{
			if (firstGangId < 0 || secondGangId < 0 || firstGangId == secondGangId)
			{
				return false;
			}
			PlayerInfo playerInfo = G.FindPlayerById(firstGangId);
			PlayerInfo playerInfo2 = G.FindPlayerById(secondGangId);
			if (playerInfo == null || playerInfo2 == null)
			{
				return false;
			}
			AlliancePact pactForPlayer = GetPactForPlayer(playerInfo.PID);
			if (pactForPlayer == null || !pactForPlayer.IsActive)
			{
				return false;
			}
			if (pactForPlayer.IsMember(playerInfo2.PID))
			{
				return true;
			}
			AlliancePact pactForPlayer2 = GetPactForPlayer(playerInfo2.PID);
			return ArePactsInAlliance(pactForPlayer, pactForPlayer2);
		}

		private static void TryClearExistingVassals()
		{
			if (!ShouldDisableGangWarsVassals())
			{
				return;
			}
			try
			{
				List<PlayerInfo> list = G.GetAllPlayers().Where((PlayerInfo p) => p != null && p.IsJustGang && p.crew != null && !p.crew.IsCrewDefeated).ToList();
				if (list.Count <= 1)
				{
					return;
				}
				int num = 0;
				foreach (MethodInfo item in FindGangWarsMethods("RemoveVassalRelation"))
				{
					if (item == null)
					{
						continue;
					}
					ParameterInfo[] parameters = item.GetParameters();
					if (parameters.Length < 2)
					{
						continue;
					}
					object obj = null;
					if (!item.IsStatic)
					{
						obj = TryResolveSingleton(item.DeclaringType);
						if (obj == null)
						{
							continue;
						}
					}
					foreach (PlayerInfo item2 in list)
					{
						foreach (PlayerInfo item3 in list)
						{
							if (item2.PID.id == item3.PID.id)
							{
								continue;
							}
							if (!TryBuildPlayerArg(parameters[0].ParameterType, item2, out object value) || !TryBuildPlayerArg(parameters[1].ParameterType, item3, out object value2))
							{
								continue;
							}
							object[] array = BuildDefaultArgs(parameters);
							array[0] = value;
							array[1] = value2;
							try
							{
								item.Invoke(obj, array);
								num++;
							}
							catch
							{
							}
						}
					}
				}
				VerificationLog("GangWarsAdapter", $"vassal cleanup attempted calls={num}");
			}
			catch (Exception ex)
			{
				Debug.LogWarning($"[GameplayTweaks] GangWars vassal cleanup failed: {ex.Message}");
			}
		}

		private static object TryResolveSingleton(Type type)
		{
			if (type == null)
			{
				return null;
			}
			try
			{
				FieldInfo fieldInfo = type.GetField("Instance", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
					?? type.GetField("instance", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
				if (fieldInfo != null)
				{
					object value = fieldInfo.GetValue(null);
					if (value != null)
					{
						return value;
					}
				}
				PropertyInfo propertyInfo = type.GetProperty("Instance", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
					?? type.GetProperty("instance", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
				if (propertyInfo != null)
				{
					object value2 = propertyInfo.GetValue(null, null);
					if (value2 != null)
					{
						return value2;
					}
				}
			}
			catch
			{
			}
			return null;
		}

		private static object[] BuildDefaultArgs(ParameterInfo[] parameters)
		{
			object[] array = new object[parameters.Length];
			for (int i = 0; i < parameters.Length; i++)
			{
				ParameterInfo parameterInfo = parameters[i];
				if (parameterInfo.HasDefaultValue)
				{
					array[i] = parameterInfo.DefaultValue;
				}
				else
				{
					Type parameterType = parameterInfo.ParameterType;
					array[i] = (parameterType.IsValueType ? Activator.CreateInstance(parameterType) : null);
				}
			}
			return array;
		}

		private static bool TryBuildPlayerArg(Type parameterType, PlayerInfo player, out object value)
		{
			value = null;
			if (parameterType == null || player == null)
			{
				return false;
			}
			if (parameterType == typeof(PlayerInfo) || parameterType.IsAssignableFrom(typeof(PlayerInfo)))
			{
				value = player;
				return true;
			}
			if (parameterType == typeof(PlayerID))
			{
				value = player.PID;
				return true;
			}
			if (parameterType == typeof(int))
			{
				value = player.PID.id;
				return true;
			}
			if (parameterType == typeof(short))
			{
				value = (short)player.PID.id;
				return true;
			}
			return false;
		}

		internal static void RunTurnAllianceProjection(SimTime now)
		{
			if (!GangWarsAdapterInitialized || !ShouldEnableGangWarsPactAdapter() || !ShouldReplaceGangWarsAlliancesWithPacts())
			{
				return;
			}
			if (now.days < 0 || _lastAllianceProjectionDay == now.days)
			{
				return;
			}
			_lastAllianceProjectionDay = now.days;
			ProjectGangWarsAllianceStateFromPacts("turn");
		}

		private static void ProjectGangWarsAllianceStateFromPacts(string sourceTag)
		{
			if (!ShouldReplaceGangWarsAlliancesWithPacts())
			{
				return;
			}
			if (_projectingAllianceState)
			{
				return;
			}
			try
			{
				_projectingAllianceState = true;
				List<PlayerInfo> list = G.GetAllPlayers().Where((PlayerInfo p) => p != null && p.IsJustGang && p.crew != null && !p.crew.IsCrewDefeated).ToList();
				if (list.Count <= 1)
				{
					return;
				}
				int num = 0;
				for (int i = 0; i < list.Count - 1; i++)
				{
					PlayerInfo playerInfo = list[i];
					for (int j = i + 1; j < list.Count; j++)
					{
						PlayerInfo playerInfo2 = list[j];
						bool shouldBeAllied = AreInSameActivePact(playerInfo.PID.id, playerInfo2.PID.id);
						if (TryApplyAllianceStateToGangWars(playerInfo, playerInfo2, shouldBeAllied))
						{
							num++;
						}
					}
				}
				VerificationLog("GangWarsAdapter", $"alliance projection source={sourceTag} applied={num}");
			}
			catch (Exception ex)
			{
				Debug.LogWarning($"[GameplayTweaks] GangWars alliance projection failed: {ex.Message}");
			}
			finally
			{
				_projectingAllianceState = false;
			}
		}

		private static bool TryApplyAllianceStateToGangWars(PlayerInfo a, PlayerInfo b, bool allied)
		{
			if (a == null || b == null || a.PID.id == b.PID.id)
			{
				return false;
			}
			string[] names = allied
				? new string[6] { "CreateAllianceRelation", "FormAlliance", "AddAlliance", "SetAllianceRelation", "ApplyAlliance", "MakeAlliance" }
				: new string[5] { "BreakAlliance", "RemoveAlliance", "ClearAlliance", "RemoveAllianceRelation", "EndAlliance" };
			bool flag = false;
			foreach (string methodName in names)
			{
				foreach (MethodInfo item in FindGangWarsMethods(methodName))
				{
					if (item == null)
					{
						continue;
					}
					ParameterInfo[] parameters = item.GetParameters();
					if (parameters.Length < 2)
					{
						continue;
					}
					if (!TryBuildPlayerArg(parameters[0].ParameterType, a, out object value) || !TryBuildPlayerArg(parameters[1].ParameterType, b, out object value2))
					{
						continue;
					}
					object obj = null;
					if (!item.IsStatic)
					{
						obj = TryResolveSingleton(item.DeclaringType);
						if (obj == null)
						{
							continue;
						}
					}
					object[] array2 = BuildDefaultArgs(parameters);
					array2[0] = value;
					array2[1] = value2;
					try
					{
						item.Invoke(obj, array2);
						flag = true;
					}
					catch
					{
					}
				}
			}
			return flag;
		}

		internal static void RunTurnPactAggroBoost(PlayerInfo humanPlayer, SimTime now)
		{
			if (!GangWarsAdapterInitialized || !ShouldEnableGangWarsPactAdapter() || !ShouldUseGangWarsAggroBoostForPacts())
			{
				return;
			}
			if (now.days < 0 || now.days == _lastPactAggroBoostDay || now.days % 7 != 0)
			{
				return;
			}
			_lastPactAggroBoostDay = now.days;
			try
			{
				List<PlayerInfo> list = G.GetAllPlayers().Where((PlayerInfo p) => p != null && p.IsJustGang && p.crew != null && !p.crew.IsCrewDefeated).ToList();
				if (list.Count == 0)
				{
					return;
				}
				int num = 0;
				foreach (AlliancePact pact in SaveData.Pacts)
				{
					if (pact == null || !pact.IsActive)
					{
						continue;
					}
					HashSet<int> source = new HashSet<int>(GetPactMemberGangIds(pact));
					foreach (int item in source)
					{
						PlayerInfo playerInfo = list.FirstOrDefault((PlayerInfo p) => p.PID.id == item);
						if (playerInfo == null || playerInfo.IsHuman)
						{
							continue;
						}
						List<PlayerInfo> list2 = list.Where((PlayerInfo p) => p.PID.id != item && !source.Contains(p.PID.id) && !ArePlayersProtectedByPactAlliance(playerInfo, p)).ToList();
						if (list2.Count == 0)
						{
							continue;
						}
						PlayerInfo playerInfo2 = list2[SharedRng.Next(list2.Count)];
						if (playerInfo2 == null)
						{
							continue;
						}
						if (IsAggroWithoutTruceOneWay(playerInfo, playerInfo2.PID))
						{
							continue;
						}
						var (_, hasTruce) = GetAggroAndTruceOneWay(playerInfo, playerInfo2.PID);
						if (hasTruce)
						{
							continue;
						}
						float bossHappiness = Mathf.Clamp01(pact.GetBossHappiness(item));
						float num2 = Mathf.Clamp(0.1f + (1f - bossHappiness) * 0.2f, 0.1f, 0.3f);
						if (SharedRng.NextDouble() <= num2)
						{
							ActivateWarBetweenPlayers(playerInfo, playerInfo2);
							num++;
						}
					}
				}
				if (num > 0)
				{
					VerificationLog("GangWarsAdapter", $"pact aggro boost applied day={now.days} wars={num}");
				}
			}
			catch (Exception ex)
			{
				Debug.LogWarning($"[GameplayTweaks] GangWars pact aggro boost failed: {ex.Message}");
			}
		}
	}
}
}
