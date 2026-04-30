namespace Game.Session;

public interface ICityGenManager : ISessionManager
{
	void OnCityGenStarted();

	void OnCityGenTurn();

	void OnCityGenDone();
}
