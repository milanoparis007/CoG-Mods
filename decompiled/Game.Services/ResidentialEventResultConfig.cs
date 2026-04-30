using Game.Core;
using Game.Session.Data;
using Game.Session.Entities;

namespace Game.Services;

public abstract class ResidentialEventResultConfig
{
	public static readonly Label FALLBACK_ID = (Label)"intro";

	public Label id;

	public VisitRequirementList visreqs;

	public string locroot;

	public string loceventresult;

	public QuestDefinition quest;

	public VisitGrantList grants;

	public abstract ResEventResultData GenerateResultData(VisitState visit);

	public abstract void AcceptResult(VisitState visit, ResEventData data);
}
