using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Game.Services;
using Game.UI;
using HarmonyLib;
using UnityEngine;
using GameRoot = Game.Game;

namespace AfterProhibitionUI
{
	internal static class PopupDockingPatch
	{
		private const float EdgePadding = 8f;
		private const float OwnedBizPopupMaxReservedLeftWidth = 640f;
		private const float OwnedBizPopupReservedLeftPercent = 0.42f;
		private const float OwnedBizPopupMinimumContentWidth = 800f;
		private const float OwnedBizPopupPanelGap = 18f;
		private static readonly HashSet<string> AdjustedTypesLogged = new HashSet<string>();
		private static readonly HashSet<int> QueuedInstanceIds = new HashSet<int>();
		private static readonly HashSet<string> CenteredOversizedLogged = new HashSet<string>();
		private static readonly HashSet<string> OwnedBizZoneLogged = new HashSet<string>();
		private static readonly HashSet<int> RightHudHiddenOwners = new HashSet<int>();
		private static CanvasGroupSnapshot RightHudSnapshot;

		private sealed class CanvasGroupSnapshot
		{
			public CanvasGroup Group;
			public float Alpha;
			public bool Interactable;
			public bool BlocksRaycasts;
			public bool AddedGroup;
		}

		public static int ApplyPatch(Harmony harmony)
		{
			int patched = 0;
			try
			{
				MethodInfo popupActivated = AccessTools.Method(typeof(BasePopup), "OnActivated");
				if (popupActivated != null)
				{
					harmony.Patch(popupActivated, postfix: new HarmonyMethod(typeof(PopupDockingPatch), nameof(BasePopupOnActivatedPostfix)));
					patched++;
				}
				MethodInfo popupDeactivated = AccessTools.Method(typeof(BasePopup), "OnDeactivated");
				if (popupDeactivated != null)
				{
					harmony.Patch(popupDeactivated, postfix: new HarmonyMethod(typeof(PopupDockingPatch), nameof(BasePopupOnDeactivatedPostfix)));
					patched++;
				}
				MethodInfo popupPopped = AccessTools.Method(typeof(BasePopup), "OnPopped");
				if (popupPopped != null)
				{
					harmony.Patch(popupPopped, postfix: new HarmonyMethod(typeof(PopupDockingPatch), nameof(BasePopupOnPoppedPostfix)));
					patched++;
				}

				AfterProhibitionUIPlugin.Log?.LogInfo("PopupDocking patch applied basePopup=" + (popupActivated != null) + " deactivate=" + (popupDeactivated != null) + " popped=" + (popupPopped != null) + " hudDialog=False");
			}
			catch (Exception ex)
			{
				AfterProhibitionUIPlugin.Log?.LogWarning("PopupDockingPatch failed: " + ex.Message);
			}

			return patched;
		}

		private static void BasePopupOnActivatedPostfix(object __instance)
		{
			HideRightHudForOwnedBizPopup(__instance);
			QueueDock(__instance, "popup");
		}

		private static void BasePopupOnDeactivatedPostfix(object __instance)
		{
			if (__instance != null)
			{
				QueuedInstanceIds.Remove(__instance.GetHashCode());
			}
			RestoreRightHudForOwnedBizPopup(__instance);
		}

		private static void BasePopupOnPoppedPostfix(object __instance)
		{
			RestoreRightHudForOwnedBizPopup(__instance);
		}

		private static void QueueDock(object instance, string source)
		{
			if (instance == null)
			{
				return;
			}

			int instanceId = instance.GetHashCode();
			if (!QueuedInstanceIds.Add(instanceId))
			{
				return;
			}

			AfterProhibitionUIPlugin.StartDeferredWork(DockNextFrame(instance, source));
		}

		private static IEnumerator DockNextFrame(object instance, string source)
		{
			yield return null;
			DockInstance(instance, source);
			yield return new WaitForSecondsRealtime(0.15f);
			DockInstance(instance, source + "-settled");
			if (IsOwnedBizAddModulePopup(instance.GetType().FullName ?? string.Empty))
			{
				yield return new WaitForSecondsRealtime(0.35f);
				DockInstance(instance, source + "-late");
			}

			if (instance != null)
			{
				QueuedInstanceIds.Remove(instance.GetHashCode());
			}
		}

		private static void DockInstance(object instance, string source)
		{
			try
			{
				GameObject root = ResolveGameObject(instance);
				RectTransform rect = ResolveDockRect(root);
				if (rect == null || rect.parent == null)
				{
					return;
				}

				string instanceName = instance.GetType().FullName ?? string.Empty;
				if (IsMainMenuPopup(instanceName))
				{
					return;
				}

				if (KeepRectInsideParent(rect, instanceName) && AdjustedTypesLogged.Add(instanceName + ":" + source))
				{
					AfterProhibitionUIPlugin.Log?.LogInfo("PopupDocking adjusted type=" + instanceName + " source=" + source);
				}
			}
			catch
			{
			}
		}

		private static GameObject ResolveGameObject(object instance)
		{
			try
			{
				if (instance is BasePopup popup)
				{
					return popup.GameObject;
				}
			}
			catch
			{
			}

			return GetFieldValue<GameObject>(instance, "_go");
		}

