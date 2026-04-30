using System.Collections.Generic;
using Game.Core;
using Game.Session.Board;
using Game.Session.Entities;
using Game.Session.Player;

namespace Game.Session.Data;

public class GrantPoliceInfoDump : VisitGrant
{
	public override GrantReq RequiredContext => GrantReq.PlayerID | GrantReq.VisitPeep;

	public override void Apply(GrantContext ctx)
	{
		PlayerInfo player = ctx.visit.GetPlayer();
		Entity entity = ctx.visit.building ?? ctx.visit.npc.components.agent.GetPlayer().ai.precinct.StationBuilding.FindEntity();
		PrecinctID? precinctId = entity.data.police?.precinctID;
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
		}, delegate(Node node)
		{
			PrecinctID precinctId2 = node.precinctId;
			PrecinctID? precinctID = precinctId;
			return precinctId2 == precinctID;
		});
		foreach (Node item2 in precinctNodes)
		{
			foreach (EntityID item3 in item2.interesting)
			{
				EntityComponents components = item3.FindEntity().components;
				if (components != null && components.modules.HasIllegalBusiness(player.PID))
				{
					player.social.FindOrMakeRelationshipsWith(BuildingUtil.FindOwnerForAnyBuilding(item3).Id).from.AddBuff(BuffConstants.RELBUFF_COP_BLACKMAIL, crewpeep);
					player.meetings.MarkNodeAsKnown(item2, expectedSeen: true, instant: true);
					player.territory.ScopeOutBuilding(item3.FindEntity(), procgen: false, setControlled: false);
				}
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
}
