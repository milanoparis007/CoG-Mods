using System.Collections.Generic;

namespace Game.Session.Player;

public abstract class PlayerCache<T>
{
	protected PlayerInfo _manager;

	protected List<T> _elements;

	public void Initialize(PlayerInfo manager)
	{
		_manager = manager;
		_elements = new List<T>();
	}

	public void Release()
	{
		_elements = null;
		_manager = null;
	}

	public List<T> GetElementsUnsafe()
	{
		return _elements;
	}

	protected abstract void RefreshCache();
}
