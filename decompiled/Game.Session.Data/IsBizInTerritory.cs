using Game.Core;
using Game.Services;
using Game.Session.Board;
using Game.Session.Entities;
using Game.Session.Player;
using SomaSim.Util;

namespace Game.Session.Data;

public sealed class IsBizInTerritory : BaseDeltaMultiplierModifier
{
	public Label id;

	public bool expected = true;

	public override ModQueryElement QueryMustProvide => ModQueryElement.Player;

	private void Validate()
	{
		if (Game.ctx.entityman.FindTemplate(id)?.biz?.bizname == null)
		{
			Logger.Warning($"Failed to find biz id {id} in {this}");
		}
	}

	public override bool DoesPass(ModQuery query)
	{
		PlayerInfo playerInfo = query.FindPlayer();
		if (playerInfo == null)
		{
			return false;
		}
		return FindBizInTerritory(playerInfo) == expected;
	}

	private bool FindBizInTerritory(PlayerInfo player)
	{
		using ListPool<Entity>.PooledBlockList pooledBlockList = ListPool<Entity>.Allocate();
		foreach (NodeID item in player.territory.GetAllOwnedNodesUnsafe())
		{
			Node node = item.FindNode();
			if (node == null)
			{
				continue;
			}
			node.FindAllInterestingBuildings(pooledBlockList);
			foreach (Entity item2 in pooledBlockList)
			{
				Entity entity = BuildingUtil.FindBizForBuilding(item2);
				if (entity != null && entity.config.Template == id)
				{
					return true;
				}
			}
		}
		return false;
	}

	public override string Explain(ModQuery query, Fixnum delta)
	{
		string key = (expected ? "mod.is-biz-in-territory.true" : "mod.is-biz-in-territory.false");
		string text = Loc.Get(Game.ctx.entityman.FindTemplate(id)?.biz?.locname);
		return Loc.Get(key, "name", text, "delta", AbstractModifier.FormatDelta(delta));
	}
}
