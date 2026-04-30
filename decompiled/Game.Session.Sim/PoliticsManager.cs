using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Game.Core;
using Game.Services;
using Game.Services.Store;
using Game.Session.Board;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Player;
using Game.Session.Tutorial;
using Game.UI.Session.Politics;
using Game.UI.Util;
using SomaSim.SION;
using SomaSim.Util;

namespace Game.Session.Sim;

public sealed class PoliticsManager : ISystemTurnSubManager<SimulationManager>, ISubManager<SimulationManager>, ISaveLoadProvider
{
	private PoliticsManagerPersistedData _pdata;

	private LawCallbackManager _lawCallbacks;

	public List<TickerData> tickersToReport = new List<TickerData>();

	public readonly List<Label> POSITIVE_POL_TRAITS = new List<Label>
	{
		new Label("trait-sociable"),
		new Label("trait-confident"),
		new Label("trait-cruel"),
		new Label("trait-talkative"),
		new Label("trait-agressive"),
		new Label("trait-friendly"),
		new Label("trait-intelligent"),
		new Label("trait-immoral")
	};

	public readonly List<Label> NEGATIVE_POL_TRAITS = new List<Label>
	{
		new Label("trait-careless"),
		new Label("trait-loner"),
		new Label("trait-nervous"),
		new Label("trait-quiet"),
		new Label("trait-irritable"),
		new Label("trait-mellow"),
		new Label("trait-slow"),
		new Label("trait-irreverant")
	};

	public static readonly Label NO_ARCHETYPE = new Label("standard");

	public PoliticsSettings Settings => Game.serv.globals.settings.politics;

	public void OnSystemTurn()
	{
		if (!Game.serv.store.IsPackInstalled(PackID.ShadowGovernment))
		{
			return;
		}
		foreach (Ward ward2 in _pdata.wards)
		{
			ward2.OnSystemTurn();
		}
		if (IsTimeToRevokeTempLaws())
		{
			TryRevokeTempLaws();
		}
		PostReportedTickers();
		tickersToReport.Clear();
		if (IsTimeToTutorialize())
		{
			Ward ward = Game.ctx.players.Human.territory.Safehouse.FindEntity().components.board.GetNode().precinctId.FindWard();
			Game.ctx.quests.StartQuest(TutorialManager.QUEST_NY_POLITICS_STARTER, ward.currentPolitician, fromRequest: false);
			string text = Loc.FormatDate(Game.ctx.clock.GetFirstTurnOfMonthThisYear(Settings.elections.campaignStartMonth), showyear: true);
			Game.ctx.hud.tickers.AddTextTicker(TickerIcon.POLITICS, TickerTitle.POLITICS, Loc.Get("ui.tickers.politics.ny-first-quest", "date", text), TickerTarget.INVALID, TickerPersistType.PolTutorialPersist);
		}
		if (ShouldShowLegislativeSessionTicker())
		{
			Game.ctx.hud.tickers.AddLawbookTicker(Loc.Get("ui.tickers.politics.legislation-start"));
		}
		if (GetElectionStage() == Election.ElectionStage.ElectionWeek)
		{
			Game.serv.ui.AddPopup(new ElectionDayPopup());
			Game.ctx.events.EnqueueOnce(SessionEventType.ElectionEnded);
		}
	}

	public IEnumerable<Ward> GetWards()
	{
		return _pdata.wards;
	}

	public void Initialize(SimulationManager manager)
	{
		_pdata = new PoliticsManagerPersistedData();
		_lawCallbacks = new LawCallbackManager();
		_lawCallbacks.Initialize();
		InitializeConsoleEntries();
	}

	public void Release()
	{
		ReleaseConsoleEntries();
	}

