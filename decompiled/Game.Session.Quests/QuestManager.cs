using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Game.Core;
using Game.Services;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Player;
using SomaSim.SION;
using SomaSim.Util;

namespace Game.Session.Quests;

public class QuestManager : AbstractSessionManager, ISaveLoadProvider, IAnimatingManager, ISessionManager
{
	private QuestManagerData _data = new QuestManagerData();

	private Dictionary<string, QuestDefinition> _defCache;

	private HashSet<QuestUUID> _dirty = new HashSet<QuestUUID>();

	private QuestRequestTracker _requests;

	public QuestRequestTracker Requests => _requests;

	public int ActiveQuestsCount => _data.active.Count;

	public override void OnInitializeDone()
	{
		base.OnInitializeDone();
		_defCache = CollectQuestDefinitions();
		Game.ctx.events.AddListener(SessionEventType.GoalProgress, OnGoalProgress);
		Game.ctx.events.AddListener(SessionEventType.OnGameBecomeInteractive, OnGameReady);
		Game.ctx.events.AddListener(SessionEventType.HumanPlayerTurnStarted, CheckQuestExpiration);
	}

	public override void OnInteractive()
	{
		base.OnInteractive();
		_requests = new QuestRequestTracker();
		_requests.Initialize(this, _data);
		if (!Game.ctx.HasSaveFile)
		{
			OnNewGameStart();
		}
	}

	public override void OnReleased()
	{
		foreach (KeyValuePair<QuestUUID, QuestActiveRecord> item in _data.active)
		{
			UnregisterGoals(item.Value);
		}
		Game.ctx.events.RemoveListener(SessionEventType.HumanPlayerTurnStarted, CheckQuestExpiration);
		Game.ctx.events.RemoveListener(SessionEventType.OnGameBecomeInteractive, OnGameReady);
		Game.ctx.events.RemoveListener(SessionEventType.GoalProgress, OnGoalProgress);
		_requests.Release();
		_requests = null;
		_defCache = null;
		base.OnReleased();
	}

	public QuestManagerData GetQuestDataUnsafe()
	{
		return _data;
	}

	public QuestActiveRecord FindActiveQuestUnsafe(QuestUUID uuid)
	{
		return _data.active.FindOrNull(uuid);
	}

	public QuestWaitingRecord FindWaitingQuestUnsafe(QuestUUID uuid)
	{
		return _data.waiting.FindOrNull(uuid);
	}

	public QuestCompletedRecord FindCompletedQuestUnsafe(QuestUUID uuid)
	{
		return _data.completed.FindOrNull(uuid);
	}

	public int FindCompleteQuestCount()
	{
		return _data.completed.Count;
	}

	public QuestRequest FindRequestedQuestUnsafe(EntityID eid)
	{
		return _data.requests.FindOrNull(eid);
	}

	public string FindQuestIDForQuest(QuestUUID uuid)
	{
		object obj = _data.active.FindOrNull(uuid)?.questid;
		if (obj == null)
		{
			obj = _data.waiting.FindOrNull(uuid)?.questid;
			if (obj == null)
			{
				QuestCompletedRecord questCompletedRecord = _data.completed.FindOrNull(uuid);
				if (questCompletedRecord == null)
				{
					return null;
				}
				obj = questCompletedRecord.questid;
			}
		}
		return (string)obj;
	}

	private Dictionary<string, QuestDefinition> CollectQuestDefinitions()
	{
		Dictionary<string, QuestDefinition> dictionary = new Dictionary<string, QuestDefinition>();
		foreach (QuestDefinition allQuest in Game.serv.globals.settings.quests.GetAllQuests())
		{
			dictionary[allQuest.id] = allQuest;
		}
		foreach (SkillDef item in Game.serv.globals.settings.skills.items)
		{
			if (item.quest != null)
			{
				dictionary.Add(item.quest.id, item.quest);
			}
		}
		foreach (ResidentialEventResultConfig result in Game.serv.globals.settings.people.residentialEvents.results)
		{
			if (result.quest != null)
			{
				dictionary.Add(result.quest.id, result.quest);
			}
		}
		return dictionary;
	}

