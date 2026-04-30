using Game.Core;
using Game.Services;
using Game.Session.Data;
using Game.Session.Quests;
using Game.UI.Session.HUD;
using SomaSim.Util;
using UnityEngine;

namespace Game.UI.Session.Quests;

public sealed class QuestBarButton
{
	public QuestUUID uuid;

	public GameObject card;

	internal void Initialize(GameObject card, QuestUUID uuid)
	{
		this.uuid = uuid;
		this.card = card;
		card.GetButton().onClick.SetListener(delegate
		{
			EntityID item = Game.ctx.quests.FindQuestDefAndTarget(uuid).target;
			if (item.IsValid)
			{
				PersonInfoUtil.TweenCameraToEntity(item);
			}
			ReportsBarItems.ShowQuestList(uuid);
		});
		RefreshContents();
	}

	internal void Release()
	{
		card.GetButton().onClick.RemoveAllListeners();
		uuid = QuestUUID.EMPTY;
		card = null;
	}

	public void RefreshContents()
	{
		string text2;
		string text3;
		if (Game.ctx.quests.IsQuestActive(uuid))
		{
			QuestActiveRecord questActiveRecord = Game.ctx.quests.FindActiveQuestUnsafe(uuid);
			QuestDefinition questDefinition = questActiveRecord.FindQuestDefinition();
			string text = Game.ctx.quests.DescribeActiveQuestGoals(questActiveRecord, showName: false);
			text2 = Loc.Get("ui.reports.quests.button", "name", Loc.Get(questDefinition.locname), "goals", text);
			text3 = Loc.Get(questDefinition.locicon);
		}
		else
		{
			QuestWaitingRecord questWaitingRecord = Game.ctx.quests.FindWaitingQuestUnsafe(uuid);
			QuestDefinition questDefinition2 = questWaitingRecord.FindQuestDefinition();
			string text4 = Game.ctx.quests.DescribeWaitingQuestGoals(questWaitingRecord, showName: false);
			text2 = Loc.Get("ui.reports.quests.button", "name", Loc.Get(questDefinition2.locname), "goals", text4);
			text3 = Loc.Get(questDefinition2.locicon);
		}
		card.SetText("Text", text2);
		card.SetText("Icon", text3);
	}
}
