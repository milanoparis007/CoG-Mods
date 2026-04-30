namespace Game.Services;

public class DiscordHandlerNoOp : IDiscordHandler
{
	public virtual void Connect()
	{
	}

	public virtual void Disconnect()
	{
	}

	public virtual void Clear()
	{
	}

	public virtual void Post(string state, string details, string imgname, string imgtext)
	{
	}

	public virtual void Update()
	{
	}
}
