namespace Game.Session.Entities;

public interface IEntityEventObserverComponent
{
	void OnEntityEvent(EntityEventType eet);
}
