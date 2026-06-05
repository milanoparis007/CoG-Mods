using System;
using System.Collections;
using System.Linq;
using System.Reflection;

namespace AfterProhibitionAssets
{
	public static class CoGCustomAssetsBridge
	{
		private const string AssemblyName = "CoGCustomAssets";

		private const string PluginTypeName = "CoGCustomAssets.Plugin";

		private static string _lastStatusKey;

		public static bool IsDetected { get; private set; }

		public static bool IsActive { get; private set; }

		public static string PluginFolder { get; private set; }

		public static bool ReleaseMode { get; private set; }

		public static int ManifestEntryCount { get; private set; }

		public static int UiAssetCount { get; private set; }

		public static int MainMenuAssetCount { get; private set; }

		public static bool Detect()
		{
			Reset();

			try
			{
				Assembly assembly = AppDomain.CurrentDomain.GetAssemblies()
					.FirstOrDefault(a => string.Equals(a.GetName().Name, AssemblyName, StringComparison.OrdinalIgnoreCase));
				if (assembly == null)
				{
					return false;
				}

				IsDetected = true;
				Type pluginType = assembly.GetType(PluginTypeName, throwOnError: false);
				if (pluginType == null)
				{
					return true;
				}

				PluginFolder = GetStaticProperty<string>(pluginType, "PluginFolder");
				ReleaseMode = GetStaticProperty<bool>(pluginType, "ReleaseMode");
				object manifest = GetStaticProperty<object>(pluginType, "Manifest");
				object uiLoader = GetStaticProperty<object>(pluginType, "UI");

				ManifestEntryCount = CountEnumerableProperty(manifest, "Entries");
				UiAssetCount = GetInstanceProperty<int>(uiLoader, "TotalCount");
				MainMenuAssetCount = CountDictionaryProperty(uiLoader, "MainMenu");
				IsActive = manifest != null || uiLoader != null || ManifestEntryCount > 0 || UiAssetCount > 0;
				return true;
			}
			catch (Exception ex)
			{
				AfterProhibitionAssetsPlugin.Log?.LogWarning("CoGCustomAssets bridge detection failed: " + ex.Message);
				return false;
			}
		}

		internal static void DetectAndLog(string source)
		{
			Detect();

			string statusKey = IsDetected + "|" + IsActive + "|" + ManifestEntryCount + "|" + UiAssetCount + "|" + MainMenuAssetCount;
			if (string.Equals(_lastStatusKey, statusKey, StringComparison.Ordinal))
			{
				return;
			}

			_lastStatusKey = statusKey;
			if (!IsDetected)
			{
				AfterProhibitionAssetsPlugin.Log?.LogInfo("CoGCustomAssets bridge source=" + source + " status=unavailable");
				return;
			}

			AfterProhibitionAssetsPlugin.Log?.LogInfo(
				"CoGCustomAssets bridge source=" + source
				+ " status=" + (IsActive ? "active" : "detected")
				+ " releaseMode=" + ReleaseMode
				+ " manifestEntries=" + ManifestEntryCount
				+ " uiAssets=" + UiAssetCount
				+ " mainMenuAssets=" + MainMenuAssetCount
				+ " folder=" + (PluginFolder ?? "<unknown>"));
		}

		private static void Reset()
		{
			IsDetected = false;
			IsActive = false;
			PluginFolder = null;
			ReleaseMode = false;
			ManifestEntryCount = 0;
			UiAssetCount = 0;
			MainMenuAssetCount = 0;
		}

		private static T GetStaticProperty<T>(Type type, string propertyName)
		{
			if (type == null)
			{
				return default(T);
			}

			PropertyInfo property = type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Static);
			if (property == null)
			{
				return default(T);
			}

			object value = property.GetValue(null, null);
			return value is T typed ? typed : default(T);
		}

		private static T GetInstanceProperty<T>(object instance, string propertyName)
		{
			if (instance == null)
			{
				return default(T);
			}

			PropertyInfo property = instance.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
			if (property == null)
			{
				return default(T);
			}

			object value = property.GetValue(instance, null);
			return value is T typed ? typed : default(T);
		}

		private static int CountEnumerableProperty(object instance, string propertyName)
		{
			object value = GetInstanceProperty<object>(instance, propertyName);
			ICollection collection = value as ICollection;
			if (collection != null)
			{
				return collection.Count;
			}

			IEnumerable enumerable = value as IEnumerable;
			if (enumerable == null)
			{
				return 0;
			}

			int count = 0;
			foreach (object _ in enumerable)
			{
				count++;
			}

			return count;
		}

		private static int CountDictionaryProperty(object instance, string propertyName)
		{
			object value = GetInstanceProperty<object>(instance, propertyName);
			IDictionary dictionary = value as IDictionary;
			return dictionary == null ? 0 : dictionary.Count;
		}
	}
}
