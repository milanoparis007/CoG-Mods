namespace Game.Session.Player;

internal interface ITurnHandlingPlayerSubmanager
{
	void OnGlobalTurnSetAdvanced();

	void OnPlayerTurnStarted();

	void OnPlayerTurnEnded();

	PlayerTurnStatus GetPlayerTurnStatus();
}
