using System;
using System.Collections.Generic;
using System.Reflection;
using Game.Services;
using Game.Session.Entities;
using Game.Session.Player;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace AfterProhibitionUI
{
	internal static class CrewInfoButtonsPatch
	{
		private static readonly HashSet<int> SuccessLoggedPopupIds = new HashSet<int>();
		private static readonly HashSet<string> FailureLogKeys = new HashSet<string>();
		private static readonly HashSet<string> IconLogKeys = new HashSet<string>(StringComparer.Ordinal);
		private static FieldInfo _popupGoField;
		private static FieldInfo _panelGoField;
		private static FieldInfo _peepField;
		private static PropertyInfo _crewMemberProperty;
		private static bool _injectionLogged;

		private static readonly string[] ManagedButtonNames =
		{
			"GT_ModBtn_CrewRel",
			"GT_ModBtn_GangPacts",
			"GT_ModBtn_MyPact",
			"GT_ModBtn_Grapevine",
			"GT_ModBtn_Safebox"
		};

		public static int ApplyPatch(Harmony harmony)
		{
			try
			{
				Type type = typeof(Entity).Assembly.GetType("Game.UI.CrewPeepInspectPopup");
				if (type == null)
				{
					AfterProhibitionUIPlugin.Log?.LogWarning("CrewInfoButtons: popup type not found");
					return 0;
				}

				string[] methodNames = { "InitializeOnPush", "RefreshDialog" };
				int patched = 0;
				foreach (string methodName in methodNames)
				{
					MethodInfo method = type.GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
					if (method == null)
					{
						continue;
					}

					harmony.Patch(method, postfix: new HarmonyMethod(typeof(CrewInfoButtonsPatch), nameof(RefreshPostfix)));
					patched++;
				}

				AfterProhibitionUIPlugin.Log?.LogInfo("CrewInfoButtons inspect patch methods=" + patched);
				return patched;
			}
			catch (Exception ex)
			{
				AfterProhibitionUIPlugin.Log?.LogWarning("CrewInfoButtons patch failed: " + ex.Message);
				return 0;
			}
		}

		private static void RefreshPostfix(object __instance)
		{
			try
			{
				EnsureButtons(__instance);
			}
			catch (Exception ex)
			{
				LogInjectFailure(__instance, "refresh-postfix-exception", ex);
			}
		}

		private static void EnsureButtons(object popupInstance)
		{
			GameObject popupRoot = ResolvePopupRoot(popupInstance);
			if (popupRoot == null)
			{
				LogInjectFailure(popupInstance, "popup-root-missing");
				return;
			}
			if (!ResolveFooterTargets(popupInstance, popupRoot, out Transform footerRoot, out Transform interactButtonsRoot, out string resolvedPath))
			{
				LogInjectFailure(popupInstance, "footer-target-missing");
				return;
			}
			CleanupLegacyActionRows(footerRoot);
			CleanupDuplicateManagedButtons(interactButtonsRoot);

			Entity currentPeep = ResolveCurrentPeep(popupInstance);
			PlayerInfo humanPlayer = CrewInfoActionBridge.GetHumanPlayer() ?? ResolveHumanPlayerLocal();
			bool localBossFallback = IsLocalHumanBoss(currentPeep, humanPlayer);
			bool selectedIsBoss = CrewInfoActionBridge.IsHumanBoss(currentPeep, humanPlayer) || localBossFallback;
			bool localBossOrUnderbossFallback = IsLocalHumanBossOrUnderboss(currentPeep, humanPlayer, selectedIsBoss);
			bool selectedIsBossOrUnderboss = CrewInfoActionBridge.IsHumanBossOrUnderboss(currentPeep, humanPlayer) || localBossOrUnderbossFallback;
			bool isHumanCrewMember = selectedIsBoss || IsHumanCrewMember(currentPeep, humanPlayer);
			bool canOpenCrewRelations = currentPeep != null && CrewInfoActionBridge.HasAction("OpenCrewRelationsFromExternalUi", 1);
			bool canOpenGangPacts = CrewInfoActionBridge.HasAction("OpenGangPactsFromExternalUi");
			bool canOpenMyPact = CrewInfoActionBridge.HasAction("OpenMyPactFromExternalUi");
			bool canOpenGrapevine = CrewInfoActionBridge.HasAction("OpenGrapevineFromExternalUi");
			bool canOpenSafebox = CrewInfoActionBridge.HasAction("OpenSafeboxFromExternalUi");
			if (!selectedIsBossOrUnderboss)
			{
				CrewInfoActionBridge.CloseBossOnlyCrewMenus(false);
			}
			else if (!selectedIsBoss)
			{
				CrewInfoActionBridge.CloseBossOnlyCrewMenus(true);
			}

			GameObject relations = GetFooterButtonByName(interactButtonsRoot, "Relationships", "Relationship", "Relations");
			GameObject orgChart = GetFooterButtonByName(interactButtonsRoot, "Org Chart", "OrgChart");
			GameObject interact = GetFooterButtonByName(interactButtonsRoot, "Interact", "Interaction");
			GameObject levelUp = GetFooterButtonByName(interactButtonsRoot, "Levelup", "Level Up", "LevelUp");
			GameObject defaultTemplate = relations ?? orgChart ?? interact ?? levelUp;
			if (defaultTemplate == null)
			{
				LogInjectFailure(popupInstance, "template-button-missing");
				return;
			}

			GameObject crewRel = EnsureActionButton(interactButtonsRoot, "GT_ModBtn_CrewRel", "Crew", relations ?? defaultTemplate, () => CrewInfoActionBridge.OpenCrewRelations(currentPeep), canOpenCrewRelations);
			GameObject gangPacts = EnsureActionButton(interactButtonsRoot, "GT_ModBtn_GangPacts", "GP", orgChart ?? relations ?? defaultTemplate, CrewInfoActionBridge.OpenGangPacts, canOpenGangPacts && selectedIsBoss);
			GameObject myPact = EnsureActionButton(interactButtonsRoot, "GT_ModBtn_MyPact", "MP", orgChart ?? relations ?? defaultTemplate, CrewInfoActionBridge.OpenMyPact, canOpenMyPact && selectedIsBossOrUnderboss);
			GameObject grapevine = EnsureActionButton(interactButtonsRoot, "GT_ModBtn_Grapevine", "GV", interact ?? relations ?? defaultTemplate, CrewInfoActionBridge.OpenGrapevine, canOpenGrapevine && isHumanCrewMember);
			bool showSafebox = selectedIsBoss
				&& canOpenSafebox
				&& CrewInfoActionBridge.IsDirtyCashEnabled()
				&& !CrewInfoActionBridge.ShouldUseExternalSafeboxUi()
				&& CrewInfoActionBridge.IsHumanPeepAtSafehouseCorner(humanPlayer, currentPeep);
			GameObject safebox = EnsureActionButton(interactButtonsRoot, "GT_ModBtn_Safebox", "$", levelUp ?? relations ?? defaultTemplate, CrewInfoActionBridge.OpenSafebox, showSafebox);

			crewRel.SetActive(canOpenCrewRelations);
			gangPacts.SetActive(canOpenGangPacts && selectedIsBoss);
			myPact.SetActive(canOpenMyPact && selectedIsBossOrUnderboss);
			grapevine.SetActive(canOpenGrapevine && isHumanCrewMember);
			safebox.SetActive(showSafebox);
			ApplyVanillaButtonIcon(crewRel, "trait-sociable.icon", "Crew");
			ApplyVanillaButtonIcon(gangPacts, "trait-boss.icon", "GP");
			ApplyVanillaButtonIcon(myPact, "trait-organized.icon", "MP");
			ApplyVanillaButtonIcon(grapevine, "trait-talkative.icon", "GV");
			ApplyVanillaButtonIcon(safebox, "res.dirty-cash.icon", "$");

			List<GameObject> ordered = new List<GameObject>();
			AddIfActive(ordered, relations);
			AddIfActive(ordered, orgChart);
			AddIfActive(ordered, interact);
			AddIfActive(ordered, levelUp);
			AddIfActive(ordered, crewRel);
			AddIfActive(ordered, gangPacts);
			AddIfActive(ordered, myPact);
			AddIfActive(ordered, grapevine);
			AddIfActive(ordered, safebox);
			for (int i = 0; i < ordered.Count; i++)
			{
				ordered[i].transform.SetSiblingIndex(i);
			}
			NormalizeFooterButtonSizes(interactButtonsRoot, ordered);

			if (SuccessLoggedPopupIds.Add(popupRoot.GetInstanceID()))
			{
				AfterProhibitionUIPlugin.Log?.LogInfo(
					"CrewInfoButtons unified count=" + ordered.Count
					+ " bounds=" + resolvedPath
					+ " selectedBoss=" + selectedIsBoss
					+ " bossFallback=" + localBossFallback
					+ " selectedBossOrUnderboss=" + selectedIsBossOrUnderboss
					+ " bossOrUnderbossFallback=" + localBossOrUnderbossFallback
					+ " humanCrewMember=" + isHumanCrewMember
					+ " actions crewRel=" + canOpenCrewRelations
					+ " gangPacts=" + canOpenGangPacts
					+ " myPact=" + canOpenMyPact
					+ " grapevine=" + canOpenGrapevine
					+ " safebox=" + canOpenSafebox
					+ " shown crewRel=" + crewRel.activeSelf
					+ " gangPacts=" + gangPacts.activeSelf
					+ " myPact=" + myPact.activeSelf
					+ " grapevine=" + grapevine.activeSelf
					+ " safebox=" + safebox.activeSelf
					+ " owner=AfterProhibitionUI");
			}
			if (!_injectionLogged)
			{
				_injectionLogged = true;
				AfterProhibitionUIPlugin.Log?.LogInfo("CrewInfoButtons injected crew inspect footer buttons owner=AfterProhibitionUI");
			}
		}

		private static void AddIfActive(List<GameObject> list, GameObject go)
		{
			if (go != null && go.activeSelf)
			{
				list.Add(go);
			}
		}

		private static bool IsLocalHumanBoss(Entity peep, PlayerInfo humanPlayer)
		{
			if (peep == null || humanPlayer == null)
			{
				return false;
			}

			try
			{
				if (humanPlayer.social != null && humanPlayer.social.PlayerPeepId == peep.Id)
				{
					return true;
				}
			}
			catch
			{
			}

			try
			{
				if (peep.components?.agent != null)
				{
					var boss = peep.components.agent.IsBoss();
					return boss.pass && boss.pid == humanPlayer.PID;
				}
			}
			catch
			{
			}

			return false;
		}

		private static PlayerInfo ResolveHumanPlayerLocal()
		{
			try
			{
				return global::Game.Game.ctx?.players?.Human;
			}
			catch
			{
				return null;
			}
		}

		private static bool IsHumanCrewMember(Entity peep, PlayerInfo humanPlayer)
		{
			if (peep == null || humanPlayer?.crew == null)
			{
				return false;
			}

			try
			{
				return humanPlayer.crew.GetCrewForPeep(peep.Id).IsValid;
			}
			catch
			{
				return false;
			}
		}

		private static bool IsLocalHumanBossOrUnderboss(Entity peep, PlayerInfo humanPlayer, bool isBoss)
		{
			if (isBoss)
			{
				return true;
			}
			if (peep == null || humanPlayer?.crew == null)
			{
				return false;
			}

			try
			{
				var crewAssignment = humanPlayer.crew.GetCrewForPeep(peep.Id);
				if (!crewAssignment.IsValid)
				{
					return false;
				}
			}
			catch
			{
				return false;
			}

			try
			{
				string crewRoleName = peep.data?.agent?.xp?.crewRole.ToString();
				return !string.IsNullOrEmpty(crewRoleName)
					&& crewRoleName.IndexOf("underboss", StringComparison.OrdinalIgnoreCase) >= 0;
			}
			catch
			{
				return false;
			}
		}

		private static bool HasHumanBossContext(PlayerInfo humanPlayer)
		{
			if (humanPlayer == null)
			{
				return false;
			}

			try
			{
				if (humanPlayer.social != null && humanPlayer.social.PlayerPeepId.IsValid)
				{
					return true;
				}
			}
			catch
			{
			}

			try
			{
				if (humanPlayer.crew != null)
				{
					var bossAssignment = humanPlayer.crew.GetCrewForIndex(0);
					return bossAssignment.IsValid;
				}
			}
			catch
			{
			}

			return false;
		}

		private static GameObject EnsureActionButton(Transform row, string buttonName, string label, GameObject template, Func<bool> onClick, bool interactable)
		{
			GameObject button = FindDirectChild(row, buttonName);
			if (button == null)
			{
				button = UnityEngine.Object.Instantiate(template, row);
				button.name = buttonName;
				button.transform.localScale = Vector3.one;
			}

			RectTransform rect = button.GetComponent<RectTransform>();
			if (rect != null)
			{
				rect.anchorMin = new Vector2(0.5f, 0.5f);
				rect.anchorMax = new Vector2(0.5f, 0.5f);
				rect.pivot = new Vector2(0.5f, 0.5f);
				rect.anchoredPosition = Vector2.zero;
				rect.localScale = Vector3.one;
			}

			LayoutElement layout = button.GetComponent<LayoutElement>() ?? button.AddComponent<LayoutElement>();
			layout.minWidth = 54f;
			layout.preferredWidth = 54f;
			layout.flexibleWidth = 0f;
			layout.minHeight = 30f;
			layout.preferredHeight = 30f;

			Button uiButton = button.GetComponent<Button>();
			if (uiButton != null)
			{
				uiButton.onClick.RemoveAllListeners();
				uiButton.onClick.AddListener((UnityAction)(() => onClick?.Invoke()));
				uiButton.interactable = interactable;
			}
			CleanupOwnedIconChildren(button.transform);
			RestoreButtonVisual(button, template, label);
			return button;
		}

		private static void RestoreButtonVisual(GameObject button, GameObject template, string fallbackLabel)
		{
			if (button == null)
			{
				return;
			}

			bool copiedText = CopyTextVisual(button, template);
			CopyImageVisuals(button, template);
			RestoreRootButtonGraphic(button, template);
			if (!copiedText && HasNonBackgroundImage(button))
			{
				ClearButtonText(button);
			}
			else if (!copiedText)
			{
				SetButtonLabel(button, fallbackLabel);
			}
		}

		private static void ApplyVanillaButtonIcon(GameObject button, string locKey, string fallbackText)
		{
			if (button == null || string.IsNullOrEmpty(locKey))
			{
				return;
			}

			string locText = TryGetLocText(locKey);
			string spriteName = ExtractSpriteNameFromLocText(locText) ?? LocKeyToSpriteName(locKey);
			Sprite sprite = TryGetUiSpriteFromAtlas(spriteName);
			if (sprite != null)
			{
				Image iconImage = ResolveOrCreateOwnedIconImage(button.transform);
				ClearButtonText(button);
				DisableOwnedTmpIcon(button.transform);
				ConfigureInnerIconImage(iconImage, sprite);
				LogIconOnce(button, locKey, "image", spriteName);
				return;
			}

			TextMeshProUGUI tmp = ResolveOrCreateInnerIconTmp(button.transform);
			if (!string.IsNullOrEmpty(locText) && locText.IndexOf("<sprite", StringComparison.OrdinalIgnoreCase) >= 0 && tmp != null)
			{
				ClearButtonText(button);
				DisableOwnedIconImage(button.transform);
				ConfigureInnerIconTmp(tmp, locText);
				LogIconOnce(button, locKey, "tmp-sprite", spriteName);
				return;
			}

			if (tmp != null)
			{
				DisableOwnedIconImage(button.transform);
				ConfigureInnerIconTmp(tmp, fallbackText);
				LogIconOnce(button, locKey, "text-fallback", spriteName);
			}
		}

		private static string TryGetLocText(string locKey)
		{
			try
			{
				string text = Loc.Get(locKey);
				return string.Equals(text, locKey, StringComparison.Ordinal) ? null : text;
			}
			catch
			{
				return null;
			}
		}

		private static string LocKeyToSpriteName(string locKey)
		{
			if (string.IsNullOrEmpty(locKey) || !locKey.EndsWith(".icon", StringComparison.OrdinalIgnoreCase))
			{
				return null;
			}

			string baseName = locKey.Substring(0, locKey.Length - 5);
			return baseName.StartsWith("res.", StringComparison.OrdinalIgnoreCase) ? baseName.Substring(4) : baseName;
		}

		private static string ExtractSpriteNameFromLocText(string locText)
		{
			if (string.IsNullOrEmpty(locText))
			{
				return null;
			}

			const string marker = "name=\"";
			int start = locText.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
			if (start < 0)
			{
				return null;
			}

			start += marker.Length;
			int end = locText.IndexOf('"', start);
			return end > start ? locText.Substring(start, end - start) : null;
		}

		private static Sprite TryGetUiSpriteFromAtlas(string spriteName)
		{
			if (string.IsNullOrEmpty(spriteName))
			{
				return null;
			}

			try
			{
				object hud = global::Game.Game.ctx?.hud;
				object uisprites = hud?.GetType().GetField("uisprites", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(hud);
				object uiatlas = uisprites?.GetType().GetField("uiatlas", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(uisprites);
				MethodInfo findMethod = uiatlas?.GetType().GetMethod("Find", new[] { typeof(string) });
				Sprite sprite = findMethod?.Invoke(uiatlas, new object[] { spriteName }) as Sprite;
				if (sprite != null)
				{
					return sprite;
				}

				if (spriteName.IndexOf(".", StringComparison.Ordinal) < 0)
				{
					return findMethod?.Invoke(uiatlas, new object[] { "res." + spriteName }) as Sprite;
				}
			}
			catch
			{
			}

			return null;
		}

		private static Image ResolveOrCreateOwnedIconImage(Transform buttonRoot)
		{
			Transform existing = buttonRoot != null ? buttonRoot.Find("GT_VanillaIcon") : null;
			Image image = existing != null ? existing.GetComponent<Image>() : null;
			if (image == null && buttonRoot != null)
			{
				GameObject iconObject = new GameObject("GT_VanillaIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
				iconObject.transform.SetParent(buttonRoot, false);
				image = iconObject.GetComponent<Image>();
			}
			if (image != null)
			{
				image.transform.SetAsLastSibling();
			}
			return image;
		}

		private static TextMeshProUGUI ResolveOrCreateInnerIconTmp(Transform buttonRoot)
		{
			if (buttonRoot == null)
			{
				return null;
			}

			Transform existing = buttonRoot.Find("GT_VanillaIconTMP");
			TextMeshProUGUI tmp = existing != null ? existing.GetComponent<TextMeshProUGUI>() : null;
			if (tmp != null)
			{
				return tmp;
			}

			GameObject textObject = new GameObject("GT_VanillaIconTMP", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
			textObject.transform.SetParent(buttonRoot, false);
			tmp = textObject.GetComponent<TextMeshProUGUI>();
			TextMeshProUGUI template = FindNearestTemplateTmp(buttonRoot, tmp);
			if (template != null && template != tmp)
			{
				tmp.font = template.font;
				tmp.fontSharedMaterial = template.fontSharedMaterial;
				tmp.spriteAsset = template.spriteAsset;
			}
			tmp.transform.SetAsLastSibling();
			return tmp;
		}

		private static TextMeshProUGUI FindNearestTemplateTmp(Transform buttonRoot, TextMeshProUGUI exclude)
		{
			Transform cursor = buttonRoot;
			while (cursor != null)
			{
				TextMeshProUGUI[] texts = cursor.GetComponentsInChildren<TextMeshProUGUI>(true);
				for (int i = 0; i < texts.Length; i++)
				{
					TextMeshProUGUI candidate = texts[i];
					if (candidate != null && candidate != exclude)
					{
						return candidate;
					}
				}
				cursor = cursor.parent;
			}
			return null;
		}

		private static void ConfigureInnerIconImage(Image image, Sprite sprite)
		{
			if (image == null || sprite == null)
			{
				return;
			}

			image.sprite = sprite;
			image.overrideSprite = null;
			image.type = Image.Type.Simple;
			image.preserveAspect = true;
			image.enabled = true;
			image.raycastTarget = false;
			image.color = Color.white;
			image.gameObject.SetActive(true);
			RectTransform rect = image.transform as RectTransform;
			if (rect != null)
			{
				rect.anchorMin = new Vector2(0.5f, 0.5f);
				rect.anchorMax = new Vector2(0.5f, 0.5f);
				rect.pivot = new Vector2(0.5f, 0.5f);
				rect.anchoredPosition = Vector2.zero;
				rect.localScale = Vector3.one;
				rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 24f);
				rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 24f);
			}
		}

		private static void ConfigureInnerIconTmp(TextMeshProUGUI tmp, string text)
		{
			if (tmp == null)
			{
				return;
			}

			tmp.text = text ?? string.Empty;
			tmp.gameObject.SetActive(true);
			tmp.enabled = true;
			tmp.transform.SetAsLastSibling();
			tmp.raycastTarget = false;
			tmp.richText = true;
			tmp.enableWordWrapping = false;
			tmp.overflowMode = TextOverflowModes.Overflow;
			tmp.alignment = TextAlignmentOptions.Center;
			tmp.fontSize = !string.IsNullOrEmpty(text) && text.IndexOf("<sprite", StringComparison.OrdinalIgnoreCase) >= 0 ? 24f : 11f;
			tmp.fontStyle = FontStyles.Bold;
			tmp.margin = Vector4.zero;
			tmp.color = Color.white;
			RectTransform rect = tmp.transform as RectTransform;
			if (rect != null)
			{
				rect.anchorMin = new Vector2(0.5f, 0.5f);
				rect.anchorMax = new Vector2(0.5f, 0.5f);
				rect.pivot = new Vector2(0.5f, 0.5f);
				rect.anchoredPosition = Vector2.zero;
				rect.localScale = Vector3.one;
				rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 24f);
				rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 24f);
			}
		}

		private static void DisableOwnedTmpIcon(Transform buttonRoot)
		{
			if (buttonRoot == null)
			{
				return;
			}

			Transform tmp = buttonRoot.Find("GT_VanillaIconTMP");
			if (tmp != null)
			{
				tmp.gameObject.SetActive(false);
			}
		}

		private static void DisableOwnedIconImage(Transform buttonRoot)
		{
			if (buttonRoot == null)
			{
				return;
			}

			Transform icon = buttonRoot.Find("GT_VanillaIcon");
			if (icon != null)
			{
				icon.gameObject.SetActive(false);
			}
		}

		private static void CleanupOwnedIconChildren(Transform buttonRoot)
		{
			if (buttonRoot == null)
			{
				return;
			}

			CleanupDirectChild(buttonRoot, "GT_VanillaIcon");
			CleanupDirectChild(buttonRoot, "GT_VanillaIconTMP");
		}

		private static void CleanupDirectChild(Transform parent, string childName)
		{
			Transform child = parent.Find(childName);
			if (child != null)
			{
				child.gameObject.SetActive(false);
				Image image = child.GetComponent<Image>();
				if (image != null)
				{
					image.sprite = null;
					image.overrideSprite = null;
				}
				TextMeshProUGUI tmp = child.GetComponent<TextMeshProUGUI>();
				if (tmp != null)
				{
					tmp.text = string.Empty;
				}
			}
		}

		private static void LogIconOnce(GameObject button, string locKey, string target, string spriteName)
		{
			string key = (button != null ? button.name : "none") + ":" + locKey + ":" + target;
			if (IconLogKeys.Add(key))
			{
				AfterProhibitionUIPlugin.Log?.LogInfo("CrewInfoButtons icon-applied button=" + (button != null ? button.name : "none") + " key=" + locKey + " target=" + target + " sprite=" + (spriteName ?? "none") + " owner=AfterProhibitionUI");
			}
		}

		private static bool CopyTextVisual(GameObject button, GameObject template)
		{
			bool copied = false;
			TextMeshProUGUI sourceTmp = template != null ? template.GetComponentInChildren<TextMeshProUGUI>(true) : null;
			TextMeshProUGUI targetTmp = button.GetComponentInChildren<TextMeshProUGUI>(true);
			if (sourceTmp != null && targetTmp != null)
			{
				targetTmp.text = sourceTmp.text;
				targetTmp.fontSize = sourceTmp.fontSize;
				targetTmp.fontStyle = sourceTmp.fontStyle;
				targetTmp.alignment = sourceTmp.alignment;
				targetTmp.color = sourceTmp.color;
				targetTmp.gameObject.SetActive(sourceTmp.gameObject.activeSelf);
				copied = true;
			}

			Text sourceText = template != null ? template.GetComponentInChildren<Text>(true) : null;
			Text targetText = button.GetComponentInChildren<Text>(true);
			if (sourceText != null && targetText != null)
			{
				targetText.text = sourceText.text;
				targetText.font = sourceText.font;
				targetText.fontSize = sourceText.fontSize;
				targetText.fontStyle = sourceText.fontStyle;
				targetText.alignment = sourceText.alignment;
				targetText.color = sourceText.color;
				targetText.gameObject.SetActive(sourceText.gameObject.activeSelf);
				copied = true;
			}

			return copied;
		}

		private static void ClearButtonText(GameObject button)
		{
			if (button == null)
			{
				return;
			}

			TextMeshProUGUI[] tmps = button.GetComponentsInChildren<TextMeshProUGUI>(true);
			for (int i = 0; i < tmps.Length; i++)
			{
				if (tmps[i] != null)
				{
					tmps[i].text = string.Empty;
				}
			}

			Text[] texts = button.GetComponentsInChildren<Text>(true);
			for (int i = 0; i < texts.Length; i++)
			{
				if (texts[i] != null)
				{
					texts[i].text = string.Empty;
				}
			}
		}

		private static void CopyImageVisuals(GameObject button, GameObject template)
		{
			if (button == null || template == null)
			{
				return;
			}

			Image[] sourceImages = template.GetComponentsInChildren<Image>(true);
			Image[] targetImages = button.GetComponentsInChildren<Image>(true);
			int count = Math.Min(sourceImages.Length, targetImages.Length);
			for (int i = 0; i < count; i++)
			{
				Image source = sourceImages[i];
				Image target = targetImages[i];
				if (source == null || target == null)
				{
					continue;
				}

				target.sprite = source.sprite;
				target.overrideSprite = source.overrideSprite;
				target.type = source.type;
				target.preserveAspect = source.preserveAspect;
				target.fillCenter = source.fillCenter;
				target.color = source.color;
				target.gameObject.SetActive(source.gameObject.activeSelf);
			}
		}

		private static void RestoreRootButtonGraphic(GameObject button, GameObject template)
		{
			if (button == null)
			{
				return;
			}

			Image target = button.GetComponent<Image>();
			Image source = template != null ? template.GetComponent<Image>() : null;
			if (target != null)
			{
				if (source != null)
				{
					target.sprite = source.sprite;
					target.overrideSprite = source.overrideSprite;
					target.type = source.type;
					target.preserveAspect = source.preserveAspect;
					target.fillCenter = source.fillCenter;
					target.color = source.color;
				}
				target.enabled = source == null || source.enabled;
				target.gameObject.SetActive(true);
			}

			Button uiButton = button.GetComponent<Button>();
			if (uiButton != null && target != null)
			{
				uiButton.targetGraphic = target;
			}
		}

		private static bool HasNonBackgroundImage(GameObject button)
		{
			if (button == null)
			{
				return false;
			}

			Image[] images = button.GetComponentsInChildren<Image>(true);
			for (int i = 0; i < images.Length; i++)
			{
				Image image = images[i];
				if (image == null || image.gameObject == button)
				{
					continue;
				}
				if (image.sprite != null || image.overrideSprite != null)
				{
					return true;
				}
			}

			return false;
		}

		private static void SetButtonLabel(GameObject button, string label)
		{
			TextMeshProUGUI tmp = button.GetComponentInChildren<TextMeshProUGUI>(true);
			if (tmp != null)
			{
				tmp.text = label;
				tmp.fontSize = 14f;
				tmp.alignment = TextAlignmentOptions.Center;
			}
			Text text = button.GetComponentInChildren<Text>(true);
			if (text != null)
			{
				text.text = label;
				text.fontSize = 11;
				text.fontStyle = FontStyle.Bold;
				text.alignment = TextAnchor.MiddleCenter;
			}
		}

		private static void NormalizeFooterButtonSizes(Transform interactButtonsRoot, List<GameObject> orderedButtons)
		{
			if (interactButtonsRoot == null || orderedButtons == null || orderedButtons.Count == 0)
			{
				return;
			}

			HorizontalLayoutGroup layoutGroup = interactButtonsRoot.GetComponent<HorizontalLayoutGroup>() ?? interactButtonsRoot.gameObject.AddComponent<HorizontalLayoutGroup>();
			layoutGroup.spacing = 2f;
			layoutGroup.childForceExpandWidth = false;
			layoutGroup.childForceExpandHeight = false;
			layoutGroup.childControlWidth = true;
			layoutGroup.childControlHeight = true;
			layoutGroup.childAlignment = TextAnchor.MiddleCenter;
			layoutGroup.padding = new RectOffset(0, 0, 0, 0);

			RectTransform rowRect = interactButtonsRoot.GetComponent<RectTransform>();
			float rowWidth = rowRect != null ? rowRect.rect.width : 0f;
			if (rowWidth <= 0f && interactButtonsRoot.parent is RectTransform parentRect)
			{
				rowWidth = parentRect.rect.width;
			}
			if (rowWidth <= 0f)
			{
				rowWidth = 40f * orderedButtons.Count + 2f * (orderedButtons.Count - 1);
			}
			float width = Mathf.Clamp(Mathf.Floor((rowWidth - 2f * (orderedButtons.Count - 1)) / orderedButtons.Count), 24f, 40f);
			if (rowRect != null)
			{
				rowRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width * orderedButtons.Count + 2f * (orderedButtons.Count - 1));
			}
			foreach (GameObject button in orderedButtons)
			{
				LayoutElement element = button.GetComponent<LayoutElement>() ?? button.AddComponent<LayoutElement>();
				element.minWidth = width;
				element.preferredWidth = width;
				element.flexibleWidth = 0f;
				element.minHeight = 26f;
				element.preferredHeight = 26f;
			}
		}

		private static GameObject FindDirectChild(Transform root, string childName)
		{
			if (root == null || string.IsNullOrEmpty(childName))
			{
				return null;
			}
			for (int i = 0; i < root.childCount; i++)
			{
				Transform child = root.GetChild(i);
				if (child != null && string.Equals(child.name, childName, StringComparison.Ordinal))
				{
					return child.gameObject;
				}
			}
			return null;
		}

		private static void CleanupLegacyActionRows(Transform footerRoot)
		{
			CleanupNamedDescendants(footerRoot, "GT_ModActions");
			CleanupNamedDescendants(footerRoot, "GT_ModActions_Row1");
			CleanupNamedDescendants(footerRoot, "GT_ModActions_Row2");
		}

		private static void CleanupNamedDescendants(Transform parent, string childName)
		{
			if (parent == null || string.IsNullOrEmpty(childName))
			{
				return;
			}

			List<GameObject> matches = new List<GameObject>();
			foreach (Transform child in parent.GetComponentsInChildren<Transform>(true))
			{
				if (child != null && string.Equals(child.name, childName, StringComparison.Ordinal))
				{
					matches.Add(child.gameObject);
				}
			}
			foreach (GameObject match in matches)
			{
				match.SetActive(false);
				UnityEngine.Object.Destroy(match);
			}
		}

		private static void CleanupDuplicateManagedButtons(Transform interactButtonsRoot)
		{
			if (interactButtonsRoot == null)
			{
				return;
			}

			foreach (string buttonName in ManagedButtonNames)
			{
				bool found = false;
				for (int i = interactButtonsRoot.childCount - 1; i >= 0; i--)
				{
					Transform child = interactButtonsRoot.GetChild(i);
					if (child == null || !string.Equals(child.name, buttonName, StringComparison.Ordinal))
					{
						continue;
					}
					if (!found)
					{
						found = true;
						continue;
					}

					child.gameObject.SetActive(false);
					UnityEngine.Object.Destroy(child.gameObject);
				}
			}
		}

		private static GameObject GetFooterButtonByName(Transform root, params string[] childNames)
		{
			if (root == null || childNames == null)
			{
				return null;
			}
			foreach (string childName in childNames)
			{
				GameObject direct = FindDirectChild(root, childName);
				if (direct != null && direct.GetComponent<Button>() != null)
				{
					return direct;
				}
			}
			string[] normalizedNames = Array.ConvertAll(childNames, NormalizeName);
			for (int i = 0; i < root.childCount; i++)
			{
				Transform child = root.GetChild(i);
				if (child == null || child.GetComponent<Button>() == null)
				{
					continue;
				}

				string normalizedChildName = NormalizeName(child.name);
				foreach (string normalizedName in normalizedNames)
				{
					if (!string.IsNullOrEmpty(normalizedName) && normalizedChildName.Contains(normalizedName))
					{
						return child.gameObject;
					}
				}
			}
			return null;
		}

		private static string NormalizeName(string value)
		{
			return string.IsNullOrEmpty(value)
				? string.Empty
				: value.Replace(" ", string.Empty).Replace("-", string.Empty).Replace("_", string.Empty).Trim().ToLowerInvariant();
		}

		private static GameObject ResolvePopupRoot(object popupInstance)
		{
			if (popupInstance == null)
			{
				return null;
			}
			if (_popupGoField == null)
			{
				Type type = popupInstance.GetType();
				while (type != null && _popupGoField == null)
				{
					_popupGoField = type.GetField("_go", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
					type = type.BaseType;
				}
			}
			return _popupGoField?.GetValue(popupInstance) as GameObject;
		}

		private static GameObject ResolvePanelRoot(object popupInstance)
		{
			if (popupInstance == null)
			{
				return null;
			}
			if (_panelGoField == null)
			{
				Type type = popupInstance.GetType();
				while (type != null && _panelGoField == null)
				{
					_panelGoField = type.GetField("_panel", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
					type = type.BaseType;
				}
			}
			return _panelGoField?.GetValue(popupInstance) as GameObject;
		}

		private static bool ResolveFooterTargets(object popupInstance, GameObject popupRoot, out Transform footerRoot, out Transform interactButtonsRoot, out string resolvedPath)
		{
			footerRoot = null;
			interactButtonsRoot = null;
			resolvedPath = "none";
			Transform root = popupRoot.transform;
			footerRoot = root.Find("Footer");
			interactButtonsRoot = root.Find("Footer/Interact Buttons");
			if (footerRoot != null && interactButtonsRoot != null)
			{
				resolvedPath = "_go/Footer";
				return true;
			}
			footerRoot = root.Find("Panel/Footer");
			interactButtonsRoot = root.Find("Panel/Footer/Interact Buttons");
			if (footerRoot != null && interactButtonsRoot != null)
			{
				resolvedPath = "_go/Panel/Footer";
				return true;
			}
			GameObject panelRoot = ResolvePanelRoot(popupInstance);
			footerRoot = panelRoot?.transform.Find("Footer");
			interactButtonsRoot = panelRoot?.transform.Find("Footer/Interact Buttons");
			if (footerRoot != null && interactButtonsRoot != null)
			{
				resolvedPath = "_panel/Footer";
				return true;
			}
			return false;
		}

		private static Entity ResolveCurrentPeep(object popupInstance)
		{
			if (popupInstance == null)
			{
				return null;
			}
			if (_crewMemberProperty == null)
			{
				_crewMemberProperty = popupInstance.GetType().GetProperty("CrewMember", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			}
			if (_crewMemberProperty?.GetValue(popupInstance) is Entity crewMember)
			{
				return crewMember;
			}
			if (_peepField == null)
			{
				_peepField = popupInstance.GetType().GetField("peep", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			}
			object value = _peepField?.GetValue(popupInstance);
			if (value is Entity entity)
			{
				return entity;
			}
			return null;
		}

		private static void LogInjectFailure(object popupInstance, string reason, Exception ex = null)
		{
			GameObject root = ResolvePopupRoot(popupInstance);
			string key = (root != null ? root.GetInstanceID().ToString() : "0") + ":" + reason;
			if (FailureLogKeys.Add(key))
			{
				AfterProhibitionUIPlugin.Log?.LogInfo("CrewInfoButtons inject-failed reason=" + reason + (ex != null ? " ex=" + ex.GetType().Name + ":" + ex.Message : string.Empty));
			}
		}
	}
}
