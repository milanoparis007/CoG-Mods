using System.Collections.Generic;
using Game.Core;
using SomaSim.Util;

namespace Game.Session.Player;

public sealed class PlayerGamblingData
{
	public Xorshift rng = new Xorshift(1u);

	public List<GamblerState> states = new List<GamblerState>();

	public List<EntityID> bannedGamblers = new List<EntityID>();

	public List<EntityID> formerGamblers = new List<EntityID>();

	public List<EntityID> assignedHomes = new List<EntityID>();

	public bool shownGamblingTicker;

	public PlayerGamblingData()
	{
	}

	public PlayerGamblingData(PlayerID pid)
	{
		rng = Game.ctx.scenario.MakeSeededRng(pid);
	}

	public GamblerState FindStateForGambler(EntityID gamblerId)
	{
		foreach (GamblerState state in states)
		{
			if (state.gamblerId == gamblerId)
			{
				return state;
			}
		}
		return null;
	}
}
