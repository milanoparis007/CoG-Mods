namespace Game.Session.Entities;

public interface ISaveObserverComponent
{
	void OnBeforeSaving();

	void OnAfterSaving();
}
