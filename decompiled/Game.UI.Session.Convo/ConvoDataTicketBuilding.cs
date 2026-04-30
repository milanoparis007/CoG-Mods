using System.Text;
using Game.Core;
using Game.Services;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Player;
using Game.Session.Sim.Modules;
using SomaSim.Util;

namespace Game.UI.Session.Convo;

public sealed class ConvoDataTicketBuilding : ConvoData
{
	public PlayerTerritory.TakeoverData takeover;

	public string name;

	public string bizname;

	public string sizeAndVert;

	public override string[] MakeReplacements(VisitState visit, int index)
	{
		return new string[8]
		{
			"name",
			name,
			"bizname",
			bizname,
			"size-and-vertical",
			sizeAndVert,
			"price",
			Loc.Price(takeover.cost)
		};
	}

	public ConvoDataTicketBuilding()
	{
	}

	public ConvoDataTicketBuilding(PlayerTerritory.TakeoverData takeover)
	{
		this.takeover = takeover;
		Entity entity = takeover.FindCandidate();
		Entity entity2 = BuildingUtil.FindBizForBuilding(takeover.buildingId);
		name = ((entity != null) ? PersonInfoUtil.GeneratePeepName(entity, showRank: false) : null);
		bizname = entity2?.data.biz.bizname ?? null;
		if (entity2 == null)
		{
			sizeAndVert = "";
			return;
		}
		if (entity2.config.biz?.playerModules == null)
		{
			sizeAndVert = "";
			return;
		}
		string text = FindTagVertSuffix(entity2);
		string text2 = Loc.Get("building-biz-segment" + text);
		string text3 = entity2.config.biz.playerModules.size.FirstOrDefaultFast().String;
		string text4 = ModulesUtil.DescribeModuleShort(takeover.buildingId.FindEntity().components.modules.FindFrontroomModule().ModuleData.Id);
		string text5 = ModulesUtil.DescribeInventoryModuleShort(takeover.buildingId.FindEntity());
		if (text3 == null)
		{
			sizeAndVert = "";
		}
		sizeAndVert = Loc.Get("building-takeover." + text3, "building-biz-segment", text2) + "\n" + text4 + "\n" + text5;
	}

	private string FindTagVertSuffix(Entity biz)
	{
		StringBuilder stringBuilder = StringBuilderPool.AllocateInstance();
		TagList verticals = biz.config.biz.playerModules.verticals;
		Label[] aLL_TAG_VERTS = TagConstants.ALL_TAG_VERTS;
		for (int i = 0; i < aLL_TAG_VERTS.Length; i++)
		{
			Label item = aLL_TAG_VERTS[i];
			if (verticals.Contains(item))
			{
				stringBuilder.Append(".");
				stringBuilder.Append(item.String.Substring(TagConstants.ALL_TAG_VERTS_PREFIX.String.Length));
			}
		}
		return stringBuilder.ToStringAndReturnToPool();
	}

	public override Entity GetVisitTopic()
	{
		return takeover.FindCandidate();
	}
}