	public int GetActiveQuestCap()
	{
		return Game.serv.globals.settings.quests.global.questNumberCap.Evaluate(PlayerID.HumanPlayer).IntCeiling();
	}

	public string ExplainActiveQuestCap()
	{
		return Game.serv.globals.settings.quests.global.questNumberCap.Explain(new ModQuery(PlayerID.HumanPlayer), addHeader: true);
	}

	public bool IsQuestActive(QuestUUID uuid)
	{
		return _data.active.ContainsKey(uuid);
	}

	public bool IsQuestWaiting(QuestUUID uuid)
	{
		return _data.waiting.ContainsKey(uuid);
	}

	public bool IsQuestCompleted(QuestUUID uuid)
	{
		return _data.completed.ContainsKey(uuid);
	}

	public bool IsQuestNotStarted(QuestUUID uuid)
	{
		if (!IsQuestActive(uuid))
		{
			return !IsQuestCompleted(uuid);
		}
		return false;
	}

	public bool IsQuestActiveByID(string questid, EntityID target)
	{
		bool isNotValid = target.IsNotValid;
		foreach (QuestActiveRecord value in _data.active.Values)
		{
			if (value.questid == questid && (isNotValid || target == value.target))
			{
				return true;
			}
		}
		return false;
	}

	public bool IsQuestCompletedByID(string questid, EntityID target)
	{
		bool isNotValid = target.IsNotValid;
		foreach (QuestCompletedRecord value in _data.completed.Values)
		{
			if (value.questid == questid && (isNotValid || target == value.target))
			{
				return true;
			}
		}
		return false;
	}

	public bool IsQuestWaitingByID(string questid, EntityID target)
	{
		bool isNotValid = target.IsNotValid;
		foreach (QuestWaitingRecord value in _data.waiting.Values)
		{
			if (value.questid == questid && (isNotValid || target == value.target))
			{
				return true;
			}
		}
		return false;
	}

	public QuestDefinition FindQuestDefinition(string questid)
	{
		return _defCache.FindOrNull(questid);
	}

	public float ProduceActiveQuestProgress(QuestUUID uuid)
	{
		QuestActiveRecord questActiveRecord = FindActiveQuestUnsafe(uuid);
		if (questActiveRecord == null || questActiveRecord.goalids == null || questActiveRecord.goalids.Count == 0)
		{
			return 0f;
		}
		float num = 0f;
		int i = 0;
		for (int count = questActiveRecord.goalids.Count; i < count; i++)
		{
			num += Game.ctx.goals.ProduceGoalCompletionFraction(questActiveRecord.goalids[i]);
		}
		return num / (float)questActiveRecord.goalids.Count;
	}

	public QuestUUID StartQuest(string questid, EntityID target, bool fromRequest)
	{
		QuestDefinition questDefinition = _defCache.FindOrNull(questid);
		if (questDefinition == null)
		{
			Logger.Error("Unknown quest: " + questid);
			return QuestUUID.EMPTY;
		}
		QuestUUID questUUID = new QuestUUID(_data.nextUUID++);
		QuestActiveRecord questActiveRecord = new QuestActiveRecord(questUUID, questDefinition, target, Game.ctx.clock.Now);
		RegisterGoals(questDefinition, questActiveRecord, target);
		_data.active.Add(questUUID, questActiveRecord);
		Game.ctx.hud.quests.Add(questUUID);
		_dirty.Add(questUUID);
		if (fromRequest)
		{
			_requests.OnAcceptedRequest(target, questUUID);
		}
		_requests.ClearUnusedRequests();
		if (questActiveRecord.expires <= Game.ctx.clock.Now)
		{
			ExpireQuest(questActiveRecord);
		}
		Game.ctx.events.SendImmediate(new SessionEvent(SessionEventType.QuestStartedImmediate, target, PlayerID.HumanPlayer, questUUID));
		return questUUID;
	}

