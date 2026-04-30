namespace Game.Session;

public interface IGlobalTurnSetHandler : ISessionManager
{
	void OnGlobalTurnSetAdvanced();
}
