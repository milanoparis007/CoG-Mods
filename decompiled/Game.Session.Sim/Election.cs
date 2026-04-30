using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Services;
using Game.Session.Board;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Player;
using Game.UI.Session;
using Game.UI.Session.Tickers;
using Game.UI.Util;
using SomaSim.Util;
using UnityEngine;

namespace Game.Session.Sim;

public sealed class Election
{
	public struct ElectionResult
	{
		public EntityID winner;

		public Dictionary<EntityID, CandidateInfo> stats;

		public List<ElectionEvent> events;

		public SimTime year;

		public ElectionResult(EntityID winner, Dictionary<EntityID, CandidateInfo> stats, List<ElectionEvent> events)
		{
			this.winner = winner;
			this.stats = stats;
			this.events = events;
			year = Game.ctx.clock.Now;
		}
	}

	public struct ElectionEvent
	{
		public Label id;

		public EntityID candidate;

		public string desc;

		public ElectionEvent(EntityID candidate, Label id, string desc)
		{
			this.id = id;
			this.candidate = candidate;
			this.desc = desc;
		}
	}

	public class ElectionStartupPerformanceMarkers
	{
		public int experience = 1;

		public int businessConnections = 1;

		public int ethnicity = 1;

		public int traitScore = 1;
	}

	public enum SupportLevel
	{
		None,
		Donor,
		Sponsor,
		Nominator
	}

	public class CandidateInfo
	{
		public PlayerID sponsor;

		public SupportLevel support;

		public int votes;

		public Fixnum warchest;

		public int colorIndex;

		public CandidateInfo()
		{
		}

		public CandidateInfo(PlayerID sponsor, SupportLevel support, int votes, Fixnum warchest, int colorIndex)
		{
			this.sponsor = sponsor;
			this.support = support;
			this.votes = votes;
			this.warchest = warchest;
			this.colorIndex = colorIndex;
		}
	}

	public enum ElectionStage
	{
		Offcycle,
		Nomination,
		Campaign,
		ElectionWeek,
		PostElection,
		Legislation
	}

	public PrecinctID ward;

	public int totalVoters;

	public ElectionStage stage;

	public Xorshift rng;

	public List<EntityID> allCandidates;

	public Dictionary<EntityID, CandidateInfo> candidateStats;

	public Dictionary<EntityID, SimTime> nextAction;

	public ElectionResult result;

	public ElectionResult displayModel;

	public List<ElectionEvent> electionLog = new List<ElectionEvent>();

	public static readonly Label HUMAN_CAMPAIGN_COOLDOWN_BUFF_ID = new Label("relbuff-recent-campaign-action");

	public static readonly Fixnum TIEBREAK_PERCENTAGE = new Fixnum(1);

	public const float Z_SCORE_FOR_95_CONFIDENCE_LEVEL = 1.96f;

	public bool IsUnresolved
	{
		get
		{
			if (stage != ElectionStage.Nomination)
			{
				return stage == ElectionStage.Campaign;
			}
			return true;
		}
	}

	public PoliticsSettings Settings => Game.serv.globals.settings.politics;

	public Ward ElectionWard => Game.ctx.simman.politics.GetWardForID(ward);

	public Election()
	{
	}

	public Election(PrecinctID ward, List<EntityID> candidates, int totalVoters, Xorshift rng)
	{
		this.ward = ward;
		allCandidates = candidates;
		this.totalVoters = totalVoters;
		stage = ElectionStage.Offcycle;
		candidateStats = new Dictionary<EntityID, CandidateInfo>();
		nextAction = new Dictionary<EntityID, SimTime>();
		this.rng = rng;
	}

