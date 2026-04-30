using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Services;
using Game.Session.Board;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Player.AI;
using Game.Session.Player.Commands;
using Game.Session.Sim;
using Game.Session.Sim.Modules;
using Game.UI;
using SomaSim.Util;

namespace Game.Session.Player;

public sealed class PlayerScheme : PlayerSubmanager, ITurnHandlingPlayerSubmanager
{
	private PlayerSchemeData _sdata;

	public Dictionary<Label, List<EntityID>> heistablesByScheme = new Dictionary<Label, List<EntityID>>();

	public const int MAX_DISTANCE_BANKS = 20;

	public static readonly Label CANADA_DISTRICT = new Label("canada");

	public static readonly Label CANADA_DISTRICT_TWO = new Label("canada-two");

	public PlayerSchemeData Data => _sdata;

	public SchemeSettings Settings => Game.serv.globals.settings.schemes;

	public override void OnPostInitialize()
	{
		base.OnPostInitialize();
		Game.ctx.events.AddListener(SessionEventType.OnAfterAIInitNewGame, OnNewGame);
		Game.ctx.events.AddListener(SessionEventType.OnGameBecomeInteractive, InitializeHeistables);
	}

	public override void OnPreRelease()
	{
		Game.ctx.events.RemoveListener(SessionEventType.OnAfterAIInitNewGame, OnNewGame);
		Game.ctx.events.RemoveListener(SessionEventType.OnGameBecomeInteractive, InitializeHeistables);
		base.OnPreRelease();
	}

	public override void OnPostSetDataSource(bool loaded)
	{
		_sdata = _data.scheme ?? _sdata;
	}

	private void OnNewGame(SessionEvent sev)
	{
	}

	public void OnGlobalTurnSetAdvanced()
	{
	}

	public void OnPlayerTurnStarted()
	{
		if (!_player.IsHuman)
		{
			return;
		}
		foreach (SchemeData ongoingScheme in Data.ongoingSchemes)
		{
			Entity entity = ongoingScheme.crewAssigned.FindEntity();
			bool flag = !_player.crew.IsOnBoard(entity);
			ongoingScheme.crewAssigned.FindEntity().components.agent.ConsumeAllPoints();
			if (ongoingScheme.DecisionPopThisTurn())
			{
				OnDecisionPop(ongoingScheme);
				if (!Game.serv.ui.ContainsPopup<SchemePopup>())
				{
					Game.serv.ui.AddPopup(new SchemePopup(ongoingScheme));
				}
			}
			if (!_player.commands.PeepHasTask(ongoingScheme.crewAssigned.id) && !ongoingScheme.IsWaitingForDecision() && ongoingScheme.epilogueText == null && !flag && ongoingScheme.crewAssigned.FindEntity().data.person.IsAlive)
			{
				ChapterData currentChapterForCrew = GetCurrentChapterForCrew(entity);
				ChapterDef chapterDef = Settings.FindChapterById(currentChapterForCrew.chapterID);
				EntityID entityID = GetRandomTargetForScheme(ongoingScheme.schemeID, ongoingScheme.crewAssigned);
				if (ongoingScheme.overallTarget.IsValid)
				{
					entityID = ongoingScheme.overallTarget;
				}
				currentChapterForCrew.scriptTarget = entityID;
				ScriptDispatcher.RunScript(chapterDef.script, _pid, ongoingScheme.crewAssigned.FindEntity(), new Deictics
				{
					targetBuilding = entityID,
					number = 1
				});
			}
			if (!ongoingScheme.DecisionPopThisTurn() && ongoingScheme.IsWaitingForDecision())
			{
				Game.ctx.hud.tickers.AddSchemeTicker(Loc.Get("ui.tickers.scheme-decision", "name", entity.data.person.FullName), entity.Id);
			}
		}
		List<Label> list = new List<Label>();
		foreach (SchemeCooldown cooldown in _sdata.cooldowns)
		{
			if (Game.ctx.clock.Now >= cooldown.becomeAvailableTime)
			{
				list.Add(cooldown.schemeId);
			}
		}
		foreach (Label item in list)
		{
			RemoveSchemeFromCooldown(item);
		}
		foreach (SchemeDef allSchemeDef in Settings.GetAllSchemeDefs())
		{
			MaybeChangeSchemeAvailability(allSchemeDef);
		}
	}

