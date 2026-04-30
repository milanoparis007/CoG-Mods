using System.Diagnostics;
using Game.Core;
using Game.Session.Data;

namespace Game.Session.Quests;

[DebuggerDisplay("{DebugString}")]
public class QuestCompletedRecord
{
	public QuestUUID uuid;

	public string questid;

	public EntityID target;

	public SimTime started;

	public SimTime finished;

	public bool successful;

	public bool expired;

	private string DebugString => $"QuestCompletedRec: {uuid} / {questid} for {target} completed on {finished}, finished = {finished}, succ = {successful}";

	public QuestCompletedRecord()
	{
	}

	public QuestCompletedRecord(QuestWaitingRecord source)
	{
		uuid = source.uuid;
		questid = source.questid;
		started = source.started;
		target = source.target;
		finished = source.finished;
		successful = source.successful;
		expired = source.wasexpired;
	}
}