	public bool IsTimeToRevokeTempLaws()
	{
		int legislationStartMonth = Settings.elections.legislationStartMonth;
		int yearsBetweenElections = Settings.elections.yearsBetweenElections;
		if ((Game.ctx.clock.Now.ToDate().Year - (Game.ctx.clock.State.turnOneDate.ToDate().Year + 1)) % yearsBetweenElections == 0)
		{
			return Game.ctx.clock.Now.days == Game.ctx.clock.GetFirstTurnOfMonthThisYear(legislationStartMonth).days;
		}
		return false;
	}

	public bool IsTimeToStartElection()
	{
		int nominationStartMonth = Settings.elections.nominationStartMonth;
		int yearsBetweenElections = Settings.elections.yearsBetweenElections;
		if ((Game.ctx.clock.Now.ToDate().Year - Game.ctx.clock.State.turnOneDate.ToDate().Year) % yearsBetweenElections == 0)
		{
			return Game.ctx.clock.Now.ToDate().Month == nominationStartMonth;
		}
		return false;
	}

	public bool IsTimeToEndElection()
	{
		int legislationEndMonth = Settings.elections.legislationEndMonth;
		int yearsBetweenElections = Settings.elections.yearsBetweenElections;
		if ((Game.ctx.clock.Now.ToDate().Year - (Game.ctx.clock.State.turnOneDate.ToDate().Year + 1)) % yearsBetweenElections == 0)
		{
			return Game.ctx.clock.Now.ToDate().Month == legislationEndMonth;
		}
		return false;
	}

	public bool IsTimeToTutorialize()
	{
		bool num = Game.ctx.session.mapconfig.id == "new-york";
		bool flag = Game.ctx.clock.CurrentTurn == 2 && Game.ctx.clock.Now >= Game.ctx.clock.LastDayOfProcGen;
		return num && flag;
	}

	public bool ShouldShowLegislativeSessionTicker()
	{
		if (Game.ctx.clock.Now >= Game.ctx.clock.LastDayOfProcGen && Game.ctx.clock.Now.ToDate().Month == Settings.elections.legislationStartMonth)
		{
			return Game.ctx.clock.Now.IncrementTurns(-1).ToDate().Month != Settings.elections.legislationStartMonth;
		}
		return false;
	}

	[Conditional("UNITY_EDITOR")]
	private void VerifyDataConsistency()
	{
	}

	public PoliticianData GetPoliticianData(EntityID person)
	{
		return _pdata.politicians.Find((PoliticianData x) => x.id == person);
	}

	public void AssignPoliticianToBoard(EntityID person, EntityID building)
	{
		GetPoliticianData(person).location = building;
		if (building.FindEntity().components.building.IsCivicBuildingType)
		{
			building.FindEntity().components.civic.SetNpc(person, expectedEmpty: true);
			return;
		}
		building.FindEntity().components.residence.MarkAsPoliticianResidence();
		building.FindEntity().components.residence.SetNpcResident(person, expectedEmpty: true);
		if (!building.FindEntity().components.building.IsScopedBy(PlayerID.HumanPlayer))
		{
			PlayerInfo human = Game.ctx.players.Human;
			human.meetings.MarkNodeAsKnown(building.FindEntity().components.board.GetNode(), expectedSeen: true, instant: false);
			human.territory.ScopeOutBuilding(building.FindEntity(), procgen: false, setControlled: false);
		}
	}

	public void UnassignPoliticianToBoard(EntityID person)
	{
		PoliticianData politicianData = GetPoliticianData(person);
		EntityID location = politicianData.location;
		if (location.FindEntity().components.building.IsCivicBuildingType)
		{
			politicianData.location.FindEntity().components.civic.ClearNpc();
		}
		else
		{
			location.FindEntity().components.residence.UnmarkAsPoliticianResidence();
			politicianData.location.FindEntity().components.residence.ClearNpcResident();
			if (!location.FindEntity().components.building.IsScopedBy(PlayerID.HumanPlayer))
			{
				PlayerInfo human = Game.ctx.players.Human;
				human.meetings.MarkNodeAsKnown(location.FindEntity().components.board.GetNode(), expectedSeen: true, instant: false);
				human.territory.ScopeOutBuilding(location.FindEntity(), procgen: false, setControlled: false);
			}
		}
		politicianData.location = EntityID.INVALID;
	}

