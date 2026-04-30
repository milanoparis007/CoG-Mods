namespace Game.Session;

public interface ISessionManager
{
	bool IsInitializingDone { get; }

	void OnInitializeStarted();

	void OnInitializeDone();

	void OnPreInteractive();

	void OnPreInteractiveAIGen();

	void OnInteractive();

	void OnPreRelease();

	void OnReleased();

	void OnDestroyed();
}