	public void UpdateVotesByValue(EntityID targetCandidate, int votes)
	{
		bool flag = votes < 0;
		int value = Math.Abs(votes);
		int num = MathUtil.ClampMin(totalVoters - candidateStats[targetCandidate].votes, 0);
		int max = (flag ? candidateStats[targetCandidate].votes : num);
		int num2 = MathUtil.ClampMax(value, max);
		int num3 = 0;
		if (num2 == 0)
		{
			return;
		}
		foreach (EntityID allCandidate in allCandidates)
		{
			if (allCandidate == targetCandidate)
			{
				if (flag)
				{
					candidateStats[allCandidate].votes -= num2;
				}
				else
				{
					candidateStats[allCandidate].votes += num2;
				}
				continue;
			}
			float num4 = (float)candidateStats[allCandidate].votes / (float)num;
			int num5 = MathUtil.ClampMax(max: Math.Min(num2 - num3, candidateStats[allCandidate].votes), value: (int)Math.Ceiling(num4 * (float)num2));
			if (flag)
			{
				num5 *= -1;
			}
			candidateStats[allCandidate].votes -= num5;
			num3 += Math.Abs(num5);
		}
	}

	public void UpdateVotesByPercentage(EntityID targetCandidate, Fixnum percentage)
	{
		UpdateVotesByValue(targetCandidate, (int)((float)totalVoters * ((float)percentage / 100f)));
	}

	public void TickElection()
	{
		TickTryAdvanceStage();
		TickUpdateModel();
		TickDisplayModel();
		if (stage == ElectionStage.ElectionWeek)
		{
			foreach (KeyValuePair<EntityID, CandidateInfo> stat in result.stats)
			{
				_ = stat;
			}
			return;
		}
		foreach (KeyValuePair<EntityID, CandidateInfo> candidateStat in candidateStats)
		{
			_ = candidateStat;
		}
	}

	public void TickDisplayModel()
	{
		switch (stage)
		{
		case ElectionStage.Nomination:
			displayModel = PredictWinnerByPolling();
			break;
		case ElectionStage.Campaign:
			displayModel = PredictWinnerByPolling(reportNewFrontrunner: true);
			break;
		case ElectionStage.ElectionWeek:
			displayModel = ElectionWard.mostRecentElectionResult;
			break;
		}
	}

	public void TickUpdateModel()
	{
		switch (stage)
		{
		case ElectionStage.Campaign:
			TryAICampaignActions();
			break;
		case ElectionStage.ElectionWeek:
			StoreElectionResultOnElectionDay();
			ElectionWard.mostRecentElectionResult = result;
			OnElectionComplete();
			break;
		}
	}

	public void TickTryAdvanceStage()
	{
		int month = Game.ctx.clock.Now.ToDate().Month;
		switch (stage)
		{
		case ElectionStage.Offcycle:
			SpinUpStartingElectionState();
			stage = ElectionStage.Nomination;
			ReportTicker(TickerIcon.POLITICS, TickerTitle.POLITICS, Loc.Get("ui.tickers.politics.nomination-start"));
			break;
		case ElectionStage.Nomination:
			if (month != Settings.elections.campaignStartMonth)
			{
				break;
			}
			stage = ElectionStage.Campaign;
			ReportTicker(TickerIcon.POLITICS, TickerTitle.POLITICS, Loc.Get("ui.tickers.politics.campaign-start"));
			{
				foreach (EntityID allCandidate in allCandidates)
				{
					nextAction[allCandidate] = Game.ctx.clock.Now.IncrementTurns((int)Settings.npcCandidateAI.smallActionCooldownWeekz);
				}
				break;
			}
		case ElectionStage.Campaign:
			if (month == Settings.elections.electionDayStartMonth)
			{
				stage = ElectionStage.ElectionWeek;
				ReportTicker(TickerIcon.POLITICS, TickerTitle.POLITICS, Loc.Get("ui.tickers.politics.election-day"));
			}
			break;
		case ElectionStage.ElectionWeek:
			if (stage == ElectionStage.ElectionWeek)
			{
				stage = ElectionStage.PostElection;
			}
			break;
		case ElectionStage.PostElection:
			if (month == Settings.elections.legislationStartMonth)
			{
				stage = ElectionStage.Legislation;
			}
			break;
		}
	}

