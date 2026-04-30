using System.Collections.Generic;
using Game.Core;
using Game.Services;
using Game.Session.Board;
using Game.Session.Entities;

namespace Game.Session.Data;

public class CheckBizInPlayerTerritory : CheckListOfItems
{
	protected override int Count(VisitState visit)
	{
		int num = 0;
		List<NodeID> allOwnedNodesUnsafe = GetPlayer(visit).territory.GetAllOwnedNodesUnsafe();
		foreach (Label item in of)
		{
			if (HasBiz(allOwnedNodesUnsafe, item))
			{
				num++;
			}
		}
		return num;
	}

	protected bool HasBiz(List<NodeID> nodes, Label id)
	{
		foreach (NodeID node in nodes)
		{
			foreach (EntityID item in node.FindNode().interesting)
			{
				Entity entity = BuildingUtil.FindBizForBuilding(item);
				if (entity != null && entity.config.Template == id)
				{
					return true;
				}
			}
		}
		return false;
	}

	protected override void VerifyData(VisitState _)
	{
		foreach (Label item in of)
		{
			EntityConfig entityConfig = Game.ctx.entityman.FindTemplate(item);
			if (entityConfig == null)
			{
				Label label = item;
				Logger.Warning("Checking biz in player territory: unknown template " + label.ToString());
			}
			else if (entityConfig.biz == null)
			{
				Label label = item;
				Logger.Warning("Checking biz in player territory: template is not a biz " + label.ToString());
			}
		}
	}

	protected override string IdToName(Label id)
	{
		EntityConfig entityConfig = Game.ctx.entityman.FindTemplate(id);
		if (entityConfig == null || entityConfig.biz?.locname == null)
		{
			return "?";
		}
		return Loc.Get(entityConfig.biz.locname);
	}

	protected override string MakeExplanationText()
	{
		return MakeExplanationText(Loc.Get("ui.requirements.biz.player.territory.expected"), Loc.Get("ui.requirements.biz.player.territory.unexpected"), Loc.Get("ui.requirements.biz.player.territory.all"), Loc.Get("ui.requirements.biz.player.territory.any"), Loc.Get("ui.requirements.biz.player.territory.none"));
	}
}
