using Game.UI;
using SomaSim.Util;

namespace Game.Services;

public sealed class GameScreenService : SmartStack<GSBase>, IUpdateService, IService
{
	public bool IsLoadingDone => true;

	public void OnCreated()
	{
	}

	public void OnStartLoading()
	{
	}

	public void OnLoaded()
	{
	}

	public void OnInitialized()
	{
	}

	public void PopAll()
	{
		while (base.Count > 0)
		{
			Pop();
		}
	}

	public void OnReleased()
	{
		if (base.Count > 0)
		{
			PopAll();
		}
	}

	public void OnDestroyed()
	{
	}

	public void OnUpdate()
	{
		if (!base.IsEmpty)
		{
			base.Top.Update();
		}
	}
}
