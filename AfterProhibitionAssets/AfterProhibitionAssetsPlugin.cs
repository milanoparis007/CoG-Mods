using System;
using System.Collections;
using System.IO;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

namespace AfterProhibitionAssets
{
	[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
	public sealed class AfterProhibitionAssetsPlugin : BaseUnityPlugin
	{
		public const string PluginGuid = "afterprohibition.assets";
		public const string PluginName = "After Prohibition Assets";
		public const string PluginVersion = "0.1.4";

		internal static ManualLogSource Log { get; private set; }

		internal static AfterProhibitionAssetsPlugin Instance { get; private set; }

		internal static string PluginDirectory { get; private set; }

		internal static string ImagesDirectory { get; private set; }

		private Harmony _harmony;

		private void Awake()
		{
			Instance = this;
			Log = Logger;
			PluginDirectory = Path.Combine(Paths.PluginPath, "AfterProhibitionAssets");
			ImagesDirectory = Path.Combine(PluginDirectory, "images");

			EnsureDirectory(PluginDirectory);
			EnsureDirectory(ImagesDirectory);

			AssetRegistry.Reload(PluginDirectory, Logger);
			Logger.LogInfo("Asset API ready. isReady=" + AssetRegistry.IsReady + " count=" + AssetRegistry.Count);
			CoGCustomAssetsBridge.DetectAndLog("awake");

			_harmony = new Harmony(PluginGuid);
			_harmony.PatchAll();

			Logger.LogInfo($"{PluginName} {PluginVersion} loaded. assetFolder={PluginDirectory}");
		}

		private IEnumerator Start()
		{
			yield return null;
			CoGCustomAssetsBridge.DetectAndLog("start-frame");
			yield return new UnityEngine.WaitForSecondsRealtime(1f);
			CoGCustomAssetsBridge.DetectAndLog("start-1s");
		}

		private void OnDestroy()
		{
			try
			{
				_harmony?.UnpatchSelf();
			}
			catch (Exception ex)
			{
				Logger.LogWarning("Could not unpatch Harmony hooks: " + ex.Message);
			}

			if (ReferenceEquals(Instance, this))
			{
				Instance = null;
			}
		}

		internal static void StartDeferredWork(IEnumerator routine)
		{
			Instance?.StartCoroutine(routine);
		}

		private static void EnsureDirectory(string path)
		{
			try
			{
				if (!Directory.Exists(path))
				{
					Directory.CreateDirectory(path);
				}
			}
			catch (Exception ex)
			{
				Log?.LogWarning("Could not create asset directory '" + path + "': " + ex.Message);
			}
		}
	}
}
