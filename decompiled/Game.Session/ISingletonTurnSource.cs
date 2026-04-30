namespace Game.Session;

public interface ISingletonTurnSource : ISessionManager
{
	bool AdvancePlayerOrSystem();

	void AdvanceGlobalGameTurn();
}
