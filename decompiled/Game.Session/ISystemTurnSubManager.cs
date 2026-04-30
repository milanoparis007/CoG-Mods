namespace Game.Session;

public interface ISystemTurnSubManager<TParentManager> : ISubManager<TParentManager> where TParentManager : ISessionManager
{
	void OnSystemTurn();
}
