using System.Collections.Generic;

namespace Game.Services.Filesystem;

public sealed class PreferencesFile
{
	public GamePreferences game = new GamePreferences();

	public List<string> enabledmods = new List<string>();
}