	private void MarkActiveQuestAsWaiting(QuestUUID uuid, bool success)
	{
		QuestActiveRecord questActiveRecord = _data.active.FindOrNull(uuid);
		if (questActiveRecord == null)
		{
			Logger.Warning("Completing quest that wasn't active: " + uuid);
			return;
		}
		if (_defCache.FindOrNull(questActiveRecord.questid) == null)
		{
			Logger.Error("Unknown quest: " + uuid);
			return;
		}
		_data.active.Remove(uuid);
		UnregisterGoals(questActiveRecord);
		QuestWaitingRecord value = new QuestWaitingRecord(questActiveRecord, Game.ctx.clock.Now, success);
		_data.waiting.Add(uuid, value);
		Game.ctx.events.SendImmediate(new SessionEvent(SessionEventType.QuestMarkedWaitingImmediate, EntityID.INVALID, PlayerID.HumanPlayer, uuid));
	}

	private void CompleteWaitingQuest(QuestUUID uuid, VisitState visit = null)
	{
		QuestWaitingRecord questWaitingRecord = _data.waiting.FindOrNull(uuid);
		if (questWaitingRecord == null)
		{
			Logger.Warning("Completing quest that wasn't active: " + uuid);
			return;
		}
		bool flag = questWaitingRecord.IsSuccessGood();
		_data.waiting.Remove(uuid);
		QuestCompletedRecord value = new QuestCompletedRecord(questWaitingRecord);
		_data.completed.Add(uuid, value);
		EntityID ownerId = visit?.npc?.Id ?? EntityID.INVALID;
		_requests.OnCompletedRequest(ownerId, uuid);
		_requests.ClearUnusedRequests();
		if (questWaitingRecord.successful && questWaitingRecord.hasChoices && questWaitingRecord.choiceIndex >= 0)
		{
			questWaitingRecord.FindQuestDefinition().choices[questWaitingRecord.choiceIndex].grants?.ApplyAll(MakeGrantContext(uuid, visit));
			if (flag)
			{
				Game.ctx.simman.hints.ShowQuestHint();
			}
		}
		if (ownerId.IsValid && flag)
		{
			EntityID sourceId = visit?.crew.peepId ?? EntityID.INVALID;
			Game.ctx.players.Human.social.PerformSocialActionOn(SocialConstants.QUEST_COMPLETE, ownerId, sourceId, uuid, Extend);
		}
		Game.ctx.events.SendImmediate(new SessionEvent(SessionEventType.QuestCompletedImmediate, EntityID.INVALID, PlayerID.HumanPlayer, uuid));
		HistoryLedgerItem Extend(HistoryLedgerItem info)
		{
			info.actor = visit?.crew.peepId ?? default(EntityID);
			info.target = ownerId;
			info.node = visit?.GetBldgNodeID() ?? default(NodeID);
			return info;
		}
	}

	private static GrantContext MakeGrantContext(QuestUUID uuid, VisitState optionalVisit)
	{
		return new GrantContext(optionalVisit ?? new VisitState(CrewAssignment.EMPTY, Game.ctx.clock.Now, PlayerID.HumanPlayer), uuid);
	}

	private void OnNewGameStart()
	{
		ProcessStarterQuests();
	}

	private void ProcessStarterQuests()
	{
		_ = Game.serv.globals.settings.general.debug.skipStarterQuests;
	}

	private void CheckQuestExpiration(SessionEvent _event)
	{
		using ListPool<QuestActiveRecord>.PooledBlockList pooledBlockList = ListPool<QuestActiveRecord>.Allocate();
		SimTime now = Game.ctx.clock.Now;
		foreach (QuestActiveRecord value in _data.active.Values)
		{
			SimTime expires = value.expires;
			if (!expires.NeverHappens && expires.days <= now.days)
			{
				pooledBlockList.Add(value);
			}
		}
		foreach (QuestActiveRecord item in pooledBlockList)
		{
			ExpireQuest(item);
		}
	}

	private void ExpireQuest(QuestActiveRecord record)
	{
		QuestUUID uuid = record.uuid;
		EntityID target = record.target;
		bool num = IsGoodExpire(record);
		string name = record.FindQuestDefinition().GetName();
		string message = (num ? Loc.Get("ui.reports.quests.ticker-expired.good", "name", name) : Loc.Get("ui.reports.quests.ticker-expired", "name", name));
		Game.ctx.hud.tickers.AddTextTicker(TickerIcon.QUEST_UPDATE, TickerTitle.QUEST_UPDATE, message, target);
		ProcessRefunds(record);
		MarkActiveQuestAsWaiting(uuid, success: false);
		QuestWaitingRecord questWaitingRecord = FindWaitingQuestUnsafe(uuid);
		bool num2 = (questWaitingRecord.FindExpirationGrantsOrNull()?.Count ?? 0) > 0;
		questWaitingRecord.SetExpiredAndWaiting();
		if (!num2)
		{
			questWaitingRecord.SetExpiredDone();
			_dirty.Add(questWaitingRecord.uuid);
		}
		Game.ctx.events.SendImmediate(new SessionEvent(SessionEventType.QuestProgressImmediate, target, PlayerID.HumanPlayer, uuid));
	}

