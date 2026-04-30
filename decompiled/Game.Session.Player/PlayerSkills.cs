using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Services;
using Game.Session.Board;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Quests;
using Game.Session.Sim;
using Game.Session.Sim.Modules;
using SomaSim.Util;

namespace Game.Session.Player;

public sealed class PlayerSkills : PlayerSubmanager, ITurnHandlingPlayerSubmanager
{
	public sealed class Unlocked
	{
		public Resource res;

		public List<EntityResDir> buildings = new List<EntityResDir>();

		public void Reset()
		{
			res = null;
			buildings.Clear();
		}

		public int IndexOf(EntityID eid)
		{
			int i = 0;
			for (int count = buildings.Count; i < count; i++)
			{
				if (buildings[i].eid == eid)
				{
					return i;
				}
			}
			return -1;
		}

		public bool Contains(EntityID eid)
		{
			return IndexOf(eid) >= 0;
		}
	}

	private PlayerSkillsData _skilldata;

	private SkillSettings _settings;

	private Unlocked _unlocked = new Unlocked();

	private static readonly SimTimeSpan RECENT_TRANSACTION_AGE = new SimTimeSpan(180);

	public static SkillModuleUnlocksCache ModuleUnlocksCache { get; private set; }

	public Unlocked UnlockedThisTurn => _unlocked;

	public PlayerSkillsData Data => _skilldata;

	public int CurrentSkillCount => _skilldata.currentSkills?.Count ?? 0;

	public override void OnPostInitialize()
	{
		base.OnPostInitialize();
		_settings = Game.serv.globals.settings.skills;
		if (_pid.IsHumanPlayer)
		{
			ModuleUnlocksCache = new SkillModuleUnlocksCache(_pid);
		}
	}

	public override void OnPostSetDataSource(bool loaded)
	{
		_skilldata = _data.skills;
		if (Game.ctx.IsSessionFromNewGame)
		{
			UnlockDefaultResources();
		}
	}

	public override void OnPreRelease()
	{
		if (_pid.IsHumanPlayer)
		{
			ModuleUnlocksCache = null;
		}
		_settings = null;
		base.OnPreRelease();
	}

	public void OnGlobalTurnSetAdvanced()
	{
	}

	public void OnPlayerTurnStarted()
	{
		if (_pid.IsHumanPlayer)
		{
			UnlockedThisTurn.Reset();
		}
	}

	public void OnPlayerTurnEnded()
	{
	}

	public PlayerTurnStatus GetPlayerTurnStatus()
	{
		return PlayerTurnStatus.TurnFinished;
	}

	public bool CanPayForSkill(VisitState visit, Label id)
	{
		Price skillPrice = GetSkillPrice(visit, id);
		return _player.finances.CanChangeMoney(visit.vehicle, skillPrice);
	}

	private Price GetSkillPrice(VisitState visit, Label id)
	{
		return FindSkillDef(id)?.GetCashPrice(visit) ?? Price.ZERO;
	}

	public bool DoPayForSkill(VisitState visit, Label id)
	{
		if (!CanPayForSkill(visit, id))
		{
			return false;
		}
		Price skillPrice = GetSkillPrice(visit, id);
		_player.finances.DoChangeMoney(visit.vehicle, skillPrice, MoneyReason.LearnSkills);
		Game.ctx.events.SendImmediate(new SessionEvent(SessionEventType.PlayerSkillsChangedImmediate, EntityID.INVALID, _pid, id));
		return true;
	}

	public bool GrantFreebieSkill(Label id, bool forceTicker = false)
	{
		VisitState visit = new VisitState(Game.ctx.players.Human.crew.GetCrewForPlayerPeep(), Game.ctx.clock.Now, PlayerID.HumanPlayer);
		return DoLearnPaidSkill(visit, QuestUUID.EMPTY, id, forceTicker);
	}

	public bool DoLearnPaidSkill(VisitState visit, QuestUUID quuid, Label id, bool forceTicker = false)
	{
		if (HasSkill(id))
		{
			return false;
		}
		_skilldata.currentSkills.Add(id);
		_skilldata.currentSkillInfos.Add(new PlayerSkillsData.SkillGained(id, visit.crew.peepId, visit.npc?.Id ?? EntityID.INVALID, visit.time));
		GrantContext ctx = new GrantContext(visit, quuid);
		SkillDef skillDef = FindSkillDef(id);
		UnlockResourcesFromDef(skillDef, startup: false);
		skillDef.grants?.ApplyAll(ctx);
		if (Game.ctx.IsInteractive || forceTicker)
		{
			ShowSkillLearnedTicker(skillDef, forceTicker);
		}
		Game.ctx.events.SendImmediate(new SessionEvent(SessionEventType.PlayerSkillsChangedImmediate, EntityID.INVALID, _pid, id));
		return true;
	}