	public int GetDaysLeftToNextStage()
	{
		switch (stage)
		{
		case ElectionStage.Nomination:
			return (Game.ctx.clock.GetFirstTurnOfMonthThisYear(Settings.elections.campaignStartMonth) - Game.ctx.clock.Now).deltadays;
		case ElectionStage.Campaign:
			return (Game.ctx.clock.GetFirstTurnOfMonthThisYear(Settings.elections.electionDayStartMonth) - Game.ctx.clock.Now).deltadays;
		case ElectionStage.ElectionWeek:
		case ElectionStage.PostElection:
			return (Game.ctx.clock.GetFirstTurnOfMonthThisYear(Settings.elections.legislationStartMonth) - Game.ctx.clock.Now).deltadays;
		case ElectionStage.Legislation:
			return (Game.ctx.clock.GetFirstTurnOfMonthThisYear(Settings.elections.legislationEndMonth) - Game.ctx.clock.Now).deltadays;
		default:
			return 0;
		}
	}

	public int GetWeeksLeftToNextStage()
	{
		return (int)Game.ctx.clock.DaysToTurns(GetDaysLeftToNextStage());
	}

	public void RefreshDisplayModel(bool redoPolls)
	{
		if (redoPolls)
		{
			displayModel = PredictWinnerByPolling();
		}
		else
		{
			displayModel = new ElectionResult(displayModel.winner, displayModel.stats, electionLog);
		}
	}

	public void OnElectionComplete()
	{
		PoliticsManager politics = Game.ctx.simman.politics;
		PlayerID sponsor = candidateStats[result.winner].sponsor;
		if (sponsor.IsValid && !sponsor.IsSystem)
		{
			politics.DoChangeInfluence(sponsor, GetInfluenceBoostValue(result.winner), result.winner);
		}
		if (result.winner == politics.GetWardForID(ward).currentPolitician)
		{
			politics.GetPoliticianData(result.winner).incumbencies++;
		}
		else
		{
			politics.RemovePoliticianFromWard(ward);
			politics.ElectPoliticianToWard(ward, result.winner);
		}
		if (!HasSponsoredACandidate(PlayerID.HumanPlayer))
		{
			return;
		}
		foreach (EntityID allCandidate in allCandidates)
		{
			if (candidateStats[allCandidate].sponsor == PlayerID.HumanPlayer)
			{
				if (allCandidate != result.winner)
				{
					politics.UnassignPoliticianToBoard(allCandidate);
				}
				Game.ctx.simman.politics.GetPoliticianData(allCandidate).relToHumanInLastElection = PoliticalRelationshipType.Sponsored;
			}
			else
			{
				Game.ctx.simman.politics.GetPoliticianData(allCandidate).relToHumanInLastElection = PoliticalRelationshipType.Opponent;
			}
		}
	}

	public void OnElectionPeriodClose()
	{
	}