	public bool IsGoodExpire(QuestActiveRecord record)
	{
		bool flag = AreAllGoalsCompleted(record);
		bool unhappyOnSuccess = Game.ctx.quests.FindQuestDefinition(record.questid).unhappyOnSuccess;
		if (!(!flag && unhappyOnSuccess))
		{
			if (flag)
			{
				return !unhappyOnSuccess;
			}
			return false;
		}
		return true;
	}

	public void CompleteExpiredWaitingQuest(QuestUUID quuid, VisitState visit)
	{
		QuestWaitingRecord questWaitingRecord = FindWaitingQuestUnsafe(quuid);
		if (questWaitingRecord != null && questWaitingRecord.IsExpiredAndWaiting)
		{
			questWaitingRecord.FindExpirationGrantsOrNull()?.ApplyAll(new GrantContext(visit));
			if (questWaitingRecord.successful)
			{
				Game.ctx.simman.hints.ShowQuestHint();
			}
			questWaitingRecord.SetExpiredDone();
			_dirty.Add(questWaitingRecord.uuid);
			Game.ctx.events.SendImmediate(new SessionEvent(SessionEventType.QuestProgressImmediate, questWaitingRecord.target, PlayerID.HumanPlayer, quuid));
		}
	}

	public QuestActiveRecord FindQuestForGoalId(string goalid)
	{
		foreach (QuestActiveRecord value in _data.active.Values)
		{
			if (value.goalids.Contains(goalid))
			{
				return value;
			}
		}
		return null;
	}

	public bool AreAllGoalsCompleted(QuestActiveRecord quest)
	{
		foreach (string goalid in quest.goalids)
		{
			BaseGoal baseGoal = Game.ctx.goals.FindGoalOrNull(goalid);
			if (baseGoal == null || !baseGoal.state.completed)
			{
				return false;
			}
		}
		return true;
	}

	public List<BaseGoal> FindAllGoalsForQuest(QuestActiveRecord record)
	{
		return (from goalId in record.goalids
			select Game.ctx.goals.FindGoalOrNull(goalId) into goal
			where goal != null
			select goal).ToList();
	}

	public bool HasActiveQuestForTarget(EntityID target)
	{
		return FindActiveQuestForTarget(target).IsSet;
	}

	public bool HasWaitingQuestForTarget(EntityID target)
	{
		return FindWaitingQuestForTarget(target).IsSet;
	}

	public bool HasCompletedQuestForTarget(EntityID target)
	{
		return FindCompletedQuestForTarget(target).IsSet;
	}

	public int CountActiveQuestsWith(EntityID target)
	{
		if (!FindActiveQuestForTarget(target).IsSet)
		{
			return 0;
		}
		return 1;
	}

	public QuestUUID FindActiveOrWaitingQuestForTarget(EntityID target)
	{
		QuestUUID result = QuestUUID.EMPTY;
		if (result.IsNotSet)
		{
			result = FindWaitingQuestForTarget(target);
		}
		if (result.IsNotSet)
		{
			result = FindActiveQuestForTarget(target);
		}
		return result;
	}

	public QuestUUID FindActiveQuestForTarget(EntityID target)
	{
		foreach (QuestActiveRecord value in _data.active.Values)
		{
			if (target == value.target)
			{
				return value.uuid;
			}
		}
		return QuestUUID.EMPTY;
	}

	public QuestUUID FindWaitingQuestForTarget(EntityID target)
	{
		foreach (QuestWaitingRecord value in _data.waiting.Values)
		{
			if (target == value.target)
			{
				return value.uuid;
			}
		}
		return QuestUUID.EMPTY;
	}

