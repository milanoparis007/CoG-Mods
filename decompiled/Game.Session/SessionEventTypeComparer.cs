using System.Collections.Generic;

namespace Game.Session;

public sealed class SessionEventTypeComparer : IEqualityComparer<SessionEventType>
{
	public bool Equals(SessionEventType x, SessionEventType y)
	{
		return x == y;
	}

	public int GetHashCode(SessionEventType obj)
	{
		return (int)obj;
	}
}
