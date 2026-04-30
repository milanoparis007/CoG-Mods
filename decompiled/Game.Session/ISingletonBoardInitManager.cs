namespace Game.Session;

public interface ISingletonBoardInitManager : ISessionManager, ICityGenManager
{
	bool IsBoardInitDone { get; }

	bool IsCityGenDone { get; }

	void OnBoardInitStarted();

	void OnBoardInitDone();
}
