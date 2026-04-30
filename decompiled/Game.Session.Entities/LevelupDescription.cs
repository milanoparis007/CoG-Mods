using Game.Services;

namespace Game.Session.Entities;

public class LevelupDescription
{
	public LevelupChain levelup;

	public string message;

	public string icon;

	public int currentLevel;

	public int nextLevel;
}
