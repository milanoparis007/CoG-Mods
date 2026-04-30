using System;
using Game.UI.Util;
using SomaSim.Util;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.Session.Picks;

public sealed class PickPool
{
	public PickType type;

	public PickManager manager;

	public Func<BasePick> factory;

	public GameObject template;

	private ObjectPoolResettable<BasePick> _pool;

	public PickPool(PickType type, PickManager manager, Func<BasePick> factory, GameObject template)
	{
		this.type = type;
		this.manager = manager;
		this.factory = factory;
		this.template = template;
	}

	public void Initialize()
	{
		_pool = new ObjectPoolResettable<BasePick>();
		_pool.Initialize(MakePick, ActivatePick, ResetPick, DestroyPick);
	}

	public void Release()
	{
		_pool.Release();
	}

	public BasePick Allocate(PickTarget t)
	{
		BasePick basePick = _pool.Allocate();
		basePick.SetTarget(t);
		return basePick;
	}

	public void Free(BasePick pick)
	{
		pick.Reset();
		_pool.Free(pick);
	}

	private BasePick MakePick()
	{
		BasePick pick = factory();
		pick.go = UnityEngine.Object.Instantiate(template, manager.GetContainer(type).gorect);
		pick.go.name = $"PICK/{type}";
		pick.gorect = pick.go.GetComponent<RectTransform>();
		pick.go.GetOrAddComponent<PickContext>().pick = pick;
		pick.go.GetOrAddComponent<IgnoreMouse>().SetIgnore(ignoreScroll: true);
		pick.go.GetComponentInChildren<Button>().onClick.SetListener(delegate
		{
			pick.OnClick();
		});
		return pick;
	}

	private void ActivatePick(BasePick pick)
	{
		pick.go.SetActive(value: true);
	}

	private void ResetPick(BasePick pick)
	{
		pick.go.SetActive(value: false);
	}

	private void DestroyPick(BasePick pick)
	{
		pick.go.GetComponentInChildren<Button>().onClick.RemoveAllListeners();
		pick.go.GetOrAddComponent<PickContext>().pick = null;
		UnityEngine.Object.Destroy(pick.go);
		pick.gorect = null;
		pick.go = null;
	}
}
