using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Session.Board;
using Game.Session.Entities;
using SomaSim.Util;
using UnityEngine;

namespace Game.UI.Session.Picks;

public struct SummaryPickData
{
	public struct Target
	{
		public Entity building;

		public bool scoped;

		public string icon;

		public Color color;

		public BuildingPick FindBuildingPickOrNull()
		{
			return Game.ctx.hud.picks.GetContainer(PickType.BuildingPick).GetOrNull(new PickTarget(building.Id)) as BuildingPick;
		}
	}

	public PlayerID owner;

	public List<Target> targets;

	public static SummaryPickData GenerateCornerButtonData(Node node)
	{
		_ = Game.ctx.players.Human;
		return new SummaryPickData
		{
			owner = node.owner.pid,
			targets = MakeTargets(node).ToList()
		};
	}

	private static List<Target> MakeTargets(Node node)
	{
		using ListPool<Entity>.PooledBlockList pooledBlockList = ListPool<Entity>.Allocate();
		node.FindBuildingsToShowGuitarPicks(PlayerID.HumanPlayer, pooledBlockList);
		List<Target> list = new List<Target>(pooledBlockList.Count);
		foreach (Entity item in pooledBlockList)
		{
			list.Add(MakeTarget(item));
		}
		return list;
	}

	private static Target MakeTarget(Entity building)
	{
		(bool scoped, string icon) tuple = BuildingPickUtil.GenerateBuildingButtonIcon(building);
		bool item = tuple.scoped;
		string item2 = tuple.icon;
		Color playerBuildingButtonColor = BuildingPickUtil.GetPlayerBuildingButtonColor(building, item, crewhere: true);
		return new Target
		{
			building = building,
			scoped = item,
			icon = item2,
			color = playerBuildingButtonColor
		};
	}
}
