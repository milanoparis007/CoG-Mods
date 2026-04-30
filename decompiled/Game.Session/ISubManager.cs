namespace Game.Session;

public interface ISubManager<TParentManager> where TParentManager : ISessionManager
{
	void Initialize(TParentManager manager);

	void Release();
}