	public void SpinUpStartingElectionState()
	{
		Dictionary<EntityID, ElectionStartupPerformanceMarkers> electionStartupStats = new Dictionary<EntityID, ElectionStartupPerformanceMarkers>();
		int votesGivenTotal = 0;
		int num = 0;
		foreach (EntityID allCandidate in allCandidates)
		{
			candidateStats[allCandidate] = new CandidateInfo(PlayerID.System, SupportLevel.None, 0, 0, num);
			electionStartupStats[allCandidate] = new ElectionStartupPerformanceMarkers();
			EvaluateExperiencePerfomance(allCandidate);
			EvaluateBusinessConnectionsPerformance(allCandidate);
			EvaluateTraitPerformance(allCandidate);
			num++;
		}
		foreach (NodeID wardNode in ElectionWard.wardNodes)
		{
			EvaluateEachCandidatesEthnicPerformanceAtThisNode(wardNode.FindNode());
		}
		Fixnum fraction = Settings.elections.experienceStartupVoteFraction.Evaluate(default(ModQuery));
		DistributeExperienceVotes(fraction);
		Fixnum fraction2 = Settings.elections.businessStartupVoteFraction.Evaluate(default(ModQuery));
		DistributeBusinessVotes(fraction2);
		Fixnum fraction3 = Settings.elections.ethnicityStartupVoteFraction.Evaluate(default(ModQuery));
		DistributeEthnicVotes(fraction3);
		Fixnum fraction4 = Settings.elections.traitStartupVoteFraction.Evaluate(default(ModQuery));
		DistributeTraitVotes(fraction4);
		DistributeRemainingVotes();
		DistributeWarchest();
		void DistributeBusinessVotes(Fixnum fraction5)
		{
			DistributeVotesGivenWeightsAndFraction(electionStartupStats.Select((KeyValuePair<EntityID, ElectionStartupPerformanceMarkers> x) => (Key: x.Key, businessConnections: x.Value.businessConnections)).ToList(), fraction5);
		}
		void DistributeEthnicVotes(Fixnum fraction5)
		{
			DistributeVotesGivenWeightsAndFraction(electionStartupStats.Select((KeyValuePair<EntityID, ElectionStartupPerformanceMarkers> x) => (Key: x.Key, ethnicity: x.Value.ethnicity)).ToList(), fraction5);
		}
		void DistributeExperienceVotes(Fixnum fraction5)
		{
			DistributeVotesGivenWeightsAndFraction(electionStartupStats.Select((KeyValuePair<EntityID, ElectionStartupPerformanceMarkers> x) => (Key: x.Key, experience: x.Value.experience)).ToList(), fraction5);
		}
		void DistributeRemainingVotes()
		{
			int num2 = 0;
			int num3;
			for (; votesGivenTotal < totalVoters; votesGivenTotal += num3)
			{
				EntityID key = allCandidates[num2 % allCandidates.Count];
				num3 = MathUtil.ClampMax(rng.DieRoll(totalVoters / allCandidates.Count), totalVoters - votesGivenTotal);
				candidateStats[key].votes += num3;
				num2++;
			}
		}
		void DistributeTraitVotes(Fixnum fraction5)
		{
			DistributeVotesGivenWeightsAndFraction(electionStartupStats.Select((KeyValuePair<EntityID, ElectionStartupPerformanceMarkers> x) => (Key: x.Key, traitScore: x.Value.traitScore)).ToList(), fraction5);
		}
		void DistributeVotesGivenWeightsAndFraction(List<(EntityID candidate, int weight)> weights, Fixnum fixnum)
		{
			int num2 = weights.Select(((EntityID candidate, int weight) x) => x.weight).Sum();
			int num3 = (int)Math.Floor((float)totalVoters * (float)fixnum);
			int num4 = 0;
			foreach (var weight in weights)
			{
				int num5 = MathUtil.ClampMax((int)Math.Ceiling((float)weight.weight / (float)num2 * (float)num3), num3 - num4);
				candidateStats[weight.candidate].votes += num5;
			}
			votesGivenTotal += num3;
		}
		void DistributeWarchest()
		{
			foreach (EntityID allCandidate2 in allCandidates)
			{
				ModQuery query = new ModQuery(PlayerID.HumanPlayer, allCandidate2, Game.ctx.players.Human.crew.GetCrewForPlayerPeep().peepId, ward.FindWard().wardOffice.FindEntity().components.board.GetNodeID(), Game.ctx.clock.Now);
				candidateStats[allCandidate2].warchest = Settings.elections.warchestPerVote.Evaluate(query) * candidateStats[allCandidate2].votes;
			}
		}
		void EvaluateBusinessConnectionsPerformance(EntityID candidate)
		{
			electionStartupStats[candidate].businessConnections += Game.ctx.simman.politics.GetNumBizConnections(candidate);
		}
		void EvaluateEachCandidatesEthnicPerformanceAtThisNode(Node node)
		{
			foreach (EntityID allCandidate3 in allCandidates)
			{
				if (allCandidate3.FindEntity().data.person.Ethnicity == Game.ctx.board.GetMainEthnicityAtNode(node))
				{
					electionStartupStats[allCandidate3].ethnicity++;
				}
			}
		}
		void EvaluateExperiencePerfomance(EntityID candidate)
		{
			PoliticianData politicianData = Game.ctx.simman.politics.GetPoliticianData(candidate);
			if (candidate == ElectionWard.currentPolitician)
			{
				electionStartupStats[candidate].experience += 5;
			}
			electionStartupStats[candidate].experience += politicianData.incumbencies;
		}
		void EvaluateTraitPerformance(EntityID candidate)
		{
			foreach (Label traitId in candidate.FindEntity().data.person.traitIds)
			{
				if (Game.ctx.simman.politics.POSITIVE_POL_TRAITS.Contains(traitId))
				{
					electionStartupStats[candidate].traitScore++;
				}
				if (Game.ctx.simman.politics.NEGATIVE_POL_TRAITS.Contains(traitId) && electionStartupStats[candidate].traitScore > 1)
				{
					electionStartupStats[candidate].traitScore--;
				}
			}
		}
	}

