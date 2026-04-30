namespace Game.Session;

public interface IPlayerTurnHandler : ISessionManager
{
	void OnPlayerTurnStarted();

	bool IsPlayerTurnDone();

	void OnPlayerTurnEnded();
}
