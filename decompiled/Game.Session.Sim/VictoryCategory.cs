using System.Collections.Generic;
using Game.Services;

namespace Game.Session.Sim;

public class VictoryCategory
{
	public string locname;

	public string locicon;

	public string locdesc;

	public PhotoConfig photo;

	public List<VictoryGoal> goals;

	public string GetName()
	{
		return Loc.Get(locname);
	}

	public string GetIcon()
	{
		return Loc.Get(locicon);
	}

	public string GetDesc()
	{
		return Loc.Get(locdesc);
	}
}
