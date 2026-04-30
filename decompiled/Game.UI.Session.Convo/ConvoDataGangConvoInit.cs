using Game.Core;
using Game.Services;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Player;
using Game.Session.Player.AI;

namespace Game.UI.Session.Convo;

public sealed class ConvoDataGangConvoInit : ConvoData
{
	public PlayerID other;

	public string gangname;

	public ConvoInitiative.Topic topic;

	public Price price;

	public int days;

	public EntityID targetId;

	public string bizname;

	public override string[] MakeReplacements(VisitState visit, int index)
	{
		return new string[10]
		{
			"groupname",
			gangname,
			"gangTarget",
			targetId.FindEntity()?.data.agent.pid.FindPlayer()?.social.FindPlayerGroupNameColorized() ?? "",
			"bizname",
			bizname,
			"amt",
			Loc.Price(price, abs: true),
			"days",
			Loc.FormatNumber(days)
		};
	}

	public ConvoDataGangConvoInit()
	{
	}

	public ConvoDataGangConvoInit(PlayerID gang)
	{
		PlayerInfo playerInfo = gang.FindPlayer();
		ConvoInitiative convoInitiativeUnsafe = playerInfo.ai.social.GetConvoInitiativeUnsafe();
		other = gang;
		gangname = playerInfo.social.FindPlayerGroupNameColorized();
		topic = convoInitiativeUnsafe.topic;
		switch (topic)
		{
		case ConvoInitiative.Topic.TiedHouse:
		{
			BuildingAndBusinessData buildingAndBusinessData = BuildingUtil.FindDataForBuilding(convoInitiativeUnsafe.targetId.FindEntity());
			(Price cost, int days) tiedHouseParameters = buildingAndBusinessData.biz.components.biz.GetTiedHouseParameters(buildingAndBusinessData.owner.Id, gang, PlayerID.HumanPlayer);
			Price item = tiedHouseParameters.cost;
			int item2 = tiedHouseParameters.days;
			targetId = convoInitiativeUnsafe.targetId;
			price = item;
			days = item2;
			bizname = buildingAndBusinessData.biz.data.biz.bizname;
			break;
		}
		case ConvoInitiative.Topic.TruceRequest:
		{
			SocialAdvisor.RequestParams requestParams4 = playerInfo.ai.social.ComputeRequestParametersForUsAskingHuman(PlayerID.HumanPlayer, ConvoInitiative.Topic.TruceRequest);
			targetId = EntityID.INVALID;
			price = new Price(-requestParams4.cost);
			days = requestParams4.days;
			bizname = null;
			break;
		}
		case ConvoInitiative.Topic.ExpansionHalt:
		{
			SocialAdvisor.RequestParams requestParams3 = playerInfo.ai.social.ComputeRequestParametersForUsAskingHuman(PlayerID.HumanPlayer, ConvoInitiative.Topic.ExpansionHalt);
			targetId = EntityID.INVALID;
			price = new Price(-requestParams3.cost);
			days = requestParams3.days;
			bizname = null;
			break;
		}
		case ConvoInitiative.Topic.JointWar:
		{
			SocialAdvisor.RequestParams requestParams2 = playerInfo.ai.social.ComputeRequestParametersForUsAskingHuman(PlayerID.HumanPlayer, ConvoInitiative.Topic.JointWar, convoInitiativeUnsafe.targetId.FindEntity().data.agent.pid);
			targetId = convoInitiativeUnsafe.targetId;
			price = new Price(-requestParams2.cost);
			days = requestParams2.days;
			bizname = null;
			break;
		}
		case ConvoInitiative.Topic.StolenOutpost:
		{
			SocialAdvisor.RequestParams requestParams = playerInfo.ai.social.ComputeRequestParametersForUsAskingHuman(PlayerID.HumanPlayer, ConvoInitiative.Topic.StolenOutpost);
			targetId = EntityID.INVALID;
			price = new Price(-requestParams.cost);
			days = requestParams.days;
			bizname = null;
			break;
		}
		}
	}
}
