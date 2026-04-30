using System.Diagnostics;
using Game.Core;
using Game.Services;
using Game.Session.Data;
using SomaSim.Util;

namespace Game.Session.Quests;

[DebuggerDisplay("{DebugString}")]
public class QuestWaitingRecord
{
	public enum Status
	{
		Active,
		WaitingOnReward,
		WaitingExpired,
		Done
	}

	public QuestUUID uuid;

	public string questid;

	public EntityID target;

	public SimTime started;

	public SimTime finished;

	public bool successful;

	public bool wasexpired;

	public Status status;

	public bool hasChoices;

	public int choiceIndex;

	public bool IsActive => status == Status.Active;

	public bool IsReadyForPlayerChoice => status == Status.WaitingOnReward;

	public bool IsExpiredAndWaiting => status == Status.WaitingExpired;

	public bool IsDone => status == Status.Done;

	private string DebugString => $"QuestWaitingRecord: {uuid} / {questid} for {target} status = {status}, succ = {successful}";

	public QuestWaitingRecord()
	{
	}

	public QuestWaitingRecord(QuestActiveRecord source, SimTime finished, bool successful)
	{
		uuid = source.uuid;
		questid = source.questid;
		started = source.started;
		target = source.target;
		this.finished = finished;
		this.successful = successful;
		status = Status.Active;
		wasexpired = false;
		QuestDefinition questDefinition = source.FindQuestDefinition();
		hasChoices = questDefinition.choices != null;
		choiceIndex = -1;
	}

	public void SetPlayerShouldChooseReward()
	{
		status = Status.WaitingOnReward;
	}

	public void SetDoneWithPlayerChoice(int choiceIndex)
	{
		int valueOrDefault = (Game.ctx.quests.FindQuestDefinition(questid)?.choices?.Count).GetValueOrDefault();
		this.choiceIndex = MathUtil.ClampMax(choiceIndex, valueOrDefault - 1);
		status = Status.Done;
	}

	public void SetDoneWithoutPlayerChoice()
	{
		status = Status.Done;
	}

	public void SetExpiredAndWaiting()
	{
		status = Status.WaitingExpired;
	}

	public void SetExpiredDone()
	{
		wasexpired = true;
		status = Status.Done;
	}

	public QuestDefinition FindQuestDefinition()
	{
		return Game.ctx.quests.FindQuestDefinition(questid);
	}

	public VisitGrantList FindExpirationGrantsOrNull()
	{
		return FindQuestDefinition().expiration?.grants;
	}

	public bool IsSuccessGood()
	{
		return !FindQuestDefinition().unhappyOnSuccess;
	}
}
