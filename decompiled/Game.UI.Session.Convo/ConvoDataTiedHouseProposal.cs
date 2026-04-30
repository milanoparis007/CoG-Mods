using Game.Core;
using Game.Services;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Player;

namespace Game.UI.Session.Convo;

public sealed class ConvoDataTiedHouseProposal : ConvoData
{
	public PlayerID other;

	public EntityID owner;

	public Price price;

	public int days;

	public bool IsGangProposal => other.IsAnyPlayer;

	public bool IsOwnerProposal => other.IsSystem;

	public bool IsValid
	{
		get
		{
			if (other.IsValid)
			{
				return owner.IsValid;
			}
			return false;
		}
	}

	public override string[] MakeReplacements(VisitState visit, int index)
	{
		BuildingAndBusinessData buildingAndBusinessData = BuildingUtil.FindDataForOwner(owner);
		string text = (other.IsAnyPlayer ? other.FindPlayer().social.FindPlayerGroupNameColorized() : "");
		return new string[10]
		{
			"groupname",
			text,
			"bizname",
			buildingAndBusinessData.biz?.data.biz.bizname,
			"npcname",
			buildingAndBusinessData.owner?.data.person.FullName,
			"amt",
			Loc.Price(price, abs: true),
			"days",
			Loc.FormatNumber(days)
		};
	}
}
