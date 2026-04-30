using Game.Core;

namespace Game.Session.Sim;

public sealed class GameClockPersistedData
{
	public GameTurnUpdate state;

	public GameAnimUpdate anim;

	public bool skip;
}
