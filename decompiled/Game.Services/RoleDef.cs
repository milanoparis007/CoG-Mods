using Game.Core;
using Game.Session.Data;

namespace Game.Services;

public sealed class RoleDef
{
	public Label id;

	public string locicon;

	public string loctitle;

	public string locdesc;

	public string locshort;

	public VisitRequirementList visReqs;

	public VisitRequirementList reqs;
}
