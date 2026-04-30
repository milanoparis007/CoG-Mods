using Game.Session.Data;

namespace Game.Services;

public sealed class QuestExpirationDefinition
{
	public int expiresAfterDayz;

	public int expireYear;

	public int expireDay;

	public string locblurb;

	public string locdesc;

	public string locepilogue;

	public VisitGrantList grants;

	public ModValue refundPercent;
}
