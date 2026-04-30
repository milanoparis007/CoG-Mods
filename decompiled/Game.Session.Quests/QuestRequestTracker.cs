using System.Collections.Generic;
using Game.Core;
using Game.Services;
using Game.Session.Data;
using SomaSim.Util;

namespace Game.Session.Quests;

public sealed class QuestRequestTracker
{
	private QuestManagerData _data;

	private QuestManager _manager;

	private List<EntityID> _tmp_todo = new List<EntityID>();

	private List<QuestDefinition> tmp_candidates = new List<QuestDefinition>();

	private QuestSettings Settings => Game.serv.globals.settings.quests;

	public void Initialize(QuestManager manager, QuestManagerData data)
	{
		_manager = manager;
		_data = data;
		Game.ctx.events.AddListener(SessionEventType.BuildingConstructionStateChanged, OnClearableAction);
	}

	public void Release()
	{
		Game.ctx.events.RemoveListener(SessionEventType.BuildingConstructionStateChanged, OnClearableAction);
		_manager = null;
		_data = null;
	}

	public void ClearUnusedRequests(bool declined = true, bool available = true, EntityID? specificNpc = null)
	{
		foreach (KeyValuePair<EntityID, QuestRequest> request in _data.requests)
		{
			bool num = !specificNpc.HasValue || specificNpc.Value == request.Key;
			bool flag = declined && request.Value.ShouldResetBecauseDeclined;
			bool flag2 = available && request.Value.ShouldResetBecauseAvailable;
			if (num && (flag || flag2))
			{
				_tmp_todo.Add(request.Key);
			}
		}
		foreach (EntityID item in _tmp_todo)
		{
			_data.requests.Remove(item);
		}
		_tmp_todo.Clear();
	}

	public void ClearUnusedRequestsFor(EntityID owner)
	{
		ClearUnusedRequests(declined: true, available: true, owner);
	}

	private void OnClearableAction(SessionEvent sev)
	{
		if (sev.pid.IsHumanPlayer)
		{
			ClearUnusedRequests();
		}
	}

	public bool ShouldPresentRequestStart(VisitState visit)
	{
		if (visit.npc == null)
		{
			return false;
		}
		if (!CheckGlobalConditions(visit))
		{
			return false;
		}
		if (Game.ctx.tutorial.AreQuestReqsSuppressed)
		{
			return false;
		}
		QuestRequest questRequest = FindExisting(visit);
		return ((questRequest == null || questRequest.questid == null) ? MakeNewRequest(visit) : questRequest).ShouldShowRequest;
	}

	public bool ShouldPresentRequestFinish(VisitState visit)
	{
		if (visit.npc == null)
		{
			return false;
		}
		QuestRequest questRequest = FindExisting(visit);
		QuestUUID questUUID = _manager.FindWaitingQuestForTarget(visit.npc.Id);
		if ((questRequest == null || questRequest.status != QuestRequest.Status.Accepted) && questUUID == QuestUUID.EMPTY)
		{
			return false;
		}
		return _manager.FindWaitingQuestUnsafe(questUUID)?.IsReadyForPlayerChoice ?? false;
	}

	public void RejectRequest(VisitState visit)
	{
		FindExisting(visit).SetDeclined();
		Game.ctx.events.EnqueueOnce(new SessionEvent(SessionEventType.QuestRequestDeclined, EntityID.INVALID, PlayerID.HumanPlayer));
	}

	private bool CheckGlobalConditions(VisitState visit)
	{
		if (_manager.HasActiveQuestForTarget(visit.npc.Id))
		{
			return false;
		}
		if (!Settings.global.reqs.AllPassLogging(visit, "quests"))
		{
			return false;
		}
		return true;
	}

	public QuestRequest FindExisting(EntityID ownerId)
	{
		return _data.requests.FindOrNull(ownerId);
	}

	public QuestRequest FindExisting(VisitState visit)
	{
		return _data.requests.FindOrNull(visit.npc.Id);
	}

	private QuestRequest MakeNewRequest(VisitState visit)
	{
		FindExisting(visit);
		QuestDefinition def = GenerateRandomForVisit(visit);
		return RememberRequest(visit.npc.Id, def);
	}

	public QuestRequest RememberRequest(EntityID ownerId, QuestDefinition def)
	{
		QuestRequest questRequest = new QuestRequest(ownerId, def?.id, def != null);
		return _data.requests[ownerId] = questRequest;
	}

	internal void OnGrantStartingARequest(EntityID ownerId, QuestDefinition def)
	{
		RememberRequest(ownerId, def);
	}

	internal void OnAcceptedRequest(EntityID ownerId, QuestUUID uuid)
	{
		QuestRequest questRequest = _data.requests.FindOrNull(ownerId);
		if (questRequest != null && questRequest.status == QuestRequest.Status.Available)
		{
			questRequest.SetAccepted(uuid);
		}
	}

	internal void OnCompletedRequest(EntityID ownerId, QuestUUID uuid)
	{
		if (!ownerId.IsNotValid)
		{
			QuestRequest questRequest = _data.requests.FindOrNull(ownerId);
			if (questRequest != null && questRequest.uuid == uuid)
			{
				_data.requests.Remove(ownerId);
			}
		}
	}

	private QuestDefinition GenerateRandomForVisit(VisitState visit)
	{
		ModQuery query = visit.MakeOwnerModQuery();
		float probability = (float)Settings.global.npcStartProb.Evaluate(query);
		if (!_data.rng.CheckProbability(probability))
		{
			return null;
		}
		Settings.PopulateMatchingQuests(visit, tmp_candidates);
		QuestDefinition result = _data.rng.PickElementOrDefault(tmp_candidates);
		tmp_candidates.Clear();
		return result;
	}
}
