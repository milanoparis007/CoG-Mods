namespace Game.Session.Entities;

public abstract class BaseComponent
{
	protected Entity _entity;

	protected BaseConfig _baseConfig;

	public Entity entity => _entity;

	public virtual void OnAfterComponentCreated(Entity entity, BaseConfig baseconfig)
	{
		_entity = entity;
		_baseConfig = baseconfig;
	}

	public virtual void OnAfterEntityCreated(bool loaded)
	{
	}

	public virtual void OnBeforeEntityDestroyed(bool shutdown)
	{
	}

	public virtual void OnBeforeComponentRemoved()
	{
		_baseConfig = null;
		_entity = null;
	}
}
