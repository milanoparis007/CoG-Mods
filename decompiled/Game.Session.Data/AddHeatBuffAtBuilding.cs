using System.Collections.Generic;
using Game.Core;
using Game.Session.Board;
using Game.Session.Entities;

namespace Game.Session.Data;

public class AddHeatBuffAtBuilding : GrantHeatBuff
{
	public bool toNeighbors;

	public override GrantReq RequiredContext => GrantReq.PlayerID | GrantReq.VisitPeep | GrantReq.VisitBuilding;

	public override List<Node> GetTargets(GrantContext ctx)
	{
		Entity building = ctx.visit.building;
		List<Node> list = new List<Node> { building.components.board.GetNode() };
		if (toNeighbors)
		{
			foreach (Node item in building.components.board.GetNode().FindAllNeighbors())
			{
				if (!list.Contains(item))
				{
					list.Add(item);
				}
			}
		}
		return list;
	}
}