	public bool IsValidResForPolitician(Entity building)
	{
		if (building?.components.residence?.IsNotAssigned == true && !_pdata.politicians.Select((PoliticianData x) => x.location).Contains(building.Id))
		{
			if (building == null)
			{
				return false;
			}
			return !building.components.building.IsSafehouse;
		}
		return false;
	}

	public EntityID FindRandomResidenceForPolitician(PrecinctID wardID)
	{
		Ward wardForID = GetWardForID(wardID);
		List<EntityID> list = new List<EntityID>();
		foreach (NodeID wardNode in wardForID.wardNodes)
		{
			list.AddRange(wardNode.FindNode().contained.Where((EntityID x) => IsValidResForPolitician(x.FindEntity())));
		}
		return _pdata.rng.PickElement(list);
	}

	public bool IsPolitician(EntityID person)
	{
		return GetPoliticianData(person) != null;
	}

	public bool IsPoliticianIncumbent(EntityID person)
	{
		Ward wardForID = GetWardForID(GetPoliticianData(person).location.FindEntity()?.components.board?.GetNode()?.precinctId ?? PrecinctID.INVALID);
		if (wardForID == null)
		{
			return false;
		}
		return wardForID.currentPolitician == person;
	}

	public void CreatePoliticianData(EntityID person, PoliticalType type)
	{
		PoliticianData politicianData = GetPoliticianData(person);
		Label archetypeId = GeneratePoliticalArchetypeFor(person);
		if (politicianData == null)
		{
			_pdata.politicians.Add(new PoliticianData(person, EntityID.INVALID, type, archetypeId));
		}
	}

	public void RemovePoliticianData(EntityID person, PoliticalType type)
	{
		PoliticianData politicianData = GetPoliticianData(person);
		if (politicianData.location.IsValid)
		{
			UnassignPoliticianToBoard(person);
		}
		_pdata.politicians.Remove(politicianData);
	}

	public void CreatePoliticianDataAndAssignToBoard(EntityID person, EntityID building, PoliticalType type)
	{
		CreatePoliticianData(person, type);
		AssignPoliticianToBoard(person, building);
	}

	public Ward GetWardForID(PrecinctID wardID)
	{
		return _pdata.wards.Find((Ward x) => x.id == wardID);
	}

	public Ward GetWardForPolitician(EntityID politician)
	{
		return _pdata.wards.Find((Ward x) => x.localPoliticians.Contains(politician));
	}

	public Ward GetWardForBuilding(EntityID building)
	{
		return GetWardForID(building.FindEntity()?.components.board?.GetNode().precinctId ?? PrecinctID.INVALID);
	}

	public void AddWard(PrecinctID precinctID, EntityID building, List<Node> precinctNodes)
	{
		Ward item = new Ward(precinctID, building, precinctNodes.Select((Node x) => x.id).ToList(), _pdata.rng);
		_pdata.wards.Add(item);
	}

	public void RemoveWard(PrecinctID precinctID)
	{
		Ward item = _pdata.wards.Find((Ward x) => x.id == precinctID);
		_pdata.wards.Remove(item);
	}

	public bool IsValidPolitician(Entity person)
	{
		PersonData person2 = person.data.person;
		if (person2.IsAlive && person2.GetAge(Game.ctx.clock.Now).YearsFloat >= 35f && person2.business.IsNotValid && person2.resassigned.IsNotValid && person.data.agent.pid.id == 0)
		{
			return !IsPolitician(person.Id);
		}
		return false;
	}

