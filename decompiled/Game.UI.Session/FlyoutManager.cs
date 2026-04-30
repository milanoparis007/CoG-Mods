using System.Collections.Generic;
using Game.Core;
using Game.Services;
using Game.Session;
using Game.Session.Entities;
using SomaSim.Util;
using UnityEngine;

namespace Game.UI.Session;

public class FlyoutManager : ISubManager<HUDManager>
{
	public struct FlyoutHandle
	{
		public static readonly FlyoutHandle INVALID;

		public int id;

		public bool IsValid => id != 0;

		public bool IsNotValid => id == 0;
	}

	private struct FlyoutEntry
	{
		public FlyoutHandle handle;

		public BaseFlyout flyout;
	}

	private Dictionary<string, FlyoutPool> _pools;

	private List<FlyoutEntry> _actives;

	private GameObject _parent;

	private GameObject _container;

	private GameObject _templates;

	private int _nextId = FlyoutHandle.INVALID.id + 1;

	public GameObject Container => _container;

	public void Initialize(HUDManager manager)
	{
		_parent = Game.serv.ui.GetUI(UIElements.Flyouts);
		_container = _parent.GetChild("Container");
		_templates = _parent.GetChild("Templates");
		_container.transform.DestroyAllChildren();
		_parent.SetActive(value: true);
		_container.SetActive(value: true);
		_templates.SetActive(value: false);
		_pools = new Dictionary<string, FlyoutPool>();
		_actives = new List<FlyoutEntry>();
	}

	public void Release()
	{
		foreach (FlyoutEntry active in _actives)
		{
			BaseFlyout flyout = active.flyout;
			flyout.StopAnimation();
			GetOrAddPool(flyout.type).Free(flyout);
		}
		_actives.Clear();
		foreach (FlyoutPool value in _pools.Values)
		{
			value.Release();
		}
		_pools.Clear();
		_parent = null;
		_container = null;
		_templates = null;
	}

	public FlyoutHandle MakeSimpleTextFlyoutOverBuilding(Entity building, string text, float seconds = 1f, float dy = 100f)
	{
		return MakeSimpleTextFlyout(building.data.board.worldpos, text, seconds, dy);
	}

	public FlyoutHandle MakeSimpleTextFlyout(WorldPos wpos, string text, float seconds = 1f, float dy = 100f)
	{
		FlyoutEntry andActivateFlyout = GetAndActivateFlyout(UIFlyoutName.SimpleTextFlyout);
		if (andActivateFlyout.flyout is TextFlyout textFlyout)
		{
			textFlyout.Start(wpos, seconds, dy);
			textFlyout.Set(text, null);
		}
		return andActivateFlyout.handle;
	}

	public void MakeCashFlyout(WorldPos pos, Money money, float seconds = 1f, float dy = 100f)
	{
		string text = Loc.Money(money);
		MakeSimpleTextFlyout(pos, text, seconds, dy);
	}

	public void MakeCashFlyout(WorldPos pos, Fixnum cash, float seconds = 1f, float dy = 100f)
	{
		MakeCashFlyout(pos, new Money(cash), seconds, dy);
	}

	public FlyoutHandle ExpireFlyout(FlyoutHandle handle)
	{
		if (handle.IsNotValid)
		{
			return handle;
		}
		int num = IndexOf(handle);
		if (num >= 0)
		{
			_actives[num].flyout.ForceExpire();
		}
		return FlyoutHandle.INVALID;
	}

	private FlyoutEntry GetAndActivateFlyout(UIFlyoutName name)
	{
		BaseFlyout flyout = GetOrAddPool(name).Allocate();
		FlyoutEntry flyoutEntry = MakeNewEntry(flyout);
		_actives.Add(flyoutEntry);
		return flyoutEntry;
	}

	internal void FreeExpiredFlyout(BaseFlyout flyout)
	{
		int num = IndexOf(flyout);
		if (num >= 0)
		{
			_actives.RemoveAt(num);
			GetOrAddPool(flyout.type).Free(flyout);
		}
	}

	private int IndexOf(FlyoutHandle handle)
	{
		int i = 0;
		for (int count = _actives.Count; i < count; i++)
		{
			if (_actives[i].handle.id == handle.id)
			{
				return i;
			}
		}
		return -1;
	}

	private int IndexOf(BaseFlyout flyout)
	{
		int i = 0;
		for (int count = _actives.Count; i < count; i++)
		{
			if (_actives[i].flyout == flyout)
			{
				return i;
			}
		}
		return -1;
	}

	private FlyoutEntry MakeNewEntry(BaseFlyout flyout)
	{
		return new FlyoutEntry
		{
			flyout = flyout,
			handle = new FlyoutHandle
			{
				id = ++_nextId
			}
		};
	}

	private FlyoutPool GetOrAddPool(UIFlyoutName name)
	{
		if (!_pools.TryGetValue(name.path, out var value))
		{
			value = new FlyoutPool(this, name, FindTemplate(name));
			value.Initialize();
			_pools.Add(name.path, value);
		}
		return value;
	}

	private GameObject FindTemplate(UIFlyoutName type)
	{
		return _templates.GetChild(type.path);
	}
}
