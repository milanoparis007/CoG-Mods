using System;
using System.Collections.Generic;
using System.Reflection;
using Game.Core;
using Game.Session.Board;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Player;
using Game.Session.Player.AI;
using Game.Session.Sim;
using Game.Session.Sim.Modules;
using HarmonyLib;
using SomaSim.Util;
using UnityEngine;

namespace GameplayTweaks
{
public partial class GameplayTweaksPlugin
{
	internal static class DirtyCashPatches
	{
		public static void ApplyPatches(Harmony harmony)
		{
			if (!GameplayTweaksPlugin.EnableDirtyCash.Value)
			{
				return;
			}
			try
			{
				if (GameplayTweaksPlugin.ExternalDirtyCashVolumeFixDetected)
				{
					Debug.Log("[GameplayTweaks] Skipping volume patch - DirtyCashVolumeFix.dll handles it");
				}
				else
				{
					Type type = typeof(GameClock).Assembly.GetType("Game.Services.Resource");
					if (type != null)
					{
						MethodInfo method = type.GetMethod("FindTotalVolume", BindingFlags.Instance | BindingFlags.Public);
						if (method != null)
						{
							harmony.Patch((MethodBase)method, (HarmonyMethod)null, new HarmonyMethod(typeof(DirtyCashPatches), "VolumePostfix", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
							Debug.Log("[GameplayTweaks] Dirty cash volume fix applied");
						}
					}
				}
				if (GameplayTweaksPlugin.ExternalDirtyCashEconomyDetected)
				{
					Debug.Log("[GameplayTweaks] Skipping DoChangeMoney patch - DirtyCashEconomy.dll handles income conversion, deliveries, and buy/sell");
				}
				else
				{
					MethodInfo[] methods = typeof(PlayerFinances).GetMethods(BindingFlags.Instance | BindingFlags.Public);
					foreach (MethodInfo methodInfo in methods)
					{
						if (methodInfo.Name == "DoChangeMoney")
						{
							ParameterInfo[] parameters = methodInfo.GetParameters();
							if (parameters.Length >= 3)
							{
								harmony.Patch((MethodBase)methodInfo, new HarmonyMethod(typeof(DirtyCashPatches), "DoChangeMoneyPrefix", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
								Debug.Log($"[GameplayTweaks] Dirty cash income conversion patched (params: {parameters.Length})");
								break;
							}
						}
					}
				}
				MethodInfo method2 = typeof(PlayerFinances).GetMethod("DoChangeMoney", BindingFlags.Instance | BindingFlags.Public, null, new[] { typeof(InventoryModuleData), typeof(Price), typeof(MoneyReason), typeof(EntityID?) }, null);
				if (method2 != null)
				{
					harmony.Patch((MethodBase)method2, new HarmonyMethod(typeof(DirtyCashPatches), "TradeDoChangeMoneyPrefix", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
					Debug.Log("[GameplayTweaks] Dirty cash trade overdraw guard applied");
				}
				Debug.Log("[GameplayTweaks] Dirty cash economy initialized" +
					(GameplayTweaksPlugin.ExternalDirtyCashEconomyDetected ? " (external economy active)" : "") +
					(GameplayTweaksPlugin.ExternalDirtyCashVolumeFixDetected ? " (external volume fix active)" : ""));
			}
			catch (Exception arg)
			{
				Debug.LogError($"[GameplayTweaks] DirtyCashPatches failed: {arg}");
			}
		}

		private static void VolumePostfix(object __instance, ref object __result)
		{
			try
			{
				string text = __instance.ToString();
				if (text != null && text.Contains("dirty-cash"))
				{
					ConstructorInfo constructor = __result.GetType().GetConstructor(new Type[1] { typeof(int) });
					if (constructor != null)
					{
						__result = constructor.Invoke(new object[1] { 1 });
					}
				}
			}
			catch
			{
			}
		}

		private static bool DoChangeMoneyPrefix(object __instance, object[] __args)
		{
			try
			{
				if (__args == null || __args.Length < 2)
				{
					return true;
				}
				object obj = null;
				object obj2 = null;
				foreach (object obj3 in __args)
				{
					if (obj3 != null)
					{
						string name = obj3.GetType().Name;
						if (name == "MoneyReason" || name.Contains("MoneyReason"))
						{
							obj = obj3;
						}
						else if (name == "Price")
						{
							obj2 = obj3;
						}
					}
				}
				if (obj == null || obj2 == null)
				{
					return true;
				}
				int num = Convert.ToInt32(obj);
				switch (num)
				{
				default:
					if (num != 80 && num != 60 && num != 61)
					{
						return true;
					}
					break;
				case 40:
				case 41:
				case 50:
				case 51:
				case 71:
				case 72:
				case 73:
				case 74:
				case 75:
					break;
				}
				int num2 = 0;
				try
				{
					FieldInfo field = obj2.GetType().GetField("cash");
					if (field != null)
					{
						num2 = GameplayTweaksPlugin.ReadFixnum(field.GetValue(obj2));
					}
				}
				catch
				{
				}
				if (num2 <= 0)
				{
					return true;
				}
				PlayerInfo humanPlayer = G.GetHumanPlayer();
				if (humanPlayer == null)
				{
					return true;
				}
				FieldInfo field2 = __instance.GetType().GetField("_player", BindingFlags.Instance | BindingFlags.NonPublic);
				if (field2 != null)
				{
					object value = field2.GetValue(__instance);
					PlayerInfo val = value as PlayerInfo;
					if (val != null && val.PID.id != humanPlayer.PID.id)
					{
						return true;
					}
				}
				EntityID safehouse = humanPlayer.territory.Safehouse;
				if (safehouse.IsNotValid)
				{
					return true;
				}
				Entity val2 = EntityIDExtensions.FindEntity(safehouse);
				if (val2 == null)
				{
					return true;
				}
				GameplayTweaksPlugin.AddDirtyCash(val2, num2, "money-prefix:" + obj);
				return false;
			}
			catch
			{
				return true;
			}
		}

		private static void TradeDoChangeMoneyPrefix(PlayerFinances __instance, ref Price delta, MoneyReason reason)
		{
			try
			{
				if (!GameplayTweaksPlugin.ShouldDeferDirtyCashRuntime() || !IsTradeMoneyReason(reason))
				{
					return;
				}
				PlayerInfo humanPlayer = G.GetHumanPlayer();
				if (humanPlayer == null || humanPlayer.finances != __instance)
				{
					return;
				}
				int deltaBefore = GameplayTweaksPlugin.ReadFixnum(delta.cash);
				if (deltaBefore >= 0)
				{
					return;
				}
				int cleanBefore = GameplayTweaksPlugin.ReadFixnum(__instance.GetMoneyTotal().cash);
				int dirtyBefore = GameplayTweaksPlugin.GetTotalDirtyCash();
				int requestedDebit = -deltaBefore;
				int deficit = Math.Max(0, requestedDebit - cleanBefore);
				int dirtyCovered = 0;
				if (deficit > 0)
				{
					dirtyCovered = GameplayTweaksPlugin.TrySpendDirtyCashFromStorage(humanPlayer, deficit);
				}
				int adjustedDebit = Math.Max(0, requestedDebit - dirtyCovered);
				bool clamped = false;
				if (adjustedDebit > cleanBefore)
				{
					adjustedDebit = cleanBefore;
					clamped = true;
				}
				if (dirtyCovered <= 0 && !clamped)
				{
					return;
				}
				delta = new Price((Fixnum)(-adjustedDebit));
				int cleanAfter = Math.Max(0, cleanBefore - adjustedDebit);
				int dirtyAfter = Math.Max(0, dirtyBefore - dirtyCovered);
				GameplayTweaksPlugin.VerificationLog("DirtyCashTradeGuard", $"reason={reason} cleanBefore={cleanBefore} dirtyBefore={dirtyBefore} deltaBefore={deltaBefore} dirtyCovered={dirtyCovered} cleanAfter={cleanAfter} dirtyAfter={dirtyAfter} deltaAfter={GameplayTweaksPlugin.ReadFixnum(delta.cash)} clamped={clamped}");
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] Dirty cash trade overdraw guard failed: " + ex.Message);
			}
		}

		private static bool IsTradeMoneyReason(MoneyReason reason)
		{
			switch (reason)
			{
			case MoneyReason.BuySell:
			case MoneyReason.ScheduledBuySell:
			case MoneyReason.OrderDelivery:
				return true;
			default:
				return false;
			}
		}

		internal static void ProcessLaundering()
		{
			try
			{
				if (GameplayTweaksPlugin.ShouldDeferDirtyCashRuntime())
				{
					return;
				}
				PlayerInfo humanPlayer = G.GetHumanPlayer();
				if (humanPlayer == null)
				{
					return;
				}
				EntityID safehouse = humanPlayer.territory.Safehouse;
				if (safehouse.IsNotValid)
				{
					return;
				}
				Entity val = EntityIDExtensions.FindEntity(safehouse);
				if (val == null)
				{
					return;
				}
				int num = GameplayTweaksPlugin.ReadInventoryAmount(val, ModConstants.DIRTY_CASH_LABEL);
				if (num <= 0)
				{
					return;
				}
				int amount = Math.Max(1, (int)((float)num * 0.05f));
				int num2 = GameplayTweaksPlugin.RemoveDirtyCash(val, amount);
				if (num2 > 0)
				{
					humanPlayer.finances.DoChangeMoneyOnSafehouse(new Price((Fixnum)(num2)), (MoneyReason)1);
					if (num2 >= 10)
					{
						Debug.Log($"[GameplayTweaks] Laundered ${num2} dirty cash");
					}
				}
			}
			catch (Exception arg)
			{
				Debug.LogError($"[GameplayTweaks] Laundering failed: {arg}");
			}
		}
	}

	internal static class FrontTrackingPatch
	{
		private static MethodInfo _findBuildingMethod;

		public static void ApplyPatch(Harmony harmony)
		{
			try
			{
				MethodInfo method = typeof(PlayerTerritory).GetMethod("PerformTakeover", BindingFlags.Instance | BindingFlags.Public);
				if (method != null)
				{
					harmony.Patch((MethodBase)method,
						new HarmonyMethod(typeof(FrontTrackingPatch), "TakeoverPrefix", (Type[])null),
						new HarmonyMethod(typeof(FrontTrackingPatch), "TakeoverPostfix", (Type[])null),
						(HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
					Debug.Log("[GameplayTweaks] Front tracking patch applied (prefix + postfix)");
				}
				PatchBusinessAdvisorTakeover(harmony, "TryTakeOver");
				PatchBusinessAdvisorTakeover(harmony, "TryTakeOverCasino");
			}
			catch (Exception arg)
			{
				Debug.LogError($"[GameplayTweaks] FrontTrackingPatch failed: {arg}");
			}
		}

		private static void PatchBusinessAdvisorTakeover(Harmony harmony, string methodName)
		{
			MethodInfo method = typeof(BusinessAdvisor).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
			if (method == null)
			{
				Debug.LogWarning($"[GameplayTweaks] Front tracking could not find BusinessAdvisor.{methodName}");
				return;
			}
			harmony.Patch((MethodBase)method,
				new HarmonyMethod(typeof(FrontTrackingPatch), "BusinessAdvisorTakeoverPresencePrefix", (Type[])null),
				(HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
			Debug.Log($"[GameplayTweaks] Front tracking presence guard applied to BusinessAdvisor.{methodName}");
		}

		private static bool TakeoverPrefix(object __instance, object[] __args)
		{
			try
			{
				PlayerTerritory val = __instance as PlayerTerritory;
				if (val == null) return true;
				PlayerInfo attacker = ((PlayerSubmanager)val).PlayerInfo;
				if (attacker == null) return true;
				object td = (__args != null && __args.Length > 1) ? __args[1] : null;
				if (td == null) return true;
				if (_findBuildingMethod == null)
				{
					_findBuildingMethod = td.GetType().GetMethod("FindBuilding", BindingFlags.Instance | BindingFlags.Public);
				}
				Entity building = _findBuildingMethod?.Invoke(td, null) as Entity;
				if (building == null) return true;
				if (ShouldBlockAiTakeoverForHumanPhysicalPresence(attacker, building, "perform-takeover"))
				{
					return false;
				}
				if (!GameplayTweaksPlugin.EnableAIAlliances.Value) return true;
				AlliancePact attackerPact = GameplayTweaksPlugin.GetPactForPlayer(attacker.PID);
				if (attackerPact == null) return true;
				PlayerID ownerId = building.data.building.controlled.Get();
				if (!ownerId.IsValid) return true;
				if (ownerId.id == attacker.PID.id) return true;
				PlayerInfo owner = ownerId.FindPlayer();
				AlliancePact ownerPact = owner != null ? GameplayTweaksPlugin.GetPactForPlayer(owner.PID) : null;
				if (attackerPact.IsMember(ownerId) || GameplayTweaksPlugin.ArePactsInAlliance(attackerPact, ownerPact))
				{
					Debug.Log($"[GameplayTweaks] Blocked pact member {attacker.social?.PlayerGroupName} from taking over building owned by protected pact gang (PID {ownerId.id})");
					return false;
				}
			}
			catch (Exception ex)
			{
				Debug.LogWarning($"[GameplayTweaks] TakeoverPrefix error: {ex.Message}");
			}
			return true;
		}

		private static bool BusinessAdvisorTakeoverPresencePrefix(object __instance, Entity building)
		{
			try
			{
				if (building == null)
				{
					return true;
				}
				PlayerInfo attacker = Traverse.Create(__instance).Field("_player").GetValue<PlayerInfo>();
				return !ShouldBlockAiTakeoverForHumanPhysicalPresence(attacker, building, "business-advisor");
			}
			catch (Exception ex)
			{
				Debug.LogWarning($"[GameplayTweaks] BusinessAdvisor takeover presence guard error: {ex.Message}");
				return true;
			}
		}

		private static bool ShouldBlockAiTakeoverForHumanPhysicalPresence(PlayerInfo attacker, Entity building, string source)
		{
			try
			{
				if (attacker == null || attacker.PID.IsHumanPlayer || building == null)
				{
					return false;
				}
				PlayerID ownerId = building.data.building.controlled.Get();
				if (!ownerId.IsHumanPlayer)
				{
					return false;
				}
				if (!TryFindHumanPhysicalFrontDefender(building, out CrewAssignment defender, out NodeID defenderNodeId, out string defenderSource, out int presentCrewCount))
				{
					return false;
				}

				GameplayTweaksPlugin.VerificationLog(
					"FrontTracking",
					$"front-takeover-blocked-human-present source={source} attacker={attacker.PID.id} building={building.Id.id} node={defenderNodeId} defender={defender.peepId.id} vehicle={(defender.IsInVehicle ? defender.VehicleID.id.ToString() : "0")} crewPresent={presentCrewCount} defenderSource={defenderSource}");
				return true;
			}
			catch (Exception ex)
			{
				Debug.LogWarning($"[GameplayTweaks] human front presence guard failed: {ex.Message}");
				return false;
			}
		}

		private static bool TryFindHumanPhysicalFrontDefender(Entity building, out CrewAssignment defender, out NodeID defenderNodeId, out string defenderSource, out int presentCrewCount)
		{
			defender = CrewAssignment.EMPTY;
			defenderNodeId = NodeID.INVALID;
			defenderSource = "none";
			presentCrewCount = 0;
			PlayerInfo humanPlayer = Game.Game.ctx?.players?.Human;
			PlayerCrew humanCrew = humanPlayer?.crew;
			if (building == null || humanCrew == null)
			{
				return false;
			}

			List<NodeID> comparisonNodeIds = CollectTakeoverPresenceNodeIds(building);
			if (comparisonNodeIds.Count <= 0)
			{
				return false;
			}

			foreach (CrewAssignment assignment in humanCrew.GetLiving())
			{
				if (!assignment.IsValid || assignment.IsDead)
				{
					continue;
				}

				if (assignment.IsInVehicle && assignment.VehicleID.IsValid)
				{
					if (!MultiCrewVehicleHelper.TryGetStrictPhysicalVehicleNode(assignment.VehicleID, out Node vehicleNode, out string vehicleSource)
						|| vehicleNode == null
						|| !ContainsNodeId(comparisonNodeIds, vehicleNode.id))
					{
						continue;
					}

					presentCrewCount++;
					if (!defender.IsValid)
					{
						defender = assignment;
						defenderNodeId = vehicleNode.id;
						defenderSource = string.IsNullOrWhiteSpace(vehicleSource) ? "vehicle-physical" : "vehicle-" + vehicleSource;
					}
					continue;
				}

				Entity peep = assignment.GetPeep();
				Node peepNode = peep?.components?.agent?.GetNode();
				if (peepNode == null || !ContainsNodeId(comparisonNodeIds, peepNode.id))
				{
					continue;
				}

				presentCrewCount++;
				if (!defender.IsValid)
				{
					defender = assignment;
					defenderNodeId = peepNode.id;
					defenderSource = "agent";
				}
			}

			return defender.IsValid;
		}

		private static List<NodeID> CollectTakeoverPresenceNodeIds(Entity building)
		{
			List<NodeID> nodeIds = new List<NodeID>();
			if (building == null)
			{
				return nodeIds;
			}

			try
			{
				if (building.data?.board != null)
				{
					AddNodeId(nodeIds, building.data.board.bead.nodeId);
				}
			}
			catch
			{
			}

			try
			{
				if (CommandButtonScopeOutPatch.TryGetScopeComparisonNodeIds(building, out _, out List<NodeID> comparisonNodeIds, out _)
					&& comparisonNodeIds != null)
				{
					foreach (NodeID comparisonNodeId in comparisonNodeIds)
					{
						AddNodeId(nodeIds, comparisonNodeId);
					}
				}
			}
			catch
			{
			}

			return nodeIds;
		}

		private static void AddNodeId(List<NodeID> nodeIds, NodeID nodeId)
		{
			if (!nodeId.IsValid || ContainsNodeId(nodeIds, nodeId))
			{
				return;
			}
			nodeIds.Add(nodeId);
		}

		private static bool ContainsNodeId(List<NodeID> nodeIds, NodeID nodeId)
		{
			if (nodeIds == null || !nodeId.IsValid)
			{
				return false;
			}
			for (int i = 0; i < nodeIds.Count; i++)
			{
				if (nodeIds[i] == nodeId)
				{
					return true;
				}
			}
			return false;
		}

		private static void TakeoverPostfix(object __instance, Entity __result)
		{
			try
			{
				if (__result == null)
				{
					return;
				}
				PlayerTerritory val = __instance as PlayerTerritory;
				if (val != null)
				{
					PlayerInfo playerInfo = ((PlayerSubmanager)val).PlayerInfo;
					if (playerInfo != null)
					{
						PlayerSocial social = playerInfo.social;
						string text = ((social != null) ? social.PlayerGroupName : null) ?? $"Gang #{playerInfo.PID.id}";
						string text2 = "a building";
						try
						{
							if (__result.data?.board != null)
							{
								Node node = __result.data.board.bead.nodeId.FindNode();
								string cornerName = node?.GetCornerNameShort();
								if (!string.IsNullOrEmpty(cornerName))
								{
									text2 = cornerName;
								}
							}
						}
						catch
						{
						}
						if (text2 == "a building")
						{
							text2 = ((object)__result).ToString() ?? "a building";
						}
						GameplayTweaksPlugin.LogGrapevine("FRONT: " + text + " opened a front at " + text2);
					}
				}
			}
			catch
			{
			}
		}
	}
}
}