	public Fixnum GetCurrentCandidatePercent(EntityID candidate)
	{
		return new Fixnum((float)candidateStats[candidate].votes / (float)totalVoters * 100f);
	}

	public Fixnum GetCurrentCandidatePercentInDisplay(EntityID candidate)
	{
		return new Fixnum((float)displayModel.stats[candidate].votes / (float)totalVoters * 100f);
	}

	public bool IsCandidateSponsored(EntityID candidate)
	{
		return candidateStats[candidate].sponsor.IsPlayerOtherThan(PlayerID.System);
	}

	public bool HasSponsoredACandidate(PlayerID player)
	{
		return candidateStats.Select((KeyValuePair<EntityID, CandidateInfo> x) => x.Value.sponsor).Contains(player);
	}

	public EntityID GetSponsoredCandidate(PlayerID player)
	{
		foreach (KeyValuePair<EntityID, CandidateInfo> candidateStat in candidateStats)
		{
			if (candidateStat.Value.sponsor == player)
			{
				return candidateStat.Key;
			}
		}
		return EntityID.INVALID;
	}

	public void AddCandidateToElection(EntityID candidate)
	{
		allCandidates.Add(candidate);
		SpinUpStartingElectionState();
		RefreshDisplayModel(redoPolls: true);
	}

	public void DoSponsorCandidate(PlayerID player, EntityID candidate, SupportLevel type)
	{
		candidateStats[candidate].sponsor = player;
		candidateStats[candidate].support = type;
		RefreshDisplayModel(redoPolls: false);
		bool num = ElectionWard.currentPolitician == candidate;
		EntityID iNVALID = EntityID.INVALID;
		if (!num)
		{
			iNVALID = Game.ctx.simman.politics.FindRandomResidenceForPolitician(ward);
			Game.ctx.simman.politics.AssignPoliticianToBoard(candidate, iNVALID);
		}
		else
		{
			iNVALID = ElectionWard.wardOffice;
		}
		PersonInfoUtil.TweenCameraToEntity(iNVALID);
		Game.ctx.events.EnqueueOnce(SessionEventType.ElectionInteraction);
	}

	public void TryUpgradeSponsorLevel(EntityID candidate, SupportLevel type)
	{
		if (candidateStats[candidate].support <= type)
		{
			candidateStats[candidate].support = type;
		}
	}

	public void TryAICampaignActions()
	{
		if (allCandidates.Count == 1 || Game.ctx.clock.GetFirstTurnOfMonthThisYear(Settings.elections.electionDayStartMonth).IncrementTurns(-1).days == Game.ctx.clock.Now.days)
		{
			return;
		}
		foreach (EntityID allCandidate in allCandidates)
		{
			if (nextAction[allCandidate] <= Game.ctx.clock.Now)
			{
				TryAICampaignAction(allCandidate);
			}
		}
	}

	public void TryAICampaignAction(EntityID candidate)
	{
		PlayerID sponsor = candidateStats[candidate].sponsor;
		Fixnum probability = Settings.npcCandidateAI.largeActionPerformChance.Evaluate(new ModQuery(sponsor));
		Fixnum probability2 = Settings.npcCandidateAI.smallActionPerformChance.Evaluate(new ModQuery(sponsor));
		if (rng.CheckProbability(probability))
		{
			PerformAICampaignAction(candidate, isLargeAction: true);
		}
		else if (rng.CheckProbability(probability2))
		{
			PerformAICampaignAction(candidate, isLargeAction: false);
		}
	}

