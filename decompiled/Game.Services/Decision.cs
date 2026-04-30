using System.Collections.Generic;
using Game.Core;
using Game.Session.Data;

namespace Game.Services;

public sealed class Decision
{
	public VisitRequirementList visReqs;

	public string locoption;

	public string locepilogue;

	public string overrideText;

	public VisitGrantList grants;

	public List<ResOrCash> cost;

	public Label nextState;

	public bool earlyEnd;
}
