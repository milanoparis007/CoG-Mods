namespace Game.Services;

public sealed class DiscordService : IUpdateService, IService
{
	public static string DEFAULT_IMAGE = "discord_square";

	public static long DISCORD_CLIENTID = 692230816667009025L;

	public IDiscordHandler handler;

	public bool IsLoadingDone => true;

	public bool IsUsingDiscord => !Game.settings.IsEditor;

	public void OnCreated()
	{
	}

	public void OnStartLoading()
	{
	}

	public void OnLoaded()
	{
	}

	public void OnInitialized()
	{
		IDiscordHandler discordHandler2;
		if (!IsUsingDiscord)
		{
			IDiscordHandler discordHandler = new DiscordHandlerNoOp();
			discordHandler2 = discordHandler;
		}
		else
		{
			IDiscordHandler discordHandler = new DiscordHandlerImpl();
			discordHandler2 = discordHandler;
		}
		handler = discordHandler2;
		handler.Connect();
	}

	public void OnReleased()
	{
		handler.Disconnect();
		handler = null;
	}

	public void OnDestroyed()
	{
	}

	public void OnUpdate()
	{
		handler.Update();
	}

	public void Clear()
	{
		handler.Clear();
	}

	public void Post(string gang, string date, string city)
	{
		string state = city + ", " + date;
		handler.Post(state, gang, DEFAULT_IMAGE, "");
	}
}