	public List<EntityID> FindPossiblePoliticiansForPrecinct(PrecinctID precinctID)
	{
		List<EntityID> list = new List<EntityID>();
		foreach (EntityID item in CopUtil.FindBuildingsInPrecinct(precinctID))
		{
			if (item.FindEntity().data.building.interesting)
			{
				_ = item.FindEntity().data.building.business;
				EntityID id = item.FindEntity().data.building.business.FindEntity().data.biz.owner.id;
				RelationshipList listOrNull = Game.ctx.simman.rels.GetListOrNull(id);
				list.AddRange((from x in listOrNull.data
					select x.to into x
					where IsValidPolitician(x.FindEntity())
					select x).ToList());
			}
		}
		if (list.Count == 0)
		{
			list = (from x in Game.ctx.simman.peoplegen.GetAllTrackedPeople()
				where IsValidPolitician(x)
				select x.Id).ToList();
		}
		return list;
	}

	public EntityID GetBestPolCandidateFrom(EntityID npc)
	{
		RelationshipList listOrNull = Game.ctx.simman.rels.GetListOrNull(npc);
		if (listOrNull == null || listOrNull.data.Count == 0)
		{
			return EntityID.INVALID;
		}
		new List<EntityID>();
		foreach (Relationship datum in listOrNull.data)
		{
			Entity entity = datum.to.FindEntity();
			if (IsValidPolitician(entity))
			{
				return entity.Id;
			}
		}
		return EntityID.INVALID;
	}

	public void RunNewGamePoliticianSetup()
	{
		foreach (PlayerInfo item in Game.ctx.players.all)
		{
			if (item.PID.IsHumanPlayer || item.IsJustGang)
			{
				_pdata.influence[item.PID] = new InfluenceWallet();
			}
		}
		foreach (Ward ward in _pdata.wards)
		{
			PrecinctID id = ward.id;
			ward.GenerateLocalPoliticians();
			List<EntityID> localPoliticians = ward.localPoliticians;
			EntityID politician = _pdata.rng.PickElement(localPoliticians);
			ElectPoliticianToWard(id, politician);
		}
	}

	public void EnactStarterPackLaws()
	{
		List<PoliticsSettings.LawStarterPack> list = Settings.lawSettings.starterPacks.Where((PoliticsSettings.LawStarterPack x) => x.visreqs.AllPass(new VisitState(CrewAssignment.EMPTY, Game.ctx.clock.Now, PlayerID.HumanPlayer))).ToList();
		List<float> weights = ((IEnumerable<PoliticsSettings.LawStarterPack>)list).Select((Func<PoliticsSettings.LawStarterPack, float>)((PoliticsSettings.LawStarterPack x) => x.weight)).ToList();
		PoliticsSettings.LawStarterPack lawStarterPack = _pdata.rng.PickElement(list, weights);
		for (int num = 0; num < lawStarterPack.numLawsToPick; num++)
		{
			List<Label> list2 = lawStarterPack.possibleLaws.Where((Label x) => !IsLawEnacted(x)).ToList();
			EnactLaw(Settings.FindLawDef(_pdata.rng.PickAndRemoveElement(list2)));
		}
	}

	public void ElectPoliticianToWard(PrecinctID wardID, EntityID politician)
	{
		Ward wardForID = GetWardForID(wardID);
		PoliticianData politicianData = GetPoliticianData(politician);
		if (politicianData == null)
		{
			CreatePoliticianDataAndAssignToBoard(politician, wardForID.wardOffice, PoliticalType.PoliticianElected);
		}
		else if (politicianData.location.IsValid)
		{
			UnassignPoliticianToBoard(politician);
			AssignPoliticianToBoard(politician, wardForID.wardOffice);
		}
		else
		{
			AssignPoliticianToBoard(politician, wardForID.wardOffice);
		}
		wardForID.SetCurrentPolitician(politician);
		politicianData.incumbencies++;
		politicianData.type = PoliticalType.PoliticianElected;
	}

