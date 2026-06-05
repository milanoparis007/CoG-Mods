using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Game.Core;
using Game.Services;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Player;
using Game.UI.Mouseovers;
using Game.UI.Session.Deliveries;
using HarmonyLib;
using SomaSim.Util;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GameplayTweaks
{
public partial class GameplayTweaksPlugin
{
	private sealed class DeliveryFrontExpansionPatch
	{
		private const string ExpandFrontsLabel = "+";
		private const string ExpandFrontsMouseoverKey = "ui.deliveries.actions.mo.collect-front";
		private const string ResourceFilterInputName = "GT_DeliveryResourceFilter";
		private const string ResourceFilterPlaceholder = "Filter";

		private static readonly FieldInfo DeliveriesEditViewModelField = AccessTools.Field(typeof(DeliveriesEditView), "_model");
		private static readonly FieldInfo DeliveriesEditViewGoField = AccessTools.Field(typeof(DeliveriesEditView), "_go");
		private static readonly FieldInfo AutomationExecutorPlayerField = AccessTools.Field(typeof(AutomationExecutor), "_player");
		private static readonly Type DeliveriesEditModelType = typeof(DeliveriesEditView).Assembly.GetType("Game.UI.Session.Deliveries.DeliveriesEditModel");
		private static readonly Type DeliveriesEditCashOrResType = DeliveriesEditModelType?.GetNestedType("CashOrRes", BindingFlags.Public | BindingFlags.NonPublic);
		private static readonly Type DeliveriesEditActionType = DeliveriesEditModelType?.GetNestedType("Action", BindingFlags.Public | BindingFlags.NonPublic);
		private static readonly FieldInfo ModelActionsField = DeliveriesEditModelType?.GetField("actions", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
		private static readonly FieldInfo ModelSequenceField = DeliveriesEditModelType?.GetField("sequence", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
		private static readonly FieldInfo ModelCurrentField = DeliveriesEditModelType?.GetField("current", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
		private static readonly FieldInfo ActionTypeField = DeliveriesEditActionType?.GetField("type", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
		private static readonly FieldInfo CashOrResResourceField = DeliveriesEditCashOrResType?.GetField("res", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
		private static readonly FieldInfo DropdownItemTextField = DeliveriesEditModelType?.GetNestedType("DropdownItem", BindingFlags.Public | BindingFlags.NonPublic)?.GetField("text", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
		private static readonly MethodInfo RegenerateModelMethod = DeliveriesEditModelType?.GetMethod("RegenerateModel", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
		private static readonly MethodInfo RefreshViewMethod = AccessTools.Method(typeof(DeliveriesEditView), "RefreshView");
		private static readonly Dictionary<int, string> DeliveryResourceFilterByViewId = new Dictionary<int, string>();
		private static readonly Dictionary<int, string> DeliveryResourceFilterByModelId = new Dictionary<int, string>();

		private struct FrontVisitState
		{
			public bool Eligible;
			public int RouteId;
			public EntityID TriggerFrontId;
			public Fixnum CashDelta;
		}

		public static void ApplyPatch(Harmony harmony)
		{
			try
			{
				MethodInfo refreshCheckbox = AccessTools.Method(typeof(DeliveriesEditView), "RefreshCheckbox");
				MethodInfo checkboxChanged = AccessTools.Method(typeof(DeliveriesEditView), "OnCheckboxChanged");
				MethodInfo initializeEdit = AccessTools.Method(typeof(DeliveriesEditView), "InitializeEdit");
				MethodInfo startEdit = AccessTools.Method(typeof(DeliveriesEditView), "StartEdit");
				MethodInfo releaseEdit = AccessTools.Method(typeof(DeliveriesEditView), "ReleaseEdit");
				MethodInfo makeUnlockedItems = DeliveriesEditModelType == null ? null : AccessTools.Method(DeliveriesEditModelType, "MakeUnlockedItemsForAction");
				MethodInfo doFrontVisit = AccessTools.Method(typeof(AutomationExecutor), "DoFrontVisit");
				MethodInfo destroySequence = AccessTools.Method(typeof(AutomationExecutor), nameof(AutomationExecutor.DestroyAutomationSequence));
				MethodInfo removeStep = AccessTools.Method(typeof(AutomationExecutor), nameof(AutomationExecutor.RemoveStep));
				MethodInfo replaceStep = AccessTools.Method(typeof(AutomationExecutor), nameof(AutomationExecutor.ReplaceStep));
				MethodInfo removeAllSteps = AccessTools.Method(typeof(AutomationExecutor), nameof(AutomationExecutor.RemoveAllSteps));

				if (refreshCheckbox != null)
				{
					harmony.Patch(refreshCheckbox, postfix: new HarmonyMethod(typeof(DeliveryFrontExpansionPatch), nameof(RefreshCheckboxPostfix)));
				}
				if (checkboxChanged != null)
				{
					harmony.Patch(checkboxChanged, prefix: new HarmonyMethod(typeof(DeliveryFrontExpansionPatch), nameof(OnCheckboxChangedPrefix)));
				}
				if (initializeEdit != null)
				{
					harmony.Patch(initializeEdit, postfix: new HarmonyMethod(typeof(DeliveryFrontExpansionPatch), nameof(InitializeEditPostfix)));
				}
				if (startEdit != null)
				{
					harmony.Patch(startEdit, postfix: new HarmonyMethod(typeof(DeliveryFrontExpansionPatch), nameof(StartEditPostfix)));
				}
				if (releaseEdit != null)
				{
					harmony.Patch(releaseEdit, prefix: new HarmonyMethod(typeof(DeliveryFrontExpansionPatch), nameof(ReleaseEditPrefix)));
				}
				if (makeUnlockedItems != null)
				{
					harmony.Patch(makeUnlockedItems, postfix: new HarmonyMethod(typeof(DeliveryFrontExpansionPatch), nameof(MakeUnlockedItemsForActionPostfix)));
				}
				if (doFrontVisit != null)
				{
					harmony.Patch(doFrontVisit,
						prefix: new HarmonyMethod(typeof(DeliveryFrontExpansionPatch), nameof(DoFrontVisitPrefix)),
						postfix: new HarmonyMethod(typeof(DeliveryFrontExpansionPatch), nameof(DoFrontVisitPostfix)));
				}
				if (destroySequence != null)
				{
					harmony.Patch(destroySequence, prefix: new HarmonyMethod(typeof(DeliveryFrontExpansionPatch), nameof(DestroyAutomationSequencePrefix)));
				}
				if (removeStep != null)
				{
					harmony.Patch(removeStep, postfix: new HarmonyMethod(typeof(DeliveryFrontExpansionPatch), nameof(RouteStepChangedPostfix)));
				}
				if (replaceStep != null)
				{
					harmony.Patch(replaceStep, postfix: new HarmonyMethod(typeof(DeliveryFrontExpansionPatch), nameof(RouteStepChangedPostfix)));
				}
				if (removeAllSteps != null)
				{
					harmony.Patch(removeAllSteps, postfix: new HarmonyMethod(typeof(DeliveryFrontExpansionPatch), nameof(RouteStepChangedPostfix)));
				}

				Debug.Log("[GameplayTweaks] Delivery front expansion route patch applied");
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] DeliveryFrontExpansionPatch.ApplyPatch failed: " + ex.Message);
			}
		}

		private static void InitializeEditPostfix(DeliveriesEditView __instance)
		{
			try
			{
				EnsureDeliveryResourceFilterInput(__instance);
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] DeliveryFrontExpansionPatch.InitializeEditPostfix failed: " + ex.Message);
			}
		}

		private static void StartEditPostfix(DeliveriesEditView __instance)
		{
			try
			{
				object model = GetEditModel(__instance);
				if (model == null)
				{
					return;
				}
				string filter = GetDeliveryResourceFilterForView(__instance);
				SetDeliveryResourceFilterForModel(model, filter);
				InputField input = GetDeliveryResourceFilterInput(__instance);
				if (input != null && !string.Equals(input.text ?? string.Empty, filter ?? string.Empty, StringComparison.Ordinal))
				{
					input.SetTextWithoutNotify(filter ?? string.Empty);
				}
				if (!string.IsNullOrWhiteSpace(filter))
				{
					RegenerateModelMethod?.Invoke(model, new object[] { false, true, false, false });
					RefreshViewMethod?.Invoke(__instance, null);
				}
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] DeliveryFrontExpansionPatch.StartEditPostfix failed: " + ex.Message);
			}
		}

		private static void ReleaseEditPrefix(DeliveriesEditView __instance)
		{
			try
			{
				ClearDeliveryResourceFilterForView(__instance);
				object model = GetEditModel(__instance);
				if (model != null)
				{
					ClearDeliveryResourceFilterForModel(model);
				}
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] DeliveryFrontExpansionPatch.ReleaseEditPrefix failed: " + ex.Message);
			}
		}

		private static void MakeUnlockedItemsForActionPostfix(object __instance, ref object __result)
		{
			try
			{
				string filter = GetDeliveryResourceFilterForModel(__instance);
				if (string.IsNullOrWhiteSpace(filter) || !(__result is IList items))
				{
					return;
				}

				IList filtered = Activator.CreateInstance(__result.GetType()) as IList;
				if (filtered == null)
				{
					return;
				}

				string normalizedFilter = NormalizeDeliveryResourceFilterText(filter);
				foreach (object item in items)
				{
					if (MatchesDeliveryResourceFilter(item, normalizedFilter))
					{
						filtered.Add(item);
					}
				}

				if (filtered.Count == 0)
				{
					VerificationLog("DeliveryResourceFilter", $"filter=\"{filter}\" before={items.Count} after=0 outcome=no-match-kept-unfiltered");
					return;
				}

				__result = filtered;
				VerificationLog("DeliveryResourceFilter", $"filter=\"{filter}\" before={items.Count} after={filtered.Count}");
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] DeliveryFrontExpansionPatch.MakeUnlockedItemsForActionPostfix failed: " + ex.Message);
			}
		}

		private static void RefreshCheckboxPostfix(object __instance)
		{
			try
			{
				object model = GetEditModel(__instance);
				if (model == null || GetSelectedAction(model) != AutoAction.FrontVisit)
				{
					return;
				}

				GameObject go = DeliveriesEditViewGoField?.GetValue(__instance) as GameObject;
				Toggle toggle = go?.GetToggle("Edit Panel/Dest/Checkbox");
				if (toggle == null)
				{
					return;
				}

				AutomationSequence sequence = ModelSequenceField?.GetValue(model) as AutomationSequence;
				AutomationStep current = ModelCurrentField?.GetValue(model) as AutomationStep;
				EntityID frontId = current?.target ?? EntityID.INVALID;
				bool enabled = IsFrontRouteExpansionEnabled(sequence, frontId);

				toggle.gameObject.SetActive(true);
				go.SetText("Edit Panel/Dest/Checkbox/Text", ExpandFrontsLabel);
				TextMouseoverContext mouseover = go.GetChild("Edit Panel/Dest/Checkbox")?.GetComponentInChildren<TextMouseoverContext>();
				if (mouseover != null)
				{
					mouseover.lockey = ExpandFrontsMouseoverKey;
				}
				toggle.SetIsOnWithoutNotify(enabled);
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] DeliveryFrontExpansionPatch.RefreshCheckboxPostfix failed: " + ex.Message);
			}
		}

		private static bool OnCheckboxChangedPrefix(object __instance, bool val)
		{
			try
			{
				object model = GetEditModel(__instance);
				if (model == null || GetSelectedAction(model) != AutoAction.FrontVisit)
				{
					return true;
				}

				AutomationSequence sequence = ModelSequenceField?.GetValue(model) as AutomationSequence;
				AutomationStep current = ModelCurrentField?.GetValue(model) as AutomationStep;
				SetFrontRouteExpansionEnabled(sequence, current?.target ?? EntityID.INVALID, val, "delivery-editor");
				return false;
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] DeliveryFrontExpansionPatch.OnCheckboxChangedPrefix failed: " + ex.Message);
				return true;
			}
		}

		private static void DoFrontVisitPrefix(AutomationExecutor __instance, AutomationStep step, CrewAssignment crew, Entity building, out FrontVisitState __state)
		{
			__state = default(FrontVisitState);
			try
			{
				if (step == null || step.action != AutoAction.FrontVisit || building == null)
				{
					return;
				}
				PlayerInfo player = ResolveAutomationPlayer(__instance, crew);
				if (player?.outposts == null || !player.PID.IsHumanPlayer)
				{
					VerificationLog("FrontRouteExpand", $"skipped=prefix-no-player front={building.Id.id}");
					return;
				}
				AutomationSequence sequence = ResolveAutomationSequence(__instance, crew);
				if (sequence == null || sequence.id.IsNotValid)
				{
					VerificationLog("FrontRouteExpand", $"skipped=prefix-no-sequence front={building.Id.id}");
					return;
				}
				if (!IsFrontRouteExpansionEnabled(sequence, building.Id))
				{
					return;
				}
				OutpostID outpostId = new OutpostID(building);
				MoneyStatus moneyStatus = player.outposts.FindOutpostCollectionStatus(outpostId);
				Fixnum cashDelta = moneyStatus?.Delta ?? Fixnum.ZERO;
				bool hasPositiveCollection = moneyStatus != null && moneyStatus.NeedsCollect && cashDelta > 0;
				__state = new FrontVisitState
				{
					Eligible = true,
					RouteId = sequence.id.id,
					TriggerFrontId = building.Id,
					CashDelta = cashDelta
				};
				VerificationLog("FrontRouteExpand", $"route={sequence.id.id} trigger={building.Id.id} armed cash={cashDelta} collection={hasPositiveCollection}");
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] DeliveryFrontExpansionPatch.DoFrontVisitPrefix failed: " + ex.Message);
			}
		}

		private static void DoFrontVisitPostfix(AutomationExecutor __instance, AutomationStep step, CrewAssignment crew, Entity building, FrontVisitState __state)
		{
			try
			{
				if (!__state.Eligible)
				{
					return;
				}
				if (building == null)
				{
					VerificationLog("FrontRouteExpand", $"route={__state.RouteId} trigger={__state.TriggerFrontId.id} skipped=postfix-no-building");
					return;
				}
				PlayerInfo player = ResolveAutomationPlayer(__instance, crew);
				AutomationSequence sequence = ResolveAutomationSequence(__instance, crew);
				if (player?.outposts == null || sequence == null || sequence.id.id != __state.RouteId)
				{
					VerificationLog("FrontRouteExpand", $"route={__state.RouteId} trigger={__state.TriggerFrontId.id} skipped=postfix-context player={player != null} sequence={sequence?.id.id ?? 0}");
					return;
				}
				MoneyStatus afterStatus = player.outposts.FindOutpostCollectionStatus(new OutpostID(building));
				if (__state.CashDelta > 0 && afterStatus != null && afterStatus.NeedsCollect)
				{
					VerificationLog("FrontRouteExpand", $"route={__state.RouteId} trigger={__state.TriggerFrontId.id} skipped=collection-not-final cash={__state.CashDelta}");
					return;
				}
				RunRouteFrontExpansion(player, sequence, crew, __state.TriggerFrontId, __state.CashDelta);
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] DeliveryFrontExpansionPatch.DoFrontVisitPostfix failed: " + ex.Message);
			}
		}

		private static void DestroyAutomationSequencePrefix(AutomationID id)
		{
			RemoveFrontRouteExpansionKeysForRoute(id.id, "route-destroyed");
		}

		private static void RouteStepChangedPostfix(AutomationID id)
		{
			NormalizeFrontRouteExpansionKeysForRoute(id.id, "route-step-changed");
		}

		private static void RunRouteFrontExpansion(PlayerInfo player, AutomationSequence sequence, CrewAssignment crew, EntityID triggerFrontId, Fixnum collectedCash)
		{
			if (player?.outposts == null || sequence == null || !sequence.id.IsValid)
			{
				return;
			}
			NormalizeFrontRouteExpansionKeysForRoute(sequence.id.id, "route-execute");

			int selectedCount = 0;
			int expandedCount = 0;
			int skippedNoCorner = 0;
			int skippedNoMoney = 0;
			int skippedOther = 0;
			int xpAwarded = 0;
			HashSet<ulong> seenFronts = new HashSet<ulong>();

			foreach (AutomationStep routeStep in sequence.steps)
			{
				if (routeStep == null || routeStep.action != AutoAction.FrontVisit || !routeStep.target.IsValid)
				{
					continue;
				}
				if (!seenFronts.Add(routeStep.target.id) || !IsFrontRouteExpansionEnabled(sequence, routeStep.target))
				{
					continue;
				}

				selectedCount++;
				Entity frontBuilding = routeStep.target.FindEntity();
				if (frontBuilding == null)
				{
					skippedOther++;
					continue;
				}

				OutpostID outpostId = new OutpostID(frontBuilding);
				if (!player.outposts.CanStartNewOutpostExpansion(outpostId))
				{
					skippedNoCorner++;
					continue;
				}

				BuildingAndBusinessData bbdata = BuildingUtil.FindDataForBuilding(frontBuilding);
				VisitState visit = new VisitState(crew, bbdata, global::Game.Game.ctx.clock.Now, player.PID);
				var expansionChoice = player.outposts.PickBestExpansion(outpostId, visit);
				OutpostSettings.ExpansionDef expansion = expansionChoice.exp;
				if (expansion == null)
				{
					skippedNoCorner++;
					continue;
				}

				Price expansionCost = new Price(expansion.monthlyCost.Evaluate(visit.MakeOwnerModQuery()).RoundCoarse());
				if (!player.finances.CanChangeMoneyOnCrew(visit, expansionCost))
				{
					skippedNoMoney++;
					continue;
				}

				player.finances.DoChangeMoneyOnCrew(visit, expansionCost, MoneyReason.FrontMaintenance, frontBuilding.Id);
				if (player.outposts.DoStartNewOutpostExpansion(outpostId, expansion))
				{
					crew.GetPeep()?.components?.agent?.AddXP(XPSource.FromSocial);
					expandedCount++;
					xpAwarded++;
				}
				else
				{
					skippedOther++;
				}
			}

			if (selectedCount > 0 || expandedCount > 0 || skippedNoCorner > 0 || skippedNoMoney > 0 || skippedOther > 0)
			{
				VerificationLog("FrontRouteExpand", $"route={sequence.id.id} trigger={triggerFrontId.id} collected={collectedCash} selected={selectedCount} expanded={expandedCount} skippedNoCorner={skippedNoCorner} skippedNoMoney={skippedNoMoney} skippedOther={skippedOther} xpAwarded={xpAwarded}");
			}
		}

		private static void EnsureDeliveryResourceFilterInput(DeliveriesEditView editView)
		{
			GameObject go = DeliveriesEditViewGoField?.GetValue(editView) as GameObject;
			GameObject itemRow = go?.GetChild("Edit Panel/Item");
			GameObject filterHost = go?.GetChild("Edit Panel/Item/Text") ?? itemRow;
			if (itemRow == null || filterHost == null)
			{
				return;
			}

			if (filterHost.transform.Find(ResourceFilterInputName) != null)
			{
				return;
			}

			GameObject inputGo = new GameObject(ResourceFilterInputName, typeof(RectTransform), typeof(Image), typeof(InputField));
			inputGo.transform.SetParent(filterHost.transform, false);
			RectTransform inputRect = inputGo.GetComponent<RectTransform>();
			inputRect.anchorMin = Vector2.zero;
			inputRect.anchorMax = Vector2.one;
			inputRect.pivot = new Vector2(0.5f, 0.5f);
			inputRect.offsetMin = new Vector2(0f, 1f);
			inputRect.offsetMax = new Vector2(-2f, -1f);

			Image image = inputGo.GetComponent<Image>();
			image.color = new Color(0.06f, 0.07f, 0.08f, 0.88f);

			Text text = CreateDeliveryFilterText(inputGo.transform, "Text", string.Empty, Color.white);
			Text placeholder = CreateDeliveryFilterText(inputGo.transform, "Placeholder", ResourceFilterPlaceholder, new Color(0.78f, 0.78f, 0.78f, 0.76f));

			InputField input = inputGo.GetComponent<InputField>();
			input.textComponent = text;
			input.placeholder = placeholder;
			input.lineType = InputField.LineType.SingleLine;
			input.characterLimit = 32;
			input.onValueChanged.AddListener(value => OnDeliveryResourceFilterChanged(editView, value));
			inputGo.AddComponent<InputFieldBlocker>();
		}

		private static Text CreateDeliveryFilterText(Transform parent, string name, string value, Color color)
		{
			GameObject textGo = new GameObject(name, typeof(RectTransform), typeof(Text));
			textGo.transform.SetParent(parent, false);
			RectTransform rect = textGo.GetComponent<RectTransform>();
			rect.anchorMin = Vector2.zero;
			rect.anchorMax = Vector2.one;
			rect.offsetMin = new Vector2(6f, 1f);
			rect.offsetMax = new Vector2(-4f, -1f);

			Text text = textGo.GetComponent<Text>();
			text.text = value;
			text.color = color;
			text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
			text.fontSize = 10;
			text.alignment = TextAnchor.MiddleLeft;
			text.horizontalOverflow = HorizontalWrapMode.Wrap;
			text.verticalOverflow = VerticalWrapMode.Truncate;
			return text;
		}

		private static void OnDeliveryResourceFilterChanged(DeliveriesEditView editView, string value)
		{
			try
			{
				object model = GetEditModel(editView);
				if (model == null)
				{
					return;
				}

				string filter = value ?? string.Empty;
				SetDeliveryResourceFilterForView(editView, filter);
				SetDeliveryResourceFilterForModel(model, filter);
				RegenerateModelMethod?.Invoke(model, new object[] { false, true, false, false });
				RefreshViewMethod?.Invoke(editView, null);
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] DeliveryFrontExpansionPatch.OnDeliveryResourceFilterChanged failed: " + ex.Message);
			}
		}

		private static InputField GetDeliveryResourceFilterInput(DeliveriesEditView editView)
		{
			GameObject go = DeliveriesEditViewGoField?.GetValue(editView) as GameObject;
			return (go?.GetChild("Edit Panel/Item/Text")?.transform.Find(ResourceFilterInputName)
				?? go?.GetChild("Edit Panel/Item")?.transform.Find(ResourceFilterInputName))?.GetComponent<InputField>();
		}

		private static bool MatchesDeliveryResourceFilter(object item, string normalizedFilter)
		{
			if (item == null || string.IsNullOrWhiteSpace(normalizedFilter))
			{
				return true;
			}

			Resource resource = CashOrResResourceField?.GetValue(item) as Resource;
			string displayText = DropdownItemTextField?.GetValue(item) as string;
			if (ContainsNormalized(displayText, normalizedFilter))
			{
				return true;
			}
			if (resource == null)
			{
				return ContainsNormalized(Loc.Get("ui.deliveries.cash.mo"), normalizedFilter) || ContainsNormalized("cash", normalizedFilter);
			}
			return ContainsNormalized(resource.GetName(), normalizedFilter)
				|| ContainsNormalized(resource.GetListName(), normalizedFilter)
				|| ContainsNormalized(resource.ToString(), normalizedFilter);
		}

		private static bool ContainsNormalized(string text, string normalizedFilter)
		{
			if (string.IsNullOrWhiteSpace(text))
			{
				return false;
			}
			return NormalizeDeliveryResourceFilterText(text).Contains(normalizedFilter);
		}

		private static string NormalizeDeliveryResourceFilterText(string text)
		{
			if (string.IsNullOrWhiteSpace(text))
			{
				return string.Empty;
			}

			string value = StripRichTextTags(text).ToLowerInvariant().Trim();
			return value;
		}

		private static string StripRichTextTags(string text)
		{
			if (string.IsNullOrEmpty(text) || text.IndexOf('<') < 0)
			{
				return text ?? string.Empty;
			}

			char[] buffer = new char[text.Length];
			int count = 0;
			bool inTag = false;
			foreach (char c in text)
			{
				if (c == '<')
				{
					inTag = true;
					continue;
				}
				if (c == '>')
				{
					inTag = false;
					continue;
				}
				if (!inTag)
				{
					buffer[count++] = c;
				}
			}
			return new string(buffer, 0, count);
		}

		private static void SetDeliveryResourceFilterForView(DeliveriesEditView editView, string filter)
		{
			int key = GetObjectKey(editView);
			if (key == 0)
			{
				return;
			}
			if (string.IsNullOrWhiteSpace(filter))
			{
				DeliveryResourceFilterByViewId.Remove(key);
			}
			else
			{
				DeliveryResourceFilterByViewId[key] = filter.Trim();
			}
		}

		private static string GetDeliveryResourceFilterForView(DeliveriesEditView editView)
		{
			int key = GetObjectKey(editView);
			return key != 0 && DeliveryResourceFilterByViewId.TryGetValue(key, out string filter) ? filter : string.Empty;
		}

		private static void ClearDeliveryResourceFilterForView(DeliveriesEditView editView)
		{
			int key = GetObjectKey(editView);
			if (key != 0)
			{
				DeliveryResourceFilterByViewId.Remove(key);
			}
		}

		private static void SetDeliveryResourceFilterForModel(object model, string filter)
		{
			int key = GetObjectKey(model);
			if (key == 0)
			{
				return;
			}
			if (string.IsNullOrWhiteSpace(filter))
			{
				DeliveryResourceFilterByModelId.Remove(key);
			}
			else
			{
				DeliveryResourceFilterByModelId[key] = filter.Trim();
			}
		}

		private static string GetDeliveryResourceFilterForModel(object model)
		{
			int key = GetObjectKey(model);
			return key != 0 && DeliveryResourceFilterByModelId.TryGetValue(key, out string filter) ? filter : string.Empty;
		}

		private static void ClearDeliveryResourceFilterForModel(object model)
		{
			int key = GetObjectKey(model);
			if (key != 0)
			{
				DeliveryResourceFilterByModelId.Remove(key);
			}
		}

		private static int GetObjectKey(object value)
		{
			return value == null ? 0 : RuntimeHelpers.GetHashCode(value);
		}

		private static PlayerInfo ResolveAutomationPlayer(AutomationExecutor executor, CrewAssignment crew)
		{
			try
			{
				PlayerInfo player = AutomationExecutorPlayerField?.GetValue(executor) as PlayerInfo;
				if (player != null)
				{
					return player;
				}
				return crew.GetPeep()?.data?.agent?.pid.FindPlayer();
			}
			catch
			{
				return null;
			}
		}

		private static AutomationSequence ResolveAutomationSequence(AutomationExecutor executor, CrewAssignment crew)
		{
			try
			{
				return executor?.GetAutoOrNull(crew);
			}
			catch
			{
				return null;
			}
		}

		private static bool HasAnyFrontRouteExpansionEnabled(AutomationSequence sequence)
		{
			if (sequence?.steps == null || SaveData?.FrontRouteExpansionKeys == null)
			{
				return false;
			}
			foreach (AutomationStep routeStep in sequence.steps)
			{
				if (routeStep?.action == AutoAction.FrontVisit
					&& routeStep.target.IsValid
					&& IsFrontRouteExpansionEnabled(sequence, routeStep.target))
				{
					return true;
				}
			}
			return false;
		}

		private static object GetEditModel(object editView)
		{
			return editView == null ? null : DeliveriesEditViewModelField?.GetValue(editView);
		}

		private static AutoAction GetSelectedAction(object model)
		{
			object actions = ModelActionsField?.GetValue(model);
			object selectedAction = actions == null ? null : actions.GetType().GetProperty("selected", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(actions, null);
			if (selectedAction == null || ActionTypeField == null)
			{
				return AutoAction.None;
			}
			return (AutoAction)ActionTypeField.GetValue(selectedAction);
		}

		private static bool IsFrontRouteExpansionEnabled(AutomationSequence sequence, EntityID frontId)
		{
			if (sequence == null || !sequence.id.IsValid || !frontId.IsValid || SaveData?.FrontRouteExpansionKeys == null)
			{
				return false;
			}
			return SaveData.FrontRouteExpansionKeys.Contains(MakeFrontRouteExpansionKey(sequence.id.id, frontId));
		}

		private static void SetFrontRouteExpansionEnabled(AutomationSequence sequence, EntityID frontId, bool enabled, string source)
		{
			if (sequence == null || !sequence.id.IsValid || !frontId.IsValid)
			{
				return;
			}
			if (SaveData == null)
			{
				SaveData = new ModSaveData();
			}
			if (SaveData.FrontRouteExpansionKeys == null)
			{
				SaveData.FrontRouteExpansionKeys = new List<string>();
			}
			string key = MakeFrontRouteExpansionKey(sequence.id.id, frontId);
			bool changed;
			if (enabled)
			{
				changed = !SaveData.FrontRouteExpansionKeys.Contains(key);
				if (changed)
				{
					SaveData.FrontRouteExpansionKeys.Add(key);
				}
			}
			else
			{
				changed = SaveData.FrontRouteExpansionKeys.RemoveAll(item => string.Equals(item, key, StringComparison.Ordinal)) > 0;
			}
			if (changed)
			{
				VerificationLog("FrontRouteExpand", $"toggle route={sequence.id.id} front={frontId.id} enabled={enabled} source={source}");
			}
		}

		private static void RemoveFrontRouteExpansionKeysForRoute(int routeId, string source)
		{
			if (routeId <= 0 || SaveData?.FrontRouteExpansionKeys == null)
			{
				return;
			}
			int removed = SaveData.FrontRouteExpansionKeys.RemoveAll(key => TryParseFrontRouteExpansionKey(key, out int parsedRoute, out _) && parsedRoute == routeId);
			if (removed > 0)
			{
				VerificationLog("FrontRouteExpand", $"cleanup route={routeId} removed={removed} source={source}");
			}
		}

		private static void NormalizeFrontRouteExpansionKeysForRoute(int routeId, string source)
		{
			if (routeId <= 0 || SaveData?.FrontRouteExpansionKeys == null)
			{
				return;
			}
			AutomationSequence sequence = G.GetHumanPlayer()?.automation?.GetAutoOrNull(new AutomationID { id = routeId });
			if (sequence == null)
			{
				RemoveFrontRouteExpansionKeysForRoute(routeId, source + "-missing-route");
				return;
			}
			HashSet<ulong> validFrontIds = new HashSet<ulong>(sequence.steps
				.Where(step => step != null && step.action == AutoAction.FrontVisit && step.target.IsValid)
				.Select(step => step.target.id));
			int removed = SaveData.FrontRouteExpansionKeys.RemoveAll(key =>
				TryParseFrontRouteExpansionKey(key, out int parsedRoute, out ulong parsedFront)
				&& parsedRoute == routeId
				&& !validFrontIds.Contains(parsedFront));
			if (removed > 0)
			{
				VerificationLog("FrontRouteExpand", $"cleanup route={routeId} removed={removed} source={source}");
			}
		}

		private static string MakeFrontRouteExpansionKey(int routeId, EntityID frontId)
		{
			return routeId.ToString(CultureInfo.InvariantCulture) + ":" + frontId.id.ToString(CultureInfo.InvariantCulture);
		}

		private static bool TryParseFrontRouteExpansionKey(string key, out int routeId, out ulong frontId)
		{
			routeId = 0;
			frontId = 0UL;
			if (string.IsNullOrWhiteSpace(key))
			{
				return false;
			}
			string[] parts = key.Split(':');
			if (parts.Length != 2)
			{
				return false;
			}
			return int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out routeId)
				&& ulong.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out frontId);
		}
	}
}
}
