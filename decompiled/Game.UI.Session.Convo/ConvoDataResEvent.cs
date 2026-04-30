using Game.Core;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Sim;

namespace Game.UI.Session.Convo;

public sealed class ConvoDataResEvent : ConvoData
{
	public EntityID building;

	public EntityID hostNpc;

	public Label eventId;

	public bool IsValid
	{
		get
		{
			if (building.IsValid && hostNpc.IsValid)
			{
				return eventId.IsSet;
			}
			return false;
		}
	}

	public ConvoDataResEvent()
	{
	}

	public ConvoDataResEvent(ResEventCandidate ctx)
	{
		building = ctx.building;
		hostNpc = ctx.hostNpc;
		eventId = ctx.eventId;
	}

	public override string[] MakeReplacements(VisitState visit, int index)
	{
		return new string[2]
		{
			"name",
			hostNpc.FindEntity()?.data.person?.FullName
		};
	}
}
