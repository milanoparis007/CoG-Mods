namespace Game.Services;

public interface IDiscordHandler
{
	void Clear();

	void Connect();

	void Disconnect();

	void Post(string state, string details, string imgname, string imgtext);

	void Update();
}
