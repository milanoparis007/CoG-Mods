namespace Game.Services;

public static class PresenceUtil
{
	public static void Set(string gang, string date, string city)
	{
		Game.serv.discord.Post(gang, date, city);
		Game.serv.store.handler.SetPresence(gang, date, city);
	}

	public static void Clear()
	{
		Game.serv.discord.Clear();
		Game.serv.store.handler.ClearPresence();
	}
}