	public void RemovePoliticianFromWard(PrecinctID wardID)
	{
		Ward wardForID = GetWardForID(wardID);
		EntityID currentPolitician = wardForID.currentPolitician;
		PoliticianData politicianData = GetPoliticianData(currentPolitician);
		UnassignPoliticianToBoard(currentPolitician);
		wardForID.ClearCurrentPolitician();
		politicianData.type = PoliticalType.PoliticianUnassigned;
	}

	public int GetNumBizConnections(EntityID candidate)
	{
		int num = 0;
		foreach (Relationship datum in Game.ctx.simman.rels.GetListOrNull(candidate).data)
		{
			Entity entity = BuildingUtil.FindBuildingForBizOwner(datum.to);
			if (entity != null && entity.components.modules != null)
			{
				num++;
			}
		}
		return num;
	}

	public Fixnum GetSponsorPrice(EntityID candidate)
	{
		return GetSupportPrice(candidate, Election.SupportLevel.Sponsor);
	}

	public Fixnum GetNominatePrice(EntityID candidate)
	{
		return GetSupportPrice(candidate, Election.SupportLevel.Nominator);
	}

	public Fixnum GetSupportPrice(EntityID candidate, Election.SupportLevel supportLevel)
	{
		Ward wardForPolitician = GetWardForPolitician(candidate);
		if (wardForPolitician == null)
		{
			return new Fixnum(0);
		}
		ModQuery query = new ModQuery(PlayerID.HumanPlayer, candidate, Game.ctx.players.Human.crew.GetCrewForPlayerPeep().peepId, wardForPolitician.wardOffice.FindEntity().components.board.GetNodeID(), Game.ctx.clock.Now);
		Fixnum fixnum = supportLevel switch
		{
			Election.SupportLevel.Nominator => Settings.elections.minimumNominatePrice.Evaluate(query), 
			Election.SupportLevel.Sponsor => Settings.elections.minimumSponsorPrice.Evaluate(query), 
			_ => new Fixnum(0), 
		};
		Election.CandidateInfo candidateInfo = wardForPolitician.currElection.candidateStats.FindOrNull(candidate);
		PoliticianData politicianData = GetPoliticianData(candidate);
		TagList traitIds = candidate.FindEntity().data.person.traitIds;
		if (candidateInfo != null)
		{
			fixnum += candidateInfo.votes * Settings.elections.supportPricePerVote;
		}
		fixnum *= Settings.FindPoliticalArchetype(politicianData.archetypeId).supportPriceMultiplier;
		foreach (Label item in traitIds)
		{
			if (POSITIVE_POL_TRAITS.Contains(item))
			{
				fixnum += Settings.elections.supportPricePerPositivePolTrait;
			}
		}
		if (shouldOverrideWithTutorialPrice())
		{
			fixnum = Settings.elections.supportPriceForTutorial;
		}
		return fixnum.Ceiling();
		bool shouldOverrideWithTutorialPrice()
		{
			return Game.ctx.players.Human.social.GetRelationshipFromPlayerTo(candidate)?.HasBuff(BuffConstants.RELBUFF_NY_POL_STARTER) ?? false;
		}
	}

	public (string basePrice, string modPrice, string votesPrice, string archetypePrice, string traitsPrice) GetNominatePriceExplanation(EntityID candidate)
	{
		return GetSupportPriceExplanation(candidate, Election.SupportLevel.Nominator);
	}

	public (string basePrice, string modPrice, string votesPrice, string archetypePrice, string traitsPrice) GetSponsorPriceExplanation(EntityID candidate)
	{
		return GetSupportPriceExplanation(candidate, Election.SupportLevel.Sponsor);
	}

