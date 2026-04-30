using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Game.Services.Filesystem;
using SomaSim.Analytics;
using UnityEngine;

namespace Game.Services;

public class AnalyticsService : AbstractService, IUpdateService, IService
{
	private enum State
	{
		Pending,
		Enabled,
		Disabled
	}

	private struct Entry
	{
		public ALogTarget target;

		public string[] arguments;
	}

	private GAProvider _glog;

	private UAProvider _ulog;

	private UserStats _user;

	private int _userid;

	private State _state;

	private Queue<Entry> _buffer = new Queue<Entry>();

	private readonly int _bufferMaxCount = 20;

	private static string _lasterror = "";

	public override void OnInitialized()
	{
		_user = Game.serv.saveload.progress.stats;
		_userid = ((_user.uuid != null) ? _user.uuid.GetHashCode() : 0);
	}

	public override void OnReleased()
	{
		if (_glog != null)
		{
			_glog.Release();
			_glog = null;
		}
		if (_ulog != null)
		{
			_ulog.Release();
			_ulog = null;
		}
		_user = null;
	}

	public void OnUpdate()
	{
		if (_state == State.Pending)
		{
			MaybeDisable();
		}
		if (_state == State.Pending)
		{
			TryFindOutState();
		}
		if (_ulog != null && _glog != null && _state == State.Enabled && _buffer.Count > 0)
		{
			LogImpl(_buffer.Dequeue());
		}
		void LogImpl(Entry entry)
		{
			bool num = entry.target == ALogTarget.Any || entry.target == ALogTarget.GAOnly;
			bool flag = entry.target == ALogTarget.Any || entry.target == ALogTarget.UAOnly;
			if (num)
			{
				_glog.LogEvent(entry.arguments);
			}
			if (flag)
			{
				_ulog.LogEvent(entry.arguments);
			}
		}
	}

	private void MaybeDisable()
	{
		GameSettings settings = Game.settings;
		if (settings.IsEditor && !settings.TestLogging)
		{
			_state = State.Disabled;
		}
		if (settings.IsShowFloor)
		{
			Logger.LogAlways("Show floor build, analytics disabled");
			_state = State.Disabled;
		}
		if (!settings.TestLogging && !settings.IsLoggingBuild && _user.session > 1)
		{
			_state = State.Disabled;
		}
	}

	private void TryFindOutState()
	{
		RemoteSettingsService remotesettings = Game.serv.remotesettings;
		switch (remotesettings.state)
		{
		case RemoteSettingsService.State.NotStarted:
			remotesettings.Start();
			break;
		case RemoteSettingsService.State.Success:
			if (remotesettings.settings.isValidForPlayer(_userid))
			{
				InitializeLog();
				_state = State.Enabled;
			}
			else
			{
				_state = State.Disabled;
			}
			break;
		default:
			_state = State.Disabled;
			break;
		case RemoteSettingsService.State.Loading:
			break;
		}
	}

	public void LogError(params string[] arguments)
	{
		if (_state == State.Enabled && Game.serv.remotesettings.settings.logerrors)
		{
			string text = string.Join("/", arguments);
			if (!(text == _lasterror))
			{
				_lasterror = text;
				Log(ALogTarget.UAOnly, ALogType.SteamOnly, arguments);
			}
		}
	}

	public void LogEvent(params object[] arguments)
	{
		IEnumerable<string> source = arguments.Select((object o) => string.Format(CultureInfo.InvariantCulture, "{0}", o));
		Log(ALogTarget.UAOnly, ALogType.SteamOnly, source.ToArray());
	}

	public void Log(ALogTarget target, ALogType type, params string[] arguments)
	{
		if (_state != State.Disabled && Game.settings.IsDesktop && (Game.settings.TestLogging || type == ALogType.SteamOnly || type == ALogType.AllPlatforms) && _buffer.Count < _bufferMaxCount)
		{
			Entry item = new Entry
			{
				target = target,
				arguments = arguments
			};
			_buffer.Enqueue(item);
		}
	}

	public void LogException(Exception ex)
	{
		Debug.LogException(ex);
	}

	public void LogException(string message)
	{
		Debug.LogException(new Exception(message));
	}

	private void InitializeLog()
	{
		_glog = new GAProvider("UA-169203907-2", _user.uuid, Game.instance);
		_glog.Initialize();
		_ulog = new UAProvider();
		_ulog.Initialize();
		string versionAndBuildString = GameSettings.GetVersionAndBuildString();
		if (_user.session == 1)
		{
			Log(ALogTarget.Any, ALogType.AllPlatforms, "install", versionAndBuildString, _user.uuid);
		}
		Log(ALogTarget.UAOnly, ALogType.SteamOnly, "startup_platform", "STEAM");
		Log(ALogTarget.UAOnly, ALogType.SteamOnly, "startup_build", GameSettings.GetPlatformAndBuildForLogging());
		Log(ALogTarget.UAOnly, ALogType.SteamOnly, "startup_build", versionAndBuildString);
		Log(ALogTarget.UAOnly, ALogType.SteamOnly, "startup_session", _user.session.ToString());
		Log(ALogTarget.UAOnly, ALogType.SteamOnly, "startup_os", SystemInfo.operatingSystem);
		Log(ALogTarget.UAOnly, ALogType.SteamOnly, "startup_screen", Screen.width + "x" + Screen.height);
		Log(ALogTarget.UAOnly, ALogType.SteamOnly, "startup_gpu", SystemInfo.graphicsDeviceName);
		Log(ALogTarget.UAOnly, ALogType.SteamOnly, "startup_gpuram", SystemInfo.graphicsMemorySize.ToString());
		Log(ALogTarget.UAOnly, ALogType.SteamOnly, "startup_sysram", SystemInfo.systemMemorySize.ToString());
	}
}
