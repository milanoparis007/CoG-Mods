using System;

namespace Game.Services;

public sealed class ServiceEventBus : AbstractService, IUpdateService, IService
{
	public sealed class BusImpl : EventBus<ServiceEventType, ServiceEvent>
	{
		protected override bool Equals(ServiceEventType x, ServiceEventType y)
		{
			return x == y;
		}

		protected override int ToInt(ServiceEventType x)
		{
			return (int)x;
		}
	}

	private BusImpl _bus;

	public override void OnCreated()
	{
		_bus = new BusImpl();
		_bus.Initialize();
	}

	public override void OnDestroyed()
	{
		_bus.Release();
		_bus = null;
	}

	public void OnUpdate()
	{
		_bus.ProcessQueue();
	}

	public bool AddListener(ServiceEventType type, Action<ServiceEvent> listener)
	{
		return _bus.AddListener(type, listener);
	}

	public bool RemoveListener(ServiceEventType type, Action<ServiceEvent> listener)
	{
		return _bus.RemoveListener(type, listener);
	}

	public void EnqueueOnce(ServiceEventType type)
	{
		_bus.SendNextFrameIfNew(new ServiceEvent(type));
	}

	public void SendImmediate(ServiceEventType type)
	{
		_bus.SendImmediate(new ServiceEvent(type));
	}
}