	public bool DoLearnFromSkillTrack(Label id)
	{
		if (HasSkill(id))
		{
			return false;
		}
		_skilldata.currentSkills.Add(id);
		_skilldata.currentSkillInfos.Add(new PlayerSkillsData.SkillGained(id, EntityID.INVALID, EntityID.INVALID, Game.ctx.clock.Now));
		SkillDef def = FindSkillDef(id);
		UnlockResourcesFromDef(def, startup: false);
		Game.ctx.events.SendImmediate(new SessionEvent(SessionEventType.PlayerSkillsChangedImmediate, EntityID.INVALID, _pid, id));
		return true;
	}

	public static void ShowSkillLearnedTicker(SkillDef def, bool forceTicker = false)
	{
		string name = def.GetName();
		string desc = def.GetDesc();
		string message = Loc.Get("ui.playerskills.learned", "name", name, "desc", desc);
		Game.ctx.hud.tickers.AddTextTicker(TickerIcon.SKILLGAIN, TickerTitle.SKILLGAIN, message, default(TickerTarget), TickerPersistType.Persist);
		if (!forceTicker)
		{
			Game.ctx.simman.hints.ShowSkillHint();
		}
	}

	public void DoUnlearnSkill(Label id)
	{
		if (HasSkill(id))
		{
			int index = _skilldata.IndexOfSkill(id);
			_skilldata.currentSkills.RemoveAt(index);
			_skilldata.currentSkillInfos.RemoveAt(index);
		}
	}

	public bool HasSkill(Label id)
	{
		return _skilldata.HasSkill(id);
	}

	public bool HasSkillAny(List<Label> ids)
	{
		if (ids == null)
		{
			return false;
		}
		int i = 0;
		for (int count = ids.Count; i < count; i++)
		{
			if (_skilldata.HasSkill(ids[i]))
			{
				return true;
			}
		}
		return false;
	}

	public SkillDef FindSkillDef(Label id)
	{
		return _settings.GetSkill(id);
	}

	public IEnumerable<SkillDef> GetCurrentSkills()
	{
		return _skilldata.currentSkills.Select((Label id) => _settings.GetSkill(id));
	}

	public IEnumerable<string> MakeSkillNameList(IEnumerable<Label> ids)
	{
		return ids.Select((Label id) => _settings.GetSkill(id)?.GetName());
	}

	public string MakeSkillNameList(IEnumerable<Label> ids, string separator)
	{
		return string.Join(separator, MakeSkillNameList(ids));
	}

	public bool HasOneSkillsToLearn(VisitState visit)
	{
		return FindOneSkillToLearn(visit) != null;
	}

	public SkillDef FindOneSkillToLearn(VisitState visit)
	{
		if (visit.npc == null)
		{
			return null;
		}
		if (visit.building.components.modules == null)
		{
			return null;
		}
		TagList skillTagsForAllModules = visit.building.components.modules.GetSkillTagsForAllModules();
		ListPool<SkillDef>.PooledBlockList pooledBlockList = ListPool<SkillDef>.Allocate();
		ListPool<SkillDef>.PooledBlockList pooledBlockList2 = ListPool<SkillDef>.Allocate();
		FindSkillsToLearn(visit, skillTagsForAllModules, pooledBlockList, pooledBlockList2);
		ListPool<SkillDef>.PooledBlockList list = ((pooledBlockList.Count > 0) ? pooledBlockList : ((pooledBlockList2.Count > 0) ? pooledBlockList2 : null));
		SkillDef result = visit.npc.components.ident.GetIdentityRNGUnchanging().PickElementOrDefault(list);
		ListPool<SkillDef>.Free(pooledBlockList);
		ListPool<SkillDef>.Free(pooledBlockList2);
		return result;
	}

	public bool FindAllSkillsToLearn(VisitState visit, List<SkillDef> results)
	{
		TagList skillTagsForAllModules = visit.building.components.modules.GetSkillTagsForAllModules();
		FindSkillsToLearn(visit, skillTagsForAllModules, results, results);
		return results.Count > 0;
	}