	public void OnPlayerTurnEnded()
	{
	}

	public PlayerTurnStatus GetPlayerTurnStatus()
	{
		return PlayerTurnStatus.TurnFinished;
	}

	public List<SchemeData> GetAllSchemesUnsafe()
	{
		return _sdata.ongoingSchemes;
	}

	public void StartSchemeForCrew(Label schemeID, Entity crew)
	{
		SchemeData schemeData = new SchemeData(Settings.FindSchemeById(schemeID), crew.Id);
		AutomationSequence autoOrNull = _player.automation.GetAutoOrNull(crew.components.agent.FindCrewAssignment());
		if (autoOrNull != null)
		{
			Game.ctx.sfx.PlayAutomationChange(start: false);
			_player.automation.ToggleExecution(autoOrNull.id, activate: false);
		}
		Data.ongoingSchemes.Add(schemeData);
		StartCurrentChapter(schemeID, forceSucceed: true);
		Game.serv.ui.AddPopup(new SchemePopup(schemeData));
		Game.ctx.events.EnqueueOnce(SessionEventType.SchemeUpdated);
	}

	public void EndSchemeForCrew(Entity crew)
	{
		SchemeData schemeForCrew = GetSchemeForCrew(crew.Id);
		Data.ongoingSchemes.Remove(schemeForCrew);
		Data.schemeHistory.Add(schemeForCrew.schemeID);
		PutSchemeOnCooldown(schemeForCrew.schemeID);
		AddSchemeToHistory(schemeForCrew.schemeID);
		Game.ctx.events.EnqueueOnce(SessionEventType.SchemeUpdated);
	}

	public void EndScheme(SchemeData schemeData)
	{
		Data.ongoingSchemes.Remove(schemeData);
		Data.schemeHistory.Add(schemeData.schemeID);
		PutSchemeOnCooldown(schemeData.schemeID);
		AddSchemeToHistory(schemeData.schemeID);
		Game.ctx.events.EnqueueOnce(SessionEventType.SchemeUpdated);
	}

	public void SetSchemeEpilogue(Entity crew, string locepilogue)
	{
		SchemeData schemeForCrew = GetSchemeForCrew(crew.Id);
		schemeForCrew.currState = SchemeData.SchemeState.Epilogue;
		schemeForCrew.epilogueText = Loc.Get(locepilogue, "name", crew.data.person.FullName);
	}

	public bool IsSchemeOnCooldownById(Label id)
	{
		return _sdata.cooldowns.FindIndex((SchemeCooldown x) => x.schemeId == id) >= 0;
	}

	public void PutSchemeOnCooldown(Label id)
	{
		SchemeDef schemeDef = Settings.FindSchemeById(id);
		SchemeCooldown item = new SchemeCooldown
		{
			schemeId = id,
			becomeAvailableTime = Game.ctx.clock.Now.IncrementDays(schemeDef.cooldownDays)
		};
		_sdata.cooldowns.Add(item);
	}

	public void AddSchemeToHistory(Label id)
	{
		_sdata.schemeHistory.Add(id);
	}

	public void AddChapterToOverallHistory(Label id, bool success, SimTime decTime)
	{
		_sdata.chapterHistory.Add(new HistoryItem(id, null, success, decTime));
	}

	public void AddChapterToSchemeHistory(SchemeData scheme, Label id, string chosenDec, bool success, SimTime decTime)
	{
		scheme.schemeChapterHistory.Add(new HistoryItem(id, chosenDec, success, decTime));
	}

	public void RemoveSchemeFromCooldown(Label id)
	{
		int num = _sdata.cooldowns.FindIndex((SchemeCooldown x) => x.schemeId == id);
		if (num != -1)
		{
			SchemeCooldown item = _sdata.cooldowns[num];
			_sdata.cooldowns.Remove(item);
		}
	}

	public bool IsInScheme(EntityID crew)
	{
		return GetSchemeForCrew(crew) != null;
	}