	public QuestUUID FindCompletedQuestForTarget(EntityID target)
	{
		foreach (QuestCompletedRecord value in _data.completed.Values)
		{
			if (target == value.target)
			{
				return value.uuid;
			}
		}
		return QuestUUID.EMPTY;
	}

	public QuestUUID FindCompletedQuestForTarget(EntityID target, string questid)
	{
		foreach (QuestCompletedRecord value in _data.completed.Values)
		{
			if (target == value.target && questid == value.questid)
			{
				return value.uuid;
			}
		}
		return QuestUUID.EMPTY;
	}

	private void RegisterGoals(QuestDefinition def, QuestActiveRecord quest, EntityID target)
	{
		for (int i = 0; i < def.goals.Count; i++)
		{
			BaseGoal original = def.goals[i];
			string text = $"{def.id}_{i}/{_data.nextGUUID++}";
			Game.ctx.goals.CloneAndRegisterGoal(original, text, target);
			quest.goalids.Add(text);
		}
	}

	private void UnregisterGoals(QuestActiveRecord quest)
	{
		foreach (string goalid in quest.goalids)
		{
			Game.ctx.goals.UnregisterClonedGoal(goalid);
		}
	}

	public void OnGoalProgress(SessionEvent ev)
	{
		QuestActiveRecord questActiveRecord = FindQuestForGoalId(ev.ctx as string);
		if (questActiveRecord != null)
		{
			_dirty.Add(questActiveRecord.uuid);
		}
	}

	public void UpdateAnimations(GameAnimUpdate anim)
	{
		foreach (QuestUUID item in _dirty)
		{
			UpdateQuest(item);
		}
		_dirty.Clear();
	}

	internal void UpdateQuestDuringVisit(QuestUUID uuid, VisitState visit)
	{
		if (_dirty.Contains(uuid))
		{
			UpdateQuest(uuid, visit);
			_dirty.Remove(uuid);
		}
	}

	private void UpdateQuest(QuestUUID uuid, VisitState visit = null)
	{
		QuestActiveRecord questActiveRecord = FindActiveQuestUnsafe(uuid);
		if (questActiveRecord != null && AreAllGoalsCompleted(questActiveRecord))
		{
			UpdateQuestAsNoLongerActive(questActiveRecord, success: true);
		}
		QuestWaitingRecord questWaitingRecord = FindWaitingQuestUnsafe(uuid);
		if (questWaitingRecord != null && questWaitingRecord.IsDone)
		{
			CompleteWaitingQuest(uuid, visit);
		}
		Game.ctx.events.SendImmediate(new SessionEvent(SessionEventType.QuestProgressImmediate, EntityID.INVALID, PlayerID.HumanPlayer, uuid));
	}

	private void UpdateQuestAsNoLongerActive(QuestActiveRecord active, bool success)
	{
		QuestUUID uuid = active.uuid;
		MarkActiveQuestAsWaiting(uuid, success);
		QuestWaitingRecord questWaitingRecord = FindWaitingQuestUnsafe(uuid);
		if (questWaitingRecord.IsActive)
		{
			if (questWaitingRecord.hasChoices)
			{
				questWaitingRecord.SetPlayerShouldChooseReward();
			}
			else
			{
				questWaitingRecord.SetDoneWithoutPlayerChoice();
			}
		}
	}

	public void OnRewardChoiceFinished(VisitState visit, QuestUUID uuid, int choiceIndex, out string afterchoice)
	{
		QuestWaitingRecord questWaitingRecord = FindWaitingQuestUnsafe(uuid);
		afterchoice = questWaitingRecord.FindQuestDefinition()?.afterchoice;
		questWaitingRecord.SetDoneWithPlayerChoice(choiceIndex);
		UpdateQuest(uuid, visit);
	}

	private void OnGameReady(SessionEvent sev)
	{
		if (Game.ctx.HasSaveFile)
		{
			AddTickersForLoadedQuests();
		}
	}

