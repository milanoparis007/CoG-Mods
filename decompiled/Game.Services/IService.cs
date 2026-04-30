namespace Game.Services;

public interface IService
{
	bool IsLoadingDone { get; }

	void OnCreated();

	void OnStartLoading();

	void OnLoaded();

	void OnInitialized();

	void OnReleased();

	void OnDestroyed();
}