	public void PerformAICampaignAction(EntityID candidate, bool isLargeAction)
	{
		List<PoliticsSettings.CandidateAction> source = (isLargeAction ? Settings.npcCandidateAI.largeActionDefs : Settings.npcCandidateAI.smallActionDefs);
		bool isHuman = candidateStats[candidate].sponsor == PlayerID.HumanPlayer;
		Label archetype = Game.ctx.simman.politics.GetPoliticianData(candidate).archetypeId;
		List<PoliticsSettings.CandidateAction> list = source.Where((PoliticsSettings.CandidateAction x) => CanPayFromWarchest(x) && IsNotHumanPerformingNPCOnlyAction(x) && IsValidForArchetype(x) && !ActionLockedByUsage(candidate, x)).ToList();
		PoliticsSettings.CandidateAction candidateAction = ((list.Count > 0) ? rng.PickElement(list) : null);
		if (candidateAction != null)
		{
			PerformCampaignAction(candidate, candidateAction);
			if (isLargeAction)
			{
				nextAction[candidate] = Game.ctx.clock.Now.IncrementTurns((int)Settings.npcCandidateAI.largeActionCooldownWeekz);
			}
			else
			{
				nextAction[candidate] = Game.ctx.clock.Now.IncrementTurns((int)Settings.npcCandidateAI.smallActionCooldownWeekz);
			}
		}
		bool CanPayFromWarchest(PoliticsSettings.CandidateAction x)
		{
			return x.warchestCost <= candidateStats[candidate].warchest;
		}
		bool IsNotHumanPerformingNPCOnlyAction(PoliticsSettings.CandidateAction x)
		{
			if (!isHuman || x.NPCOnly)
			{
				return !isHuman;
			}
			return true;
		}
		bool IsValidForArchetype(PoliticsSettings.CandidateAction x)
		{
			if (x.validArchetypes == null)
			{
				return true;
			}
			if (x.validArchetypes.Contains(archetype))
			{
				return true;
			}
			return false;
		}
	}

	public void PerformPlayerCampaignAction(PlayerID player, Label actionID, bool shouldCauseCooldown = true)
	{
		PoliticsSettings.CandidateAction action = Settings.FindPlayerCandidateAction(actionID);
		EntityID sponsoredCandidate = GetSponsoredCandidate(player);
		PerformCampaignAction(sponsoredCandidate, action);
		if (shouldCauseCooldown)
		{
			Relationship item = Game.ctx.players.Human.social.FindOrMakeRelationshipsWith(sponsoredCandidate).from;
			CrewAssignment crewForPlayerPeep = Game.ctx.players.Human.crew.GetCrewForPlayerPeep();
			item.AddBuff(HUMAN_CAMPAIGN_COOLDOWN_BUFF_ID, crewForPlayerPeep.peepId);
			RefreshDisplayModel(redoPolls: true);
		}
	}

	public void PerformCampaignAction(EntityID candidate, PoliticsSettings.CandidateAction action)
	{
		ModifyCandidateWarchest(candidate, -action.warchestCost);
		Fixnum fixnum = action.maxVoteEffect - action.minVoteEffect;
		Fixnum percentage = action.minVoteEffect + fixnum * new Fixnum(rng.GenerateFloat());
		Color item = ColorConstants.PoliticianColors[candidateStats[candidate].colorIndex].dark;
		UpdateVotesByPercentage(candidate, percentage);
		string text = Loc.Get("politicalaction.npc.name", "name", TextUtil.ColorWrap(candidate.FindEntity().data.person.FullName, item));
		LogElectionEvent(candidate, action.id, Loc.Get(action.locdesc, "candidate", text));
	}

