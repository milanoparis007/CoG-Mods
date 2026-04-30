using Game.Core;

namespace Game.Session.Entities;

public sealed class ForcedClosedInfo
{
	public PlayerID originator;

	public SimTime started;

	public SimTime expires;
}
