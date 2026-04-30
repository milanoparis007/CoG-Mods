using System;
using System.Collections;
using Game.Platform;
using Game.Services;
using Game.Session;
using UnityEngine;

namespace Game;

public class Game : MonoBehaviour
{
	public static Game instance;

	public static GameSettings settings;

	public static ServiceContext serv;

	public static SessionContext ctx;

	public static BasePlatform platform;

	private void Awake()
	{
		Logger.InitializeLogLevels(debug: false);
		Logger.LogAlways("STARTING GAME");
	}

	private void Start()
	{
		instance = this;
		settings = new GameSettings();
		platform = CreatePlatform();
		serv = new ServiceContext();
		ctx = null;
		Logger.LogAlways("BUILD", GameSettings.GetVersionString(), " - PLATFORM ", platform.GetType().Name);
		StartCoroutine(InitializeEverything());
	}

	private IEnumerator InitializeEverything()
	{
		yield return platform.Initialize();
		yield return null;
		serv.Initialize();
		yield return null;
		yield return null;
		if (platform.LoadPrefsAfterEngagement)
		{
			serv.saveload.LoadPrefsOnStartup();
		}
	}

	private void Update()
	{
		if (platform != null)
		{
			platform.Update();
		}
		if (serv != null)
		{
			serv.Update();
		}
		if (ctx != null)
		{
			ctx.Update();
		}
	}

	private void LateUpdate()
	{
		if (ctx != null)
		{
			serv.LateUpdate();
		}
	}

	private BasePlatform CreatePlatform()
	{
		return new DesktopPlatform();
	}

	public void LogPlatformsStats()
	{
		Logger.LogAlways(GetPlatformStats());
	}

	public string GetPlatformStats()
	{
		return $"Graphics model: {SystemInfo.graphicsDeviceType}, shader model {SystemInfo.graphicsShaderLevel}, {SystemInfo.graphicsMemorySize} vram; \n" + $"Compute shaders: {SystemInfo.supportsComputeShaders}, max compute {SystemInfo.maxComputeBufferInputsCompute}, work group size {SystemInfo.maxComputeWorkGroupSize} / " + $"x {SystemInfo.maxComputeWorkGroupSizeX} / y {SystemInfo.maxComputeWorkGroupSizeY} / z {SystemInfo.maxComputeWorkGroupSizeZ}; \n" + $"Device: {SystemInfo.graphicsDeviceVersion}; {SystemInfo.graphicsDeviceID} {SystemInfo.graphicsDeviceName}; {SystemInfo.graphicsDeviceVendorID} {SystemInfo.graphicsDeviceVendor}; \n" + $"Screen size: {Screen.width} x {Screen.height}, {Screen.dpi} dpi, {serv?.ui?.UIScaleFactor} % ui scale, fullscreen = {Screen.fullScreen}";
	}

	public void ReclaimMemoryAsync(bool waitForGC = false)
	{
		TimerUtil.RunNextFrame(delegate
		{
			GC.Collect();
			Resources.UnloadUnusedAssets();
			if (waitForGC)
			{
				GC.WaitForPendingFinalizers();
			}
		});
	}

	public void Quit()
	{
		serv.screens.PopAll();
		serv.Release();
		Application.Quit();
	}

	private void OnDestroy()
	{
		ctx = null;
		serv = null;
		settings = null;
		platform = null;
	}
}