	public (string basePrice, string modPrice, string votesPrice, string archetypePrice, string traitsPrice) GetSupportPriceExplanation(EntityID candidate, Election.SupportLevel supportLevel)
	{
		Ward wardForPolitician = GetWardForPolitician(candidate);
		ModQuery query = new ModQuery(PlayerID.HumanPlayer, candidate, Game.ctx.players.Human.crew.GetCrewForPlayerPeep().peepId, wardForPolitician.wardOffice.FindEntity().components.board.GetNodeID(), Game.ctx.clock.Now);
		Election.CandidateInfo candidateInfo = wardForPolitician.currElection.candidateStats.FindOrNull(candidate);
		TagList traitIds = candidate.FindEntity().data.person.traitIds;
		PoliticianData politicianData = GetPoliticianData(candidate);
		Fixnum value = supportLevel switch
		{
			Election.SupportLevel.Nominator => Settings.elections.minimumNominatePrice.value, 
			Election.SupportLevel.Sponsor => Settings.elections.minimumSponsorPrice.value, 
			_ => new Fixnum(0), 
		};
		string item = "\n" + Loc.Get("ui.politics.sponsor-price.mo.base", "value", TextUtil.ColorGreenRed(value, Loc.FormatNumberPlusMinus(value)));
		Fixnum fixnum = supportLevel switch
		{
			Election.SupportLevel.Nominator => Settings.elections.minimumNominatePrice.Evaluate(query), 
			Election.SupportLevel.Sponsor => Settings.elections.minimumSponsorPrice.Evaluate(query), 
			_ => new Fixnum(0), 
		};
		string text = supportLevel switch
		{
			Election.SupportLevel.Nominator => "\n" + Settings.elections.minimumNominatePrice.Explain(query, addHeader: false), 
			Election.SupportLevel.Sponsor => "\n" + Settings.elections.minimumSponsorPrice.Explain(query, addHeader: false), 
			_ => "", 
		};
		text = Loc.Get("ui.politics.sponsor-price.mo.mods", "text", text);
		Fixnum fixnum2 = ((Fixnum?)candidateInfo?.votes) ?? (0 * Settings.elections.supportPricePerVote);
		string item2 = "\n" + Loc.Get("ui.politics.sponsor-price.mo.votes", "value", TextUtil.ColorGreenRed(fixnum2, Loc.FormatNumberPlusMinus(fixnum2)));
		Fixnum value2 = (fixnum + fixnum2) * Settings.FindPoliticalArchetype(politicianData.archetypeId).supportPriceMultiplier - (fixnum + fixnum2);
		string item3 = "\n" + Loc.Get("ui.politics.sponsor-price.mo.archetype", "value", TextUtil.ColorGreenRed(value2, Loc.FormatNumberPlusMinus(value2)));
		int num = 0;
		foreach (Label item5 in traitIds)
		{
			if (POSITIVE_POL_TRAITS.Contains(item5))
			{
				num += (int)Settings.elections.supportPricePerPositivePolTrait;
			}
		}
		string item4 = "\n" + Loc.Get("ui.politics.sponsor-price.mo.traits", "value", TextUtil.ColorGreenRed(fixnum2, Loc.FormatNumberPlusMinus(num)));
		return (basePrice: item, modPrice: text, votesPrice: item2, archetypePrice: item3, traitsPrice: item4);
	}

	public InfluenceWallet GetInfluenceWallet(PlayerID pid)
	{
		return _pdata.influence[pid];
	}

	public Fixnum GetCurrentInfluence(PlayerID pid)
	{
		return _pdata.influence[pid].current;
	}

	public Fixnum GetHumanInfluenceEarned()
	{
		return _pdata.influence[PlayerID.HumanPlayer].influenceSources.Select((InfluenceSource x) => x.amount).Sum();
	}

	public List<InfluenceSource> GetHumanInfluenceBreakdown()
	{
		return _pdata.influence[PlayerID.HumanPlayer].influenceSources;
	}

	public Fixnum GetHumanInfluenceFrom(EntityID candidate)
	{
		return GetHumanInfluenceBreakdown().Find((InfluenceSource x) => x.source == candidate).amount;
	}

	public void DoChangeInfluence(PlayerID pid, int delta, EntityID from)
	{
		DoChangeInfluenceWithoutLogging(pid, delta);
		_pdata.influence[pid].influenceSources.Add(new InfluenceSource(from, delta));
	}

