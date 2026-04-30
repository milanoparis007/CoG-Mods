using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Game.Core;
using Game.Session.Actions;
using Game.Session.Board;
using Game.Session.Entities;
using Game.Session.Data;
using Game.Session;
using Game.Session.Player;
using Game.Session.Player.Commands;
using Game.Session.Sim;
using Game.Session.Input;
using Game.Services;
using Game.UI.Session.Combat;
using Game.UI.Session;
using Game.UI.Session.Convo;
using Game.UI.Session.Crew;
using Game.UI.Session.Popups;
using Game.UI.Util;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GameplayTweaks
{
	internal static class CrewCardExtraCrewPatch
	{
		private const string PassengerParentPath = "Extras";
		private const string ExtrasContentsName = "Contents";
		private const string PassengerRowName = "Passengers";
		private const string PassengersLabelName = "PassengersLabel";
		private const string InitialsLineName = "InitialsLine";
		private const string TopButtonsPath = "Info/Panel/Rows/Top/Buttons";
		private const string ScoutRowName = "Scout";
		private const string DriveRowName = "Drive";
		private const string SetAsDriverRowName = "SetAsDriver";
		private const string PickUpTopRowName = "PickUpVehicle";
		private const string ScoutButtonName = "ScoutButton";
		private const string DriveButtonName = "DriveButton";
		private const string SetAsDriverButtonName = "SetAsDriverButton";
		private const float TopRowButtonWidth = 24f;
		private const float TopRowButtonHeight = 18f;
		private const int MaxPassengerSlots = 3;
		private const int SmallButtonHeight = 20;
		private const int SmallFontSize = 10;
		private const int LabelFontSize = 11;
		private const int InitialsFontSize = 9;

		[HarmonyPostfix]
		internal static void Postfix(CrewCardContext __instance)
		{
			if (__instance?.card == null || __instance.data == null)
				return;
			try
			{
				PlayerCrew humanCrew = G.GetHumanCrew();
				if (__instance.data.type == CrewCardType.CrewUnassigned)
				{
					SetPassengerRowActive(__instance, active: false);
					SetScoutRowActive(__instance, active: false);
					SetDriveRowActive(__instance, active: false);
					SetSetAsDriverRowActive(__instance, active: false);
					bool showPickUpForUnassigned = humanCrew != null
						&& __instance.data.crew.peepId.IsValid
						&& !__instance.data.crew.IsInVehicle
						&& MultiCrewVehicleHelper.GetStrandedVehicles(humanCrew).Any();
					SetPickUpTopRowActive(__instance, showPickUpForUnassigned);
					return;
				}

				if (__instance.data.type != CrewCardType.CrewMuscle)
				{
					SetPassengerRowActive(__instance, active: false);
					SetScoutRowActive(__instance, active: false);
					SetDriveRowActive(__instance, active: false);
					SetSetAsDriverRowActive(__instance, active: false);
					SetPickUpTopRowActive(__instance, false);
					return;
				}
				if (!__instance.data.crew.IsInVehicle)
				{
					SetPassengerRowActive(__instance, active: false);
					SetScoutRowActive(__instance, active: false);
					SetDriveRowActive(__instance, active: false);
					SetSetAsDriverRowActive(__instance, active: false);
					SetPickUpTopRowActive(__instance, false);
					return;
				}
				SetPickUpTopRowActive(__instance, false);
				if (humanCrew == null)
					return;
				bool isPassenger = !MultiCrewVehicleHelper.IsDriver(humanCrew, __instance.data.crew);
				bool isDriver = !isPassenger;
				SetScoutRowActive(__instance, active: isPassenger);
				SetDriveRowActive(__instance, active: isDriver);
				EntityID vehicleId = __instance.data.crew.VehicleID;
				int inVehicleCount = MultiCrewVehicleHelper.GetAllCrewInVehicle(humanCrew, vehicleId).Count;
				SetSetAsDriverRowActive(__instance, active: isPassenger && inVehicleCount > 1);
				int vehicleSlots = MultiCrewVehicleHelper.GetVehicleCrewSlots(vehicleId);
				int maxPassengers = Math.Min(Math.Max(vehicleSlots - 1, 0), MaxPassengerSlots);
				List<CrewAssignment> allInVehicle = MultiCrewVehicleHelper.GetAllCrewInVehicle(humanCrew, vehicleId);
				var others = allInVehicle
					.Where(c => c.peepId != __instance.data.crew.peepId && c.IsNotDead)
					.Take(maxPassengers)
					.ToList();
				if (others.Count == 0)
				{
					SetPassengerRowActive(__instance, active: false);
					return;
				}
				GameObject row = GetOrCreatePassengerRow(__instance.card);
				if (row == null)
					return;
				row.SetActive(true);
				string initialsLine = string.Join(", ", others.Select(c => GetInitials(c.GetPeep())));
				Transform initialsTr = row.transform.Find(InitialsLineName);
				if (initialsTr != null)
				{
					Text t = initialsTr.GetComponent<Text>();
					if (t != null)
						t.text = initialsLine;
				}
			}
			catch (TypeLoadException ex)
			{
				Debug.LogWarning("[GameplayTweaks] CrewCardExtraCrewPatch (type load): " + ex.Message);
			}
			catch (ReflectionTypeLoadException ex)
			{
				Debug.LogWarning("[GameplayTweaks] CrewCardExtraCrewPatch (reflection type load): " + ex.Message);
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] CrewCardExtraCrewPatch: " + ex.Message);
			}
		}

		private static string GetInitials(Entity peep)
		{
			if (peep?.data?.person == null)
				return "?";
			string first = peep.data.person.first?.Trim();
			string last = peep.data.person.last?.Trim();
			char c1 = (first != null && first.Length > 0) ? char.ToUpperInvariant(first[0]) : '?';
			char c2 = (last != null && last.Length > 0) ? char.ToUpperInvariant(last[0]) : '?';
			return (c1 == '?' && c2 == '?') ? "?" : $"{c1}{c2}";
		}

		internal static bool TryGetCurrentVehicleRole(EntityID peepId, out PlayerCrew crew, out CrewAssignment assignment, out bool isDriver)
		{
			crew = null;
			assignment = CrewAssignment.EMPTY;
			isDriver = false;
			if (!peepId.IsValid)
			{
				return false;
			}

			crew = G.GetHumanCrew();
			if (crew == null)
			{
				return false;
			}

			assignment = crew.GetCrewForPeep(peepId);
			if (!assignment.IsValid || !assignment.IsInVehicle || !assignment.VehicleID.IsValid)
			{
				return false;
			}

			isDriver = MultiCrewVehicleHelper.IsDriver(crew, assignment);
			return true;
		}

		private static Text CreateTextLine(Transform parent, string text, string gameObjectName, int fontSize, bool allowOverflow = false)
		{
			GameObject go = new GameObject(gameObjectName, typeof(RectTransform), typeof(Text));
			go.transform.SetParent(parent, false);
			Text t = go.GetComponent<Text>();
			t.text = text;
			t.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
			t.fontSize = fontSize;
			((Graphic)t).color = new Color(0.94f, 0.94f, 0.92f);
			t.alignment = TextAnchor.UpperLeft;
			t.supportRichText = false;
			t.raycastTarget = false;
			if (allowOverflow)
			{
				t.horizontalOverflow = HorizontalWrapMode.Overflow;
				t.verticalOverflow = VerticalWrapMode.Overflow;
			}
			return t;
		}

		private static void SetPassengerRowActive(CrewCardContext ctx, bool active)
		{
			if (ctx?.card == null)
				return;
			Transform extras = ctx.card.transform.Find(PassengerParentPath);
			if (extras == null)
				return;
			Transform row = extras.Find(PassengerRowName);
			if (row != null)
				row.gameObject.SetActive(active);
			Transform col = extras.Find("PassengerColumn");
			if (col != null)
				col.gameObject.SetActive(false);
			Transform legacy = ctx.card.transform.Find("Info/Panel/" + PassengerRowName);
			if (legacy != null)
				legacy.gameObject.SetActive(false);
		}

		private static void SetScoutRowActive(CrewCardContext ctx, bool active)
		{
			if (ctx?.card == null)
				return;
			try
			{
				GameObject scoutBtn = GetOrCreateScoutRow(ctx);
				if (scoutBtn != null)
					scoutBtn.SetActive(active);
			}
			catch (TypeLoadException ex)
			{
				Debug.LogWarning("[GameplayTweaks] SetScoutRowActive (type load): " + ex.Message);
			}
			catch (ReflectionTypeLoadException ex)
			{
				Debug.LogWarning("[GameplayTweaks] SetScoutRowActive (reflection type load): " + ex.Message);
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] SetScoutRowActive: " + ex.Message);
			}
		}

		private static void SetDriveRowActive(CrewCardContext ctx, bool active)
		{
			if (ctx?.card == null)
				return;
			try
			{
				GameObject driveBtn = GetOrCreateDriveRow(ctx);
				if (driveBtn != null)
					driveBtn.SetActive(active);
			}
			catch (TypeLoadException ex)
			{
				Debug.LogWarning("[GameplayTweaks] SetDriveRowActive (type load): " + ex.Message);
			}
			catch (ReflectionTypeLoadException ex)
			{
				Debug.LogWarning("[GameplayTweaks] SetDriveRowActive (reflection type load): " + ex.Message);
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] SetDriveRowActive: " + ex.Message);
			}
		}

		private static void SetSetAsDriverRowActive(CrewCardContext ctx, bool active)
		{
			if (ctx?.card == null)
				return;
			try
			{
				GameObject setDriverBtn = GetOrCreateSetAsDriverRow(ctx);
				if (setDriverBtn != null)
					setDriverBtn.SetActive(active);
			}
			catch (TypeLoadException ex)
			{
				Debug.LogWarning("[GameplayTweaks] SetSetAsDriverRowActive (type load): " + ex.Message);
			}
			catch (ReflectionTypeLoadException ex)
			{
				Debug.LogWarning("[GameplayTweaks] SetSetAsDriverRowActive (reflection type load): " + ex.Message);
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] SetSetAsDriverRowActive: " + ex.Message);
			}
		}

		private static void SetPickUpTopRowActive(CrewCardContext ctx, bool active)
		{
			if (ctx?.card == null)
				return;
			try
			{
				GameObject btn = GetOrCreatePickUpTopRowButton(ctx);
				if (btn != null)
					btn.SetActive(active);
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] SetPickUpTopRowActive: " + ex.Message);
			}
		}

		private static GameObject GetOrCreatePickUpTopRowButton(CrewCardContext ctx)
		{
			if (ctx?.card == null)
				return null;
			try
			{
				Transform buttonsParent = ctx.card.transform.Find(TopButtonsPath);
				if (buttonsParent == null)
					return null;
				Transform existing = buttonsParent.Find(PickUpTopRowName);
				if (existing != null)
					return existing.gameObject;
				GameObject btnGo = CreateTopRowButton(buttonsParent, PickUpTopRowName, "Pick up", new Color(0.35f, 0.4f, 0.3f));
				btnGo.transform.SetSiblingIndex(GetTopRowInsertIndex(buttonsParent) + 3);
				CrewCardContext cardCtx = ctx;
				btnGo.GetComponent<Button>().onClick.AddListener(() =>
				{
					try
					{
						HandlePickUpTopRowClick(cardCtx);
					}
					catch (Exception ex)
					{
						Debug.LogWarning("[GameplayTweaks] Pick up Top row click: " + ex.Message);
					}
				});
				return btnGo;
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] GetOrCreatePickUpTopRowButton: " + ex.Message);
				return null;
			}
		}

		private static GameObject CreateTopRowButton(Transform buttonsParent, string name, string label, Color bgColor)
		{
			GameObject btnGo = new GameObject(name, typeof(RectTransform), typeof(Button), typeof(Image), typeof(LayoutElement));
			btnGo.transform.SetParent(buttonsParent, false);
			var le = btnGo.GetComponent<LayoutElement>();
			le.minWidth = TopRowButtonWidth;
			le.preferredWidth = TopRowButtonWidth;
			le.flexibleWidth = 0f;
			le.minHeight = TopRowButtonHeight;
			le.preferredHeight = TopRowButtonHeight;
			var btnRect = btnGo.GetComponent<RectTransform>();
			btnRect.sizeDelta = new Vector2(TopRowButtonWidth, TopRowButtonHeight);
			Image img = btnGo.GetComponent<Image>();
			if (img != null)
				img.color = bgColor;
			Button btn = btnGo.GetComponent<Button>();
			var colors = btn.colors;
			colors.normalColor = bgColor;
			btn.colors = colors;
			GameObject textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
			textGo.transform.SetParent(btnGo.transform, false);
			var textRect = textGo.GetComponent<RectTransform>();
			textRect.anchorMin = Vector2.zero;
			textRect.anchorMax = Vector2.one;
			textRect.offsetMin = Vector2.zero;
			textRect.offsetMax = Vector2.zero;
			Text text = textGo.GetComponent<Text>();
			text.text = label;
			text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
			text.fontSize = SmallFontSize;
			text.alignment = TextAnchor.MiddleCenter;
			text.color = new Color(0.94f, 0.94f, 0.92f);
			return btnGo;
		}

		private static int GetTopRowInsertIndex(Transform buttonsParent)
		{
			Transform inspect = buttonsParent.Find("Inspect");
			return inspect != null ? inspect.GetSiblingIndex() + 1 : buttonsParent.childCount;
		}

		private static bool IsCrewUnassignedCard(CrewCardContext ctx)
		{
			if (ctx?.data == null)
				return false;
			if (ctx.data.type != CrewCardType.CrewUnassigned)
				return false;
			if (!ctx.data.crew.peepId.IsValid)
				return false;
			if (GameplayTweaksPlugin.TryGetCrewAssignmentBlockReason(ctx.data.crew.peepId, out _))
				return false;
			return !ctx.data.crew.IsInVehicle;
		}

		private static bool TryAssignUnassignedCrewToStrandedVehicle(EntityID peepId, EntityID vehicleId)
		{
			if (!peepId.IsValid || !vehicleId.IsValid)
				return false;
			if (GameplayTweaksPlugin.TryGetCrewAssignmentBlockReason(peepId, out string blockedReason))
			{
				MultiCrewVehicleHelper.ShowHudMessage(blockedReason);
				return false;
			}
			try
			{
				PlayerCrew humanCrew = G.GetHumanCrew();
				if (humanCrew == null)
					return false;

				try
				{
					MultiCrewVehicleHelper.PickUpStrandedVehicleInProgress = true;
					humanCrew.AssignCrewToVehicle(peepId, vehicleId);
				}
				finally
				{
					MultiCrewVehicleHelper.PickUpStrandedVehicleInProgress = false;
				}

				bool assigned = false;
				try
				{
					CrewAssignment updated = humanCrew.GetCrewForPeep(peepId);
					assigned = updated.IsInVehicle && updated.VehicleID == vehicleId;
				}
				catch (TypeLoadException)
				{
					assigned = true;
				}
				catch (ReflectionTypeLoadException)
				{
					assigned = true;
				}

				if (!assigned)
				{
					MultiCrewVehicleHelper.LogPickupAssignStickFailure(vehicleId, peepId);
					MultiCrewVehicleHelper.ShowHudMessage("Could not pick up vehicle.");
					return false;
				}

				MultiCrewVehicleHelper.TryRefreshCrewHudCardsForVehicle(vehicleId);
				return true;
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] TryAssignUnassignedCrewToStrandedVehicle: " + ex.Message);
				return false;
			}
		}

		private static void HandlePickUpTopRowClick(CrewCardContext cardCtx)
		{
			if (!IsCrewUnassignedCard(cardCtx))
			{
				MultiCrewVehicleHelper.ShowHudMessage("Selected crew is no longer unassigned.");
				return;
			}

			PlayerCrew humanCrew = G.GetHumanCrew();
			if (humanCrew == null)
				return;

			EntityID peepId = cardCtx.data.crew.peepId;
			if (GameplayTweaksPlugin.TryGetCrewAssignmentBlockReason(peepId, out string blockedReason))
			{
				MultiCrewVehicleHelper.ShowHudMessage(blockedReason);
				return;
			}
			List<EntityID> stranded = MultiCrewVehicleHelper.GetStrandedVehicles(humanCrew).ToList();
			if (stranded.Count == 0)
			{
				MultiCrewVehicleHelper.ShowHudMessage("No stranded vehicles.");
				return;
			}

			if (stranded.Count == 1)
			{
				EntityID vehicleId = stranded[0];
				MultiCrewVehicleHelper.LogPickupDirectAssign(vehicleId, peepId);
				TryAssignUnassignedCrewToStrandedVehicle(peepId, vehicleId);
				return;
			}

			MultiCrewVehicleHelper.LogPickupSelector(peepId, stranded.Count);
			EntitySelectionPopup.ShowVehicleSelector(stranded, "Pick vehicle to drive", vehicleId =>
			{
				TryAssignUnassignedCrewToStrandedVehicle(peepId, vehicleId);
			});
		}

		private static void RunScoutAction(EntityID peepId, CrewCardContext cardCtx)
		{
			try
			{
				EntityID currentPeepId = cardCtx?.data?.crew.peepId.IsValid == true ? cardCtx.data.crew.peepId : peepId;
				if (!TryGetCurrentVehicleRole(currentPeepId, out _, out _, out bool isDriver) || isDriver)
				{
					MultiCrewVehicleHelper.ShowHudMessage("Only passengers can scout.");
					return;
				}
				if (!MultiCrewVehicleHelper.TryGetScoutableUnknownNodes(currentPeepId, out List<Node> unknown, out string failureReason))
				{
					MultiCrewVehicleHelper.ShowHudMessage(failureReason);
					return;
				}
				if (MultiCrewVehicleHelper.TryScoutOneNode(currentPeepId, unknown[0], out string scoutFailureReason))
				{
					cardCtx.RefreshCard();
					if (cardCtx?.data?.crew.IsInVehicle == true)
						MultiCrewVehicleHelper.TryRefreshCrewHudCardsForVehicle(cardCtx.data.crew.VehicleID);
				}
				else if (!string.IsNullOrWhiteSpace(scoutFailureReason))
				{
					MultiCrewVehicleHelper.ShowHudMessage(scoutFailureReason);
				}
			}
			catch (TypeLoadException ex)
			{
				Debug.LogWarning("[GameplayTweaks] Scout click TypeLoadException: " + ex.Message);
				MultiCrewVehicleHelper.ShowHudMessage("Scout failed (try again).");
			}
			catch (ReflectionTypeLoadException ex)
			{
				Debug.LogWarning("[GameplayTweaks] Scout click ReflectionTypeLoadException: " + ex.Message);
				MultiCrewVehicleHelper.ShowHudMessage("Scout failed (try again).");
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] Scout click: " + ex.Message);
				MultiCrewVehicleHelper.ShowHudMessage("Scout failed.");
			}
		}

		private static GameObject GetOrCreateScoutRow(CrewCardContext ctx)
		{
			if (ctx?.card == null)
				return null;
			try
			{
				Transform buttonsParent = ctx.card.transform.Find(TopButtonsPath);
				if (buttonsParent == null)
					return null;
				Transform existing = buttonsParent.Find(ScoutRowName);
				GameObject btnGo = existing != null
					? existing.gameObject
					: CreateTopRowButton(buttonsParent, ScoutRowName, "Scout (7 MP)", new Color(0.3f, 0.5f, 0.3f));
				if (existing == null)
				{
					btnGo.transform.SetSiblingIndex(GetTopRowInsertIndex(buttonsParent) + 1);
				}
				EntityID peepId = ctx.data.crew.peepId;
				CrewCardContext cardCtx = ctx;
				Button scoutButton = btnGo.GetComponent<Button>();
				scoutButton?.onClick.RemoveAllListeners();
				scoutButton?.onClick.AddListener(() =>
				{
					try
					{
						RunScoutAction(peepId, cardCtx);
					}
					catch (TypeLoadException ex)
					{
						Debug.LogWarning("[GameplayTweaks] Scout click TypeLoadException: " + ex.Message);
						try { MultiCrewVehicleHelper.ShowHudMessage("Scout failed (try again)."); } catch { }
					}
					catch (ReflectionTypeLoadException ex)
					{
						Debug.LogWarning("[GameplayTweaks] Scout click ReflectionTypeLoadException: " + ex.Message);
						try { MultiCrewVehicleHelper.ShowHudMessage("Scout failed (try again)."); } catch { }
					}
					catch (Exception ex)
					{
						Debug.LogWarning("[GameplayTweaks] Scout click: " + ex.Message);
						try { MultiCrewVehicleHelper.ShowHudMessage("Scout failed."); } catch { }
					}
				});
				return btnGo;
			}
			catch (TypeLoadException ex)
			{
				Debug.LogWarning("[GameplayTweaks] GetOrCreateScoutRow (type load): " + ex.Message);
				return null;
			}
			catch (ReflectionTypeLoadException ex)
			{
				Debug.LogWarning("[GameplayTweaks] GetOrCreateScoutRow (reflection type load): " + ex.Message);
				return null;
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] GetOrCreateScoutRow: " + ex.Message);
				return null;
			}
		}

		private static GameObject GetOrCreateDriveRow(CrewCardContext ctx)
		{
			if (ctx?.card == null)
				return null;
			try
			{
				Transform buttonsParent = ctx.card.transform.Find(TopButtonsPath);
				if (buttonsParent == null)
					return null;
				Transform existing = buttonsParent.Find(DriveRowName);
				GameObject btnGo = existing != null
					? existing.gameObject
					: CreateTopRowButton(buttonsParent, DriveRowName, "Drive", new Color(0.35f, 0.45f, 0.55f));
				if (existing == null)
					btnGo.transform.SetSiblingIndex(GetTopRowInsertIndex(buttonsParent));
				CrewCardContext cardCtx = ctx;
				Button driveButton = btnGo.GetComponent<Button>();
				driveButton?.onClick.RemoveAllListeners();
				driveButton?.onClick.AddListener(() =>
				{
					try
					{
						if (!TryGetCurrentVehicleRole(cardCtx?.data?.crew.peepId ?? EntityID.INVALID, out _, out CrewAssignment latestAssignment, out bool isDriver) || !isDriver)
						{
							MultiCrewVehicleHelper.ShowHudMessage("Only the driver can control the vehicle.");
							return;
						}
						Entity vehicle = latestAssignment.VehicleID.FindEntity();
						if (vehicle != null)
							MultiCrewVehicleHelper.TweenCameraToEntitySafe(vehicle);
					}
					catch (Exception ex)
					{
						Debug.LogWarning("[GameplayTweaks] Drive click: " + ex.Message);
					}
				});
				return btnGo;
			}
			catch (TypeLoadException ex)
			{
				Debug.LogWarning("[GameplayTweaks] GetOrCreateDriveRow (type load): " + ex.Message);
				return null;
			}
			catch (ReflectionTypeLoadException ex)
			{
				Debug.LogWarning("[GameplayTweaks] GetOrCreateDriveRow (reflection type load): " + ex.Message);
				return null;
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] GetOrCreateDriveRow: " + ex.Message);
				return null;
			}
		}

		private static GameObject GetOrCreateSetAsDriverRow(CrewCardContext ctx)
		{
			if (ctx?.card == null)
				return null;
			try
			{
				Transform buttonsParent = ctx.card.transform.Find(TopButtonsPath);
				if (buttonsParent == null)
					return null;
				Transform existing = buttonsParent.Find(SetAsDriverRowName);
				GameObject btnGo;
				if (existing != null)
				{
					btnGo = existing.gameObject;
				}
				else
				{
					btnGo = CreateTopRowButton(buttonsParent, SetAsDriverRowName, "Set driver", new Color(0.4f, 0.35f, 0.25f));
					btnGo.transform.SetSiblingIndex(GetTopRowInsertIndex(buttonsParent) + 2);
				}
				EntityID vehicleId = ctx.data.crew.VehicleID;
				CrewCardContext cardCtx = ctx;
				Button btn = btnGo.GetComponent<Button>();
				if (btn != null)
				{
					btn.onClick.RemoveAllListeners();
					btn.onClick.AddListener(() =>
					{
						if (!MultiCrewVehicleHelper.TryBeginSetDriverUi())
							return;
						try
						{
							if (!TryGetCurrentVehicleRole(cardCtx?.data?.crew.peepId ?? EntityID.INVALID, out _, out _, out bool isDriver) || isDriver)
							{
								MultiCrewVehicleHelper.ShowHudMessage(isDriver ? "Already the driver." : "Crew is not in a vehicle.");
								return;
							}
							if (MultiCrewVehicleHelper.TryBecomeDriverFromCardContext(cardCtx))
							{
								try
								{
									MultiCrewVehicleHelper.ShowHudMessage("Driver set.");
								}
								catch (TypeLoadException)
								{
								}
								catch (ReflectionTypeLoadException)
								{
								}

								try
								{
									MultiCrewVehicleHelper.TryRefreshCrewHudCardsForVehicle(vehicleId);
								}
								catch (TypeLoadException)
								{
								}
								catch (ReflectionTypeLoadException)
								{
								}
							}
						}
						catch (TypeLoadException ex)
						{
							Debug.LogWarning("[GameplayTweaks] Set driver TypeLoadException: " + ex.Message);
						}
						catch (ReflectionTypeLoadException ex)
						{
							Debug.LogWarning("[GameplayTweaks] Set driver ReflectionTypeLoadException: " + ex.Message);
						}
						catch (Exception ex)
						{
							Debug.LogWarning("[GameplayTweaks] Set driver: " + ex.Message);
						}
						finally
						{
							MultiCrewVehicleHelper.EndSetDriverUi();
						}
					});
				}
				return btnGo;
			}
			catch (TypeLoadException ex)
			{
				Debug.LogWarning("[GameplayTweaks] GetOrCreateSetAsDriverRow (type load): " + ex.Message);
				return null;
			}
			catch (ReflectionTypeLoadException ex)
			{
				Debug.LogWarning("[GameplayTweaks] GetOrCreateSetAsDriverRow (reflection type load): " + ex.Message);
				return null;
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] GetOrCreateSetAsDriverRow: " + ex.Message);
				return null;
			}
		}

		private static GameObject CreateSmallExtrasButton(Transform parent, string name, string label, Color bgColor)
		{
			GameObject btnGo = new GameObject(name, typeof(RectTransform), typeof(Button), typeof(Image));
			btnGo.transform.SetParent(parent, false);
			var btnRect = btnGo.GetComponent<RectTransform>();
			btnRect.anchorMin = new Vector2(0f, 0.5f);
			btnRect.anchorMax = new Vector2(1f, 0.5f);
			btnRect.sizeDelta = new Vector2(0f, SmallButtonHeight);
			Button btn = btnGo.GetComponent<Button>();
			var colors = btn.colors;
			colors.normalColor = bgColor;
			btn.colors = colors;
			GameObject textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
			textGo.transform.SetParent(btnGo.transform, false);
			var textRect = textGo.GetComponent<RectTransform>();
			textRect.anchorMin = Vector2.zero;
			textRect.anchorMax = Vector2.one;
			textRect.offsetMin = Vector2.zero;
			textRect.offsetMax = Vector2.zero;
			Text text = textGo.GetComponent<Text>();
			text.text = label;
			text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
			text.fontSize = SmallFontSize;
			text.alignment = TextAnchor.MiddleCenter;
			text.color = new Color(0.94f, 0.94f, 0.92f);
			return btnGo;
		}

		private static GameObject GetOrCreatePassengerRow(GameObject card)
		{
			if (card == null)
				return null;

			Transform extras = card.transform.Find(PassengerParentPath);
			if (extras == null)
				return null;

			Transform contents = extras.Find(ExtrasContentsName);
			int insertIndex = contents != null ? contents.GetSiblingIndex() : extras.childCount;

			Transform existing = extras.Find(PassengerRowName);
			if (existing != null)
			{
				existing.SetSiblingIndex(insertIndex);
				if (existing.Find(PassengersLabelName) == null || existing.Find(InitialsLineName) == null)
				{
					for (int i = existing.childCount - 1; i >= 0; i--)
						UnityEngine.Object.Destroy(existing.GetChild(i).gameObject);
					CreatePassengerRowContent(existing.gameObject);
				}
				return existing.gameObject;
			}

			GameObject row = new GameObject(PassengerRowName);
			row.transform.SetParent(extras, worldPositionStays: false);
			row.transform.SetSiblingIndex(insertIndex);

			RectTransform rect = row.AddComponent<RectTransform>();
			rect.anchorMin = new Vector2(0f, 0.5f);
			rect.anchorMax = new Vector2(1f, 0.5f);
			rect.pivot = new Vector2(0f, 0.5f);
			rect.anchoredPosition = Vector2.zero;
			rect.sizeDelta = new Vector2(0f, 32f);
			var le = row.AddComponent<LayoutElement>();
			le.minHeight = 28f;
			le.preferredHeight = 32f;
			le.flexibleWidth = 0f;

			CreatePassengerRowContent(row);
			return row;
		}

		private static void CreatePassengerRowContent(GameObject row)
		{
			var vLayout = row.AddComponent<VerticalLayoutGroup>();
			vLayout.spacing = 2f;
			vLayout.childAlignment = TextAnchor.UpperLeft;
			vLayout.childControlWidth = true;
			vLayout.childControlHeight = true;
			vLayout.childForceExpandWidth = true;
			vLayout.childForceExpandHeight = false;
			vLayout.padding = new RectOffset(0, 0, 0, 4);

			CreateTextLine(row.transform, "Passengers:", PassengersLabelName, LabelFontSize);
			GameObject initialsLine = new GameObject(InitialsLineName, typeof(RectTransform), typeof(Text), typeof(LayoutElement));
			initialsLine.transform.SetParent(row.transform, false);
			var le = initialsLine.GetComponent<LayoutElement>();
			le.flexibleWidth = 1f;
			le.minWidth = 80f;
			Text t = initialsLine.GetComponent<Text>();
			t.text = "";
			t.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
			t.fontSize = InitialsFontSize;
			((Graphic)t).color = new Color(0.94f, 0.94f, 0.92f);
			t.alignment = TextAnchor.UpperLeft;
			t.supportRichText = false;
			t.raycastTarget = false;
			t.horizontalOverflow = HorizontalWrapMode.Overflow;
			t.verticalOverflow = VerticalWrapMode.Overflow;
		}
	}

	/// <summary>
	/// On CrewUnassigned cards, add "Pick up vehicle" button that assigns the selected unassigned crew
	/// to a stranded vehicle (0 crew, not at owned building/safehouse).
	/// </summary>
	internal static class CrewCardVehicleUnassignedPickUpPatch
	{
		private const string PickUpBtnName = "PickUpVehicleBtn";
		private static bool _loggedTypeLoadOnce;

		[HarmonyPostfix]
		internal static void Postfix(CrewCardContext __instance)
		{
			try
			{
				if (__instance?.card == null || __instance.data == null)
					return;
				if (!IsCrewUnassignedCard(__instance))
				{
					SetPickUpButtonActive(__instance, false);
					return;
				}

				PlayerCrew crew = G.GetHumanCrew();
				if (crew == null)
				{
					SetPickUpButtonActive(__instance, false);
					return;
				}

				List<EntityID> stranded = GetStrandedVehicles(crew);
				if (stranded.Count == 0)
				{
					SetPickUpButtonActive(__instance, false);
					return;
				}

				GameObject btnGo = GetOrCreatePickUpButton(__instance);
				if (btnGo != null)
					btnGo.SetActive(true);
			}
			catch (TypeLoadException ex)
			{
				LogTypeLoadOnce(ex);
				SetPickUpButtonActiveSafe(__instance, false);
			}
			catch (ReflectionTypeLoadException ex)
			{
				LogTypeLoadOnce(ex);
				SetPickUpButtonActiveSafe(__instance, false);
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] CrewCardVehicleUnassignedPickUpPatch: " + ex.Message);
			}
		}

		private static bool IsCrewUnassignedCard(CrewCardContext ctx)
		{
			if (ctx?.data == null)
				return false;
			if (ctx.data.type != CrewCardType.CrewUnassigned)
				return false;
			if (!ctx.data.crew.peepId.IsValid)
				return false;
			if (GameplayTweaksPlugin.TryGetCrewAssignmentBlockReason(ctx.data.crew.peepId, out _))
				return false;
			return !ctx.data.crew.IsInVehicle;
		}

		private static List<EntityID> GetStrandedVehicles(PlayerCrew crew)
		{
			return MultiCrewVehicleHelper.GetStrandedVehicles(crew).ToList();
		}

		private static void SetPickUpButtonActiveSafe(CrewCardContext ctx, bool active)
		{
			try
			{
				if (ctx?.card == null)
					return;
				Transform tr = ctx.card.transform.Find("Info/Panel/" + PickUpBtnName);
				if (tr != null)
					tr.gameObject.SetActive(active);
			}
			catch { }
		}

		private static void LogTypeLoadOnce(Exception ex)
		{
			if (_loggedTypeLoadOnce)
				return;
			_loggedTypeLoadOnce = true;
			Debug.LogWarning($"[GameplayTweaks] CrewCardVehicleUnassignedPickUpPatch disabled by type load: {ex.GetType().Name}: {ex.Message}");
		}

		private static void DoPickUpVehicleForUnassignedCrew(CrewCardContext ctx)
		{
			if (!IsCrewUnassignedCard(ctx))
				return;

			PlayerCrew humanCrew = G.GetHumanCrew();
			if (humanCrew == null)
				return;

			EntityID peepId = ctx.data.crew.peepId;
			if (GameplayTweaksPlugin.TryGetCrewAssignmentBlockReason(peepId, out string blockedReason))
			{
				MultiCrewVehicleHelper.ShowHudMessage(blockedReason);
				if (global::Game.Game.ctx?.hud?.crew != null)
					global::Game.Game.ctx.hud.crew.RefreshCards();
				return;
			}
			CrewAssignment assignment = humanCrew.GetCrewForPeep(peepId);
			if (!assignment.peepId.IsValid || assignment.IsInVehicle)
			{
				MultiCrewVehicleHelper.ShowHudMessage("Selected crew is no longer unassigned.");
				if (global::Game.Game.ctx?.hud?.crew != null)
					global::Game.Game.ctx.hud.crew.RefreshCards();
				return;
			}

			List<EntityID> strandedVehicles = GetStrandedVehicles(humanCrew);
			if (strandedVehicles.Count == 0)
			{
				MultiCrewVehicleHelper.ShowHudMessage("No stranded vehicles available.");
				if (global::Game.Game.ctx?.hud?.crew != null)
					global::Game.Game.ctx.hud.crew.RefreshCards();
				return;
			}

			EntitySelectionPopup.ShowVehicleSelector(strandedVehicles, "Select stranded vehicle", vehicleId =>
			{
				try
				{
					MultiCrewVehicleHelper.PickUpStrandedVehicleInProgress = true;
					humanCrew.AssignCrewToVehicle(peepId, vehicleId);
				}
				finally
				{
					MultiCrewVehicleHelper.PickUpStrandedVehicleInProgress = false;
				}
				if (global::Game.Game.ctx?.hud?.crew != null)
					global::Game.Game.ctx.hud.crew.RefreshCards();
			});
		}

		private static void SetPickUpButtonActive(CrewCardContext ctx, bool active)
		{
			if (ctx?.card == null)
				return;
			Transform tr = ctx.card.transform.Find("Info/Panel/" + PickUpBtnName);
			if (tr != null)
				tr.gameObject.SetActive(active);
		}

		private static GameObject GetOrCreatePickUpButton(CrewCardContext ctx)
		{
			Transform panel = ctx.card.transform.Find("Info/Panel");
			if (panel == null)
				return null;

			Transform existing = panel.Find(PickUpBtnName);
			GameObject btnGo = existing?.gameObject;
			if (btnGo == null)
			{
				btnGo = MultiCrewVehicleHelper.CreateButtonFromCommandTemplate(panel, PickUpBtnName, "Pick up vehicle");
				if (btnGo == null)
					btnGo = CreateMinimalPickUpButton(panel);
			}
			if (btnGo == null)
				return null;

			RectTransform rect = btnGo.GetComponent<RectTransform>();
			if (rect != null)
			{
				rect.anchorMin = new Vector2(0f, 0f);
				rect.anchorMax = new Vector2(1f, 0f);
				rect.pivot = new Vector2(0.5f, 0f);
				rect.anchoredPosition = new Vector2(0f, 2f);
				rect.sizeDelta = new Vector2(0f, 22f);
			}

			Button btn = btnGo.GetComponent<Button>();
			if (btn != null)
			{
				btn.onClick.RemoveAllListeners();
				CrewCardContext cardCtx = ctx;
				btn.onClick.AddListener(() =>
				{
					try
					{
						DoPickUpVehicleForUnassignedCrew(cardCtx);
					}
					catch (TypeLoadException ex)
					{
						Debug.LogWarning("[GameplayTweaks] Pick up vehicle click TypeLoadException: " + ex.Message);
					}
					catch (ReflectionTypeLoadException ex)
					{
						Debug.LogWarning("[GameplayTweaks] Pick up vehicle click ReflectionTypeLoadException: " + ex.Message);
					}
					catch (Exception ex)
					{
						Debug.LogWarning("[GameplayTweaks] Pick up vehicle click: " + ex.Message);
					}
				});
			}
			return btnGo;
		}

		private static GameObject CreateMinimalPickUpButton(Transform panel)
		{
			GameObject btnGo = new GameObject(PickUpBtnName, typeof(RectTransform), typeof(Button), typeof(Image));
			btnGo.transform.SetParent(panel, false);
			var img = btnGo.GetComponent<Image>();
			if (img != null)
				img.color = new Color(0.25f, 0.4f, 0.25f);

			Button btn = btnGo.GetComponent<Button>();
			var colors = btn.colors;
			colors.normalColor = new Color(0.25f, 0.4f, 0.25f);
			btn.colors = colors;

			GameObject textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
			textGo.transform.SetParent(btnGo.transform, false);
			RectTransform textRect = textGo.GetComponent<RectTransform>();
			textRect.anchorMin = Vector2.zero;
			textRect.anchorMax = Vector2.one;
			textRect.offsetMin = Vector2.zero;
			textRect.offsetMax = Vector2.zero;
			Text text = textGo.GetComponent<Text>();
			text.text = "Pick up vehicle";
			text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
			text.fontSize = 10;
			text.alignment = TextAnchor.MiddleCenter;
			text.color = new Color(0.94f, 0.94f, 0.92f);
			return btnGo;
		}
	}

	internal static class CrewDialogLayoutRefreshHelper
	{
		private const string ExtrasPath = "Extras";
		private static readonly FieldInfo AllSectionsField = typeof(CrewDialog).GetField("_allSections", BindingFlags.Instance | BindingFlags.NonPublic);
		private static readonly FieldInfo AllCardsField = typeof(CrewDialog).GetField("_allCards", BindingFlags.Instance | BindingFlags.NonPublic);
		private static readonly MethodInfo ForceRebuildLayoutMethod = typeof(CrewDialog).GetMethod("ForceRebuildLayoutImmediate", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
		private static readonly MethodInfo RepositionBottomChromeMethod = typeof(CrewDialog).GetMethod("RepositionBottomChrome", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);

		internal static void RefreshNowAndNextFrame(CrewDialog dialog, CrewCardContext ctx, string source)
		{
			try
			{
				ForceCrewDialogLayout(dialog, ctx);
				ScheduleDeferredLayoutRefresh(dialog, ctx, source, remainingPasses: 2);
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] Crew dialog layout refresh failed source=" + source + " error=" + ex.Message);
			}
		}

		private static void ScheduleDeferredLayoutRefresh(CrewDialog dialog, CrewCardContext ctx, string source, int remainingPasses)
		{
			if (remainingPasses <= 0)
			{
				return;
			}

			global::Game.TimerUtil.RunNextFrame(delegate
			{
				try
				{
					ForceCrewDialogLayout(dialog, ctx);
				}
				catch (Exception ex)
				{
					Debug.LogWarning("[GameplayTweaks] Crew dialog layout deferred refresh failed source=" + source + " pass=" + remainingPasses + " error=" + ex.Message);
				}

				ScheduleDeferredLayoutRefresh(dialog, ctx, source, remainingPasses - 1);
			});
		}

		private static void ForceCrewDialogLayout(CrewDialog dialog, CrewCardContext ctx)
		{
			if (dialog == null)
			{
				return;
			}

			Canvas.ForceUpdateCanvases();
			ForceCardLayout(ctx);

			List<CrewCardContext> allCards = AllCardsField?.GetValue(dialog) as List<CrewCardContext>;
			if (allCards != null)
			{
				foreach (CrewCardContext cardCtx in allCards)
				{
					if (cardCtx == null || ReferenceEquals(cardCtx, ctx))
					{
						continue;
					}

					ForceCardLayout(cardCtx);
				}
			}

			GameObject allSections = AllSectionsField?.GetValue(dialog) as GameObject;
			if ((UnityEngine.Object)(object)allSections != (UnityEngine.Object)null)
			{
				RectTransform sectionsRect = allSections.GetComponent<RectTransform>();
				if ((UnityEngine.Object)(object)sectionsRect != (UnityEngine.Object)null)
				{
					LayoutRebuilder.ForceRebuildLayoutImmediate(sectionsRect);
				}

				for (int i = 0; i < allSections.transform.childCount; i++)
				{
					Transform section = allSections.transform.GetChild(i);
					RectTransform sectionRect = section as RectTransform;
					if ((UnityEngine.Object)(object)sectionRect != (UnityEngine.Object)null)
					{
						LayoutRebuilder.ForceRebuildLayoutImmediate(sectionRect);
					}

					Transform cards = section.Find("Cards");
					RectTransform cardsRect = cards as RectTransform;
					if ((UnityEngine.Object)(object)cardsRect != (UnityEngine.Object)null)
					{
						LayoutRebuilder.ForceRebuildLayoutImmediate(cardsRect);
					}
				}

				ResetAllChildScrollRects(allSections);
				if ((UnityEngine.Object)(object)sectionsRect != (UnityEngine.Object)null)
				{
					LayoutRebuilder.ForceRebuildLayoutImmediate(sectionsRect);
				}

				ScrollRect scrollRect = allSections.GetComponentInParent<ScrollRect>();
				if ((UnityEngine.Object)(object)scrollRect != (UnityEngine.Object)null)
				{
					scrollRect.Rebuild(CanvasUpdate.Prelayout);
					scrollRect.Rebuild(CanvasUpdate.Layout);
					if ((UnityEngine.Object)(object)scrollRect.content != (UnityEngine.Object)null)
					{
						LayoutRebuilder.ForceRebuildLayoutImmediate(scrollRect.content);
					}
					if ((UnityEngine.Object)(object)scrollRect.viewport != (UnityEngine.Object)null)
					{
						LayoutRebuilder.ForceRebuildLayoutImmediate(scrollRect.viewport);
					}
					scrollRect.Rebuild(CanvasUpdate.PostLayout);
					scrollRect.velocity = Vector2.zero;
					scrollRect.StopMovement();
				}
			}

			ForceRebuildLayoutMethod?.Invoke(dialog, null);
			RepositionBottomChromeMethod?.Invoke(dialog, null);
			Canvas.ForceUpdateCanvases();
		}

		private static void ResetAllChildScrollRects(GameObject root)
		{
			if ((UnityEngine.Object)(object)root == (UnityEngine.Object)null)
			{
				return;
			}

			ScrollRect[] componentsInChildren = root.GetComponentsInChildren<ScrollRect>(true);
			for (int i = 0; i < componentsInChildren.Length; i++)
			{
				ScrollRect scrollRect = componentsInChildren[i];
				if ((UnityEngine.Object)(object)scrollRect == (UnityEngine.Object)null)
				{
					continue;
				}

				scrollRect.velocity = Vector2.zero;
				scrollRect.StopMovement();
				scrollRect.normalizedPosition = new Vector2(0f, 1f);
				scrollRect.Rebuild(CanvasUpdate.Prelayout);
				scrollRect.Rebuild(CanvasUpdate.Layout);
				scrollRect.Rebuild(CanvasUpdate.PostLayout);
			}
		}

		private static void ForceCardLayout(CrewCardContext ctx)
		{
			RectTransform cardRect = ctx?.card?.GetComponent<RectTransform>();
			if ((UnityEngine.Object)(object)cardRect == (UnityEngine.Object)null)
			{
				return;
			}

			LayoutRebuilder.ForceRebuildLayoutImmediate(cardRect);
			RectTransform parentRect = ctx.card.transform.parent as RectTransform;
			if ((UnityEngine.Object)(object)parentRect != (UnityEngine.Object)null)
			{
				LayoutRebuilder.ForceRebuildLayoutImmediate(parentRect);
			}

			Transform extras = ctx.card.transform.Find(ExtrasPath);
			if ((UnityEngine.Object)(object)extras != (UnityEngine.Object)null)
			{
				RectTransform extrasRect = ((Component)extras).GetComponent<RectTransform>();
				if ((UnityEngine.Object)(object)extrasRect != (UnityEngine.Object)null)
				{
					LayoutRebuilder.ForceRebuildLayoutImmediate(extrasRect);
				}
			}
		}
	}

	internal static class CrewDialogCardToggleLayoutPatch
	{
		[HarmonyPostfix]
		internal static void Postfix(CrewDialog __instance, CrewCardContext ctx, bool isOn)
		{
			CrewDialogLayoutRefreshHelper.RefreshNowAndNextFrame(__instance, ctx, "SetCardState");
		}
	}

	internal static class CrewDialogRefreshLayoutPatch
	{
		[HarmonyPostfix]
		internal static void Postfix(CrewDialog __instance)
		{
			CrewDialogLayoutRefreshHelper.RefreshNowAndNextFrame(__instance, null, "RefreshCards");
		}
	}

	/// <summary>
	/// Adds "Pick up stranded vehicle" button to the crew management popup when there are vehicles with 0 crew not at an owned building.
	/// </summary>
	internal static class CrewMgmtPickUpStrandedPatch
	{
		private const string PickUpBtnName = "PickUpStrandedBtn";

		[HarmonyPostfix]
		internal static void Postfix(object __instance)
		{
			try
			{
				PlayerCrew crew = G.GetHumanCrew();
				if (crew == null)
					return;
				var popupType = __instance.GetType();
				FieldInfo selectedField = popupType.GetField("_selected", BindingFlags.NonPublic | BindingFlags.Instance);
				FieldInfo panelField = popupType.GetField("_panel", BindingFlags.NonPublic | BindingFlags.Instance);
				if (selectedField == null || panelField == null)
					return;
				GameObject panel = panelField.GetValue(__instance) as GameObject;
				if (panel == null)
					return;
				Transform infoTr = panel.transform.Find("Info");
				Transform parent = infoTr != null ? infoTr : panel.transform;
				Transform btnTr = parent.Find(PickUpBtnName);
				GameObject btnGo;
				Button btn;
				if (btnTr == null)
				{
					btnGo = new GameObject(PickUpBtnName, typeof(RectTransform), typeof(Button), typeof(Image));
					btnGo.transform.SetParent(parent, false);
					var rect = btnGo.GetComponent<RectTransform>();
					rect.anchorMin = new Vector2(0f, 1f);
					rect.anchorMax = new Vector2(1f, 1f);
					rect.pivot = new Vector2(0.5f, 1f);
					rect.anchoredPosition = new Vector2(0f, 0f);
					rect.sizeDelta = new Vector2(0f, 28f);
					btn = btnGo.GetComponent<Button>();
					var colors = btn.colors;
					colors.normalColor = new Color(0.25f, 0.45f, 0.25f);
					btn.colors = colors;
					Image btnImg = btnGo.GetComponent<Image>();
					if (btnImg != null)
						btnImg.color = new Color(0.22f, 0.28f, 0.22f);
					GameObject textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
					textGo.transform.SetParent(btnGo.transform, false);
					var textRect = textGo.GetComponent<RectTransform>();
					textRect.anchorMin = Vector2.zero;
					textRect.anchorMax = Vector2.one;
					textRect.offsetMin = Vector2.zero;
					textRect.offsetMax = Vector2.zero;
					Text text = textGo.GetComponent<Text>();
					text.text = "Pick up stranded vehicle";
					text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
					text.fontSize = 12;
					text.alignment = TextAnchor.MiddleCenter;
					text.color = new Color(0.94f, 0.94f, 0.92f);
				}
				else
				{
					btnGo = btnTr.gameObject;
					btn = btnGo.GetComponent<Button>();
				}

				if (btn != null)
				{
					btn.onClick.RemoveAllListeners();
					object popup = __instance;
					btn.onClick.AddListener(() =>
					{
						try
						{
							if (!TryGetSelectedVehicleId(popup, out EntityID selectedVehicleId))
							{
								MultiCrewVehicleHelper.ShowHudMessage("Select a stranded vehicle first.");
								return;
							}
							var crewNotInVehicle = crew.GetLiving()
								.Where(c => !c.IsInVehicle && !GameplayTweaksPlugin.TryGetCrewAssignmentBlockReason(c.peepId, out _))
								.Select(c => c.peepId)
								.ToList();
							if (crewNotInVehicle.Count == 0)
							{
								MultiCrewVehicleHelper.ShowHudMessage("No crew available to drive.");
								return;
							}
							EntitySelectionPopup.ShowCrewSelector(crewNotInVehicle, "Select crew to drive", peepId =>
							{
								if (GameplayTweaksPlugin.TryGetCrewAssignmentBlockReason(peepId, out string blockedReason))
								{
									MultiCrewVehicleHelper.ShowHudMessage(blockedReason);
									return;
								}
								try
								{
									MultiCrewVehicleHelper.PickUpStrandedVehicleInProgress = true;
									crew.AssignCrewToVehicle(peepId, selectedVehicleId);
								}
								finally
								{
									MultiCrewVehicleHelper.PickUpStrandedVehicleInProgress = false;
								}
								var refreshCards = popup.GetType().GetMethod("RefreshCards", BindingFlags.NonPublic | BindingFlags.Instance, null, new[] { typeof(bool) }, null);
								var refreshInfo = popup.GetType().GetMethod("RefreshInfoPanel", BindingFlags.NonPublic | BindingFlags.Instance);
								refreshCards?.Invoke(popup, new object[] { false });
								refreshInfo?.Invoke(popup, null);
							});
						}
						catch (TypeLoadException ex)
						{
							Debug.LogWarning("[GameplayTweaks] Pick up stranded vehicle click TypeLoadException: " + ex.Message);
						}
						catch (ReflectionTypeLoadException ex)
						{
							Debug.LogWarning("[GameplayTweaks] Pick up stranded vehicle click ReflectionTypeLoadException: " + ex.Message);
						}
						catch (Exception ex)
						{
							Debug.LogWarning("[GameplayTweaks] Pick up stranded vehicle click: " + ex.Message);
						}
					});
				}

				bool showButton = TryGetSelectedVehicleId(__instance, out EntityID selectedVehicle) && IsSelectedVehicleStranded(crew, selectedVehicle);
				btnGo.SetActive(showButton);
			}
			catch (Exception ex)
			{
				Debug.LogWarning($"[GameplayTweaks] CrewMgmtPickUpStrandedPatch: {ex.Message}");
			}
		}

		private static bool TryGetSelectedVehicleId(object popup, out EntityID vehicleId)
		{
			vehicleId = EntityID.INVALID;
			if (popup == null)
				return false;
			try
			{
				FieldInfo selectedField = popup.GetType().GetField("_selected", BindingFlags.NonPublic | BindingFlags.Instance);
				object entry = selectedField?.GetValue(popup);
				if (entry == null)
					return false;

				FieldInfo vehicleEntityField = entry.GetType().GetField("vehicle", BindingFlags.Public | BindingFlags.Instance);
				Entity vehicleEntity = vehicleEntityField?.GetValue(entry) as Entity;
				if (vehicleEntity != null && vehicleEntity.Id.IsValid)
				{
					vehicleId = vehicleEntity.Id;
					return true;
				}

				FieldInfo crewField = entry.GetType().GetField("crew", BindingFlags.Public | BindingFlags.Instance);
				object crewObj = crewField?.GetValue(entry);
				if (crewObj is CrewAssignment assignment && assignment.IsInVehicle && assignment.VehicleID.IsValid)
				{
					vehicleId = assignment.VehicleID;
					return true;
				}
			}
			catch
			{
			}
			return false;
		}

		private static bool IsSelectedVehicleStranded(PlayerCrew crew, EntityID vehicleId)
		{
			if (crew == null || !vehicleId.IsValid)
				return false;
			if (MultiCrewVehicleHelper.GetVehicleCrewCount(crew, vehicleId) != 0)
				return false;
			if (!MultiCrewVehicleHelper.TryIsVehicleAtOwnedBuilding(vehicleId, out bool atOwned))
				return true;
			return !atOwned;
		}
	}

	/// <summary>
	/// Adds Scout (passenger) and Drive (driver) buttons to the crew management popup right panel when the selected crew is in a vehicle.
	/// </summary>
	internal static class CrewMgmtScoutDrivePatch
	{
		private const string ScoutBtnName = "ScoutBtn";
		private const string DriveBtnName = "DriveBtn";
		private const string SetDriverBtnName = "SetDriverBtn";
		private static bool _disabledDueToTypeLoad;

		[HarmonyPostfix]
		internal static void Postfix(object __instance)
		{
			if (_disabledDueToTypeLoad)
			{
				TryRemoveButtons(__instance);
				return;
			}
			try
			{
				PlayerCrew humanCrew = G.GetHumanCrew();
				if (humanCrew == null)
					return;
				var popupType = __instance.GetType();
				FieldInfo selectedField = popupType.GetField("_selected", BindingFlags.NonPublic | BindingFlags.Instance);
				FieldInfo panelField = popupType.GetField("_panel", BindingFlags.NonPublic | BindingFlags.Instance);
				if (selectedField == null || panelField == null)
					return;
				object entry = selectedField.GetValue(__instance);
				if (entry == null)
					return;
				var entryType = entry.GetType();
				FieldInfo crewField = entryType.GetField("crew", BindingFlags.Public | BindingFlags.Instance);
				if (crewField == null)
					return;
				CrewAssignment assignment = (CrewAssignment)crewField.GetValue(entry);
				if (!assignment.IsInVehicle)
				{
					SetScoutDriveButtonsActive(__instance, panelField, false, false, false);
					return;
				}
				bool isDriver = MultiCrewVehicleHelper.IsDriver(humanCrew, assignment);
				int inVehicleCount = MultiCrewVehicleHelper.GetAllCrewInVehicle(humanCrew, assignment.VehicleID).Count;
				GameObject panel = panelField.GetValue(__instance) as GameObject;
				if (panel == null)
					return;
				Transform infoTr = panel.transform.Find("Info");
				Transform parent = infoTr != null ? infoTr : panel.transform;
				GameObject scoutBtn = GetOrCreatePanelButton(parent, ScoutBtnName, "Scout (7 MP)");
				GameObject driveBtn = GetOrCreatePanelButton(parent, DriveBtnName, "Drive (focus)");
				GameObject setDriverBtn = GetOrCreatePanelButton(parent, SetDriverBtnName, "Set driver");
				if (scoutBtn != null && driveBtn != null && setDriverBtn != null)
				{
					scoutBtn.SetActive(!isDriver);
					driveBtn.SetActive(isDriver);
					setDriverBtn.SetActive(!isDriver && inVehicleCount > 1);
					EntityID peepId = assignment.peepId;
					object popup = __instance;
					Button sb = scoutBtn.GetComponent<Button>();
					if (sb != null)
					{
						sb.onClick.RemoveAllListeners();
						sb.onClick.AddListener(() =>
						{
							try
							{
								if (!CrewCardExtraCrewPatch.TryGetCurrentVehicleRole(peepId, out _, out _, out bool isCurrentDriver) || isCurrentDriver)
								{
									MultiCrewVehicleHelper.ShowHudMessage("Only passengers can scout.");
									return;
								}
								if (!MultiCrewVehicleHelper.TryGetScoutableUnknownNodes(peepId, out List<Node> unknown, out string failureReason))
								{
									MultiCrewVehicleHelper.ShowHudMessage(failureReason);
								}
								else if (MultiCrewVehicleHelper.TryScoutOneNode(peepId, unknown[0], out string scoutFailureReason))
								{
									CrewAssignment updated = G.GetHumanCrew()?.GetCrewForPeep(peepId) ?? CrewAssignment.EMPTY;
									if (updated.IsInVehicle)
										MultiCrewVehicleHelper.TryRefreshCrewHudCardsForVehicle(updated.VehicleID);
									RefreshPopup(popup);
								}
								else if (!string.IsNullOrWhiteSpace(scoutFailureReason))
								{
									MultiCrewVehicleHelper.ShowHudMessage(scoutFailureReason);
								}
							}
							catch (TypeLoadException ex)
							{
								Debug.LogWarning("[GameplayTweaks] Scout click TypeLoadException: " + ex.Message);
								MultiCrewVehicleHelper.ShowHudMessage("Scout failed (try again).");
							}
							catch (ReflectionTypeLoadException ex)
							{
								Debug.LogWarning("[GameplayTweaks] Scout click ReflectionTypeLoadException: " + ex.Message);
								MultiCrewVehicleHelper.ShowHudMessage("Scout failed (try again).");
							}
							catch (Exception ex)
							{
								Debug.LogWarning("[GameplayTweaks] Scout click: " + ex.Message);
								MultiCrewVehicleHelper.ShowHudMessage("Scout failed.");
							}
						});
					}
					Button db = driveBtn.GetComponent<Button>();
					if (db != null)
					{
						db.onClick.RemoveAllListeners();
						db.onClick.AddListener(() =>
						{
							try
							{
								if (!CrewCardExtraCrewPatch.TryGetCurrentVehicleRole(peepId, out _, out CrewAssignment latestAssignment, out bool isCurrentDriver) || !isCurrentDriver)
								{
									MultiCrewVehicleHelper.ShowHudMessage("Only the driver can control the vehicle.");
									return;
								}
								Entity vehicleEntity = latestAssignment.VehicleID.FindEntity();
								MultiCrewVehicleHelper.TweenCameraToEntitySafe(vehicleEntity);
							}
							catch (TypeLoadException ex)
							{
								Debug.LogWarning("[GameplayTweaks] Drive click TypeLoadException: " + ex.Message);
							}
							catch (ReflectionTypeLoadException ex)
							{
								Debug.LogWarning("[GameplayTweaks] Drive click ReflectionTypeLoadException: " + ex.Message);
							}
							catch (Exception ex)
							{
								Debug.LogWarning("[GameplayTweaks] Drive click: " + ex.Message);
							}
						});
					}
					Button setDriverButton = setDriverBtn.GetComponent<Button>();
					if (setDriverButton != null)
					{
						setDriverButton.onClick.RemoveAllListeners();
						setDriverButton.onClick.AddListener(() =>
						{
							if (!MultiCrewVehicleHelper.TryBeginSetDriverUi())
								return;
							try
							{
								if (!CrewCardExtraCrewPatch.TryGetCurrentVehicleRole(peepId, out _, out CrewAssignment latestAssignment, out bool isCurrentDriver) || isCurrentDriver)
								{
									MultiCrewVehicleHelper.ShowHudMessage(isCurrentDriver ? "Already the driver." : "Crew is not in a vehicle.");
									return;
								}
								if (MultiCrewVehicleHelper.TryBecomeDriver(latestAssignment, "crew-management"))
								{
									MultiCrewVehicleHelper.ShowHudMessage("Driver set.");
									MultiCrewVehicleHelper.TryRefreshCrewHudCardsForVehicle(latestAssignment.VehicleID);
									RefreshPopup(popup);
								}
							}
							catch (TypeLoadException ex)
							{
								Debug.LogWarning("[GameplayTweaks] Set driver popup TypeLoadException: " + ex.Message);
							}
							catch (ReflectionTypeLoadException ex)
							{
								Debug.LogWarning("[GameplayTweaks] Set driver popup ReflectionTypeLoadException: " + ex.Message);
							}
							catch (Exception ex)
							{
								Debug.LogWarning("[GameplayTweaks] Set driver popup: " + ex.Message);
							}
							finally
							{
								MultiCrewVehicleHelper.EndSetDriverUi();
							}
						});
					}
				}
			}
			catch (TypeLoadException ex)
			{
				_disabledDueToTypeLoad = true;
				Debug.LogWarning($"[GameplayTweaks] CrewMgmtScoutDrivePatch disabled (TypeLoadException): {ex.Message}");
				TryRemoveButtons(__instance);
			}
			catch (ReflectionTypeLoadException ex)
			{
				_disabledDueToTypeLoad = true;
				Debug.LogWarning($"[GameplayTweaks] CrewMgmtScoutDrivePatch disabled (ReflectionTypeLoadException): {ex.Message}");
				TryRemoveButtons(__instance);
			}
			catch (Exception ex)
			{
				Debug.LogWarning($"[GameplayTweaks] CrewMgmtScoutDrivePatch: {ex.GetType().Name}: {ex.Message}");
			}
		}

		private static void TryRemoveButtons(object popup)
		{
			try
			{
				if (popup == null)
					return;
				FieldInfo panelField = popup.GetType().GetField("_panel", BindingFlags.NonPublic | BindingFlags.Instance);
				GameObject panel = panelField?.GetValue(popup) as GameObject;
				if (panel == null)
					return;
				Transform infoTr = panel.transform.Find("Info");
				Transform parent = infoTr != null ? infoTr : panel.transform;
				Transform st = parent.Find(ScoutBtnName);
				Transform dt = parent.Find(DriveBtnName);
				Transform sd = parent.Find(SetDriverBtnName);
				if (st != null)
					UnityEngine.Object.Destroy(st.gameObject);
				if (dt != null)
					UnityEngine.Object.Destroy(dt.gameObject);
				if (sd != null)
					UnityEngine.Object.Destroy(sd.gameObject);
			}
			catch
			{
			}
		}

		private static void SetScoutDriveButtonsActive(object popup, FieldInfo panelField, bool scout, bool drive, bool setDriver)
		{
			try
			{
				GameObject panel = panelField?.GetValue(popup) as GameObject;
				if (panel == null)
					return;
				Transform infoTr = panel.transform.Find("Info");
				Transform parent = infoTr != null ? infoTr : panel.transform;
				Transform st = parent.Find(ScoutBtnName);
				Transform dt = parent.Find(DriveBtnName);
				Transform sd = parent.Find(SetDriverBtnName);
				if (st != null) st.gameObject.SetActive(scout);
				if (dt != null) dt.gameObject.SetActive(drive);
				if (sd != null) sd.gameObject.SetActive(setDriver);
			}
			catch { }
		}

		private static GameObject GetOrCreatePanelButton(Transform parent, string name, string label)
		{
			Transform t = parent.Find(name);
			if (t != null)
				return t.gameObject;
			GameObject btnGo = MultiCrewVehicleHelper.CreateButtonFromCommandTemplate(parent, name, label);
			if (btnGo != null)
			{
				Image img = btnGo.GetComponent<Image>();
				if (img != null)
					img.color = new Color(0.22f, 0.28f, 0.32f);
				return btnGo;
			}
			btnGo = new GameObject(name, typeof(RectTransform), typeof(Button), typeof(Image));
			btnGo.transform.SetParent(parent, false);
			var rect = btnGo.GetComponent<RectTransform>();
			rect.anchorMin = new Vector2(0f, 1f);
			rect.anchorMax = new Vector2(1f, 1f);
			rect.pivot = new Vector2(0.5f, 1f);
			rect.anchoredPosition = new Vector2(0f, 0f);
			rect.sizeDelta = new Vector2(0f, 26f);
			Button btn = btnGo.GetComponent<Button>();
			var colors = btn.colors;
			colors.normalColor = new Color(0.3f, 0.4f, 0.5f);
			btn.colors = colors;
			Image btnImg = btnGo.GetComponent<Image>();
			if (btnImg != null)
				btnImg.color = new Color(0.22f, 0.28f, 0.32f);
			GameObject textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
			textGo.transform.SetParent(btnGo.transform, false);
			var textRect = textGo.GetComponent<RectTransform>();
			textRect.anchorMin = Vector2.zero;
			textRect.anchorMax = Vector2.one;
			textRect.offsetMin = Vector2.zero;
			textRect.offsetMax = Vector2.zero;
			Text text = textGo.GetComponent<Text>();
			text.text = label;
			text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
			text.fontSize = 11;
			text.alignment = TextAnchor.MiddleCenter;
			text.color = new Color(0.94f, 0.94f, 0.92f);
			return btnGo;
		}

		private static void RefreshPopup(object popup)
		{
			try
			{
				var refreshInfo = popup?.GetType().GetMethod("RefreshInfoPanel", BindingFlags.NonPublic | BindingFlags.Instance);
				refreshInfo?.Invoke(popup, null);
			}
			catch { }
		}
	}

	/// <summary>
	/// Appends "Crew: N/4" to the crew management popup info text when a vehicle is selected.
	/// </summary>
	internal static class CrewMgmtCrewCountPatch
	{
		[HarmonyPostfix]
		internal static void Postfix(object __instance)
		{
			if (__instance == null)
				return;
			Type popupType = __instance.GetType();
			FieldInfo selectedField = popupType.GetField("_selected", BindingFlags.NonPublic | BindingFlags.Instance);
			FieldInfo panelField = popupType.GetField("_panel", BindingFlags.NonPublic | BindingFlags.Instance);
			if (selectedField == null || panelField == null)
				return;
			object entry = selectedField.GetValue(__instance);
			if (entry == null)
				return;
			GameObject panel = panelField.GetValue(__instance) as GameObject;
			if (panel == null)
				return;
			Transform textTransform = panel.transform.Find("Info/Viewport/Content/Text");
			if (textTransform == null)
				return;
			FieldInfo crewField = entry.GetType().GetField("crew", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
			CrewAssignment selectedCrew = crewField != null ? (CrewAssignment)crewField.GetValue(entry) : CrewAssignment.EMPTY;
			string custodyStatus = selectedCrew.IsValid && selectedCrew.peepId.IsValid && GameplayTweaksPlugin.TryGetCrewAssignmentBlockReason(selectedCrew.peepId, out string blockedReason)
				? blockedReason
				: null;
			FieldInfo vehicleField = entry.GetType().GetField("vehicle", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
			Entity vehicle = vehicleField != null ? vehicleField.GetValue(entry) as Entity : null;
			string crewLine = null;
			if (vehicle != null)
			{
				PlayerCrew crew = G.GetHumanCrew();
				if (crew != null)
				{
					int n = MultiCrewVehicleHelper.GetVehicleCrewCount(crew, vehicle.Id);
					int maxSlots = MultiCrewVehicleHelper.GetVehicleCrewSlots(vehicle);
					crewLine = "\nCrew: " + n + "/" + maxSlots;
				}
			}
			var tmp = textTransform.GetComponent<TMP_Text>();
			if (tmp != null)
			{
				if (!string.IsNullOrWhiteSpace(custodyStatus) && tmp.text.IndexOf(custodyStatus, StringComparison.OrdinalIgnoreCase) < 0)
				{
					tmp.text += "\n\n" + custodyStatus;
				}
				if (!string.IsNullOrEmpty(crewLine))
				{
					tmp.text += crewLine;
				}
				return;
			}
			var txt = textTransform.GetComponent<Text>();
			if (txt != null)
			{
				if (!string.IsNullOrWhiteSpace(custodyStatus) && txt.text.IndexOf(custodyStatus, StringComparison.OrdinalIgnoreCase) < 0)
				{
					txt.text += "\n\n" + custodyStatus;
				}
				if (!string.IsNullOrEmpty(crewLine))
				{
					txt.text += crewLine;
				}
			}
		}
	}
}
