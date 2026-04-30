using System;
using Game.Core;
using Game.Services;
using Game.Session.Data;
using Game.Session.Quests;
using Game.UI.Session.HUD;

namespace Game.UI.Session.Quests;

internal class QuestListItem : ItemListDialog.IEntry, IComparable
{
	public QuestUUID uuid;

	public EntityID target;

	public QuestActiveRecord active;

	public QuestWaitingRecord waiting;

	public QuestDefinition def;

	public QuestListItem(QuestUUID uuid)
	{
		this.uuid = uuid;
		active = Game.ctx.quests.FindActiveQuestUnsafe(uuid);
		waiting = Game.ctx.quests.FindWaitingQuestUnsafe(uuid);
		def = active?.FindQuestDefinition() ?? waiting?.FindQuestDefinition();
		target = active?.target ?? waiting?.target ?? EntityID.INVALID;
	}

	public string GetDebug()
	{
		return "Quest " + def.id;
	}

	public string GetName()
	{
		return Loc.Get(def.locname);
	}

	public string GetIcon()
	{
		return Loc.Get(def.locicon);
	}

	public string GetDescription()
	{
		(string name, string desc) tuple = Game.ctx.quests.DescribeQuestMain(uuid);
		string item = tuple.name;
		string item2 = tuple.desc;
		string text = ((active != null) ? Game.ctx.quests.DescribeActiveQuestGoals(active, showName: true) : Game.ctx.quests.DescribeWaitingQuestGoals(waiting, showName: true));
		return Loc.Get("ui.reports.quests.item.desc", "name", item, "desc", item2, "goals", text);
	}

	public bool ShowGoTo()
	{
		return target.IsValid;
	}

	public void OnGoTo()
	{
		PersonInfoUtil.TweenCameraToEntity(target);
	}

	public int CompareTo(object obj)
	{
		if (active == null || !(obj is QuestListItem questListItem))
		{
			return 0;
		}
		return active.CompareTo(questListItem.active);
	}
}