	public bool IsInScheme(Entity crew)
	{
		return IsInScheme(crew?.Id ?? EntityID.INVALID);
	}

	public SchemeData GetSchemeForCrew(Entity crew)
	{
		return GetSchemeForCrew(crew.Id);
	}

	public SchemeData GetSchemeForCrew(EntityID crew)
	{
		foreach (SchemeData ongoingScheme in Data.ongoingSchemes)
		{
			if (ongoingScheme.crewAssigned == crew)
			{
				return ongoingScheme;
			}
		}
		return null;
	}

	public SchemeData GetSchemeForBuilding(Entity building)
	{
		return GetSchemeForBuilding(building.Id);
	}

	public SchemeData GetSchemeForBuilding(EntityID building)
	{
		foreach (SchemeData ongoingScheme in Data.ongoingSchemes)
		{
			if (ongoingScheme.overallTarget == building)
			{
				return ongoingScheme;
			}
		}
		return null;
	}

	public SchemeData GetOngoingSchemeForID(Label id)
	{
		foreach (SchemeData ongoingScheme in Data.ongoingSchemes)
		{
			if (ongoingScheme.schemeID == id)
			{
				return ongoingScheme;
			}
		}
		return null;
	}

	public int GetNumOngoingSchemes()
	{
		return _sdata.ongoingSchemes.Count();
	}

	public int GetNumUniqueFinishedSchemes()
	{
		return _sdata.schemeHistory.Distinct().Count();
	}

	public ChapterData GetCurrentChapterForCrew(Entity crew)
	{
		return GetSchemeForCrew(crew.Id)?.currentChapter;
	}

	public void ProcessDecision(Label schemeID, Decision choice)
	{
		SchemeData ongoingSchemeForId = Data.GetOngoingSchemeForId(schemeID);
		Settings.FindSchemeById(schemeID);
		CrewAssignment crew = ongoingSchemeForId.crewAssigned.FindEntity().components.agent.FindCrewAssignment();
		BuildingAndBusinessData bbdata = (ongoingSchemeForId.overallTarget.IsValid ? BuildingUtil.FindDataForBuilding(ongoingSchemeForId.overallTarget) : default(BuildingAndBusinessData));
		VisitState visitState = new VisitState(crew, bbdata, Game.ctx.clock.Now, _pid);
		visitState.npc = _player.crew.GetCrewForPlayerPeep().GetPeep();
		if (CanPayDecisionCost(choice))
		{
			DoPayDecisionCost(choice);
			EndCurrentChapter(schemeID, choice, ongoingSchemeForId.currentChapter.succeeded);
			if (choice.grants != null)
			{
				choice.grants.ApplyAll(new GrantContext(visitState));
			}
			if (choice.nextState.IsSet)
			{
				ongoingSchemeForId.currentChapter = new ChapterData(Settings.FindChapterById(choice.nextState), ongoingSchemeForId.crewAssigned, schemeID);
				StartCurrentChapter(schemeID);
			}
			else
			{
				SetSchemeEpilogue(ongoingSchemeForId.crewAssigned.FindEntity(), choice.locepilogue);
			}
		}
	}

	public void StartCurrentChapter(Label schemeID, bool forceSucceed = false)
	{
		SchemeData ongoingSchemeForId = Data.GetOngoingSchemeForId(schemeID);
		ChapterDef chapterDef = Settings.FindChapterById(ongoingSchemeForId.currentChapter.chapterID);
		Entity entity = ongoingSchemeForId.crewAssigned.FindEntity();
		ModQuery query = new ModQuery(_pid, ongoingSchemeForId.crewAssigned, entity.components.agent.GetNode().id);
		if (forceSucceed)
		{
			ongoingSchemeForId.currentChapter.succeeded = true;
		}
		else
		{
			ongoingSchemeForId.currentChapter.succeeded = entity.data.ident.rng.CheckProbability(chapterDef.successChance.Evaluate(query));
		}
		if (ongoingSchemeForId.currentChapter.scriptTarget.IsValid)
		{
			ScriptDispatcher.RunScript(chapterDef.script, _pid, ongoingSchemeForId.crewAssigned.FindEntity(), new Deictics
			{
				targetBuilding = ongoingSchemeForId.currentChapter.scriptTarget,
				number = 1
			});
		}
		else
		{
			ScriptDispatcher.RunScript(chapterDef.script, _pid, ongoingSchemeForId.crewAssigned.FindEntity());
		}
	}