		private static RectTransform ResolveDockRect(GameObject root)
		{
			if (root == null)
			{
				return null;
			}

			Transform panel = root.transform.Find("Panel");
			if (panel is RectTransform panelRect)
			{
				return panelRect;
			}

			return root.GetComponent<RectTransform>();
		}

		private static bool KeepRectInsideParent(RectTransform rect, string instanceName)
		{
			RectTransform parentRect = rect.parent as RectTransform;
			if (parentRect == null)
			{
				return false;
			}

			Rect parent = parentRect.rect;
			float minX = parent.xMin + EdgePadding;
			float maxX = parent.xMax - EdgePadding;
			float minY = parent.yMin + EdgePadding;
			float maxY = parent.yMax - EdgePadding;
			float fullMinX = minX;
			float fullMaxX = maxX;
			bool isOwnedBizAddModulePopup = IsOwnedBizAddModulePopup(instanceName);
			if (isOwnedBizAddModulePopup)
			{
				float reservedLeft = Mathf.Min(OwnedBizPopupMaxReservedLeftWidth, parent.width * OwnedBizPopupReservedLeftPercent);
				float detectedPanelRight = 0f;
				bool detectedPanel = TryFindOwnedBizPanelRightEdge(parentRect, parent, out detectedPanelRight);
				if (detectedPanel)
				{
					reservedLeft = Mathf.Max(reservedLeft, detectedPanelRight - parent.xMin + OwnedBizPopupPanelGap);
				}

				float maxReservedLeft = Mathf.Max(0f, parent.width - OwnedBizPopupMinimumContentWidth - (EdgePadding * 2f));
				if (maxReservedLeft > 0f)
				{
					reservedLeft = Mathf.Min(reservedLeft, maxReservedLeft);
				}

				float ownedBizMinX = parent.xMin + reservedLeft + EdgePadding;
				if (ownedBizMinX < maxX)
				{
					minX = Mathf.Max(minX, ownedBizMinX);
					if (OwnedBizZoneLogged.Add(instanceName))
					{
						AfterProhibitionUIPlugin.Log?.LogInfo("PopupDocking owned-biz content-zone type=" + instanceName + " reservedLeft=" + reservedLeft + " detectedPanel=" + detectedPanel + " detectedRight=" + detectedPanelRight + " parentWidth=" + parent.width);
					}
				}

			}

			Bounds bounds = CalculateSelfBounds(parentRect, rect);
			float availableWidth = maxX - minX;
			float availableHeight = maxY - minY;
			Vector2 delta = Vector2.zero;
			if (bounds.size.x > availableWidth)
			{
				if (isOwnedBizAddModulePopup && bounds.size.x <= fullMaxX - fullMinX)
				{
					delta.x += maxX - bounds.max.x;
				}
				else
				{
					delta.x += ((minX + maxX) * 0.5f) - bounds.center.x;
				}
			}
			else if (bounds.min.x < minX)
			{
				delta.x += minX - bounds.min.x;
			}
			else if (bounds.max.x > maxX)
			{
				delta.x -= bounds.max.x - maxX;
			}

			if (bounds.size.y > availableHeight)
			{
				delta.y += ((minY + maxY) * 0.5f) - bounds.center.y;
				LogOversizedAxis(rect, "y");
			}
			else if (bounds.min.y < minY)
			{
				delta.y += minY - bounds.min.y;
			}
			else if (bounds.max.y > maxY)
			{
				delta.y -= bounds.max.y - maxY;
			}

			if (Mathf.Abs(delta.x) < 0.5f && Mathf.Abs(delta.y) < 0.5f)
			{
				return false;
			}

			rect.anchoredPosition += delta;
			if (bounds.size.x > availableWidth)
			{
				LogOversizedAxis(rect, isOwnedBizAddModulePopup ? "x-ownedbiz" : "x");
			}
			return true;
		}

		private static bool IsOwnedBizAddModulePopup(string instanceName)
		{
			return !string.IsNullOrEmpty(instanceName) && instanceName.IndexOf("OwnedBizAddModulePopup", StringComparison.OrdinalIgnoreCase) >= 0;
		}

		private static bool IsMainMenuPopup(string instanceName)
		{
			return !string.IsNullOrEmpty(instanceName) && instanceName.IndexOf("MainMenuPopup", StringComparison.OrdinalIgnoreCase) >= 0;
		}