	private void AddTickersForLoadedQuests()
	{
		foreach (KeyValuePair<QuestUUID, QuestActiveRecord> item in _data.active)
		{
			Add(item.Key, item.Value.questid, item.Value.uuid);
		}
		foreach (KeyValuePair<QuestUUID, QuestWaitingRecord> item2 in _data.waiting)
		{
			Add(item2.Key, item2.Value.questid, item2.Value.uuid);
		}
		void Add(QuestUUID key, string questid, QuestUUID uuid)
		{
			_dirty.Add(key);
			if (_defCache.FindOrNull(questid) != null)
			{
				Game.ctx.hud.quests.Add(uuid);
			}
			else
			{
				Logger.Warning($"Quest definition {key} missing, not adding a ticker");
			}
		}
	}

	public string ExplainExpirationOrNull(QuestDefinition quest, string[] replacements)
	{
		if (quest.expiration == null)
		{
			return null;
		}
		return Loc.Get(quest.expiration.locblurb, replacements).Trim();
	}

	public bool IsRefundable(QuestDefinition quest)
	{
		return quest.goals?.All((BaseGoal g) => g.IsRefundable) ?? false;
	}

	public Fixnum GetRefundRate(QuestDefinition quest)
	{
		Fixnum? fixnum = quest.expiration?.refundPercent?.Evaluate(PlayerID.HumanPlayer);
		if (!fixnum.HasValue)
		{
			Logger.Warning("Quest " + quest.id + " missing expiration refundPercent value; assuming 0%");
			return 0;
		}
		return Fixnum.Clamp(fixnum.Value / 100, 0, 1);
	}

	public void ProcessRefunds(QuestActiveRecord record)
	{
		QuestDefinition quest = record.FindQuestDefinition();
		if (!IsRefundable(quest))
		{
			return;
		}
		List<BaseGoal> list = FindAllGoalsForQuest(record);
		Fixnum refundRate = GetRefundRate(quest);
		foreach (BaseGoal item in list)
		{
			if (item.IsRefundable)
			{
				item.DoRefund(refundRate);
			}
		}
	}

	public (QuestDefinition def, EntityID target) FindQuestDefAndTarget(QuestUUID uuid)
	{
		QuestActiveRecord questActiveRecord = _data.active.FindOrNull(uuid);
		if (questActiveRecord != null)
		{
			return (def: questActiveRecord.FindQuestDefinition(), target: questActiveRecord.target);
		}
		QuestWaitingRecord questWaitingRecord = _data.waiting.FindOrNull(uuid);
		if (questWaitingRecord != null)
		{
			return (def: questWaitingRecord.FindQuestDefinition(), target: questWaitingRecord.target);
		}
		return (def: null, target: EntityID.INVALID);
	}

	public (string name, string desc) DescribeQuestMain(QuestUUID uuid)
	{
		var (questDefinition, id) = FindQuestDefAndTarget(uuid);
		if (questDefinition == null)
		{
			return (name: null, desc: null);
		}
		LocReplacementContext ctx = default(LocReplacementContext);
		if (id.IsValid)
		{
			LocPersonReplacements person = NameUtils.MakeLocPerson(id.FindEntity());
			ctx = ctx.SetPerson(person);
		}
		string item = Loc.Get(questDefinition.locname, ctx);
		string item2 = Loc.Get(questDefinition.locdesc, ctx);
		return (name: item, desc: item2);
	}

	public string DescribeQuestGoals(QuestUUID uuid, bool showName)
	{
		QuestActiveRecord questActiveRecord = _data.active.FindOrNull(uuid);
		if (questActiveRecord != null)
		{
			DescribeActiveQuestGoals(questActiveRecord, showName);
		}
		QuestWaitingRecord questWaitingRecord = _data.waiting.FindOrNull(uuid);
		if (questWaitingRecord != null)
		{
			DescribeWaitingQuestGoals(questWaitingRecord, showName);
		}
		return null;
	}

	internal string DescribeFutureQuestGoals(QuestDefinition def, EntityID target, bool header)
	{
		StringBuilder stringBuilder = StringBuilderPool.AllocateInstance();
		if (header)
		{
			stringBuilder.AppendLine(Game.ctx.goals.GetGoalHeader());
		}
		foreach (BaseGoal goal in def.goals)
		{
			Game.ctx.goals.ProduceFutureGoalDescription(goal, target, stringBuilder);
		}
		return stringBuilder.ToStringAndReturnToPool();
	}