	public void DoChangeInfluenceWithoutLogging(PlayerID pid, int delta)
	{
		if (pid.IsHumanPlayer || pid.FindPlayer().IsJustGang)
		{
			_pdata.influence[pid].current += delta;
			if (_pdata.influence[pid].current >= _pdata.influence[pid].highwater)
			{
				_pdata.influence[pid].highwater = _pdata.influence[pid].current;
			}
			Game.ctx.events.EnqueueOnce(SessionEventType.InfluenceChange);
		}
	}

	public Election.ElectionStage GetElectionStage()
	{
		if (!_pdata.wards[0].ElectionOngoing)
		{
			return Election.ElectionStage.Offcycle;
		}
		return _pdata.wards[0].currElection.stage;
	}

	public List<Label> GetEnactedLaws()
	{
		return _pdata.laws;
	}

	public bool IsLawEnacted(Label lawId)
	{
		return _pdata.laws.Contains(lawId);
	}

	public void EnactLaw(PoliticsSettings.Law law)
	{
		if (!_pdata.laws.Contains(law.id))
		{
			_pdata.laws.Add(law.id);
		}
		_lawCallbacks.ProcessCallbackFromName(law.onEnactCallback);
		Game.ctx.events.EnqueueOnce(SessionEventType.LawChanged);
		Game.ctx.hud.tickers.AddLawTicker(Loc.Get("ui.tickers.law-enacted", "law", Loc.Get(law.locname), "cityName", Game.ctx.session.mapconfig.CityName), enacted: true, law);
	}

	public void RevokeLaw(PoliticsSettings.Law law)
	{
		if (_pdata.laws.Contains(law.id))
		{
			_pdata.laws.Remove(law.id);
		}
		_lawCallbacks.ProcessCallbackFromName(law.onRevokeCallback);
		Game.ctx.events.EnqueueOnce(SessionEventType.LawChanged);
		Game.ctx.hud.tickers.AddLawTicker(Loc.Get("ui.tickers.law-revoked", "law", Loc.Get(law.locname), "cityName", Game.ctx.session.mapconfig.CityName), enacted: false, law);
	}

	public void TryRevokeTempLaws()
	{
		for (int num = _pdata.laws.Count() - 1; num > 0; num--)
		{
			PoliticsSettings.Law law = Settings.FindLawDef(_pdata.laws[num]);
			if (law.expireNextElectionYear)
			{
				RevokeLaw(law);
			}
		}
	}

	public int GetNumPoliticiansControlled()
	{
		int num = 0;
		foreach (Ward ward in GetWards())
		{
			if (GetPoliticianData(ward.currentPolitician).relToHumanInLastElection == PoliticalRelationshipType.Sponsored)
			{
				num++;
			}
		}
		return num;
	}

	private void InitializeConsoleEntries()
	{
		Game.ctx.console.Add(this, new DebugConsoleEntry("politics", "add-votes", CheatAddVotes));
		Game.ctx.console.Add(this, new DebugConsoleEntry("politics", "enact-law", CheatEnactLaw));
		Game.ctx.console.Add(this, new DebugConsoleEntry("politics", "revoke-law", CheatRevokeLaw));
		Game.ctx.console.Add(this, new DebugConsoleEntry("politics", "add-influence", CheatAddInfluence));
	}

	private void ReleaseConsoleEntries()
	{
		Game.ctx.console.Remove(this);
	}

