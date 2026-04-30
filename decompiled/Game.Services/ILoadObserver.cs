namespace Game.Services;

public interface ILoadObserver
{
	void OnAfterManagerLoad();

	void OnAfterEntityLoad();
}
