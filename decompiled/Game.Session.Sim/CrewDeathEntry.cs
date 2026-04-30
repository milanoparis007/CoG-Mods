using Game.Core;

namespace Game.Session.Sim;

public struct CrewDeathEntry
{
	public enum PlayerChoice
	{
		Ignore,
		Support,
		Quest
	}

	public static readonly CrewDeathEntry EMPTY;

	public EntityID peepId;

	public PlayerChoice choice;

	public Price perTurn;

	public bool IsValid => peepId != EMPTY.peepId;

	public bool IsNotValid => peepId == EMPTY.peepId;

	public CrewDeathEntry SetChoice(PlayerChoice newChoice)
	{
		return new CrewDeathEntry
		{
			peepId = peepId,
			perTurn = perTurn,
			choice = newChoice
		};
	}
}
