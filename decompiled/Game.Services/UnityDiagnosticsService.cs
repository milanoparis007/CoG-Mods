using System.Globalization;
using UnityEngine;
using UnityEngine.CrashReportHandler;

namespace Game.Services;

public class UnityDiagnosticsService : AbstractService, IUpdateService, IService
{
	public bool _previous;

	public override void OnCreated()
	{
		SetPlatformMetadata();
	}

	public void OnUpdate()
	{
		bool flag = Game.ctx != null;
		if (_previous != flag)
		{
			_previous = flag;
			if (flag)
			{
				SetSessionMetadata();
			}
			else
			{
				ClearSessionMetadata();
			}
		}
	}

	private static void SetPlatformMetadata()
	{
		Set("!version", GameSettings.version);
		Set("!build", GameSettings.build);
		Set("culture info", CultureInfo.CurrentCulture.Name + " / " + CultureInfo.CurrentUICulture.Name);
		Set("graphics model", SystemInfo.graphicsDeviceType);
		Set("shader model", SystemInfo.graphicsShaderLevel);
		Set("compute shader", SystemInfo.supportsComputeShaders);
		Set("max compute", $"{SystemInfo.maxComputeBufferInputsCompute}, work group size {SystemInfo.maxComputeWorkGroupSize} / " + $"x {SystemInfo.maxComputeWorkGroupSizeX} / y {SystemInfo.maxComputeWorkGroupSizeY} / z {SystemInfo.maxComputeWorkGroupSizeZ}");
	}

	private static void SetSessionMetadata()
	{
		Set("!uid", Game.serv.saveload.progress.stats.uuid);
		Set("!session", Game.serv.saveload.progress.stats.session);
		Set("!citytype", Game.ctx.session.mapconfig.citytype);
		Set("!cityid", Game.ctx.session.mapconfig.id);
		Set("!rng", Game.ctx.scenario.rngseed);
	}

	private static void ClearSessionMetadata()
	{
		Clear("!uid");
		Clear("!session");
		Clear("!scenario");
		Clear("!rng");
	}

	private static void Set(string key, object val)
	{
		CrashReportHandler.SetUserMetadata(key, val?.ToString());
	}

	private static void Clear(string key)
	{
		CrashReportHandler.SetUserMetadata(key, null);
	}
}