	public void EndCurrentChapter(Label schemeID, Decision decision = null, bool succeeded = false)
	{
		SchemeData ongoingSchemeForId = Data.GetOngoingSchemeForId(schemeID);
		ChapterDef chapterDef = Settings.FindChapterById(ongoingSchemeForId.currentChapter.chapterID);
		Game.ctx.events.EnqueueOnce(SessionEventType.SchemeUpdated);
		AddChapterToOverallHistory(chapterDef.id, succeeded, ongoingSchemeForId?.currentChapter.finishTime ?? SimTime.MIN_DATE);
		AddChapterToSchemeHistory(ongoingSchemeForId, chapterDef.id, decision?.locoption, succeeded, ongoingSchemeForId?.currentChapter.finishTime ?? SimTime.MIN_DATE);
	}

	public void OnDecisionPop(SchemeData scheme)
	{
		if (scheme.currentChapter.GetChapterDef().selectTarget)
		{
			scheme.SetTarget(GetRandomTargetForScheme(scheme.schemeID, scheme.crewAssigned));
		}
		ChapterDef chapterDef = Settings.FindChapterById(scheme.currentChapter.chapterID);
		_player.commands.FlushQueue(scheme.crewAssigned.id, cancelActive: true);
		EntityID entityID = (scheme.overallTarget.IsValid ? scheme.overallTarget : scheme.currentChapter.scriptTarget);
		if (entityID.IsValid)
		{
			NodeID nodeID = entityID.FindEntity().components.board.GetNodeID();
			ScriptDispatcher.RunScript(chapterDef.recoveryScript, _pid, scheme.crewAssigned.FindEntity(), new Deictics
			{
				targetBuilding = entityID,
				targetNode = nodeID,
				number = 1
			});
		}
		else
		{
			ScriptDispatcher.RunScript(chapterDef.recoveryScript, _pid, scheme.crewAssigned.FindEntity());
		}
		Game.ctx.events.EnqueueOnce(SessionEventType.SchemeUpdated);
	}

	public bool CanPaySchemeStartupCost(SchemeDef scheme)
	{
		if (scheme.startup.cost == null)
		{
			return true;
		}
		return CanPayGeneral(scheme.startup.cost);
	}

	public bool CanPayDecisionCost(Decision decision)
	{
		if (decision.cost == null)
		{
			return true;
		}
		return CanPayGeneral(decision.cost);
	}

	public bool CanPayGeneral(List<ResOrCash> costs)
	{
		InventoryModule inventory = ModulesUtil.GetInventory(_player.territory.Safehouse);
		foreach (ResOrCash cost in costs)
		{
			if (cost.IsCash && !_player.finances.CanChangeMoney(_player.territory.Safehouse.FindEntity(), new Price(cost.money.cash)))
			{
				return false;
			}
			if (cost.IsResource && !inventory.data.WillBeNonNegative(cost.raq.id, -cost.raq.qty))
			{
				return false;
			}
		}
		return true;
	}

	public void DoPaySchemeStartupCost(SchemeDef scheme)
	{
		if (scheme.startup.cost != null)
		{
			DoPayGeneral(scheme.startup.cost);
		}
	}

	public void DoPayDecisionCost(Decision decision)
	{
		if (decision.cost != null)
		{
			DoPayGeneral(decision.cost);
		}
	}

	public void DoPayGeneral(List<ResOrCash> costs)
	{
		InventoryModule inventory = ModulesUtil.GetInventory(_player.territory.Safehouse);
		foreach (ResOrCash cost in costs)
		{
			if (cost.IsCash)
			{
				_player.finances.DoChangeMoney(_player.territory.Safehouse.FindEntity(), new Price(cost.money.cash), MoneyReason.Scheme);
			}
			if (cost.IsResource)
			{
				inventory.data.Increment(cost.raq.id, -cost.raq.qty);
			}
		}
	}

