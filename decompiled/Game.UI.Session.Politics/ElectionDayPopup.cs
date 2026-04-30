using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Services;
using Game.Session.Entities;
using Game.Session.Sim;
using SomaSim.Util;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.Session.Politics;

public class ElectionDayPopup : BasePopup
{
	private const string CARD_TEMPLATE = "Templates/Election Rundown";

	private const string VOTE_TEMPLATE = "Templates/Vote Breakdown";

	private const string ELECTIONS_CONTAINER = "Panel/Election Info/Scroll View List/Viewport/Content";

	private const string START_COUNT = "Panel/Election Info/Start Animation Button/Button";

	private const string START_COUNT_ELEMENT = "Panel/Election Info/Start Animation Button";

	private const string CLOSE = "Panel/Header/Close Button";

	private const string INACTIVE_GREY_OUT = "Panel/Election Info/Inactive Grey Out";

	public GameObject _template;

	public GameObject _templateVotes;

	public int wardsFinished;

	public bool startCount;

	public const string WARD_NAME = "Header/Ward Name";

	public const string VOTE_TOTALS = "BG/Totals";

	public const string COUNT = "BG/Vote Count";

	public const string GREY_OUT = "Not Interesting";

	public const string CROWN = "Header/IsSponsored";

	public const string NAME_AND_PERCENT = "Percentage Bar/Text";

	public const string BAR = "Percentage Bar";

	public const string NAME = "Name";

	public const string RESULTS = "Panel/Election Info/Winfo";

	public const string WIN_COUNT = "Panel/Election Info/Winfo/Win Count/Text";

	public const string INFLUENCE_TOTAL = "Panel/Election Info/Winfo/Influence Gain";

	public const string COUNTING = "Panel/Election Info/Counting";

	public override UIReference UIReference => UIElements.ElectionDayPopup;

	public PoliticsManager pManager => Game.ctx.simman.politics;

	protected override void InitializeOnPush()
	{
		ResetState();
		_template = _go.GetChild("Templates/Election Rundown");
		_templateVotes = _go.GetChild("Templates/Vote Breakdown");
		_go.GetButton("Panel/Header/Close Button").onClick.SetListener(Close);
		_go.GetButton("Panel/Election Info/Start Animation Button/Button").onClick.SetListener(OnStartCountClick);
		RefreshPanel();
	}

	protected override void ReleaseOnPop()
	{
		_template = (_templateVotes = null);
		_go.GetButton("Panel/Header/Close Button").onClick.RemoveListener(Close);
		_go.GetButton("Panel/Election Info/Start Animation Button/Button").onClick.RemoveListener(OnStartCountClick);
	}

	public void ResetState()
	{
		wardsFinished = 0;
		startCount = false;
		_go.SetActive("Panel/Election Info/Winfo", value: false);
		_go.GetChild("Panel/Election Info/Inactive Grey Out").SetActive(value: true);
		_go.GetChild("Panel/Election Info/Start Animation Button").SetActive(value: true);
		_go.GetChild("Panel/Election Info/Counting").SetActive(value: false);
		_go.GetChild("Panel/Election Info/Scroll View List/Viewport/Content").DestroyAllChildren();
	}

	protected void RefreshPanel()
	{
		RefreshHeader();
		RefreshElectionCards();
	}

	private void RefreshHeader()
	{
	}

	private void RefreshElectionCards()
	{
		List<Election> data = (from x in Game.ctx.simman.politics.GetWards()
			select x.currElection into x
			orderby GetVariance(x) descending
			orderby x.HasSponsoredACandidate(PlayerID.HumanPlayer) descending
			select x).ToList();
		GameObject child = _go.GetChild("Panel/Election Info/Scroll View List/Viewport/Content");
		child.EnsureChildCount(data, _template);
		child.InitializeChildren(data, InitializeElectionCard);
	}

	public void InitializeElectionCard(int count, GameObject card, Election election)
	{
		card.SetText("Header/Ward Name", election.ElectionWard.WardNo.ToUpper());
		card.SetText("BG/Vote Count", Loc.Get("ui.election-day.votes-counted", "counted", 0, "total", election.totalVoters));
		bool flag = election.HasSponsoredACandidate(PlayerID.HumanPlayer);
		card.GetChild("Not Interesting").SetActive(!flag);
		card.GetChild("Header/IsSponsored").SetActive(flag);
		GameObject child = card.GetChild("BG/Totals");
		Dictionary<EntityID, Election.CandidateInfo> dictionary = Game.serv.serializer.instance.Clone(election.displayModel.stats);
		child.EnsureChildCount(dictionary.ToList(), _templateVotes);
		child.InitializeChildren(dictionary.ToList(), InitializeVoteTotal);
		card.GetOrAddComponent<ElectionDayRundownAnimator>().SetModel(dictionary, Game.ctx.scenario.MakeSeededRng<ElectionDayPopup>(), election.result.winner);
	}

	public void InitializeVoteTotal(int count, GameObject card, KeyValuePair<EntityID, Election.CandidateInfo> candidate)
	{
		string text = ((candidate.Value.sponsor == PlayerID.HumanPlayer) ? (Loc.Get("ui.crew-boss") + " ") : "");
		card.SetText("Name", text + candidate.Key.FindEntity().data.person.LastName);
		card.GetText("Percentage Bar/Text").color = ColorConstants.PoliticianColors[candidate.Value.colorIndex].dark;
		card.GetChild("Percentage Bar").GetComponent<Image>().color = ColorConstants.PoliticianColors[candidate.Value.colorIndex].primary;
	}

	public void RefreshResults()
	{
		_go.SetActive("Panel/Election Info/Winfo", value: true);
		_go.SetActive("Panel/Election Info/Counting", value: false);
		List<EntityID> list = (from x in pManager.GetWards()
			select x.mostRecentElectionResult.winner into x
			where pManager.GetPoliticianData(x).relToHumanInLastElection == PoliticalRelationshipType.Sponsored
			select x).ToList();
		int count = list.Count;
		Fixnum fixnum = list.Select((EntityID x) => pManager.GetHumanInfluenceFrom(x)).Sum();
		_go.SetText("Panel/Election Info/Winfo/Win Count/Text", Loc.GetPluralized("ui.election-day.total-wins", count, "num", count));
		_go.SetText("Panel/Election Info/Winfo/Influence Gain", Loc.Get("ui.election-day.influence-gained", "influence", fixnum));
	}

	public float GetVariance(Election election)
	{
		float average = (float)election.candidateStats.Select((KeyValuePair<EntityID, Election.CandidateInfo> x) => x.Value.votes).Sum() / (float)election.candidateStats.Count();
		return election.candidateStats.Select((KeyValuePair<EntityID, Election.CandidateInfo> x) => Math.Abs((float)x.Value.votes - average)).Sum() / (float)election.candidateStats.Count();
	}

	public void OnStartCountClick()
	{
		startCount = true;
		_go.GetChild("Panel/Election Info/Inactive Grey Out").SetActive(value: false);
		_go.GetChild("Panel/Election Info/Start Animation Button").SetActive(value: false);
		_go.GetChild("Panel/Election Info/Counting").SetActive(value: true);
	}
}