	internal string DescribeActiveQuestGoals(QuestActiveRecord record, bool showName)
	{
		StringBuilder sb = StringBuilderPool.AllocateInstance();
		if (showName)
		{
			DescribeQuestGiver(record.target, showGoto: false, sb);
		}
		if (!record.expires.NeverHappens)
		{
			DescribeExpiration(record.expires, expired: false, sb);
		}
		foreach (string goalid in record.goalids)
		{
			Game.ctx.goals.ProduceActiveGoalDescription(goalid, sb);
		}
		return sb.ToStringAndReturnToPool();
	}

	internal string DescribeWaitingQuestGoals(QuestWaitingRecord record, bool showName)
	{
		StringBuilder sb = StringBuilderPool.AllocateInstance();
		if (showName)
		{
			DescribeQuestGiver(record.target, showGoto: false, sb);
		}
		if (record.IsExpiredAndWaiting && record.IsSuccessGood())
		{
			DescribeExpiration(record.finished, expired: true, sb);
		}
		if (record.IsReadyForPlayerChoice || (record.IsExpiredAndWaiting && record.IsSuccessGood()))
		{
			DescribeQuestGiver(record.target, showGoto: true, sb);
		}
		return sb.ToStringAndReturnToPool();
	}

	private void DescribeExpiration(SimTime date, bool expired, StringBuilder sb)
	{
		string key = (expired ? "ui.reports.quests.expired" : "ui.reports.quests.expires-soon");
		string text = Loc.FormatDateShort(date);
		sb.AppendLine(Loc.Get(key, "date", text));
	}

	private void DescribeQuestGiver(EntityID target, bool showGoto, StringBuilder sb)
	{
		if (!target.IsNotValid)
		{
			PersonData personData = target.FindEntity()?.data.person;
			if (personData != null)
			{
				string value = (showGoto ? Loc.Get("ui.reports.quests.visit", "name", personData.FirstName) : Loc.Get("ui.reports.quests.name", "name", personData.FullName));
				sb.AppendLine(value);
			}
		}
	}

	public List<ResOrCash> MakeListOfResourcesOutstanding(QuestUUID uuid)
	{
		List<ResOrCash> list = new List<ResOrCash>();
		QuestActiveRecord questActiveRecord = FindActiveQuestUnsafe(uuid);
		if (questActiveRecord == null)
		{
			return list;
		}
		foreach (string goalid in questActiveRecord.goalids)
		{
			BaseGoal baseGoal = Game.ctx.goals.FindGoalOrNull(goalid);
			if (!(baseGoal is GoalDeliverCash goalDeliverCash))
			{
				if (baseGoal is GoalDeliverGoods goalDeliverGoods)
				{
					list.Add(new ResOrCash(new ResourceAndQty(goalDeliverGoods.resource, goalDeliverGoods.Delta)));
				}
			}
			else
			{
				list.Add(new ResOrCash(new Money(goalDeliverCash.Delta)));
			}
		}
		return list;
	}

	public string MakeChoiceDescription(QuestUUID quuid, QuestDefinition def, VisitState visit, int index)
	{
		if (index >= def.choices.Count)
		{
			return null;
		}
		QuestGrantChoice questGrantChoice = def.choices[index];
		StringBuilder stringBuilder = StringBuilderPool.Instance.Allocate();
		stringBuilder.AppendLine(Loc.Get(questGrantChoice.locdesc));
		if (questGrantChoice.grants != null)
		{
			stringBuilder.AppendLine(questGrantChoice.grants.Describe(new GrantContext(visit, quuid), multiline: true));
		}
		return stringBuilder.ToStringAndReturnToPool().Trim();
	}

	public string MakeExpirationDescription(QuestDefinition def)
	{
		if (def.expiration != null)
		{
			return Loc.Get(def.expiration.locdesc);
		}
		return "";
	}

	public void Save(Serializer s, ConcurrentSaveTable results)
	{
		results.Set("data", s.Serialize(_data));
	}

	public IEnumerator Load(Hashtable data)
	{
		SaveLoadUtils.DeserializeSingleKey(data, "data", delegate(QuestManagerData result)
		{
			_data = result;
		});
		yield break;
	}
}
