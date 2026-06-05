using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AfterProhibitionUI
{
	public static class UiRethemeBridge
	{
		private enum ButtonVariant
		{
			Default,
			Accent,
			Success,
			Danger,
			Close
		}

		private enum PanelVariant
		{
			Default,
			Header,
			Subtle
		}

		private static readonly Color ButtonDarkGray = new Color(0.24f, 0.26f, 0.3f, 0.98f);
		private static readonly Color ButtonDarkGrayDisabled = new Color(0.16f, 0.17f, 0.19f, 0.72f);
		private static readonly Color PanelDarkGray = new Color(0.2f, 0.21f, 0.24f, 0.96f);
		private static readonly Color PanelDarkGrayHeader = new Color(0.23f, 0.25f, 0.29f, 0.9f);
		private static readonly Color PanelDarkGraySubtle = new Color(0.21f, 0.22f, 0.25f, 0.92f);
		private static readonly HashSet<string> RethemeLogs = new HashSet<string>(StringComparer.Ordinal);

		public static bool TryRethemeMenuHierarchy(GameObject root, bool aggressiveGlyphCleanup)
		{
			if (root == null || AfterProhibitionUIPlugin.EnableMenuRethemeBridge?.Value != true)
			{
				return false;
			}

			try
			{
				int buttons = 0;
				int panels = 0;
				foreach (Button button in root.GetComponentsInChildren<Button>(true))
				{
					if (button == null)
					{
						continue;
					}

					ApplyStandardButtonTheme(button, ResolveButtonVariant(button.name));
					buttons++;
				}

				foreach (Image image in root.GetComponentsInChildren<Image>(true))
				{
					if (image == null || image.GetComponent<Button>() != null)
					{
						continue;
					}

					if (ShouldSkipPanelImage(image))
					{
						continue;
					}

					PanelVariant variant = ResolvePanelVariant(image.gameObject.name ?? string.Empty);
					ApplyStandardPanelTheme(image.gameObject, variant);
					panels++;
				}

				SanitizeMissingGlyphsInHierarchy(root, aggressiveGlyphCleanup);
				string logKey = root.GetInstanceID() + ":" + buttons + ":" + panels;
				if (RethemeLogs.Add(logKey))
				{
					AfterProhibitionUIPlugin.Log?.LogInfo("UITheme rethemed root=" + root.name + " buttons=" + buttons + " panels=" + panels + " owner=AfterProhibitionUI");
				}
				return true;
			}
			catch (Exception ex)
			{
				AfterProhibitionUIPlugin.Log?.LogWarning("UITheme bridge failed root=" + root.name + ": " + ex.Message);
				return false;
			}
		}

		private static ButtonVariant ResolveButtonVariant(string controlName)
		{
			if (string.IsNullOrEmpty(controlName))
			{
				return ButtonVariant.Default;
			}

			string text = controlName.ToLowerInvariant();
			if (text.Contains("close"))
			{
				return ButtonVariant.Close;
			}
			if (text.Contains("deny") || text.Contains("delete") || text.Contains("clear") || text.Contains("remove") || text.Contains("decline") || text.Contains("disappear"))
			{
				return ButtonVariant.Danger;
			}
			if (text.Contains("accept") || text.Contains("save") || text.Contains("confirm") || text.Contains("collect") || text.Contains("take") || text.Contains("hire") || text.Contains("join") || text.Contains("create"))
			{
				return ButtonVariant.Success;
			}
			if (text.Contains("toggle") || text.Contains("tier") || text.Contains("verify") || text.Contains("vote"))
			{
				return ButtonVariant.Accent;
			}

			return ButtonVariant.Default;
		}

		private static void ApplyStandardButtonTheme(Button button, ButtonVariant variant)
		{
			Image image = button.GetComponent<Image>();
			if (image == null)
			{
				return;
			}

			Color color = ButtonDarkGray;
			switch (variant)
			{
			case ButtonVariant.Accent:
				color = new Color(0.24f, 0.26f, 0.32f, 0.98f);
				break;
			case ButtonVariant.Success:
				color = new Color(0.23f, 0.29f, 0.24f, 0.98f);
				break;
			case ButtonVariant.Danger:
				color = new Color(0.31f, 0.23f, 0.24f, 0.98f);
				break;
			case ButtonVariant.Close:
				color = new Color(0.3f, 0.22f, 0.23f, 0.99f);
				break;
			}

			image.color = color;
			ColorBlock colors = button.colors;
			colors.normalColor = color;
			colors.highlightedColor = Color.Lerp(color, Color.white, 0.16f);
			colors.pressedColor = Color.Lerp(color, Color.black, 0.24f);
			colors.selectedColor = colors.highlightedColor;
			colors.disabledColor = ButtonDarkGrayDisabled;
			button.colors = colors;

			Outline outline = button.GetComponent<Outline>();
			if (outline == null)
			{
				outline = button.gameObject.AddComponent<Outline>();
			}
			outline.effectColor = new Color(0.82f, 0.86f, 0.92f, 0.42f);
			outline.effectDistance = new Vector2(1f, -1f);

			Text text = button.GetComponentInChildren<Text>();
			if (text != null)
			{
				text.color = (variant == ButtonVariant.Danger || variant == ButtonVariant.Close)
					? new Color(0.99f, 0.94f, 0.93f)
					: new Color(0.96f, 0.96f, 0.95f);
			}
			TMP_Text tmpText = button.GetComponentInChildren<TMP_Text>();
			if (tmpText != null)
			{
				tmpText.color = (variant == ButtonVariant.Danger || variant == ButtonVariant.Close)
					? new Color(0.99f, 0.94f, 0.93f)
					: new Color(0.96f, 0.96f, 0.95f);
			}
		}

		private static bool ShouldSkipPanelImage(Image image)
		{
			if (image == null)
			{
				return true;
			}

			string name = image.gameObject.name ?? string.Empty;
			if (image.color.a <= 0.02f)
			{
				return true;
			}

			return name.IndexOf("Fill", StringComparison.OrdinalIgnoreCase) >= 0
				|| name.IndexOf("Bar", StringComparison.OrdinalIgnoreCase) >= 0
				|| name.IndexOf("Swatch", StringComparison.OrdinalIgnoreCase) >= 0
				|| name.IndexOf("Icon", StringComparison.OrdinalIgnoreCase) >= 0
				|| name.IndexOf("Portrait", StringComparison.OrdinalIgnoreCase) >= 0
				|| name.IndexOf("Mugshot", StringComparison.OrdinalIgnoreCase) >= 0
				|| name.IndexOf("Preview", StringComparison.OrdinalIgnoreCase) >= 0
				|| name.IndexOf("Logo", StringComparison.OrdinalIgnoreCase) >= 0
				|| name.IndexOf("Stamp", StringComparison.OrdinalIgnoreCase) >= 0
				|| name.IndexOf("Pattern", StringComparison.OrdinalIgnoreCase) >= 0
				|| name.IndexOf("Deco", StringComparison.OrdinalIgnoreCase) >= 0
				|| name.IndexOf("Sprite", StringComparison.OrdinalIgnoreCase) >= 0
				|| name.IndexOf("Map", StringComparison.OrdinalIgnoreCase) >= 0
				|| IsLikelyAssetImage(image, name);
		}

		private static bool IsLikelyAssetImage(Image image, string name)
		{
			if (image.sprite == null)
			{
				return false;
			}

			bool panelLikeName = name.IndexOf("BG", StringComparison.OrdinalIgnoreCase) >= 0
				|| name.IndexOf("Background", StringComparison.OrdinalIgnoreCase) >= 0
				|| name.IndexOf("Panel", StringComparison.OrdinalIgnoreCase) >= 0
				|| name.IndexOf("Header", StringComparison.OrdinalIgnoreCase) >= 0
				|| name.IndexOf("Tab", StringComparison.OrdinalIgnoreCase) >= 0
				|| name.Equals("Bg", StringComparison.OrdinalIgnoreCase);
			if (panelLikeName)
			{
				return false;
			}

			RectTransform rect = image.transform as RectTransform;
			return rect == null || rect.rect.width > 18f || rect.rect.height > 18f;
		}

		private static PanelVariant ResolvePanelVariant(string name)
		{
			if (name.IndexOf("Header", StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("Tab", StringComparison.OrdinalIgnoreCase) >= 0)
			{
				return PanelVariant.Header;
			}
			if (name.IndexOf("Popup", StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("Panel", StringComparison.OrdinalIgnoreCase) >= 0 || name == "Bg")
			{
				return PanelVariant.Default;
			}

			return PanelVariant.Subtle;
		}

		private static void ApplyStandardPanelTheme(GameObject panel, PanelVariant variant)
		{
			if (panel == null)
			{
				return;
			}

			Image image = panel.GetComponent<Image>();
			if (image != null)
			{
				Color color = variant == PanelVariant.Header
					? PanelDarkGrayHeader
					: (variant == PanelVariant.Subtle ? PanelDarkGraySubtle : PanelDarkGray);
				color.a = Mathf.Clamp(image.color.a, 0.78f, 0.99f);
				image.color = color;
			}

			Outline outline = panel.GetComponent<Outline>();
			if (outline == null)
			{
				outline = panel.AddComponent<Outline>();
			}
			outline.effectColor = new Color(0.72f, 0.76f, 0.84f, 0.36f);
			outline.effectDistance = new Vector2(1f, -1f);

			Shadow shadow = panel.GetComponent<Shadow>();
			if (shadow == null)
			{
				shadow = panel.AddComponent<Shadow>();
			}
			shadow.effectColor = new Color(0f, 0f, 0f, 0.42f);
			shadow.effectDistance = new Vector2(2f, -2f);
		}

		private static void SanitizeMissingGlyphsInHierarchy(GameObject root, bool aggressive)
		{
			if (root == null)
			{
				return;
			}

			foreach (Text text in root.GetComponentsInChildren<Text>(true))
			{
				if (text == null)
				{
					continue;
				}

				string sanitized = SanitizeUiGlyphText(text.text, aggressive);
				if (!string.Equals(sanitized, text.text, StringComparison.Ordinal))
				{
					text.text = sanitized;
				}
			}

			foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
			{
				if (text == null)
				{
					continue;
				}

				string sanitized = SanitizeUiGlyphText(text.text, aggressive);
				if (!string.Equals(sanitized, text.text, StringComparison.Ordinal))
				{
					text.text = sanitized;
				}
			}
		}

		private static string SanitizeUiGlyphText(string raw, bool aggressive)
		{
			if (string.IsNullOrEmpty(raw))
			{
				return string.Empty;
			}

			string text = raw.Replace("\uFFFD", string.Empty);
			if (aggressive)
			{
				text = text.Replace("\u25A1", string.Empty);
			}
			return text;
		}
	}
}