	public bool HasCompletedScheme(Label schemeID)
	{
		return Data.schemeHistory.Contains(schemeID);
	}

	public bool HasNotCompletedScheme(Label schemeID)
	{
		return !Data.schemeHistory.Contains(schemeID);
	}

	public bool HasCompletedChapter(Label chapterID)
	{
		return Data.chapterHistory.Select((HistoryItem x) => x.chapterId).Contains(chapterID);
	}

	public bool HasNotCompletedChapter(Label chapterID)
	{
		return !HasCompletedChapter(chapterID);
	}

	public IEnumerable<HistoryItem> GetCompletedChapterDatasById(Label chapterID)
	{
		return Data.chapterHistory.Where((HistoryItem x) => x.chapterId == chapterID);
	}

	public void MaybeChangeSchemeAvailability(SchemeDef scheme)
	{
		Label id = scheme.startup.visreqs.WhereTypeIs<CheckCrewConvoRole>().First().id;
		CrewAssignment oneCrewForRole = _player.crew.GetOneCrewForRole(id);
		if (!oneCrewForRole.peepId.IsNotValid)
		{
			VisitState visit = new VisitState(_player.crew.GetCrewForPlayerPeep(), oneCrewForRole.GetPeep(), Game.ctx.clock.Now, _player.PID);
			if (scheme.startup.visreqs.AllPass(visit))
			{
				AddToAvailableSchemes(scheme, id);
			}
			else
			{
				RemoveFromAvailableSchemes(scheme);
			}
		}
	}

	public void AddToAvailableSchemes(SchemeDef scheme, Label roleId)
	{
		if (!_sdata.schemesAvailable.Contains(scheme.id))
		{
			RoleDef roleById = Game.serv.globals.settings.people.social.crew.roleSettings.GetRoleById(roleId);
			_sdata.schemesAvailable.Add(scheme.id);
			Game.ctx.hud.tickers.AddTextTicker(TickerIcon.SCHEME_AVAILABLE, TickerTitle.SCHEME_UPDATE, Loc.Get("ui.tickers.scheme-available", "title", Loc.Get(scheme.display.loctitle), "role", Loc.Get(roleById.loctitle)));
		}
	}

	public void RemoveFromAvailableSchemes(SchemeDef scheme)
	{
		if (_sdata.schemesAvailable.Contains(scheme.id))
		{
			_sdata.schemesAvailable.Remove(scheme.id);
		}
	}

	public void InitializeHeistables(SessionEvent _)
	{
		foreach (KeyValuePair<Label, List<Label>> heistableTemplate in GetHeistableTemplates())
		{
			List<EntityID> list = (heistablesByScheme[heistableTemplate.Key] = new List<EntityID>());
			List<EntityID> list3 = list;
			foreach (Label item in heistableTemplate.Value)
			{
				list3.AddRange((from x in Game.ctx.entityman.GetCachedEntitiesByTemplateUnsafe(item)
					select BuildingUtil.FindBuildingForBiz(x).Id).ToList());
			}
		}
	}

	public Dictionary<Label, List<Label>> GetHeistableTemplates()
	{
		Dictionary<Label, List<Label>> dictionary = new Dictionary<Label, List<Label>>();
		foreach (SchemeDef schemeDef in Settings.schemeDefs)
		{
			List<Label> list = new List<Label>();
			foreach (Label targetIdPrefix in schemeDef.startup.targetIdPrefixes)
			{
				list.AddRange((from x in Game.ctx.entityman.FindCachedTemplatesByPrefix(new Label(targetIdPrefix.String + "*"))
					select x.Template).ToList());
			}
			dictionary[schemeDef.id] = list;
		}
		return dictionary;
	}

	public EntityID GetRandomTargetForScheme(Label schemeId, EntityID crew)
	{
		return _sdata.rng.PickElement(GetAllTargetsForScheme(schemeId, crew));
	}

	public bool HasTargetsForScheme(Label schemeId, EntityID crew)
	{
		return GetAllTargetsForScheme(schemeId, crew).Count > 0;
	}

