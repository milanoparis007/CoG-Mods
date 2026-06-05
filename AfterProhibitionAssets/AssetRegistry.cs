using System;
using System.Collections.Generic;
using System.IO;
using BepInEx.Logging;
using UnityEngine;

namespace AfterProhibitionAssets
{
	public static class AssetRegistry
	{
		private static readonly Dictionary<string, LoadedImageAsset> Images = new Dictionary<string, LoadedImageAsset>(StringComparer.OrdinalIgnoreCase);

		public static bool IsReady { get; private set; }

		public static int Count
		{
			get { return Images.Count; }
		}

		public static bool ContainsKey(string key)
		{
			return key != null && Images.ContainsKey(key);
		}

		public static string GetSourcePath(string key)
		{
			LoadedImageAsset asset;
			if (key != null && Images.TryGetValue(key, out asset))
			{
				return asset.SourcePath;
			}

			return null;
		}

		public static bool TryGetSprite(string key, out Sprite sprite)
		{
			LoadedImageAsset asset;
			if (IsReady && key != null && Images.TryGetValue(key, out asset))
			{
				sprite = asset.Sprite;
				return sprite != null;
			}

			sprite = null;
			return false;
		}

		public static bool TryGetTexture(string key, out Texture2D texture)
		{
			LoadedImageAsset asset;
			if (IsReady && key != null && Images.TryGetValue(key, out asset))
			{
				texture = asset.Texture;
				return texture != null;
			}

			texture = null;
			return false;
		}

		public static bool Reload()
		{
			if (string.IsNullOrEmpty(AfterProhibitionAssetsPlugin.PluginDirectory))
			{
				AfterProhibitionAssetsPlugin.Log?.LogWarning("Image registry reload requested before plugin directory is ready.");
				return false;
			}

			return ReloadInternal(AfterProhibitionAssetsPlugin.PluginDirectory, AfterProhibitionAssetsPlugin.Log);
		}

		internal static void Reload(string pluginDirectory, ManualLogSource log)
		{
			ReloadInternal(pluginDirectory, log);
		}

		private static bool ReloadInternal(string pluginDirectory, ManualLogSource log)
		{
			Images.Clear();
			IsReady = false;

			try
			{
				List<ImageManifestEntry> entries = ImageManifest.Load(pluginDirectory, log);
				foreach (ImageManifestEntry entry in entries)
				{
					LoadImage(pluginDirectory, entry, log);
				}

				IsReady = true;
				log?.LogInfo("Image registry ready. loaded=" + Images.Count + " entries=" + entries.Count);
				return true;
			}
			catch (Exception ex)
			{
				log?.LogWarning("Image registry reload failed: " + ex.Message);
				return false;
			}
		}

		private static void LoadImage(string pluginDirectory, ImageManifestEntry entry, ManualLogSource log)
		{
			if (entry.RelativePath.StartsWith("game:", StringComparison.OrdinalIgnoreCase))
			{
				LoadGameResource(entry, log);
				return;
			}

			string fullPath;
			if (!TryResolveRelativePath(pluginDirectory, entry.RelativePath, out fullPath))
			{
				log?.LogWarning("Image manifest line " + entry.LineNumber + " escapes the asset folder and was skipped: " + entry.RelativePath);
				return;
			}

			string extension = Path.GetExtension(fullPath);
			if (!string.Equals(extension, ".png", StringComparison.OrdinalIgnoreCase)
				&& !string.Equals(extension, ".jpg", StringComparison.OrdinalIgnoreCase)
				&& !string.Equals(extension, ".jpeg", StringComparison.OrdinalIgnoreCase))
			{
				log?.LogWarning("Image manifest line " + entry.LineNumber + " has unsupported image type: " + entry.RelativePath);
				return;
			}

			if (!File.Exists(fullPath))
			{
				log?.LogWarning("Image file not found for key '" + entry.Key + "': " + fullPath);
				return;
			}

			try
			{
				byte[] data = File.ReadAllBytes(fullPath);
				var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
				if (!ImageConversion.LoadImage(texture, data, false))
				{
					UnityEngine.Object.Destroy(texture);
					log?.LogWarning("Image file could not be decoded for key '" + entry.Key + "': " + fullPath);
					return;
				}

				texture.name = "AfterProhibitionAssets." + entry.Key;
				var rect = new Rect(0f, 0f, texture.width, texture.height);
				var pivot = new Vector2(0.5f, 0.5f);
				Sprite sprite = Sprite.Create(texture, rect, pivot);
				sprite.name = texture.name + ".sprite";

				Images[entry.Key] = new LoadedImageAsset(texture, sprite, fullPath);
				log?.LogInfo("Loaded image key='" + entry.Key + "' size=" + texture.width + "x" + texture.height + " path=" + fullPath);
			}
			catch (Exception ex)
			{
				log?.LogWarning("Could not load image for key '" + entry.Key + "': " + ex.Message);
			}
		}

		private static void LoadGameResource(ImageManifestEntry entry, ManualLogSource log)
		{
			string resourcePath = entry.RelativePath.Substring("game:".Length).Trim();
			if (resourcePath.Length == 0)
			{
				log?.LogWarning("Image manifest line " + entry.LineNumber + " has an empty game resource path.");
				return;
			}

			try
			{
				Sprite sprite = Resources.Load<Sprite>(resourcePath);
				if (sprite != null)
				{
					Texture2D texture = sprite.texture;
					Images[entry.Key] = new LoadedImageAsset(texture, sprite, "game:" + resourcePath);
					log?.LogInfo("Loaded game sprite key='" + entry.Key + "' path=game:" + resourcePath);
					return;
				}

				Texture2D texture2D = Resources.Load<Texture2D>(resourcePath);
				if (texture2D != null)
				{
					var rect = new Rect(0f, 0f, texture2D.width, texture2D.height);
					var pivot = new Vector2(0.5f, 0.5f);
					Sprite generatedSprite = Sprite.Create(texture2D, rect, pivot);
					generatedSprite.name = "AfterProhibitionAssets." + entry.Key + ".game.sprite";
					Images[entry.Key] = new LoadedImageAsset(texture2D, generatedSprite, "game:" + resourcePath);
					log?.LogInfo("Loaded game texture key='" + entry.Key + "' size=" + texture2D.width + "x" + texture2D.height + " path=game:" + resourcePath);
					return;
				}

				log?.LogWarning("Game resource not found for key '" + entry.Key + "': game:" + resourcePath);
			}
			catch (Exception ex)
			{
				log?.LogWarning("Could not load game resource for key '" + entry.Key + "': " + ex.Message);
			}
		}

		private static bool TryResolveRelativePath(string pluginDirectory, string relativePath, out string fullPath)
		{
			fullPath = null;
			if (Path.IsPathRooted(relativePath))
			{
				return false;
			}

			string root = Path.GetFullPath(pluginDirectory);
			string candidate = Path.GetFullPath(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
			string rootWithSeparator = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;

			if (!candidate.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
			{
				return false;
			}

			fullPath = candidate;
			return true;
		}
	}

	internal sealed class LoadedImageAsset
	{
		internal LoadedImageAsset(Texture2D texture, Sprite sprite, string sourcePath)
		{
			Texture = texture;
			Sprite = sprite;
			SourcePath = sourcePath;
		}

		internal Texture2D Texture { get; private set; }

		internal Sprite Sprite { get; private set; }

		internal string SourcePath { get; private set; }
	}
}
