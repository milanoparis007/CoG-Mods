using System;
using System.Collections.Generic;
using Game.Core;
using Game.Session.Assets;
using SomaSim.Util;
using UnityEngine;

namespace Game.Session.Overlays;

public sealed class ArrowHandles
{
	public static readonly ArrowType[] ARROW_TYPES = Enum.GetValues(typeof(ArrowType)) as ArrowType[];

	private List<VFXManager.WorldArrowHandle> _handles;

	public ArrowType Type { get; private set; }

	public bool NoArrows => _handles.Count == 0;

	public ArrowHandles(ArrowType type)
	{
		Type = type;
		_handles = new List<VFXManager.WorldArrowHandle>();
	}

	public IEnumerable<VFXManager.WorldArrowHandle> GetResourcesToShowUnsafe()
	{
		return _handles;
	}

	public VFXManager.WorldArrowHandle? FindHandleForContext(EntityID ctx)
	{
		int num = IndexOfContext(_handles, ctx);
		if (num >= 0)
		{
			return _handles[num];
		}
		return null;
	}

	public void RefreshColorsForOrders(Func<VFXManager.WorldArrowHandle, Color?> selector)
	{
		foreach (VFXManager.WorldArrowHandle handle in _handles)
		{
			Game.ctx.vfx.RefreshArrowColor(handle, selector(handle));
		}
		Game.ctx.events.EnqueueOnce(SessionEventType.UIOrderArrowsChanged);
	}

	public void ShowArrow(ArrowChainLink link)
	{
		ShowArrow(link.from.Id, link.to.Id, link.ResColor, link.GetBuilding().Id, link);
	}

	public void ShowArrow(EntityID from, EntityID to, Color color, EntityID ctx, object extras = null)
	{
		int num = IndexOf(_handles, from, to);
		if (num >= 0)
		{
			Game.ctx.vfx.RefreshArrowColor(_handles[num], color);
		}
		else
		{
			_handles.Add(Game.ctx.vfx.ShowWorldArrow(from, to, color, ctx, Type, extras));
		}
	}

	public void HideAllArrows()
	{
		while (_handles.Count > 0)
		{
			Game.ctx.vfx.HideWorldArrow(_handles.RemoveLast());
		}
		Game.ctx.events.EnqueueOnce(SessionEventType.UIOrderArrowsChanged);
		Game.ctx.events.EnqueueOnce(SessionEventType.UIRelationshipArrowsChanged);
	}

	private static int IndexOf(IReadOnlyList<VFXManager.WorldArrowHandle> handles, EntityID from, EntityID to)
	{
		int i = 0;
		for (int count = handles.Count; i < count; i++)
		{
			VFXManager.WorldArrowHandle worldArrowHandle = handles[i];
			if (worldArrowHandle.from == from && worldArrowHandle.to == to)
			{
				return i;
			}
		}
		return -1;
	}

	private static int IndexOfContext(IReadOnlyList<VFXManager.WorldArrowHandle> handles, EntityID ctx)
	{
		int i = 0;
		for (int count = handles.Count; i < count; i++)
		{
			if (handles[i].ctx == ctx)
			{
				return i;
			}
		}
		return -1;
	}
}