	public List<EntityID> GetAllTargetsForScheme(Label schemeId, EntityID crew)
	{
		SchemeDef schemeDef = Settings.FindSchemeById(schemeId);
		switch (schemeDef.startup.alternateTargeting)
		{
		case SchemeDef.TargetingStyle.FromPrefixes:
			return (from x in GetRevealedHeistablesForScheme(schemeId)
				where NotInCanada(x) && HasPathWithinRange(20, x, crew)
				select x).ToList();
		case SchemeDef.TargetingStyle.FindPrecinctsForBlackmail:
			return (from x in GetPrecincts()
				where NotInCanada(x) && HasNotBeenBribedBefore(x.FindEntity().data.police.officers[0])
				select x).ToList();
		case SchemeDef.TargetingStyle.FindPrecincts:
			return GetPrecincts().ToList();
		case SchemeDef.TargetingStyle.FindDistantFronts:
		{
			List<OutpostEntry> outpostEntriesUnsafe2 = _player.outposts.GetOutpostEntriesUnsafe();
			Node safehouseNode = _player.territory.Safehouse.FindEntity().components.board.GetNode();
			HashSet<Node> hashSet = new HashSet<Node>();
			foreach (OutpostEntry front in outpostEntriesUnsafe2)
			{
				hashSet.UnionWith(from node in Game.ctx.board.nodes.FindAndSortNodesInRadius(front.OutpostNode.nodeId.FindNode().pos, 150f, sort: false)
					where (front.OutpostNode.nodeId.FindNode().pos - node.pos).Magnitude > 50f
					select node);
			}
			return (from x in hashSet
				where NotInCanada(x) && IsGoodOutpostCandidate(x)
				orderby (x.pos - safehouseNode.pos).Magnitude descending
				select x.interesting[0]).ToList();
		}
		case SchemeDef.TargetingStyle.FindBuildingsToTakeOver:
			return (from x in _player.territory.GetAllOwnedNodesUnsafe()
				select x.FindNode()?.potential ?? EntityID.INVALID into x
				where NotInCanada(x) && (x.FindEntity()?.data.building.controlled.IsNotSet ?? false)
				select x).ToList();
		case SchemeDef.TargetingStyle.FindNearbyIndependentPoliticians:
		{
			PoliticsManager politicsManager = Game.ctx.simman.politics;
			IEnumerable<Ward> source = from x in politicsManager.GetWards()
				where politicsManager.GetPoliticianData(x.currentPolitician).relToHumanInLastElection != PoliticalRelationshipType.Sponsored
				select x;
			List<OutpostEntry> outpostEntriesUnsafe = _player.outposts.GetOutpostEntriesUnsafe();
			_player.territory.Safehouse.FindEntity().components.board.GetNode();
			HashSet<Node> nodesCloseEnough = new HashSet<Node>();
			foreach (OutpostEntry item in outpostEntriesUnsafe)
			{
				nodesCloseEnough.UnionWith(Game.ctx.board.nodes.FindAndSortNodesInRadius(item.OutpostNode.nodeId.FindNode().pos, 200f, sort: false));
			}
			source.Where((Ward x) => nodesCloseEnough.Contains(x.wardOffice.FindEntity().components.board.GetNode()));
			return source.Select((Ward x) => x.wardOffice).ToList();
		}
		default:
			return GetRevealedHeistablesForScheme(schemeId);
		}
	}

	public IEnumerable<EntityID> GetPrecincts()
	{
		return from x in _player.territory.GetAllOwnedNodesUnsafe()
			group x by x.FindNode().precinctId into x
			select x.Key into x
			select Game.ctx.simman.cops.StationForPrecinct(x);
	}

	public bool HasNotBeenBribedBefore(EntityID cop)
	{
		return !_player.social.FindOrMakeRelationshipsWith(cop).from.HasBuff(BuffConstants.RELBUFF_COP_BLACKMAIL);
	}

	public bool IsGoodOutpostCandidate(Node node)
	{
		List<Node> list = node.FindAllNeighbors();
		int count = list.Count;
		int num = node.interesting.Count;
		bool isNotSet = node.owner.IsNotSet;
		foreach (Node item in list)
		{
			num += item.interesting.Count;
		}
		return node.interesting.Count >= 1 && count >= 3 && num >= 4 && isNotSet;
	}

