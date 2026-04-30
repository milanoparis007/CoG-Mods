using System;

namespace Game.Services;

public struct ServiceEvent : IEventOfType<ServiceEventType>, IEquatable<ServiceEvent>
{
	public ServiceEventType type { get; set; }

	public ServiceEvent(ServiceEventType type)
	{
		this.type = type;
	}

	public bool Equals(ServiceEvent other)
	{
		return type == other.type;
	}
}