	public List<SkillDef> FindAllDistinctSkillsToLearn(VisitState visit)
	{
		List<SkillDef> list = new List<SkillDef>();
		TagList skillTagsForAllModules = visit.building.components.modules.GetSkillTagsForAllModules();
		FindSkillsToLearn(visit, skillTagsForAllModules, list, list);
		return (from x in list
			group x by x.locname into @group
			select @group.First()).ToList();
	}

	public bool FindAllTaggedSkillsToLearn(VisitState visit, List<SkillDef> results)
	{
		TagList skillTagsForAllModules = visit.building.components.modules.GetSkillTagsForAllModules();
		FindSkillsToLearn(visit, skillTagsForAllModules, results);
		return results.Count > 0;
	}

	public bool FindAllUntaggedSkillsToLearn(VisitState visit, List<SkillDef> results)
	{
		FindSkillsToLearn(visit, new TagList(), null, results);
		return results.Count > 0;
	}

	public void FindSkillsToLearn(VisitState visit, TagList moduleSkillTags, List<SkillDef> outTagged = null, List<SkillDef> outUntagged = null)
	{
		outTagged?.Clear();
		outUntagged?.Clear();
		foreach (SkillDef item in _settings.items)
		{
			if (!HasSkill(item.id) && CheckVisReqs(item, visit) && CheckSkillTags(item, moduleSkillTags))
			{
				((item.tags == null || item.tags.Count == 0) ? outUntagged : outTagged)?.Add(item);
			}
		}
	}

	private bool CheckVisReqs(SkillDef def, VisitState visit)
	{
		return def.visreqs.AllPass(visit);
	}

	private bool CheckSkillTags(SkillDef def, TagList moduleSkillTags)
	{
		bool num = def.tags == null || def.tags.Count == 0;
		bool flag = def.tags != null && moduleSkillTags != null && moduleSkillTags.ContainsAtLeastOneOf(def.tags);
		return num || flag;
	}

	public SkillDef FindSkillForQuest(QuestUUID uuid)
	{
		string text = Game.ctx.quests.FindQuestIDForQuest(uuid);
		return _settings.GetSkillByQuestID((Label)text);
	}

	public void StartQuestFromSkillConvo(Label skillId, Entity owner)
	{
		QuestDefinition quest = FindSkillDef(skillId).quest;
		Game.ctx.quests.StartQuest(quest.id, owner.Id, fromRequest: false);
	}

	public string ExplainSkillPrereqs(SkillDef def, VisitState visit, bool brief)
	{
		bool num = def.quest != null;
		bool flag = def.cost.cash != null;
		if (num && flag)
		{
			Label id = def.id;
			Logger.Warning("Skill should not have both quest and cost definition: " + id.ToString());
		}
		if (num)
		{
			return ExplainQuest(def, brief);
		}
		if (flag)
		{
			return Loc.Get("quest-generic.cost", "price", Loc.Price(def.GetCashPrice(visit)));
		}
		return "";
	}

	private static string ExplainQuest(SkillDef def, bool brief)
	{
		return Loc.Get("quest-generic.explain", "desc", def.quest.goals.SelectToString((BaseGoal goal) => ExplainGoal(goal, brief), "; "));
	}

	private static string ExplainGoal(BaseGoal goal, bool brief)
	{
		if (!brief || !(goal is DeliveryGoal deliveryGoal))
		{
			return goal.GetLocDesc();
		}
		return deliveryGoal.GetLocBrief();
	}

	private void UnlockDefaultResources()
	{
		PlayerType playerType = _pid.FindPlayer().PlayerType;
		if (playerType != PlayerType.HumanPlayer && playerType != PlayerType.GangPlayer)
		{
			return;
		}
		SimulationManager simman = Game.ctx.simman;
		foreach (Label item in Game.ctx.session.mapconfig.playerStart.resourcesUnlocked)
		{
			if (simman.FindResource(item) != null)
			{
				UnlockResource(item, startup: true);
				continue;
			}
			Label label = item;
			Logger.Warning("Unknown default resource in map definition: " + label.ToString());
		}
	}

	public List<Label> GetUnlockedResourcesUnsafe()
	{
		return _skilldata.resUnlocked;
	}

	public bool HasResourceUnlocked(Label resId)
	{
		return _skilldata.HasUnlockedRes(resId);
	}

