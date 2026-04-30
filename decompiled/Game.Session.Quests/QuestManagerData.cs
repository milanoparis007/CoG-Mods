using System.Collections.Generic;
using Game.Core;
using Game.Session.Data;
using SomaSim.Util;

namespace Game.Session.Quests;

public class QuestManagerData
{
	public Xorshift rng = Game.ctx.scenario.MakeSeededRng<QuestManager>();

	public int nextUUID = 1001;

	public int nextGUUID = 9001;

	public Dictionary<QuestUUID, QuestActiveRecord> active = new Dictionary<QuestUUID, QuestActiveRecord>();

	public Dictionary<QuestUUID, QuestWaitingRecord> waiting = new Dictionary<QuestUUID, QuestWaitingRecord>();

	public Dictionary<QuestUUID, QuestCompletedRecord> completed = new Dictionary<QuestUUID, QuestCompletedRecord>();

	public Dictionary<EntityID, QuestRequest> requests = new Dictionary<EntityID, QuestRequest>();
}