	public List<EntityID> GetRevealedHeistablesForScheme(Label schemeId)
	{
		List<EntityID> list = new List<EntityID>();
		foreach (EntityID item in heistablesByScheme[schemeId])
		{
			if (item.FindEntity().components.board.GetNode().known.Get(_pid))
			{
				list.Add(item);
			}
		}
		return list;
	}

	public bool HasPathWithinRange(int maxMoves, EntityID target, EntityID crew)
	{
		Node node = target.FindEntity().components.board.GetNode();
		PathData pathData = CommandGoto.MakePath(_player.PID, crew.FindEntity(), node, new Fixnum(maxMoves));
		if (pathData.nodes.Count != 0)
		{
			return node.id == pathData.nodes.LastOrDefault().node.id;
		}
		return true;
	}

	public bool NotInCanada(EntityID target)
	{
		return NotInCanada(target.FindEntity()?.components.board?.GetNode().districts);
	}

	public bool NotInCanada(Node node)
	{
		return NotInCanada(node?.districts);
	}

	public bool NotInCanada(List<NodeDistrictData> district)
	{
		if (district == null)
		{
			return true;
		}
		IEnumerable<Label> source = district.SelectMany((NodeDistrictData x) => x.GetDistrictTags());
		if (source.Contains(CANADA_DISTRICT) || source.Contains(CANADA_DISTRICT_TWO))
		{
			return false;
		}
		return true;
	}

	public bool HasRevealedHeistableForScheme(Label schemeId)
	{
		return GetRevealedHeistablesForScheme(schemeId).Count > 0;
	}

	public void CrewInteract(Entity crewPeep)
	{
		SchemeData schemeForCrew = Game.ctx.players.Human.schemes.GetSchemeForCrew(crewPeep);
		if (schemeForCrew != null)
		{
			Game.serv.ui.AddPopup(new SchemePopup(schemeForCrew));
		}
		else
		{
			Game.ctx.hud.convoDialog.Controller.StartCrewVisit(crewPeep, _player.crew.GetCrewForPlayerPeep());
		}
	}

	public List<Decision> GenerateExtraSelections(ChapterDef def, Label schemeId, bool success)
	{
		switch (def.optionGeneration)
		{
		case ChapterDef.GenerateOptions.None:
			return new List<Decision>();
		case ChapterDef.GenerateOptions.ForTakeover:
			if (success)
			{
				return GenerateTakeoverSelections(def, schemeId);
			}
			return new List<Decision>();
		case ChapterDef.GenerateOptions.ForAggroRivals:
			if (success)
			{
				return GenerateAggroRivalSelections(def, schemeId);
			}
			return new List<Decision>();
		case ChapterDef.GenerateOptions.ForStalledProductions:
			if (success)
			{
				return GenerateStalledProductionSelections(def, schemeId);
			}
			return new List<Decision>();
		default:
			return new List<Decision>();
		}
	}

	public List<Decision> GenerateTakeoverSelections(ChapterDef def, Label schemeId)
	{
		Decision optionTemplate = def.optionTemplate;
		List<EntityID> allTargetsForScheme = GetAllTargetsForScheme(schemeId, EntityID.INVALID);
		List<EntityID> list = new List<EntityID>();
		List<EntityID> list2 = new List<EntityID>();
		List<Recipe> list3 = new List<Recipe>();
		List<Decision> list4 = new List<Decision>();
		foreach (EntityID item2 in allTargetsForScheme)
		{
			if (item2.FindEntity().components.modules.FindFrontroomModule() is ManufactureModule manufactureModule)
			{
				bool isProducer = manufactureModule.CurrentRecipe.IsProducer;
				bool flag = !list3.Contains(manufactureModule.CurrentRecipe);
				if (isProducer && flag && list2.Count < 6)
				{
					list2.Add(item2);
					list3.Add(manufactureModule.CurrentRecipe);
					list.Add(item2);
				}
			}
		}
		foreach (EntityID item3 in list)
		{
			allTargetsForScheme.Remove(item3);
		}
		if (list2.Count < 6 && allTargetsForScheme.Count > 0)
		{
			EntityID item = _sdata.rng.PickElement(allTargetsForScheme);
			list2.Add(item);
		}
		foreach (EntityID item4 in list2)
		{
			string text = ModulesUtil.DescribeModuleShort(item4.FindEntity().components.modules.FindFrontroomModule().ModuleData.Id);
			string text2 = ModulesUtil.DescribeInventoryModuleShort(item4.FindEntity());
			string overrideText = Loc.Get("scheme.takeover-option.generic", "specifications", text + "\n" + text2);
			Decision decision = Game.serv.serializer.instance.Clone(optionTemplate);
			decision.overrideText = overrideText;
			decision.grants = new VisitGrantList
			{
				new SetSchemeTarget
				{
					target = item4
				}
			};
			list4.Add(decision);
		}
		return list4;
	}

