using Game.Core;
using Game.Session.Data;
using Game.Session.Entities;

namespace Game.UI.Session.Convo;

public sealed class ConvoDataCrewHire : ConvoData
{
	public EntityID targetId;

	public EntityID introducerId;

	public override bool IsConvoStepEnabled(VisitState _, ConvoButtonState __)
	{
		return targetId.IsValid;
	}

	public override string[] MakeReplacements(VisitState visit, int index)
	{
		string text = (targetId.IsValid ? PersonInfoUtil.GeneratePeepName(targetId.FindEntity(), showRank: false) : "");
		return new string[2] { "name", text };
	}

	public ConvoDataCrewHire()
	{
	}

	public ConvoDataCrewHire(EntityID targetId, EntityID introducerId)
	{
		this.targetId = targetId;
		this.introducerId = introducerId;
	}
}
