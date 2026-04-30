namespace Game.Services;

internal static class LoggerCallbacks
{
	public static void DisplayWarning(object message)
	{
		if (Game.serv != null)
		{
			if (Game.serv.log != null)
			{
				Game.serv.log.ShowInUI(LoggerLevel.WARNING, "Warning: " + message);
			}
			if (Game.serv.stats != null)
			{
				Game.serv.stats.LogError("trace", "warning", GameSettings.GetVersionAndBuildString(), message.ToString());
			}
		}
	}

	public static void DisplayError(object message)
	{
		if (Game.serv != null)
		{
			if (Game.serv.log != null)
			{
				Game.serv.log.ShowInUI(LoggerLevel.ERROR, "ERROR: " + message);
			}
			if (Game.serv.stats != null)
			{
				Game.serv.stats.LogError("trace", "error", GameSettings.GetVersionAndBuildString(), message.ToString());
			}
		}
	}
}
