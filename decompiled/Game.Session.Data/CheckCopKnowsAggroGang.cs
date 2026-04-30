using Game.Core;
using Game.Services;
using Game.Session.Player;
using Game.Session.Sim;
using SomaSim.Util;

namespace Game.Session.Data;

public sealed class CheckCopKnowsAggroGang : AbstractVisitRequirement
{
	public bool truce;

	public override bool DoesPass(VisitState visit)
	{
		PlayerInfo player = GetPlayer(visit);
		PlayerInfo playerInfo = CopUtil.FindPrecinctOrNull(visit.npc);
		if (playerInfo == null)
		{
			return false;
		}
		using ListPool<PlayerID>.PooledBlockList pooledBlockList = ListPool<PlayerID>.Allocate();
		playerInfo.ai.precinct.ProduceAllGangsForFedHint(player.PID, pooledBlockList);
		return pooledBlockList.Count > 0;
	}

	public override ReqExplanation Explain(VisitState visit)
	{
		string key = (truce ? "ui.requirements.check-cop-knows-aggro-gang.truce" : "ui.requirements.check-cop-knows-aggro-gang.no-truce");
		return new ReqExplanation(DoesPass(visit), Loc.Get(key));
	}
}
