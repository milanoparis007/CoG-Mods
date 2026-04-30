using Game.Core;
using Game.Services;
using Game.Session.Data;
using Game.Session.Entities;

namespace Game.UI.Session.Convo;

public sealed class ConvoDataCopsJailBribe : ConvoData
{
	public PlayerID pid;

	public EntityID crewId;

	public SimTime trialDate;

	public Price payOffCost;

	public ConvoDataCopsJailBribe()
	{
	}

	public ConvoDataCopsJailBribe(PlayerID pid, EntityID crewId, SimTime trialDate, Price cost)
	{
		this.pid = pid;
		this.crewId = crewId;
		this.trialDate = trialDate;
		payOffCost = cost;
	}

	public override string[] MakeReplacements(VisitState visit, int index)
	{
		return new string[6]
		{
			"crewname",
			crewId.FindEntity().data.person.ShortName,
			"trialdate",
			Loc.FormatDateShort(trialDate),
			"amt",
			Loc.Price(payOffCost, abs: true)
		};
	}
}
