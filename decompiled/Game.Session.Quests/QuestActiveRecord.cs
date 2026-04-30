using System.Collections.Generic;
using System.Diagnostics;
using Game.Core;
using Game.Services;
using Game.Session.Data;

namespace Game.Session.Quests;

[DebuggerDisplay("{DebugString}")]
public class QuestActiveRecord
{
	public QuestUUID uuid;

	public string questid;

	public EntityID target;

	public SimTime started;

	public SimTime expires;

	public List<string> goalids = new List<string>();

	private string DebugString => $"QuestActiveRec {uuid} / {questid} for {target}";

	public QuestActiveRecord()
	{
	}

	public QuestActiveRecord(QuestUUID uuid, QuestDefinition def, EntityID target, SimTime started)
	{
		this.uuid = uuid;
		questid = def.id;
		this.target = target;
		this.started = started;
		expires = ((def.expiration == null) ? SimTime.MAX_DATE : ((def.expiration.expiresAfterDayz != 0) ? started.IncrementDays(def.expiration.expiresAfterDayz) : new SimTime(def.expiration.expireYear, def.expiration.expireDay)));
	}

	public QuestDefinition FindQuestDefinition()
	{
		return Game.ctx.quests.FindQuestDefinition(questid);
	}

	public int CompareTo(object obj)
	{
		if (!(obj is QuestActiveRecord questActiveRecord))
		{
			return 0;
		}
		if (started.days == questActiveRecord.started.days)
		{
			return questid.CompareTo(questActiveRecord.questid);
		}
		if (started.days <= questActiveRecord.started.days)
		{
			return -1;
		}
		return 1;
	}
}
