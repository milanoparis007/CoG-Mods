using Game.Core;
using Game.Session.Data;
using Game.Session.Entities;
using SomaSim.Util;

namespace Game.Services;

public sealed class ResidentialEventConfig
{
	public Label id;

	public VisitRequirementList visreqs;

	public ModValue waitturns;

	public ModValue buyincash;

	public ModValue probskip;

	public string locroot;

	public string locicon;

	public string locname;

	public string locdesc;

	public string npcintro;

	public string tickerinfo;

	public string tickerstart;

	public LabelDictionary<float> results;

	public string GetIcon()
	{
		return Loc.Get(locicon);
	}

	public string GetName()
	{
		return Loc.Get(locname);
	}

	public string GetDesc(Entity host)
	{
		return Loc.Get(locdesc, "name", host.data.person.FullName);
	}

	public bool ShouldSkip(PlayerID pid, IRandom rng)
	{
		Fixnum probability = probskip.Evaluate(pid);
		return rng.CheckProbability(probability);
	}

	public int FindWaitTurns(PlayerID pid)
	{
		return (int)waitturns.Evaluate(pid);
	}
}
