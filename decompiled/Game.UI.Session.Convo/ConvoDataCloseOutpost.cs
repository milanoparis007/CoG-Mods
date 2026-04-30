using Game.Core;
using Game.Services;
using Game.Session.Data;
using Game.Session.Player;
using Game.Session.Sim;

namespace Game.UI.Session.Convo;

public sealed class ConvoDataCloseOutpost : ConvoData
{
	public NodeID nodeId;

	public PlayerID owner;

	public DemandsTracker.BribeResult bribeInfo;

	public Demand.State demandState;

	public string GangName => owner.FindPlayer().social.PlayerGroupName;

	public override string[] MakeReplacements(VisitState visit, int index)
	{
		return new string[4]
		{
			"price",
			Loc.Price(bribeInfo.price),
			"gangname",
			GangName
		};
	}

	public ConvoDataCloseOutpost()
	{
	}

	public ConvoDataCloseOutpost(NodeID nodeId, PlayerID owner, DemandsTracker.BribeResult bribeInfo)
	{
		this.nodeId = nodeId;
		this.owner = owner;
		this.bribeInfo = bribeInfo;
	}

	public static DemandDef GetDemandDef()
	{
		return Game.serv.globals.settings.people.social.demands.FindOrNullDemand(Demand.Type.CloseOutpost);
	}
}