	public void UnlockResourcesFromDef(SkillDef def, bool startup)
	{
		if (def.unlocksResources == null)
		{
			return;
		}
		foreach (Label unlocksResource in def.unlocksResources)
		{
			UnlockResource(unlocksResource, startup);
		}
	}

	public void UnlockResource(Label resId, bool startup)
	{
		if (HasResourceUnlocked(resId) || Resource.Find(resId) == null || Resource.Find(resId).forceNoReveal)
		{
			return;
		}
		_skilldata.resUnlocked.Add(resId);
		if (!startup)
		{
			Resource resource = Resource.Find(resId);
			if (_pid.IsHumanPlayer)
			{
				Game.ctx.hud.tickers.AddTextTicker(TickerIcon.MakeRaw(resource.GetIcon()), TickerTitle.RESOURCE, Loc.Get("ui.playerskills.resource", "name", resource.GetIconAndName(), "desc", resource.GetDesc()), default(TickerTarget), TickerPersistType.Persist);
				_unlocked.Reset();
				_unlocked.res = resource;
				ProduceBusinessesThatTradeResource(resource, _unlocked.buildings);
				Game.ctx.events.EnqueueOnce(new SessionEvent(SessionEventType.PlayerResourcesChanged, EntityID.INVALID, _pid, resource));
			}
		}
	}

	public void ProduceBusinessesThatTradedRecently(List<EntityResDir> results)
	{
		ProduceBusinessesHelper(null, results);
	}

	public void ProduceBusinessesThatTradeResource(Resource res, List<EntityResDir> results)
	{
		ProduceBusinessesHelper(res, results);
	}

	private void ProduceBusinessesHelper(Resource resOrNull, List<EntityResDir> results)
	{
		IEnumerable<Node> allKnownNodesExpensive = _player.territory.GetAllKnownNodesExpensive();
		results.Clear();
		foreach (Node item in allKnownNodesExpensive)
		{
			CollectBusinessesToUpdate(resOrNull, item, results);
		}
	}

	private void CollectBusinessesToUpdate(Resource resOrNull, Node node, List<EntityResDir> results)
	{
		using ListPool<Entity>.PooledBlockList pooledBlockList = ListPool<Entity>.Allocate();
		node.FindBuildingsToShowGuitarPicks(_player.PID, pooledBlockList);
		foreach (Entity item3 in pooledBlockList)
		{
			if (resOrNull != null)
			{
				var (flag, toBldg) = CanBuySell(resOrNull, item3);
				if (flag)
				{
					EntityResDir item = new EntityResDir
					{
						eid = item3.Id,
						res = resOrNull,
						toBldg = toBldg
					};
					results.Add(item);
				}
			}
			else
			{
				ResourceAndQty resourceAndQty = HasRecentTransaction(item3);
				if (resourceAndQty.IsSet)
				{
					EntityResDir item2 = new EntityResDir
					{
						eid = item3.Id,
						res = resourceAndQty.FindResource(),
						toBldg = (resourceAndQty.qty > 0)
					};
					results.Add(item2);
				}
			}
		}
	}

	public (bool valid, bool toBldg) CanBuySell(Resource res, Entity building)
	{
		if (!_player.territory.IsScoped(building))
		{
			return (valid: false, toBldg: false);
		}
		Entity entity = BuildingUtil.FindOwnerOrManagerForAnyBuilding(building);
		if (entity == null)
		{
			return (valid: false, toBldg: false);
		}
		bool flag = _player.social.AreIllegalItemsLocked(entity, building);
		var (flag2, flag3) = building.components.modules?.CanPlayerBuyOrSell(_pid, res.resid, !flag) ?? (false, false);
		return (valid: flag2 || flag3, toBldg: flag3);
	}

	private ResourceAndQty HasRecentTransaction(Entity building)
	{
		return (BuildingUtil.FindBizForBuilding(building)?.data.biz.GetTradeSummariesOrNull(_pid))?.GetLargestIfRecent(RECENT_TRANSACTION_AGE) ?? ResourceAndQty.NONE;
	}

	public void AddFlag(Label key)
	{
		if (!ContainsFlag(key))
		{
			_skilldata.playerFlags = _skilldata.playerFlags ?? new List<Label>();
			_skilldata.playerFlags.Add(key);
		}
	}

