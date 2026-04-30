using Game.Core;
using Game.Services;
using Game.Session.Data;

namespace Game.Session.Entities;

public sealed class ResEventResultData
{
	public Label resultId;

	public EntityID targetNpc;

	public string eventResultMessage;

	public QuestUUID quuid;

	public ResidentialEventResultConfig GetConfig()
	{
		return Game.serv.globals.settings.people.residentialEvents.FindResult(resultId);
	}
}
