using System;
using System.Collections.Generic;
using SomaSim.Util;

namespace Game.Services;

public abstract class EventBus<TEventTypeEnum, TEvent> where TEvent : IEventOfType<TEventTypeEnum>, IEquatable<TEvent>
{
	protected sealed class Comparer : IEqualityComparer<TEventTypeEnum>
	{
		private EventBus<TEventTypeEnum, TEvent> _bus;

		public Comparer(EventBus<TEventTypeEnum, TEvent> bus)
		{
			_bus = bus;
		}

		public bool Equals(TEventTypeEnum x, TEventTypeEnum y)
		{
			return _bus.Equals(x, y);
		}

		public int GetHashCode(TEventTypeEnum obj)
		{
			return _bus.ToInt(obj);
		}
	}

	private Deque<TEvent> _queue;

	private Dictionary<TEventTypeEnum, HashSet<Action<TEvent>>> _listeners;

	protected abstract bool Equals(TEventTypeEnum x, TEventTypeEnum y);

	protected abstract int ToInt(TEventTypeEnum x);

	public void Initialize()
	{
		_queue = new Deque<TEvent>();
		_listeners = new Dictionary<TEventTypeEnum, HashSet<Action<TEvent>>>(new Comparer(this));
	}

	public void Release()
	{
		_listeners.ClearDeep();
		_queue.Clear();
	}

	public bool AddListener(TEventTypeEnum type, Action<TEvent> listener)
	{
		return _listeners.FindOrAddNew(type).Add(listener);
	}

	public bool RemoveListener(TEventTypeEnum type, Action<TEvent> listener)
	{
		return _listeners.FindOrAddNew(type).Remove(listener);
	}

	public void RemoveAllListeners(TEventTypeEnum type)
	{
		_listeners.FindOrNull(type)?.Clear();
	}

	public void ProcessQueue()
	{
		while (_queue.Count > 0)
		{
			TEvent ev = _queue.RemoveFirst();
			Dispatch(ev);
		}
	}

	private void Dispatch(TEvent ev)
	{
		using ListPool<Action<TEvent>>.PooledBlockList pooledBlockList = ListPool<Action<TEvent>>.Allocate();
		HashSet<Action<TEvent>> collection = _listeners.FindOrAddNew(ev.type);
		pooledBlockList.AddRange(collection);
		foreach (Action<TEvent> item in pooledBlockList)
		{
			item(ev);
		}
	}

	public void SendNextFrameIfNew(TEvent ev)
	{
		if (!_queue.Contains(ev))
		{
			_queue.AddLast(ev);
		}
	}

	public void SendNextFrame(TEvent ev)
	{
		_queue.AddLast(ev);
	}

	public void SendImmediate(TEvent ev)
	{
		Dispatch(ev);
	}
}