	private string CheatAddVotes(string[] args)
	{
		if (args.Length != 5)
		{
			return DebugConsoleEntry.InvalidParam("Invalid parameters", args, 4, "<precinct-id> <entity-id> <votes-delta>");
		}
		if (!short.TryParse(args[2], out var result))
		{
			return DebugConsoleEntry.InvalidParam("Invalid precinct id", args, 4, "<precinct-id> <entity-id> <votes-delta>");
		}
		if (!ulong.TryParse(args[3], out var result2))
		{
			return DebugConsoleEntry.InvalidParam("Invalid candidate id", args, 4, "<precinct-id> <entity-id> <votes-delta>");
		}
		if (!int.TryParse(args[4], out var result3))
		{
			return DebugConsoleEntry.InvalidParam("Invalid candidate id", args, 4, "<precinct-id> <entity-id> <votes-delta>");
		}
		EntityID entityID = EntityID.FromID(result2).IncrementVersion();
		Game.ctx.simman.politics.GetWardForID(new PrecinctID(result)).currElection.UpdateVotesByValue(entityID, result3);
		return $"Added {result3} to {entityID.FindEntity().data.person.FullName}'s campaign";
	}

	private string CheatEnactLaw(string[] args)
	{
		if (args.Length != 3)
		{
			return DebugConsoleEntry.InvalidParam("Invalid parameters", args, 2, "<law-id>");
		}
		PoliticsSettings.Law law = Game.serv.globals.settings.politics.FindLawDef(new Label(args[2]));
		if (law == null)
		{
			return DebugConsoleEntry.InvalidParam("Invalid law id", args, 2, "<law-id>");
		}
		EnactLaw(law);
		return "Enacted law " + Loc.Get(law.locname) + ".";
	}

	private string CheatRevokeLaw(string[] args)
	{
		if (args.Length != 3)
		{
			return DebugConsoleEntry.InvalidParam("Invalid parameters", args, 2, "<law-id>");
		}
		PoliticsSettings.Law law = Game.serv.globals.settings.politics.FindLawDef(new Label(args[2]));
		if (law == null)
		{
			return DebugConsoleEntry.InvalidParam("Invalid law id", args, 2, "<law-id>");
		}
		RevokeLaw(law);
		return "Revoked law " + Loc.Get(law.locname) + ".";
	}

	private string CheatAddInfluence(string[] args)
	{
		if (args.Length != 3)
		{
			return DebugConsoleEntry.InvalidParam("Invalid parameters", args, 2, "<influence-amt>");
		}
		if (!int.TryParse(args[2], out var result))
		{
			return DebugConsoleEntry.InvalidParam("Invalid influence delta", args, 4, "<precinct-id> <entity-id> <votes-delta>");
		}
		DoChangeInfluence(PlayerID.HumanPlayer, result, EntityID.INVALID);
		return $"Gave {result} influence to player";
	}

	public Label GeneratePoliticalArchetypeFor(EntityID politician)
	{
		TagList traitIds = politician.FindEntity().data.person.traitIds;
		List<Label> list = new List<Label>();
		List<float> list2 = new List<float>();
		foreach (PoliticsSettings.PoliticalArchetype archetypeDef in Settings.archetypeDefs)
		{
			int num = 0;
			foreach (Label favoredTrait in archetypeDef.favoredTraits)
			{
				if (traitIds.Contains(favoredTrait))
				{
					num++;
				}
			}
			if (num >= 1)
			{
				list.Add(archetypeDef.id);
				list2.Add(num);
			}
		}
		if (list.Count > 0)
		{
			return _pdata.rng.PickElement(list, list2);
		}
		return NO_ARCHETYPE;
	}

	public void PostReportedTickers()
	{
		foreach (TickerData item in tickersToReport)
		{
			Game.ctx.hud.tickers.AddTicker(item);
		}
	}

	public void ReportTicker(TickerData data)
	{
		if (!tickersToReport.Select((TickerData x) => x.message).Contains(data.message))
		{
			tickersToReport.Add(data);
		}
	}

	public void Save(Serializer s, ConcurrentSaveTable results)
	{
		results.Set("data", s.Serialize(_pdata));
	}

	public IEnumerator Load(Hashtable data)
	{
		SaveLoadUtils.DeserializeSingleKey(data, "data", delegate(PoliticsManagerPersistedData result)
		{
			_pdata = result;
		});
		yield break;
	}
}
