using Game.Core;
using Game.Services;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Sim;
using SomaSim.Util;

namespace Game.UI.Session.Convo;

public sealed class ConvoDataPolitician : ConvoData
{
	public Label selectedActionId;

	public EntityID candidateId;

	public PrecinctID wardId;

	public override string[] MakeReplacements(VisitState visit, int index)
	{
		Fixnum amt = new Fixnum(0);
		if (selectedActionId.IsSet)
		{
			amt = Game.serv.globals.settings.politics.FindPlayerCandidateAction(selectedActionId).warchestCost;
		}
		Ward wardForBuilding = Game.ctx.simman.politics.GetWardForBuilding(visit.building.Id);
		Fixnum amt2 = new Fixnum(0);
		if (wardForBuilding != null && wardForBuilding.ElectionOngoing)
		{
			amt2 = wardForBuilding.currElection.GetCandidateWarchest(visit.npc.Id);
		}
		return new string[6]
		{
			"name",
			candidateId.FindEntity().data.person.FullName,
			"price",
			Loc.Money(amt),
			"warchestValue",
			Loc.Money(amt2)
		};
	}

	public ConvoDataPolitician()
	{
	}

	public ConvoDataPolitician(EntityID candidateId, Label selectedActionId, PrecinctID wardId)
	{
		this.candidateId = candidateId;
		this.selectedActionId = selectedActionId;
		this.wardId = wardId;
	}
}