	public bool ActionLockedByUsage(EntityID candidate, PoliticsSettings.CandidateAction action)
	{
		List<ElectionEvent> source = electionLog.Where((ElectionEvent x) => x.id == action.id).ToList();
		if (action.oncePerCandidate)
		{
			return source.FirstOrDefault((ElectionEvent x) => x.candidate == candidate).candidate.IsValid;
		}
		if (action.oncePerElection)
		{
			return source.Count() > 0;
		}
		return false;
	}

	public void LogElectionEvent(EntityID candidate, Label id, string actionString)
	{
		electionLog.Insert(0, new ElectionEvent(candidate, id, actionString));
	}

	public void StoreElectionResultOnElectionDay()
	{
		int mostPercentage = candidateStats.Keys.Select((EntityID x) => (int)GetCurrentCandidatePercent(x)).Max();
		List<KeyValuePair<EntityID, CandidateInfo>> list = candidateStats.Where((KeyValuePair<EntityID, CandidateInfo> x) => (int)GetCurrentCandidatePercent(x.Key) == mostPercentage).ToList();
		if (list.Count() > 1)
		{
			EntityID key = rng.PickElement(list).Key;
			UpdateVotesByPercentage(key, TIEBREAK_PERCENTAGE);
		}
		result = PredictWinnerPerfect();
	}

	public ElectionResult PredictWinnerPerfect()
	{
		if (allCandidates.Count == 1)
		{
			return new ElectionResult(allCandidates[0], candidateStats, electionLog);
		}
		Dictionary<EntityID, CandidateInfo> model = Game.serv.serializer.instance.Clone(candidateStats);
		return PredictWinnerGivenModel(model);
	}

	public ElectionResult PredictWinnerByPolling(bool reportNewFrontrunner = false)
	{
		if (allCandidates.Count == 1)
		{
			return new ElectionResult(allCandidates[0], candidateStats, electionLog);
		}
		Dictionary<EntityID, CandidateInfo> model = CreateModelBySampling(Settings.elections.baseSamplingNumber);
		ElectionResult electionResult = PredictWinnerGivenModel(model);
		if (reportNewFrontrunner && electionResult.winner != displayModel.winner && displayModel.winner.IsValid && HasSponsoredACandidate(PlayerID.HumanPlayer))
		{
			string message = Loc.Get("ui.tickers.politics.frontrunner-changed", "wardname", Game.ctx.simman.politics.GetWardForID(ward).WardName, "previous", displayModel.winner.FindEntity().data.person.FullName, "current", electionResult.winner.FindEntity().data.person.FullName);
			ReportWardTicker(TickerIcon.POLITICS, TickerTitle.POLITICS, message, Game.ctx.simman.politics.GetWardForID(ward).wardOffice);
		}
		return electionResult;
	}

	public ElectionResult PredictWinnerGivenModel(Dictionary<EntityID, CandidateInfo> model)
	{
		int mostVotes = model.Values.Select((CandidateInfo x) => x.votes).Max();
		List<KeyValuePair<EntityID, CandidateInfo>> list = model.Where((KeyValuePair<EntityID, CandidateInfo> x) => x.Value.votes == mostVotes).ToList();
		EntityID iNVALID = EntityID.INVALID;
		if (list.Count() > 1)
		{
			iNVALID = rng.PickElement(list).Key;
			model[iNVALID].votes++;
		}
		else
		{
			iNVALID = list.FirstOrDefault().Key;
		}
		return new ElectionResult(iNVALID, model, electionLog);
	}

