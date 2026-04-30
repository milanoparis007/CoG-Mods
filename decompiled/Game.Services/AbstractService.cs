namespace Game.Services;

public abstract class AbstractService : IService
{
	public virtual bool IsLoadingDone => true;

	public virtual void OnCreated()
	{
	}

	public virtual void OnStartLoading()
	{
	}

	public virtual void OnLoaded()
	{
	}

	public virtual void OnInitialized()
	{
	}

	public virtual void OnReleased()
	{
	}

	public virtual void OnDestroyed()
	{
	}
}
