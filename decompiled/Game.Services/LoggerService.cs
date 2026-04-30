using System;
using Game.UI.Session.Popups;

namespace Game.Services;

public class LoggerService : AbstractService
{
	public LoggerLevel Level = LoggerLevel.WARNING;

	private bool _showing;

	public bool ModdingLogEnabled { get; set; }

	public override void OnInitialized()
	{
		Logger.OnWarningCallback = (Action<object>)Delegate.Combine(Logger.OnWarningCallback, new Action<object>(LoggerCallbacks.DisplayWarning));
		Logger.OnErrorCallback = (Action<object>)Delegate.Combine(Logger.OnErrorCallback, new Action<object>(LoggerCallbacks.DisplayError));
	}

	public override void OnReleased()
	{
		Logger.OnWarningCallback = (Action<object>)Delegate.Remove(Logger.OnWarningCallback, new Action<object>(LoggerCallbacks.DisplayWarning));
		Logger.OnErrorCallback = (Action<object>)Delegate.Remove(Logger.OnErrorCallback, new Action<object>(LoggerCallbacks.DisplayError));
	}

	public void ShowInUI(LoggerLevel level, string message)
	{
		if (Game.serv.ui != null && Loc.instance != null && Game.serv.ui.IsLoadingDone && level >= Level && ModdingLogEnabled && !_showing)
		{
			_showing = true;
			OkPopup.Show(message);
			_showing = false;
		}
	}
}