	public List<Decision> GenerateStalledProductionSelections(ChapterDef def, Label schemeId)
	{
		Decision optionTemplate = def.optionTemplate;
		List<EntityID> list = (from x in _player.territory.GetAllControlledBuildingsUnsafe()
			where x.FindEntity().components.modules.FindBackroomModule() is ManufactureModule manufactureModule && manufactureModule.data.lastStall >= Game.ctx.clock.Now.IncrementDays(-365)
			select x).ToList();
		List<Decision> list2 = new List<Decision>();
		foreach (EntityID item in list)
		{
			string text = ModulesUtil.DescribeModuleShort(item.FindEntity().components.modules.FindBackroomModule().ModuleData.Id);
			string overrideText = Loc.Get("scheme.stalled-production.generic", "module", text);
			Decision decision = Game.serv.serializer.instance.Clone(optionTemplate);
			decision.overrideText = overrideText;
			decision.grants = new VisitGrantList
			{
				new SetSchemeTarget
				{
					target = item
				}
			};
			list2.Add(decision);
		}
		return list2;
	}

	public List<Decision> GenerateAggroRivalSelections(ChapterDef def, Label schemeId)
	{
		Decision optionTemplate = def.optionTemplate;
		IEnumerable<PlayerInfo> enumerable = from x in _player.social.GetAllGangsAggroOnMe()
			where !x.crew.IsCrewDefeated
			select x;
		List<Decision> list = new List<Decision>();
		foreach (PlayerInfo item in enumerable)
		{
			string overrideText = Loc.Get("scheme.rival-tip-off.generic", "rival", item.social.FindPlayerGroupNameColorized());
			Decision decision = Game.serv.serializer.instance.Clone(optionTemplate);
			decision.overrideText = overrideText;
			decision.grants = new VisitGrantList
			{
				new GrantFedTipOffOnRival
				{
					target = item.PID
				}
			};
			list.Add(decision);
		}
		return list;
	}

	protected override void InitializeConsoleEntries()
	{
		base.InitializeConsoleEntries();
		foreach (SchemeDef allSchemeDef in Game.serv.globals.settings.schemes.GetAllSchemeDefs())
		{
			Game.ctx.console.Add(this, new DebugConsoleEntry("scheme", "start", allSchemeDef.id.String, CheatStartScheme));
		}
	}

	protected override void ReleaseConsoleEntries()
	{
		Game.ctx.console.Remove(this);
		base.ReleaseConsoleEntries();
	}

	private string CheatStartScheme(string[] args)
	{
		if (args.Length != 4)
		{
			return DebugConsoleEntry.InvalidParam("Invalid parameters", args, 1, "<scheme-id> <person-id>");
		}
		if (!int.TryParse(args[3], out var result))
		{
			return DebugConsoleEntry.InvalidParam("Invalid person id, number expected", args, 1, "<scheme-id> <person-id>");
		}
		Entity entity = _player.crew.GetCrewForIndex(result).peepId.FindEntity();
		if (entity?.components.person == null)
		{
			return "person id does not correspond to a person";
		}
		SchemeDef schemeDef = Settings.FindSchemeById(new Label(args[2]));
		if (schemeDef == null)
		{
			return "schemeID " + args[2];
		}
		StartSchemeForCrew(schemeDef.id, entity);
		return $"Started scheme {Loc.Get(schemeDef.display.loctitle)} for crew {entity}";
	}
}
