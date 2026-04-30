using System.Collections.Generic;

namespace Game.Services.Filesystem;

public sealed class GameplayProgressFile
{
	public UserStats stats = new UserStats();

	public Dictionary<string, bool> hints = new Dictionary<string, bool>();

	public Dictionary<string, bool> announcements = new Dictionary<string, bool>();
}
