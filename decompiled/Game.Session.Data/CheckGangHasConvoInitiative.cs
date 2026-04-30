using Game.Session.Player;
using Game.Session.Player.AI;

namespace Game.Session.Data;

public class CheckGangHasConvoInitiative : AbstractVisitRequirement
{
	public enum Type
	{
		None,
		Any,
		Truce,
		TiedHouse,
		ExpansionHalt,
		StolenOutpost,
		JointWar
	}

	public Type type;

	public override bool DoesPass(VisitState visit)
	{
		PlayerInfo playerInfo = visit.npc.data.agent.pid.FindPlayer();
		if (!playerInfo.IsAnyAIPlayer)
		{
			return false;
		}
		ConvoInitiative convoInitiative = playerInfo.ai?.social?.GetConvoInitiativeUnsafe();
		if (convoInitiative == null)
		{
			return false;
		}
		switch (type)
		{
		case Type.Truce:
			if (convoInitiative.pid == visit.pid)
			{
				return convoInitiative.topic == ConvoInitiative.Topic.TruceRequest;
			}
			return false;
		case Type.TiedHouse:
			if (convoInitiative.pid == visit.pid)
			{
				return convoInitiative.topic == ConvoInitiative.Topic.TiedHouse;
			}
			return false;
		case Type.ExpansionHalt:
			if (convoInitiative.pid == visit.pid)
			{
				return convoInitiative.topic == ConvoInitiative.Topic.ExpansionHalt;
			}
			return false;
		case Type.JointWar:
			if (convoInitiative.pid == visit.pid)
			{
				return convoInitiative.topic == ConvoInitiative.Topic.JointWar;
			}
			return false;
		case Type.StolenOutpost:
			if (convoInitiative.pid == visit.pid)
			{
				return convoInitiative.topic == ConvoInitiative.Topic.StolenOutpost;
			}
			return false;
		case Type.Any:
			if (convoInitiative.pid == visit.pid)
			{
				return convoInitiative.IsValid;
			}
			return false;
		default:
			if (convoInitiative.pid == visit.pid)
			{
				return !convoInitiative.IsValid;
			}
			return true;
		}
	}

	public override ReqExplanation Explain(VisitState visit)
	{
		return new ReqExplanation(DoesPass(visit), null);
	}
}