	public void RemoveFlag(Label key)
	{
		if (_skilldata.playerFlags != null)
		{
			_skilldata.playerFlags.Remove(key);
			if (_skilldata.playerFlags.Count == 0)
			{
				_skilldata.playerFlags = null;
			}
		}
	}

	public void ToggleFlag(Label key, bool value)
	{
		if (value)
		{
			AddFlag(key);
		}
		else
		{
			RemoveFlag(key);
		}
	}

	public bool ContainsFlag(Label key)
	{
		return _skilldata.playerFlags?.Contains(key) ?? false;
	}

	public bool TestFlag(Label key, bool expected)
	{
		return ContainsFlag(key) == expected;
	}

	public void SetCounter(Label key, int value)
	{
		_skilldata.playerCounters = _skilldata.playerCounters ?? new Dictionary<Label, int>();
		_skilldata.playerCounters[key] = value;
	}

	public void RemoveCounter(Label key)
	{
		if (_skilldata.playerCounters != null)
		{
			_skilldata.playerCounters.Remove(key);
			if (_skilldata.playerCounters.Count == 0)
			{
				_skilldata.playerCounters = null;
			}
		}
	}

	public int? GetCounterOrNull(Label key)
	{
		return _skilldata.playerCounters?.FindOrNullable(key);
	}

	public int GetCounterOrDefault(Label key, int defaultValue = 0)
	{
		return GetCounterOrNull(key) ?? defaultValue;
	}

	public void IncrementCounter(Label key, int delta)
	{
		SetCounter(key, GetCounterOrDefault(key) + delta);
	}

	protected override void InitializeConsoleEntries()
	{
		base.InitializeConsoleEntries();
		foreach (SkillDef item in Game.serv.globals.settings.skills.items)
		{
			string second = item.id.String;
			Game.ctx.console.Add(this, new DebugConsoleEntry("skills", "add", second, CheatAddSkill));
			Game.ctx.console.Add(this, new DebugConsoleEntry("skills", "remove", second, CheatRemoveSkill));
		}
		foreach (KeyValuePair<Label, Resource> item2 in Game.ctx.resManager.resourcesCache)
		{
			string second2 = item2.Key.String;
			if (!item2.Value.forceNoReveal)
			{
				Game.ctx.console.Add(this, new DebugConsoleEntry("resources", "unlock", second2, CheatUnlockResource));
				Game.ctx.console.Add(this, new DebugConsoleEntry("resources", "add", second2, CheatIncrementResource));
			}
		}
	}

	protected override void ReleaseConsoleEntries()
	{
		Game.ctx.console.Remove(this);
		base.ReleaseConsoleEntries();
	}

	private string CheatAddSkill(string[] args)
	{
		if (args.Length != 3)
		{
			return DebugConsoleEntry.InvalidParam("Invalid parameters", args, 2, "<skill-id>");
		}
		GrantFreebieSkill((Label)args[2]);
		return "Added skill: " + args[2] + ".";
	}

	private string CheatRemoveSkill(string[] args)
	{
		if (args.Length != 3)
		{
			return DebugConsoleEntry.InvalidParam("Invalid parameters", args, 2, "<skill-id>");
		}
		DoUnlearnSkill((Label)args[2]);
		return "Removed skill: " + args[2];
	}

	private string CheatUnlockResource(string[] args)
	{
		if (args.Length != 3)
		{
			return DebugConsoleEntry.InvalidParam("Invalid parameters", args, 2, "<resource-id>");
		}
		UnlockResource((Label)args[2], startup: false);
		return "Unlocked resource: " + args[2] + ".";
	}

	private string CheatIncrementResource(string[] args)
	{
		if (args.Length != 4)
		{
			return DebugConsoleEntry.InvalidParam("Invalid parameters", args, 2, "<resource-id> <delta>");
		}
		Label resid = (Label)args[2];
		if (!int.TryParse(args[3], out var result))
		{
			return DebugConsoleEntry.InvalidParam("Invalid quantity; expected", args, 3, "<delta>");
		}
		InventoryModule inventory = ModulesUtil.GetInventory(Game.ctx.players.Human.territory.Safehouse);
		if (inventory == null)
		{
			return "Player safehouse is missing an inventory? Cannot increment resource.";
		}
		bool flag = inventory.data.Increment(resid, result);
		Fixnum qty = inventory.data.Get(resid).qty;
		string text = (flag ? "Incremented" : "Failed to change");
		return $"{text} safehouse resource {args[2]} by {result}, currently in storage: {qty}.";
	}
}
