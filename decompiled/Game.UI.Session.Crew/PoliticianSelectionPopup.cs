using System;
using System.Collections.Generic;
using Game.Core;
using Game.Services;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Sim;
using Game.Session.Sim.Modules;
using Game.UI.Session.Politics;
using SomaSim.Util;
using UnityEngine;

namespace Game.UI.Session.Crew;

public class PoliticianSelectionPopup : BasePopup
{
	private const string CARD_TEMPLATE = "Templates/Potential Candidate";

	private const string CONTAINER = "Panel/Scroll View List/Viewport/Content";

	private const string CLOSE = "Panel/Close Button";

	private const string TASK = "Panel/Header/Text";

	private List<EntityID> _politicians;

	private EntityID _selected;

	private Action<EntityID> _onSelect;

	private Ward _ward;

	private VisitState _visit;

	public const string HEADER_BG = "Header";

	public const string CANDIDATE_NAME = "Header/Name";

	public const string CANDIDATE_SPONSOR = "Nominate Button";

	public const string CANDIDATE_PORTRAIT = "Info/Portrait/Portrait";

	public const string CANDIDATE_VOTES = "Info/Votes";

	public const string CANDIDATE_ARCHETYPE = "Info/Archetype";

	public const string CANDIDATE_TRAITS = "Info/Traits";

	public const string CANDIDATE_PEDIGREE = "Info/Pedigree";

	public const string CANDIDATE_ETHNICITY = "Info/Ethnicity";

	public const string INFO_TEXT = "/Text";

	public override UIReference UIReference => UIElements.PoliticianSelectPopup;

	public PoliticianSelectionPopup(List<EntityID> politicians, Action<EntityID> onSelect, VisitState visit)
	{
		_politicians = politicians;
		_onSelect = onSelect;
		_selected = EntityID.INVALID;
		_ward = Game.ctx.simman.politics.GetWardForBuilding(visit.building.Id);
		_visit = visit;
	}

	protected override void InitializeOnPush()
	{
		_go.GetButton("Panel/Close Button").onClick.SetListener(Close);
		GameObject child = _go.GetChild("Templates/Potential Candidate");
		GameObject child2 = _go.GetChild("Panel/Scroll View List/Viewport/Content");
		child2.EnsureChildCount(_politicians, child);
		child2.InitializeChildren(_politicians, InitializePoliticianCard);
	}

	protected override void ReleaseOnPop()
	{
		ProcessCallbacksAfterClose(_selected, _onSelect);
		_go.GetChild("Panel/Scroll View List/Viewport/Content").DestroyAllChildren();
		_go.ClearButtonListeners("Panel/Close Button");
	}

	public void InitializePoliticianCard(int count, GameObject card, EntityID candidate)
	{
		PoliticianData politicianData = Game.ctx.simman.politics.GetPoliticianData(candidate);
		PoliticsSettings.PoliticalArchetype politicalArchetype = Game.serv.globals.settings.politics.FindPoliticalArchetype(politicianData.archetypeId);
		_ = candidate == _ward.currentPolitician;
		InitializeHeader();
		card.SetImage("Info/Portrait/Portrait", HUDUtil.GetCrewSprite(candidate.FindEntity()));
		InitializeInfoBox("Info/Pedigree", Loc.Get("ui.politics.candidate.pedigree", "num", politicianData.incumbencies), PoliticianCardCtx.CtxType.PedigreeInfo);
		InitializeInfoBox("Info/Traits", PersonInfoUtil.GenerateTraitsIcons(candidate.FindEntity()), PoliticianCardCtx.CtxType.TraitsInfo);
		InitializeInfoBox("Info/Archetype", Loc.Get(politicalArchetype.locname), PoliticianCardCtx.CtxType.ArchetypeInfo);
		InitializeInfoBox("Info/Ethnicity", Loc.Get(candidate.FindEntity().data.person.GetEthDef().loc.adjEthnicity), PoliticianCardCtx.CtxType.EthnicityInfo);
		InitializeSponsorButton();
		bool CanPayForSponsor()
		{
			return Game.ctx.simman.politics.GetSponsorPrice(candidate) <= ModulesUtil.GetInventory(_visit.vehicle).data.money.cash;
		}
		void InitializeHeader()
		{
			card.SetText("Header/Name", Loc.Get("ui.politics.select-candidate.header", "name", candidate.FindEntity().data.person.FullName));
		}
		void InitializeInfoBox(string path, string contents, PoliticianCardCtx.CtxType type)
		{
			card.SetText(path + "/Text", contents);
			card.GetChild(path + "/Text").GetOrAddComponent<PoliticianCardCtx>().Set(type, candidate);
		}
		void InitializeSponsorButton()
		{
			bool active = _ward.ElectionOngoing && _ward.currElection.stage == Election.ElectionStage.Nomination && !_ward.currElection.HasSponsoredACandidate(PlayerID.HumanPlayer);
			card.GetChild("Nominate Button").SetActive(active);
			card.GetChild("Nominate Button").GetOrAddComponent<SponsorCtx>().Set(candidate, isNomination: false);
			card.GetButton("Nominate Button").onClick.SetListener(delegate
			{
				OnCandidateSponsorButtonClick(candidate);
			});
			card.GetButton("Nominate Button").interactable = _visit.vehicle != null && CanPayForSponsor();
		}
	}

	public void OnCandidateSponsorButtonClick(EntityID candidate)
	{
		_selected = candidate;
		Close();
	}

	public void OnCandidateInfoButtonClick(EntityID candidate)
	{
		Game.serv.ui.AddPopup(new PoliticianInfoPopup(candidate));
	}

	private static void ProcessCallbacksAfterClose(EntityID selected, Action<EntityID> onSelect)
	{
		TimerUtil.RunNextFrame(delegate
		{
			if (selected.IsValid)
			{
				onSelect?.Invoke(selected);
			}
		});
	}
}
