using System.Collections.Generic;
using Game.Session.Data;
using SomaSim.Util;

namespace Game.Services;

public sealed class DemandsSettings
{
	public List<DemandDef> defs;

	public List<DemandTransition> transitions;

	public ModValue cashcost;

	public DemandDef FindOrNullDemand(Demand.Type type)
	{
		for (int i = 0; i < defs.Count; i++)
		{
			if (defs[i].demand == type)
			{
				return defs[i];
			}
		}
		return null;
	}

	public DemandTransition FindTransition(Demand.Target target)
	{
		bool isOwner = target.IsOwner;
		bool flag = target.IsPlayer && target.ptype == PlayerType.GoonPlayer;
		bool flag2 = target.IsPlayer && target.ptype == PlayerType.GangPlayer;
		for (int i = 0; i < transitions.Count; i++)
		{
			DemandTransition demandTransition = transitions[i];
			if (demandTransition.atOwner == isOwner && demandTransition.atGoon == flag && demandTransition.atGang == flag2)
			{
				return demandTransition;
			}
		}
		return transitions.FirstOrDefaultFast();
	}
}
