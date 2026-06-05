using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Game.Services;
using Game.UI;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace AfterProhibitionAssets
{
	[HarmonyPatch(typeof(MainMenuPopup), "InitializeOnPush")]
	internal static class MainMenuInitializePatch
	{
		[HarmonyPriority(Priority.Last)]
		private static void Prefix(MainMenuPopup __instance)
		{
			MainMenuBackgroundOverride.CaptureVanillaDefault(__instance);
		}

		[HarmonyPriority(Priority.Last)]
		private static void Postfix(MainMenuPopup __instance)
		{
			MainMenuBackgroundOverride.ApplyDeferred(__instance, "initialize");
		}
	}

	[HarmonyPatch(typeof(BasePopup), "OnActivated")]
	internal static class MainMenuActivatedPatch
	{
		[HarmonyPriority(Priority.Last)]
		private static void Postfix(BasePopup __instance)
		{
			MainMenuPopup popup = __instance as MainMenuPopup;
			if (popup != null)
			{
				MainMenuBackgroundOverride.ApplyDeferred(popup, "activated");
			}
		}
	}

	internal static class MainMenuBackgroundOverride
	{
		private const string ImageKey = "main-menu.primary";

		private static readonly string[] ImageKeys =
		{
			ImageKey,
			"main-menu.alt1",
			"main-menu.alt2",
			"main-menu.alt3",
			"main-menu.alt4",
			"main-menu.alt5",
			"main-menu.alt6",
			"main-menu.alt7",
			"main-menu.alt8",
			"main-menu.alt9"
		};

		private const string OverlayName = "AfterProhibitionAssets Main Menu Background";

		private static readonly FieldInfo PanelField = AccessTools.Field(typeof(BasePopup), "_panel");

		private static readonly FieldInfo GoField = AccessTools.Field(typeof(BasePopup), "_go");

		private static readonly System.Random Random = new System.Random();

		private static readonly HashSet<string> LoggedAppliedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		private static Sprite _vanillaDefaultSprite;

		private static Image _vanillaTargetImage;

		private static string _vanillaTargetPath;

		private static int _lastRandomIndex = -1;

		private static bool _hasStableSelection;

		private static Sprite _stableSelectedSprite;

		private static string _stableSelectedKey;

		private static bool _loggedDisabled;

		private static bool _loggedExternalMainMenuFallback;

		private static bool _loggedVanillaCapture;

		internal static void CaptureVanillaDefault(MainMenuPopup popup)
		{
			if (_vanillaDefaultSprite != null)
			{
				return;
			}

			GameObject root = GoField.GetValue(popup) as GameObject;
			if (root == null)
			{
				return;
			}

			_vanillaTargetImage = FindMainMenuTargetImage(root);
			_vanillaDefaultSprite = _vanillaTargetImage?.sprite ?? FindLargestSprite(root);
			if (_vanillaDefaultSprite != null && !_loggedVanillaCapture)
			{
				AfterProhibitionAssetsPlugin.Log?.LogInfo("Captured embedded main menu default sprite: " + _vanillaDefaultSprite.name + " target=" + (_vanillaTargetPath ?? "<unknown>"));
				_loggedVanillaCapture = true;
			}
		}

		internal static void ApplyDeferred(MainMenuPopup popup, string source)
		{
			Sprite selectedSprite;
			string selectedKey;
			TryGetStableConfiguredMainMenuSprite(out selectedSprite, out selectedKey);
			ApplyNow(popup, source, selectedSprite, selectedKey);
			AfterProhibitionAssetsPlugin.StartDeferredWork(ApplyDeferredRoutine(popup, selectedSprite, selectedKey));
		}

		private static IEnumerator ApplyDeferredRoutine(MainMenuPopup popup, Sprite selectedSprite, string selectedKey)
		{
			yield return null;
			ApplyNow(popup, "deferred-frame", selectedSprite, selectedKey);
			yield return new WaitForSecondsRealtime(0.1f);
			ApplyNow(popup, "deferred-0.1s", selectedSprite, selectedKey);
			yield return new WaitForSecondsRealtime(0.25f);
			ApplyNow(popup, "deferred-0.35s", selectedSprite, selectedKey);
			yield return new WaitForSecondsRealtime(0.65f);
			ApplyNow(popup, "deferred-1s", selectedSprite, selectedKey);
			yield return new WaitForSecondsRealtime(1f);
			ApplyNow(popup, "deferred-2s", selectedSprite, selectedKey);
			yield return new WaitForSecondsRealtime(2f);
			ApplyNow(popup, "deferred-4s", selectedSprite, selectedKey);
		}

		private static void ApplyNow(MainMenuPopup popup, string source, Sprite configuredSprite, string configuredKey)
		{
			if (popup == null)
			{
				return;
			}

			Sprite sprite = configuredSprite;
			if (sprite == null)
			{
				GameObject root = GoField.GetValue(popup) as GameObject;
				if (ShouldLeaveExternalMainMenuInPlace())
				{
					if (!_loggedExternalMainMenuFallback)
					{
						AfterProhibitionAssetsPlugin.Log?.LogInfo("Main menu image override skipped. reason=no-local-slot external=CoGCustomAssets mainMenuAssets=" + CoGCustomAssetsBridge.MainMenuAssetCount);
						_loggedExternalMainMenuFallback = true;
					}

					return;
				}

				Image currentTargetImage = FindMainMenuTargetImage(root);
				_vanillaTargetImage = currentTargetImage ?? _vanillaTargetImage;
				sprite = _vanillaDefaultSprite ?? currentTargetImage?.sprite ?? FindLargestSprite(root);
				if (sprite == null)
				{
					if (!_loggedDisabled && !AnyMainMenuSlotLoaded())
					{
						AfterProhibitionAssetsPlugin.Log?.LogInfo("Main menu image override inactive. Add " + ImageKey + "=images/after_prohibition_main_menu.png to images.txt to enable it. Optional rotating slots: main-menu.alt1 through main-menu.alt9.");
						_loggedDisabled = true;
					}

					return;
				}
			}

			GameObject parent = GoField.GetValue(popup) as GameObject;
			if (parent == null)
			{
				parent = PanelField.GetValue(popup) as GameObject;
			}

			if (parent == null)
			{
				AfterProhibitionAssetsPlugin.Log?.LogWarning("Main menu image override could not find popup panel.");
				return;
			}

			Image targetImage = FindMainMenuTargetImage(parent);
			if (targetImage != null)
			{
				_vanillaTargetImage = targetImage;
				targetImage.sprite = sprite;
				targetImage.color = Color.white;
				targetImage.enabled = true;
				targetImage.preserveAspect = true;
			}

			Image image = FindOrCreateBackgroundImage(parent);
			image.sprite = sprite;
			image.color = Color.white;
			image.enabled = true;
			image.preserveAspect = true;
			image.raycastTarget = false;
			image.transform.SetAsFirstSibling();

			string logKey = configuredKey ?? "embedded-ui-default";
			if (LoggedAppliedKeys.Add(logKey))
			{
				string assetSource = configuredKey == null ? "embedded-ui-default" : AssetRegistry.GetSourcePath(configuredKey);
				AfterProhibitionAssetsPlugin.Log?.LogInfo("Main menu image override applied. source=" + source + " key=" + logKey + " assetSource=" + assetSource);
			}
		}

		private static bool TrySelectConfiguredMainMenuSprite(out Sprite sprite, out string key)
		{
			var candidates = new List<MainMenuImageCandidate>();
			for (int i = 0; i < ImageKeys.Length; i++)
			{
				Sprite candidateSprite;
				if (AssetRegistry.TryGetSprite(ImageKeys[i], out candidateSprite))
				{
					candidates.Add(new MainMenuImageCandidate(ImageKeys[i], candidateSprite, i));
				}
			}

			if (candidates.Count == 0)
			{
				sprite = null;
				key = null;
				return false;
			}

			int candidateIndex = candidates.Count == 1 ? 0 : Random.Next(candidates.Count);
			if (candidates.Count > 1 && candidates[candidateIndex].OriginalIndex == _lastRandomIndex)
			{
				candidateIndex = (candidateIndex + 1) % candidates.Count;
			}

			MainMenuImageCandidate selected = candidates[candidateIndex];
			_lastRandomIndex = selected.OriginalIndex;
			sprite = selected.Sprite;
			key = selected.Key;
			return true;
		}

		private static bool TryGetStableConfiguredMainMenuSprite(out Sprite sprite, out string key)
		{
			if (_hasStableSelection && _stableSelectedSprite != null)
			{
				sprite = _stableSelectedSprite;
				key = _stableSelectedKey;
				return true;
			}

			if (!TrySelectConfiguredMainMenuSprite(out sprite, out key))
			{
				return false;
			}

			_stableSelectedSprite = sprite;
			_stableSelectedKey = key;
			_hasStableSelection = true;
			return true;
		}

		private static bool AnyMainMenuSlotLoaded()
		{
			for (int i = 0; i < ImageKeys.Length; i++)
			{
				if (AssetRegistry.ContainsKey(ImageKeys[i]))
				{
					return true;
				}
			}

			return false;
		}

		private static bool ShouldLeaveExternalMainMenuInPlace()
		{
			if (AnyMainMenuSlotLoaded())
			{
				return false;
			}

			CoGCustomAssetsBridge.Detect();
			return CoGCustomAssetsBridge.IsActive && CoGCustomAssetsBridge.MainMenuAssetCount > 0;
		}

		private static Image FindMainMenuTargetImage(GameObject root)
		{
			if (root == null)
			{
				return null;
			}

			Image panelImage = FindImageAtPath(root.transform, "Panel/Image");
			if (panelImage != null && panelImage.sprite != null)
			{
				_vanillaTargetPath = "Panel/Image";
				return panelImage;
			}

			Image rootImage = root.GetComponent<Image>();
			if (IsMainMenuImage(rootImage))
			{
				_vanillaTargetPath = root.name;
				return rootImage;
			}

			Image[] images = root.GetComponentsInChildren<Image>(true);
			foreach (Image image in images)
			{
				if (IsMainMenuImage(image))
				{
					_vanillaTargetPath = GetTransformPath(root.transform, image.transform);
					return image;
				}
			}

			_vanillaTargetPath = root.name;
			return rootImage;
		}

		private static Image FindImageAtPath(Transform root, string path)
		{
			Transform target = root?.Find(path);
			return target == null ? null : target.GetComponent<Image>();
		}

		private static string GetTransformPath(Transform root, Transform target)
		{
			if (root == null || target == null)
			{
				return null;
			}

			string path = target.name;
			Transform cursor = target.parent;
			while (cursor != null && cursor != root)
			{
				path = cursor.name + "/" + path;
				cursor = cursor.parent;
			}

			return path;
		}

		private static bool IsMainMenuImage(Image image)
		{
			if (image == null || image.sprite == null)
			{
				return false;
			}

			if (image.gameObject.name == "Main Menu")
			{
				return true;
			}

			return image.sprite.name == "Main Menu";
		}

		private static Sprite FindLargestSprite(GameObject root)
		{
			if (root == null)
			{
				return null;
			}

			Image[] images = root.GetComponentsInChildren<Image>(true);
			Sprite best = null;
			int bestArea = 0;
			foreach (Image image in images)
			{
				if (image == null || image.sprite == null || image.gameObject.name == OverlayName)
				{
					continue;
				}

				Texture2D texture = image.sprite.texture;
				if (texture == null)
				{
					continue;
				}

				int area = texture.width * texture.height;
				if (area > bestArea)
				{
					bestArea = area;
					best = image.sprite;
				}
			}

			return best;
		}

		private static Image FindOrCreateBackgroundImage(GameObject parent)
		{
			Transform existing = parent.transform.Find(OverlayName);
			if (existing != null)
			{
				Image existingImage = existing.GetComponent<Image>();
				if (existingImage != null)
				{
					return existingImage;
				}
			}

			var overlay = new GameObject(OverlayName, typeof(RectTransform), typeof(Image));
			var rect = overlay.GetComponent<RectTransform>();
			rect.SetParent(parent.transform, false);
			rect.anchorMin = Vector2.zero;
			rect.anchorMax = Vector2.one;
			rect.offsetMin = Vector2.zero;
			rect.offsetMax = Vector2.zero;
			rect.pivot = new Vector2(0.5f, 0.5f);
			return overlay.GetComponent<Image>();
		}

		private sealed class MainMenuImageCandidate
		{
			internal MainMenuImageCandidate(string key, Sprite sprite, int originalIndex)
			{
				Key = key;
				Sprite = sprite;
				OriginalIndex = originalIndex;
			}

			internal string Key { get; private set; }

			internal Sprite Sprite { get; private set; }

			internal int OriginalIndex { get; private set; }
		}
	}
}
