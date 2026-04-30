using SomaSim.Util;
using UnityEngine;

namespace Game.UI.Session;

public class FlyoutPool
{
	public readonly FlyoutManager mgr;

	public readonly UIFlyoutName type;

	public readonly GameObject template;

	private ObjectPoolResettable<BaseFlyout> _pool;

	public FlyoutPool(FlyoutManager mgr, UIFlyoutName type, GameObject template)
	{
		this.mgr = mgr;
		this.type = type;
		this.template = template;
	}

	public void Initialize()
	{
		_pool = new ObjectPoolResettable<BaseFlyout>();
		_pool.Initialize(MakeFlyout, ActivateFlyout, ResetFlyout, DestroyFlyout);
	}

	public void Release()
	{
		_pool.Release();
	}

	public BaseFlyout Allocate()
	{
		return _pool.Allocate();
	}

	public void Free(BaseFlyout flyout)
	{
		_pool.Free(flyout);
	}

	private BaseFlyout MakeFlyout()
	{
		BaseFlyout baseFlyout;
		if (type == UIFlyoutName.SimpleTextFlyout)
		{
			baseFlyout = new TextFlyout();
		}
		else
		{
			Logger.Error("Unknown flyout type: " + type.path);
			baseFlyout = new TextFlyout();
		}
		baseFlyout.type = type;
		baseFlyout.go = Object.Instantiate(template);
		baseFlyout.go.transform.SetParent(mgr.Container.transform);
		baseFlyout.go.SetActive(value: false);
		baseFlyout.OnAfterCreate();
		return baseFlyout;
	}

	private void ActivateFlyout(BaseFlyout flyout)
	{
		flyout.go.SetActive(value: true);
	}

	private void ResetFlyout(BaseFlyout flyout)
	{
		flyout.go.SetActive(value: false);
	}

	private void DestroyFlyout(BaseFlyout flyout)
	{
		flyout.OnBeforeDestroy();
		Object.Destroy(flyout.go);
		flyout.go = null;
		flyout.type = null;
	}
}
