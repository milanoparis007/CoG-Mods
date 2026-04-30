namespace Game.Session;

public interface ISystemTurnHandler : ISessionManager
{
	void OnSystemTurn();
}
