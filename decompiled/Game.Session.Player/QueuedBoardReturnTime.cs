using Game.Core;

namespace Game.Session.Player;

public struct QueuedBoardReturnTime
{
	public EntityID crew;

	public SimTime automaticReturnTime;

	public QueuedBoardReturnTime(EntityID crew, SimTime automaticReturnTime)
	{
		this.crew = crew;
		this.automaticReturnTime = automaticReturnTime;
	}
}
