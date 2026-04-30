using System.Collections.Generic;
using Game.Core;
using Game.Services;
using Game.Session.Data;

namespace Game.UI.Session.Convo;

public sealed class ConvoDataQuestRequest : ConvoData
{
	public EntityID owner;

	public string questId;

	public List<string> blurbsleft;

	public override bool IsConvoStepEnabled(VisitState _, ConvoButtonState __)
	{
		return questId != null;
	}

	public ConvoDataQuestRequest()
	{
	}

	public ConvoDataQuestRequest(EntityID owner, QuestDefinition def)
	{
		if (def.requestinfo?.introsteps == null)
		{
			Logger.Warning("Missing introsteps in quest", def.id);
		}
		this.owner = owner;
		questId = def.id;
		blurbsleft = new List<string> { null, null };
		if (def.requestinfo?.introsteps != null)
		{
			blurbsleft.AddRange(def.requestinfo.introsteps);
		}
	}

	public QuestDefinition GetDef()
	{
		return Game.ctx.quests.FindQuestDefinition(questId);
	}

	public override string[] MakeReplacements(VisitState visit, int index)
	{
		return new string[0];
	}
}
