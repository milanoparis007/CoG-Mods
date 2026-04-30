namespace Game.Services.Input;

internal abstract class InputSource
{
	protected InputService _service;

	public virtual void Initialize(InputService service)
	{
		_service = service;
	}

	public virtual void Release()
	{
		_service = null;
	}

	public abstract bool Update();
}