		private static bool TryFindOwnedBizPanelRightEdge(RectTransform referenceParent, Rect parent, out float rightEdge)
		{
			rightEdge = 0f;
			try
			{
				if (GameRoot.serv == null || GameRoot.serv.ui == null)
				{
					return false;
				}

				GameObject ownedBiz = GameRoot.serv.ui.GetUI(UIElements.HUDOwnedBiz);
				if (ownedBiz == null || !ownedBiz.activeInHierarchy)
				{
					return false;
				}

				RectTransform ownedBizPanel = null;
				Transform panel = ownedBiz.transform.Find("Panel");
				if (panel is RectTransform panelRect)
				{
					ownedBizPanel = panelRect;
				}
				else
				{
					ownedBizPanel = ownedBiz.GetComponent<RectTransform>();
				}

				if (ownedBizPanel == null || !ownedBizPanel.gameObject.activeInHierarchy)
				{
					return false;
				}

				Bounds bounds = CalculateSelfBounds(referenceParent, ownedBizPanel);
				if (bounds.size.x < 220f || bounds.size.y < parent.height * 0.35f)
				{
					return false;
				}

				float leftHalfLimit = parent.xMin + parent.width * 0.58f;
				if (bounds.min.x > leftHalfLimit || bounds.max.x <= parent.xMin)
				{
					return false;
				}

				float maxAllowedRight = parent.xMax - OwnedBizPopupMinimumContentWidth - EdgePadding;
				rightEdge = Mathf.Clamp(bounds.max.x, parent.xMin + 260f, maxAllowedRight);
				return rightEdge > parent.xMin;
			}
			catch
			{
				rightEdge = 0f;
				return false;
			}
		}

		private static void HideRightHudForOwnedBizPopup(object instance)
		{
			try
			{
				if (instance == null || !IsOwnedBizAddModulePopup(instance.GetType().FullName ?? string.Empty))
				{
					return;
				}

				int instanceId = instance.GetHashCode();
				if (!RightHudHiddenOwners.Add(instanceId))
				{
					return;
				}

				GameObject rightHud = TryGetRightHud();
				if (rightHud == null)
				{
					RightHudHiddenOwners.Remove(instanceId);
					return;
				}

				CanvasGroup group = rightHud.GetComponent<CanvasGroup>();
				bool addedGroup = false;
				if (group == null)
				{
					group = rightHud.AddComponent<CanvasGroup>();
					addedGroup = true;
				}

				if (RightHudSnapshot == null)
				{
					RightHudSnapshot = new CanvasGroupSnapshot
					{
						Group = group,
						Alpha = group.alpha,
						Interactable = group.interactable,
						BlocksRaycasts = group.blocksRaycasts,
						AddedGroup = addedGroup
					};
				}

				group.alpha = 0f;
				group.interactable = false;
				group.blocksRaycasts = false;

				if (OwnedBizZoneLogged.Add("right-hud-hidden"))
				{
					AfterProhibitionUIPlugin.Log?.LogInfo("PopupDocking right-hud hidden type=" + instance.GetType().FullName + " owner=AfterProhibitionUI");
				}
			}
			catch
			{
			}
		}

		private static void RestoreRightHudForOwnedBizPopup(object instance)
		{
			try
			{
				if (instance == null || !IsOwnedBizAddModulePopup(instance.GetType().FullName ?? string.Empty))
				{
					return;
				}

				RightHudHiddenOwners.Remove(instance.GetHashCode());
				if (RightHudHiddenOwners.Count > 0 || RightHudSnapshot == null)
				{
					return;
				}

				CanvasGroup group = RightHudSnapshot.Group;
				if (group != null)
				{
					group.alpha = RightHudSnapshot.Alpha;
					group.interactable = RightHudSnapshot.Interactable;
					group.blocksRaycasts = RightHudSnapshot.BlocksRaycasts;
					if (RightHudSnapshot.AddedGroup)
					{
						UnityEngine.Object.Destroy(group);
					}
				}

				RightHudSnapshot = null;
			}
			catch
			{
				RightHudSnapshot = null;
			}
		}

		private static GameObject TryGetRightHud()
		{
			try
			{
				if (GameRoot.serv == null || GameRoot.serv.ui == null)
				{
					return null;
				}

				GameObject crewDialog = GameRoot.serv.ui.GetUI(UIElements.CrewDialog);
				if (crewDialog == null || !crewDialog.activeInHierarchy)
				{
					return null;
				}

				return crewDialog;
			}
			catch
			{
				return null;
			}
		}

		private static Bounds CalculateSelfBounds(RectTransform relativeTo, RectTransform rect)
		{
			var corners = new Vector3[4];
			rect.GetWorldCorners(corners);
			Vector3 first = relativeTo.InverseTransformPoint(corners[0]);
			Bounds bounds = new Bounds(first, Vector3.zero);
			for (int i = 1; i < corners.Length; i++)
			{
				bounds.Encapsulate(relativeTo.InverseTransformPoint(corners[i]));
			}

			return bounds;
		}

		private static void LogOversizedAxis(RectTransform rect, string axis)
		{
			string key = rect.GetInstanceID() + ":" + axis;
			if (CenteredOversizedLogged.Add(key))
			{
				AfterProhibitionUIPlugin.Log?.LogInfo("PopupDocking adjusted oversized axis=" + axis + " rect=" + rect.name + " owner=AfterProhibitionUI");
			}
		}

		private static T GetFieldValue<T>(object instance, string fieldName) where T : class
		{
			Type type = instance.GetType();
			while (type != null)
			{
				FieldInfo field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				if (field != null)
				{
					return field.GetValue(instance) as T;
				}

				type = type.BaseType;
			}

			return null;
		}
	}
}
