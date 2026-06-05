using System;
using System.Collections.Generic;
using System.IO;
using BepInEx.Logging;

namespace AfterProhibitionAssets
{
	internal static class ImageManifest
	{
		internal static readonly string ManifestFileName = "images.txt";

		internal static List<ImageManifestEntry> Load(string pluginDirectory, ManualLogSource log)
		{
			var entries = new List<ImageManifestEntry>();
			string manifestPath = Path.Combine(pluginDirectory, ManifestFileName);

			if (!File.Exists(manifestPath))
			{
				log?.LogWarning("Image manifest not found: " + manifestPath);
				return entries;
			}

			string[] lines;
			try
			{
				lines = File.ReadAllLines(manifestPath);
			}
			catch (Exception ex)
			{
				log?.LogWarning("Could not read image manifest '" + manifestPath + "': " + ex.Message);
				return entries;
			}

			var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			for (int i = 0; i < lines.Length; i++)
			{
				int lineNumber = i + 1;
				string line = StripInlineComment(lines[i]).Trim();

				if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
				{
					continue;
				}

				int separator = line.IndexOf('=');
				if (separator <= 0 || separator == line.Length - 1)
				{
					log?.LogWarning("Invalid image manifest line " + lineNumber + ": " + line);
					continue;
				}

				string key = line.Substring(0, separator).Trim();
				string relativePath = line.Substring(separator + 1).Trim();

				if (key.Length == 0 || relativePath.Length == 0)
				{
					log?.LogWarning("Invalid image manifest line " + lineNumber + ": key and path are required.");
					continue;
				}

				if (!seenKeys.Add(key))
				{
					log?.LogWarning("Duplicate image manifest key skipped at line " + lineNumber + ": " + key);
					continue;
				}

				entries.Add(new ImageManifestEntry(key, relativePath, lineNumber));
			}

			log?.LogInfo("Image manifest loaded. entries=" + entries.Count + " path=" + manifestPath);
			return entries;
		}

		private static string StripInlineComment(string line)
		{
			if (string.IsNullOrEmpty(line))
			{
				return line;
			}

			for (int i = 0; i < line.Length; i++)
			{
				if (line[i] == '#' && (i == 0 || char.IsWhiteSpace(line[i - 1])))
				{
					return line.Substring(0, i);
				}
			}

			return line;
		}
	}

	internal sealed class ImageManifestEntry
	{
		internal ImageManifestEntry(string key, string relativePath, int lineNumber)
		{
			Key = key;
			RelativePath = relativePath;
			LineNumber = lineNumber;
		}

		internal string Key { get; private set; }

		internal string RelativePath { get; private set; }

		internal int LineNumber { get; private set; }
	}
}
