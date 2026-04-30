using System.Linq;

namespace Game.Session.Player.AI;

public struct GoonLootFSM
{
	public GoonLootState currentstate;

	public GoonLootFSM(GoonLootState state)
	{
		currentstate = state;
	}

	private static GoonLootFSM MakeInvalid(GoonLootState from, GoonLootState to)
	{
		return new GoonLootFSM(GoonLootState.Invalid);
	}

	private static GoonLootFSM MakeNextState(GoonLootState from, GoonLootState to, params GoonLootState[] valid)
	{
		if (!valid.Contains(from))
		{
			return MakeInvalid(from, to);
		}
		return new GoonLootFSM(to);
	}

	public GoonLootFSM TransitionToIdle()
	{
		if (currentstate != GoonLootState.Disabled)
		{
			return new GoonLootFSM(GoonLootState.Disabled);
		}
		return MakeInvalid(currentstate, GoonLootState.Disabled);
	}

	public GoonLootFSM TransitionToCooldownFromEmpty()
	{
		return MakeNextState(currentstate, GoonLootState.Cooldown, GoonLootState.Cooldown);
	}

	public GoonLootFSM TransitionToCoolDown()
	{
		return MakeNextState(currentstate, GoonLootState.Cooldown, GoonLootState.Disabled, GoonLootState.Offered, GoonLootState.Ready, GoonLootState.Raided);
	}

	public GoonLootFSM TransitionToOffered()
	{
		return MakeNextState(currentstate, GoonLootState.Offered, GoonLootState.Cooldown);
	}

	public GoonLootFSM TransitionToAccepted()
	{
		return MakeNextState(currentstate, GoonLootState.Accepted, GoonLootState.Offered);
	}

	public GoonLootFSM TransitionToReady()
	{
		return MakeNextState(currentstate, GoonLootState.Ready, GoonLootState.Accepted);
	}

	public GoonLootFSM TransitionToRaided()
	{
		return MakeNextState(currentstate, GoonLootState.Raided, GoonLootState.Accepted, GoonLootState.Ready);
	}
}
