using Game.Services;
using Game.Session.Data;

namespace Game.UI.Session.Convo;

public sealed class ConvoDataQuestActive : ConvoData
{
	public QuestUUID quuid;

	public string questid;

	public ResOrCash delivered;

	public override bool IsConvoStepEnabled(VisitState _, ConvoButtonState __)
	{
		return questid != null;
	}

	public ConvoDataQuestActive()
	{
	}

	public ConvoDataQuestActive(QuestUUID quuid, string questid)
	{
		this.quuid = quuid;
		this.questid = questid;
	}

	public QuestDefinition GetDef()
	{
		return Game.ctx.quests.FindQuestDefinition(questid);
	}

	public override string[] MakeReplacements(VisitState visit, int index)
	{
		string text = Game.ctx.quests.MakeChoiceDescription(quuid, GetDef(), visit, index);
		string text2 = Game.ctx.quests.MakeExpirationDescription(GetDef());
		return new string[4] { "choicetext", text, "expiretext", text2 };
	}
}
