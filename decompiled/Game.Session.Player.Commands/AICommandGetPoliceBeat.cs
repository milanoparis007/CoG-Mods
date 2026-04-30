using Game.Core;
using Game.Session.Player.AI;

namespace Game.Session.Player.Commands;

public class AICommandGetPoliceBeat : InstantAICommand
{
	public AICommandGetPoliceBeat()
	{
	}

	public AICommandGetPoliceBeat(PlayerID pid, EntityID eid, Deictics vars)
		: base(pid, CommandType.GetPoliceBeat, eid)
	{
		vars.targetNode = GetNextBeatNode();
	}

	private NodeID GetNextBeatNode()
	{
		return GetPlayer().ai.precinct?.GetNextBeatNode(peepId) ?? NodeID.INVALID;
	}
}
