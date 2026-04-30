using System;
using System.Collections.Generic;
using Game.Services;
using SomaSim.Util;
using UnityEngine;

namespace Game.UI.Session.Picks;

public sealed class PickContainer
{
	internal enum SortType
	{
		None,
		Linear,
		Full
	}

	private struct SortHelper
	{
		public Transform tr;

		public int ypos;
	}

	public PickManager manager;

	public PickType type;

	public GameObject go;

	public RectTransform gorect;

	public Dictionary<PickTarget, BasePick> picks;

	public PickPool pool;

	private List<SortHelper> _elements = new List<SortHelper>(128);

	public void Initialize(PickType t, PickManager mgr, GameObject container, Func<BasePick> factory, GameObject pickTemplate)
	{
		type = t;
		manager = mgr;
		go = UnityEngine.Object.Instantiate(container, container.transform.parent);
		gorect = go.GetComponent<RectTransform>();
		picks = new Dictionary<PickTarget, BasePick>(new PickTargetEqualityComparer());
		pool = new PickPool(t, mgr, factory, pickTemplate);
		pool.Initialize();
		gorect.DestroyAllChildren();
		go.SetActive(value: true);
		go.name = t.ToString();
	}

	public void Release()
	{
		foreach (BasePick value in picks.Values)
		{
			pool.Free(value);
		}
		picks.Clear();
		pool.Release();
		UnityEngine.Object.Destroy(go);
		pool = null;
		picks = null;
		gorect = null;
		go = null;
		type = PickType.None;
	}

	public void RefreshShowingPicks()
	{
		foreach (BasePick value in picks.Values)
		{
			if (value.IsShowing)
			{
				RefreshContents(value);
			}
		}
	}

	public BasePick AddOrRefreshPick(PickTarget t, bool resetExisting = false)
	{
		(BasePick pick, bool existed) orAdd = GetOrAdd(t);
		var (basePick, _) = orAdd;
		if (orAdd.existed && resetExisting)
		{
			basePick.SetTarget(t);
		}
		RefreshContents(basePick);
		RefreshPosition(basePick);
		return basePick;
	}

	public BasePick RefreshPickIfExists(PickTarget t)
	{
		BasePick orNull = GetOrNull(t);
		if (orNull != null)
		{
			RefreshContents(orNull);
			RefreshPosition(orNull);
		}
		return orNull;
	}

	internal BasePick GetOrNull(PickTarget target)
	{
		return picks.FindOrNull(target);
	}

	internal (BasePick pick, bool existed) GetOrAdd(PickTarget target)
	{
		BasePick basePick = picks.FindOrNull(target);
		if (basePick != null)
		{
			return (pick: basePick, existed: true);
		}
		BasePick basePick2 = (picks[target] = pool.Allocate(target));
		basePick = basePick2;
		return (pick: basePick, existed: false);
	}

	internal bool Remove(PickTarget target)
	{
		if (!picks.TryGetValue(target, out var value))
		{
			return false;
		}
		picks.Remove(target);
		pool.Free(value);
		return true;
	}

	internal void RemoveAll()
	{
		foreach (PickTarget item in new List<PickTarget>(picks.Keys))
		{
			Remove(item);
		}
	}

	internal void RefreshAll(bool show, SortType sortType)
	{
		if (show != go.activeSelf)
		{
			go.SetActive(show);
		}
		if (!show)
		{
			return;
		}
		foreach (BasePick value in picks.Values)
		{
			RefreshPosition(value);
		}
		switch (sortType)
		{
		case SortType.Linear:
			ResortSiblings();
			break;
		case SortType.Full:
			ResortFull();
			break;
		case SortType.None:
			break;
		}
	}

	internal void RefreshPosition(PickTarget target)
	{
		BasePick orNull = GetOrNull(target);
		if (orNull != null)
		{
			RefreshPosition(orNull);
		}
	}

	private void ResortSiblings()
	{
		int childCount = gorect.childCount;
		if (childCount == 0)
		{
			return;
		}
		Transform transform = gorect.GetChild(0);
		for (int i = 1; i < childCount; i++)
		{
			Transform child = gorect.GetChild(i);
			float y = transform.transform.position.y;
			float y2 = child.transform.position.y;
			if (y < y2)
			{
				transform.SetSiblingIndex(i);
			}
			transform = child;
		}
	}

	private void ResortFull()
	{
		_elements.Clear();
		foreach (Transform item in gorect)
		{
			_elements.Add(new SortHelper
			{
				tr = item,
				ypos = (int)item.position.y
			});
		}
		_elements.StableSort((SortHelper a, SortHelper b) => b.ypos - a.ypos);
		int num = 0;
		for (int count = _elements.Count; num < count; num++)
		{
			_elements[num].tr.SetSiblingIndex(num);
		}
		_elements.Clear();
	}

	private void RefreshContents(BasePick pick)
	{
		pick.RefreshContents();
	}

	private void RefreshPosition(BasePick pick)
	{
		Vector2 vector = pick.MakeScreenVector();
		bool num = manager.IsAnchorWithinMargins(vector);
		bool flag = IsVisibleAtCurrentZoomLevel(pick);
		bool show = num && flag;
		pick.SetPositionAndVisibility(vector, show);
	}

	private bool IsVisibleAtCurrentZoomLevel(BasePick pick)
	{
		if (type != PickType.BuildingPick && type != PickType.SummaryPick)
		{
			return true;
		}
		BuildingPick obj = pick as BuildingPick;
		SummaryPick summaryPick = pick as SummaryPick;
		bool flag = obj != null;
		bool flag2 = summaryPick != null;
		if (!flag && !flag2)
		{
			return true;
		}
		if (manager.OverrideDontHidePicksOnZoom)
		{
			return flag;
		}
		float cameraZoomPercent = manager.CameraZoomPercent;
		TerritorySettings territory = Game.serv.globals.settings.general.territory;
		return (flag ? territory.showBuildingPicksZoomRange : territory.showSummaryPicksZoomRange).InInterval(cameraZoomPercent);
	}
}