	public Dictionary<EntityID, CandidateInfo> CreateModelBySampling(int numSamples)
	{
		Xorshift xorshift = Game.ctx.scenario.MakeSeededRng((uint)((Game.ctx.clock.Now.days << 8) ^ ward.id));
		int num = 0;
		List<EntityID> list = candidateStats.Select((KeyValuePair<EntityID, CandidateInfo> x) => x.Key).ToList();
		List<float> weights = ((IEnumerable<KeyValuePair<EntityID, CandidateInfo>>)candidateStats).Select((Func<KeyValuePair<EntityID, CandidateInfo>, float>)((KeyValuePair<EntityID, CandidateInfo> x) => x.Value.votes)).ToList();
		Dictionary<EntityID, int> dictionary = new Dictionary<EntityID, int>();
		for (; num < numSamples; num++)
		{
			EntityID key = xorshift.PickElement(list, weights);
			dictionary.Increment(key, 1);
		}
		Dictionary<EntityID, CandidateInfo> dictionary2 = Game.serv.serializer.instance.Clone(candidateStats);
		int num2 = totalVoters;
		foreach (KeyValuePair<EntityID, int> item in dictionary)
		{
			int num3 = MathUtil.ClampMax((int)Math.Ceiling((float)item.Value / (float)numSamples * (float)totalVoters), num2);
			num2 -= num3;
			dictionary2[item.Key].votes = num3;
		}
		return dictionary2;
	}

	public Fixnum CalculateSamplingError(EntityID candidate, float numSamples)
	{
		float num = (float)displayModel.stats[candidate].votes / (float)totalVoters;
		return new Fixnum((float)(Math.Sqrt(num * (1f - num) / numSamples) * Math.Sqrt(((float)totalVoters - numSamples) / (float)(totalVoters - 1))) * 1.96f);
	}

	public void ReportTicker(TickerIcon icon, TickerTitle title, string message, TickerTarget target = default(TickerTarget), TickerPersistType persisted = TickerPersistType.Temporary)
	{
		Game.ctx.simman.politics.ReportTicker(new TickerData
		{
			type = TickerType.TextPopup,
			icon = icon,
			title = title,
			message = message,
			target = target,
			date = Game.ctx.clock.Now,
			persisted = persisted
		});
	}

	public void ReportWardTicker(TickerIcon icon, TickerTitle title, string message, TickerTarget target = default(TickerTarget), TickerPersistType persisted = TickerPersistType.Temporary)
	{
		Game.ctx.simman.politics.ReportTicker(new TickerWardData
		{
			type = TickerType.TextPopup,
			icon = icon,
			title = title,
			message = message,
			target = target,
			date = Game.ctx.clock.Now,
			persisted = persisted
		});
	}

	public void ModifyCandidateWarchest(EntityID candidate, Fixnum delta)
	{
		Fixnum fixnum = ((candidateStats[candidate].warchest + delta > 0) ? delta : (-candidateStats[candidate].warchest));
		candidateStats[candidate].warchest += fixnum;
		RefreshDisplayModel(redoPolls: false);
	}

	public Fixnum GetCandidateWarchest(EntityID candidate)
	{
		return candidateStats[candidate].warchest;
	}

	public int GetInfluenceBoostValue(EntityID candidate)
	{
		PoliticianData politicianData = Game.ctx.simman.politics.GetPoliticianData(candidate);
		if (candidateStats[candidate].support == SupportLevel.Donor)
		{
			return Settings.elections.donorInfluenceGain;
		}
		PoliticsSettings.PoliticalArchetype politicalArchetype = Settings.FindPoliticalArchetype(politicianData.archetypeId);
		Fixnum fixnum = new Fixnum(100f * ((float)result.stats[candidate].votes / (float)totalVoters)) * Settings.elections.influencePerPercentAtWin.Evaluate(candidateStats[candidate].sponsor, candidate.FindEntity(), null);
		Fixnum fixnum2 = result.stats[candidate].warchest / 100 * Settings.elections.influencePer100InWarchestAtWin;
		return ((fixnum + fixnum2) * politicalArchetype.influenceMultiplier * GetMultiplierForSupportLevel(candidateStats[candidate].support)).IntCeiling();
	}

	public Fixnum GetMultiplierForSupportLevel(SupportLevel support)
	{
		switch (support)
		{
		case SupportLevel.None:
		case SupportLevel.Donor:
			return 0;
		case SupportLevel.Sponsor:
			return Settings.elections.sponsorInfluenceMultiplier;
		case SupportLevel.Nominator:
			return Settings.elections.nominatorInfluenceMultiplier;
		default:
			return 0;
		}
	}
}
