using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Services;
using Game.Session.Data;
using Game.Session.Player;
using Game.Session.Sim;
using Game.UI.Session.Crew;
using Game.UI.Session.OwnedBiz;
using Game.UI.Session.Popups;
using SomaSim.Util;
using UnityEngine;

namespace Game.UI.Session.Politics;

public sealed class PoliticsController : HUDController<PoliticsModel, PoliticsDialog, PoliticsController>
{
	internal void SetModel(VisitState visit)
	{
		base.Model.visit = visit;
	}

	internal void OnBeforeViewHide()
	{
		SetModel(null);
	}

	internal void OnCloseButton()
	{
		Game.ctx.selection.ClearActive();
	}

	internal void OnSwitchCrew(CrewAssignment newCrew)
	{
		base.Model.visit.SetCrew(newCrew);
		base.View.ControllerRequestsFullRefresh();
	}

	internal void OnModuleButtonClick(GameObject card)
	{
		ModuleToggleContext component = card.GetComponent<ModuleToggleContext>();
		OnModuleButtonClick(component);
	}

	internal void OnModuleButtonClick(ModuleToggleContext ctx)
	{
		base.Model.currentSlot = ctx;
	}

	internal void OnCandidateSponsorButtonClick(EntityID candidate)
	{
		Fixnum sponsorPrice = Game.ctx.simman.politics.GetSponsorPrice(candidate);
		PlayerFinances finances = Game.ctx.players.Human.finances;
		OkPopup.ShowOkCancel(Loc.Get("ui.politics.sponsor-confirm", "price", sponsorPrice), delegate
		{
			finances.DoChangeMoney(base.Model.visit.vehicle, new Price(-sponsorPrice), MoneyReason.CampaignDonations);
			base.Model.ThisWard.currElection.DoSponsorCandidate(PlayerID.HumanPlayer, candidate, Election.SupportLevel.Sponsor);
			base.Model.ThisWard.currElection.ModifyCandidateWarchest(candidate, sponsorPrice);
			base.Model.visit.crew.GetPeep().components.agent.IncrementStat(CrewStats.PoliticalEvents, 1);
			base.View.ControllerRequestsFullRefresh();
		}, delegate
		{
		});
	}

	internal void OnNominateButtonClick()
	{
		List<EntityID> politicians = base.Model.ThisWard.localPoliticians.Where((EntityID x) => !base.Model.ThisWard.currElection.allCandidates.Contains(x)).ToList();
		Game.serv.ui.AddPopup(new PoliticianSelectionPopup(politicians, NominateConfirm, base.Model.visit));
	}

	internal void NominateConfirm(EntityID politician)
	{
		Fixnum sponsorPrice = Game.ctx.simman.politics.GetNominatePrice(politician);
		PlayerFinances finances = Game.ctx.players.Human.finances;
		OkPopup.ShowOkCancel(Loc.Get("ui.politics.nominate-confirm", "price", sponsorPrice), delegate
		{
			finances.DoChangeMoney(base.Model.visit.vehicle, new Price(-sponsorPrice), MoneyReason.CampaignDonations);
			base.Model.ThisWard.currElection.AddCandidateToElection(politician);
			base.Model.ThisWard.currElection.DoSponsorCandidate(PlayerID.HumanPlayer, politician, Election.SupportLevel.Nominator);
			base.Model.ThisWard.currElection.ModifyCandidateWarchest(politician, sponsorPrice);
			base.Model.visit.crew.GetPeep().components.agent.IncrementStat(CrewStats.PoliticalEvents, 1);
			base.View.ControllerRequestsFullRefresh();
		}, delegate
		{
		});
	}

	internal void OnShopButtonClick()
	{
		Game.serv.ui.AddPopup(new LawPopup());
	}

	internal void OnCandidateInteractButtonClick(EntityID politician)
	{
		PersonInfoUtil.TweenCameraToEntity(Game.ctx.simman.politics.GetPoliticianData(politician).location);
	}

	internal void OnCandidateInfoButtonClick(EntityID politician)
	{
		Game.serv.ui.AddPopup(new PoliticianInfoPopup(politician));
	}
}
