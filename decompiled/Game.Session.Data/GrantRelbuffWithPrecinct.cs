using System.Collections.Generic;
using Game.Core;
using Game.Services;
using Game.Session.Board;
using Game.Session.Entities;
using Game.Session.Player;
using Game.Session.Sim;

namespace Game.Session.Data;

public class GrantRelbuffWithPrecinct : VisitGrant
{
	public Label relbuffID;

	public override GrantReq RequiredContext => GrantReq.PlayerID | GrantReq.VisitPeep;

	public override void Apply(GrantContext ctx)
	{
		PlayerInfo player = ctx.visit.GetPlayer();
		PrecinctID precinctId = ctx.visit.building.components.board.GetNode().precinctId;
		Entity entity = precinctId.FindPrecinct().ai.precinct.StationBuilding.FindEntity();
		EntityID targetId = entity.data.police.officers[0];
		Relationship item = player.social.FindOrMakeRelationshipsWith(targetId).from;
		EntityID crewpeep = ctx.visit?.crew.peepId ?? EntityID.INVALID;
		item.AddBuff(BuffConstants.RELBUFF_COP_BLACKMAIL, crewpeep);
		List<Node> precinctNodes = new List<Node>();
		Game.ctx.board.nodes.VisitNeighborhoodBFS(entity.data.board.bead.nodeId.FindNode(), int.MaxValue, delegate(Node node)
		{
			if (IsInteresting(node))
			{
				precinctNodes.Add(node);
			}
		}, (Node node) => node.precinctId == precinctId);
		foreach (Node item2 in precinctNodes)
		{
			foreach (EntityID item3 in item2.interesting)
			{
				player.social.FindOrMakeRelationshipsWith(BuildingUtil.FindOwnerForAnyBuilding(item3).Id).from.AddBuff(relbuffID, crewpeep);
			}
		}
		static bool IsInteresting(Node node)
		{
			if (node != null)
			{
				return node.contained.Count > 0;
			}
			return false;
		}
	}

	public override string Describe(GrantContext ctx)
	{
		return Loc.Get("ui.grants.precinct-relbuff");
	}
}
